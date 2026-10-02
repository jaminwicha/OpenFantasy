using System.Runtime.ExceptionServices;
using System.Net.WebSockets;
using System.Threading.Channels;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Diagnostics;

namespace OpenFantasy.Hosting.Networking;

/// <summary>
/// Routes one admitted connection to its existing host port. A pending world action never blocks
/// readiness input. One action, one query and a bounded response queue isolate slow clients from host commits.
/// </summary>
public sealed class PlayerCommandSession
{
    public const int MaximumQueuedResponses = 8;
    public const int MaximumInputsPerSecond = 128;
    public static readonly TimeSpan SendLimit = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan ObservationInterval = TimeSpan.FromMilliseconds(100);
    private readonly AuthenticatedPlayerChannel _channel;
    private readonly TimeProvider _time;
    private readonly Channel<PlayerMessage> _responses = Channel.CreateBounded<PlayerMessage>(new BoundedChannelOptions(MaximumQueuedResponses)
    {
        SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait, AllowSynchronousContinuations = false
    });
    private readonly TaskCompletionSource<Exception> _requestFailure = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task _action = Task.CompletedTask;
    private Task _query = Task.CompletedTask;
    private int _busy;
    private int _queryBusy;
    private int _started;
    private int _observationRequested;
    private int _forceFullObservation;
    private int _observationPending;
    private int _disconnectReason;

    public PlayerCommandSession(AuthenticatedPlayerChannel channel, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(channel);
        _channel = channel;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Owns the channel until all loops terminate; caller cancellation is ordinary local shutdown.</summary>
    public Task RunAsync(CancellationToken cancellationToken = default) => RunAsync(cancellationToken, CancellationToken.None);

    /// <summary>Separates server shutdown from an established HTTP request whose transport was aborted.</summary>
    public async Task RunAsync(CancellationToken cancellationToken, CancellationToken transportAborted)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0) throw new InvalidOperationException("A player command session can run only once.");
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, transportAborted);
        Task receiver = ReceiveAsync(stop.Token);
        Task sender = SendAsync(stop.Token);
        Task observations = PublishObservationsAsync(stop.Token);
        Exception? failure = null;
        try
        {
            Task ended = await Task.WhenAny(receiver, sender, observations, _channel.Player.HostCompletion, _requestFailure.Task).ConfigureAwait(false);
            if (ReferenceEquals(ended, _requestFailure.Task)) throw await _requestFailure.Task.ConfigureAwait(false);
            await ended.ConfigureAwait(false);
        }
        catch (Exception error) { failure = error; }
        finally
        {
            HostTimingEvents.Log.Stage(_channel.Player.TimingOwner, "session.cleanup.begin", value: (long)_channel.Transport.Termination);
            if (transportAborted.IsCancellationRequested) MarkDisconnected(WorldControllerDisconnectReason.TransportLost);
            stop.Cancel();
            try
            {
                int reason = Volatile.Read(ref _disconnectReason);
                if (reason == 0)
                    reason = _channel.Transport.Termination switch
                    {
                        PlayerTransportTermination.ProtocolFailure => (int)WorldControllerDisconnectReason.ProtocolFailure,
                        PlayerTransportTermination.TransportLost => (int)WorldControllerDisconnectReason.TransportLost,
                        _ => 0
                    };
                if (!cancellationToken.IsCancellationRequested
                    && _channel.Transport.Termination != PlayerTransportTermination.LocalDisposal && reason != 0)
                {
                    HostTimingEvents.Log.Stage(_channel.Player.TimingOwner, "disconnect.begin", value: reason);
                    await _channel.ConfirmTransportDisconnectedAsync((WorldControllerDisconnectReason)reason).ConfigureAwait(false);
                    HostTimingEvents.Log.Stage(_channel.Player.TimingOwner, "disconnect.end", value: reason);
                }
                else await _channel.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception error) { failure ??= error; }
            // Closing the port releases an uncommitted action waiter; a committed effect stays durable.
            foreach (Task task in new[] { receiver, sender, observations })
            {
                try { await task.ConfigureAwait(false); }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
                catch (ObjectDisposedException) when (stop.IsCancellationRequested) { }
                catch (Exception error) { failure ??= error; }
            }
            await _action.ConfigureAwait(false);
            await _query.ConfigureAwait(false);
            if (_requestFailure.Task.IsCompletedSuccessfully) failure ??= await _requestFailure.Task.ConfigureAwait(false);
            HostTimingEvents.Log.Stage(_channel.Player.TimingOwner, "session.cleanup.end");
        }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private async Task ReceiveAsync(CancellationToken cancellationToken)
    {
        ulong lastRequest = 0;
        long window = _time.GetTimestamp();
        int inputs = 0;
        while (await ReceiveFrameAsync(cancellationToken).ConfigureAwait(false) is { } bytes)
        {
            if (_time.GetElapsedTime(window, _time.GetTimestamp()) >= TimeSpan.FromSeconds(1))
            {
                window = _time.GetTimestamp();
                inputs = 0;
            }
            if (++inputs > MaximumInputsPerSecond)
            {
                MarkDisconnected(WorldControllerDisconnectReason.ProtocolFailure);
                throw new InvalidDataException("The player exceeded its input-rate limit.");
            }
            PlayerCommandInput input;
            try { input = PlayerCommandInput.Decode(bytes, lastRequest); }
            catch (FormatException)
            {
                MarkDisconnected(WorldControllerDisconnectReason.ProtocolFailure);
                throw;
            }
            PlayerMessage message = input.Message;
            HostTimingEvents.Log.Stage(_channel.Player.TimingOwner, "server.input", unchecked((long)message.RequestId), (long)message.Kind);
            lastRequest = message.RequestId;
            switch (message.Kind)
            {
                case PlayerMessageKind.Readiness:
                    _channel.Player.SetReady(input.Ready);
                    break;
                case PlayerMessageKind.Movement:
                    _channel.Player.SetMovement(input.X, input.Y);
                    break;
                case PlayerMessageKind.Command:
                    if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
                        Enqueue(Refusal(message.RequestId, PlayerRequestRefusal.Busy));
                    else _action = CompleteActionAsync(message.RequestId, input.Command!, cancellationToken);
                    break;
                case PlayerMessageKind.Query:
                    if (Interlocked.CompareExchange(ref _queryBusy, 1, 0) != 0)
                        Enqueue(Refusal(message.RequestId, PlayerRequestRefusal.Busy));
                    else _query = CompleteQueryAsync(message.RequestId, input.Query!, cancellationToken);
                    break;
                case PlayerMessageKind.Ping:
                    Enqueue(new PlayerMessage(PlayerMessageKind.Pong, message.RequestId, []));
                    break;
                case PlayerMessageKind.Observation:
                    Interlocked.Exchange(ref _forceFullObservation, 1);
                    Volatile.Write(ref _observationRequested, 1);
                    break;
                case PlayerMessageKind.Goodbye:
                    MarkDisconnected(WorldControllerDisconnectReason.PeerClosed);
                    return;
            }
        }
        if (!cancellationToken.IsCancellationRequested) MarkDisconnected(WorldControllerDisconnectReason.PeerClosed);
    }

    private async ValueTask<byte[]?> ReceiveFrameAsync(CancellationToken cancellationToken)
    {
        try { return await _channel.Transport.ReceiveAsync(cancellationToken).ConfigureAwait(false); }
        catch (InvalidDataException)
        {
            MarkDisconnected(WorldControllerDisconnectReason.ProtocolFailure);
            throw;
        }
        catch (OperationCanceledException) when (_channel.Transport.Termination == PlayerTransportTermination.TransportLost)
        {
            if (!cancellationToken.IsCancellationRequested) MarkDisconnected(WorldControllerDisconnectReason.TransportLost);
            throw;
        }
        catch (Exception error) when (error is IOException or WebSocketException or ChannelClosedException)
        {
            if (!cancellationToken.IsCancellationRequested) MarkDisconnected(WorldControllerDisconnectReason.TransportLost);
            throw;
        }
    }

    private async Task CompleteActionAsync(ulong requestId, WorldActionCommand command, CancellationToken cancellationToken)
    {
        try
        {
            WorldActionReceipt receipt = await _channel.Player.SubmitAction(command).ConfigureAwait(false);
            Enqueue(new PlayerMessage(PlayerMessageKind.Receipt, requestId, WorldReceiptCodecV1.Encode(receipt)));
        }
        catch (AuthorityDispatchException)
        {
            try { Enqueue(Refusal(requestId, PlayerRequestRefusal.NotAdmitted)); }
            catch (Exception error) { _requestFailure.TrySetResult(error); }
        }
        // Host shutdown cancels action waiters before its completion task necessarily becomes observable.
        // That cancellation is ordinary teardown; a real host failure still propagates through HostCompletion.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested
            || _channel.Player.ReadRunState() == Simulation.LocalWorldRunState.Unavailable) { }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception error) { _requestFailure.TrySetResult(error); }
        finally { Volatile.Write(ref _busy, 0); }
    }

    private async Task CompleteQueryAsync(ulong requestId, PlayerQuery query, CancellationToken cancellationToken)
    {
        try
        {
            PlayerQueryResult result = await PlayerPortQueries.ExecuteAsync(_channel, query).ConfigureAwait(false);
            if (!cancellationToken.IsCancellationRequested)
                Enqueue(new PlayerMessage(PlayerMessageKind.QueryResult, requestId, PlayerQueryResultCodec.Encode(result)));
        }
        catch (AuthorityDispatchException)
        {
            try
            {
                if (!cancellationToken.IsCancellationRequested) Enqueue(Refusal(requestId, PlayerRequestRefusal.NotAdmitted));
            }
            catch (Exception error) { _requestFailure.TrySetResult(error); }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested
            || _channel.Player.ReadRunState() == Simulation.LocalWorldRunState.Unavailable) { }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception error) { _requestFailure.TrySetResult(error); }
        finally { Volatile.Write(ref _queryBusy, 0); }
    }

    private async Task SendAsync(CancellationToken cancellationToken)
    {
        await foreach (PlayerMessage message in _responses.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            HostTimingEvents.Log.Stage(_channel.Player.TimingOwner, "server.send.begin", unchecked((long)message.RequestId), (long)message.Kind);
            long started = _time.GetTimestamp();
            using var deadline = new CancellationTokenSource(SendLimit, _time);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
            // Register after the linked source: cancellation callbacks run last-in-first-out, so the
            // deadline origin is visible before a socket abort wakes the competing receive loop.
            using var deadlineOrigin = deadline.Token.Register(() =>
            {
                if (!cancellationToken.IsCancellationRequested) MarkDisconnected(WorldControllerDisconnectReason.TransportLost);
            });
            byte[] bytes = PlayerMessageCodec.Encode(message);
            try { await _channel.Transport.SendAsync(bytes, linked.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                MarkDisconnected(WorldControllerDisconnectReason.TransportLost);
                throw;
            }
            catch (Exception error) when (error is IOException or WebSocketException or ChannelClosedException)
            {
                if (!cancellationToken.IsCancellationRequested) MarkDisconnected(WorldControllerDisconnectReason.TransportLost);
                throw;
            }
            if (_time.GetElapsedTime(started, _time.GetTimestamp()) >= SendLimit)
            {
                MarkDisconnected(WorldControllerDisconnectReason.TransportLost);
                throw new PlayerResponseTimeoutException();
            }
            if (message.Kind == PlayerMessageKind.Observation) Volatile.Write(ref _observationPending, 0);
            HostTimingEvents.Log.Stage(_channel.Player.TimingOwner, "server.send.end", unchecked((long)message.RequestId), (long)message.Kind);
        }
    }

    private async Task PublishObservationsAsync(CancellationToken cancellationToken)
    {
        var stream = new PlayerObservationStream();
        using var timer = new PeriodicTimer(ObservationInterval, _time);
        long? lastPublication = null;
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            if (Volatile.Read(ref _observationRequested) == 0
                || lastPublication is { } previous && _time.GetElapsedTime(previous, _time.GetTimestamp()) < ObservationInterval
                || Interlocked.CompareExchange(ref _observationPending, 1, 0) != 0) continue;
            bool full = Interlocked.Exchange(ref _forceFullObservation, 0) != 0;
            HostTimingEvents.Log.Stage(_channel.Player.TimingOwner, "observation.begin", value: full ? 1 : 0);
            PlayerObservationFrame? frame = stream.Capture(_channel.Player.ReadView(), full);
            HostTimingEvents.Log.Stage(_channel.Player.TimingOwner, "observation.captured", value: frame is null ? 0 : 1);
            if (frame is null)
            {
                Volatile.Write(ref _observationPending, 0);
                continue;
            }
            Enqueue(new PlayerMessage(PlayerMessageKind.Observation, 0, PlayerObservationFrameCodec.Encode(frame)));
            HostTimingEvents.Log.Stage(_channel.Player.TimingOwner, "observation.queued");
            lastPublication = _time.GetTimestamp();
        }
    }

    private void Enqueue(PlayerMessage message)
    {
        if (!_responses.Writer.TryWrite(message))
        {
            MarkDisconnected(WorldControllerDisconnectReason.TransportLost);
            throw new IOException("The player response queue is full.");
        }
    }

    private static PlayerMessage Refusal(ulong requestId, PlayerRequestRefusal refusal) =>
        new(PlayerMessageKind.Refusal, requestId, new[] { (byte)refusal });

    private void MarkDisconnected(WorldControllerDisconnectReason reason) =>
        Interlocked.CompareExchange(ref _disconnectReason, (int)reason, 0);
}
