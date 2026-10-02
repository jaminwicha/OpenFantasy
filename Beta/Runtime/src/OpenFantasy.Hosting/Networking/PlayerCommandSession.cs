using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Networking;

/// <summary>
/// Routes one admitted connection to its existing host port. A pending world action never blocks
/// readiness input. One action and a bounded response queue isolate slow clients from host commits.
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
    private readonly TaskCompletionSource<Exception> _actionFailure = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task _action = Task.CompletedTask;
    private int _busy;
    private int _started;
    private int _observationRequested;
    private int _forceFullObservation;
    private int _observationPending;

    public PlayerCommandSession(AuthenticatedPlayerChannel channel, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(channel);
        _channel = channel;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Owns the channel until all loops terminate; may be called once. Closing it journals no disconnect policy.</summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0) throw new InvalidOperationException("A player command session can run only once.");
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task receiver = ReceiveAsync(stop.Token);
        Task sender = SendAsync(stop.Token);
        Task observations = PublishObservationsAsync(stop.Token);
        Exception? failure = null;
        try
        {
            Task ended = await Task.WhenAny(receiver, sender, observations, _channel.Player.HostCompletion, _actionFailure.Task).ConfigureAwait(false);
            if (ReferenceEquals(ended, _actionFailure.Task)) throw await _actionFailure.Task.ConfigureAwait(false);
            await ended.ConfigureAwait(false);
        }
        catch (Exception error) { failure = error; }
        finally
        {
            stop.Cancel();
            try { await _channel.DisposeAsync().ConfigureAwait(false); }
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
            if (_actionFailure.Task.IsCompletedSuccessfully) failure ??= await _actionFailure.Task.ConfigureAwait(false);
        }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private async Task ReceiveAsync(CancellationToken cancellationToken)
    {
        ulong lastRequest = 0;
        long window = _time.GetTimestamp();
        int inputs = 0;
        while (await _channel.Transport.ReceiveAsync(cancellationToken).ConfigureAwait(false) is { } bytes)
        {
            if (_time.GetElapsedTime(window, _time.GetTimestamp()) >= TimeSpan.FromSeconds(1))
            {
                window = _time.GetTimestamp();
                inputs = 0;
            }
            if (++inputs > MaximumInputsPerSecond) throw new InvalidDataException("The player exceeded its input-rate limit.");
            PlayerMessage message = PlayerMessageCodec.Decode(bytes);
            if (message.RequestId <= lastRequest) throw new FormatException("Player request IDs must strictly increase within a connection.");
            lastRequest = message.RequestId;
            switch (message.Kind)
            {
                case PlayerMessageKind.Readiness:
                    _channel.Player.SetReady(PlayerGameplayCodec.ReadReadiness(message));
                    break;
                case PlayerMessageKind.Movement:
                    (int x, int y) = PlayerGameplayCodec.ReadMovement(message);
                    _channel.Player.SetMovement(x, y);
                    break;
                case PlayerMessageKind.Command:
                    WorldActionCommand command = PlayerGameplayCodec.ReadCommand(message);
                    if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
                        Enqueue(Refusal(message.RequestId, PlayerRequestRefusal.Busy));
                    else _action = CompleteActionAsync(message.RequestId, command, cancellationToken);
                    break;
                case PlayerMessageKind.Ping:
                    RequireEmpty(message);
                    Enqueue(new PlayerMessage(PlayerMessageKind.Pong, message.RequestId, []));
                    break;
                case PlayerMessageKind.Observation:
                    RequireEmpty(message);
                    Interlocked.Exchange(ref _forceFullObservation, 1);
                    Volatile.Write(ref _observationRequested, 1);
                    break;
                case PlayerMessageKind.Goodbye:
                    RequireEmpty(message);
                    return;
                default: throw new FormatException("Unsupported player input message.");
            }
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
            catch (Exception error) { _actionFailure.TrySetResult(error); }
        }
        // Host shutdown cancels action waiters before its completion task necessarily becomes observable.
        // That cancellation is ordinary teardown; a real host failure still propagates through HostCompletion.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested
            || _channel.Player.ReadRunState() == Simulation.LocalWorldRunState.Unavailable) { }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception error) { _actionFailure.TrySetResult(error); }
        finally { Volatile.Write(ref _busy, 0); }
    }

    private async Task SendAsync(CancellationToken cancellationToken)
    {
        await foreach (PlayerMessage message in _responses.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            long started = _time.GetTimestamp();
            using var deadline = new CancellationTokenSource(SendLimit, _time);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
            await _channel.Transport.SendAsync(PlayerMessageCodec.Encode(message), linked.Token).ConfigureAwait(false);
            if (_time.GetElapsedTime(started, _time.GetTimestamp()) >= SendLimit)
                throw new TimeoutException("The player response exceeded its send deadline.");
            if (message.Kind == PlayerMessageKind.Observation) Volatile.Write(ref _observationPending, 0);
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
            PlayerObservationFrame? frame = stream.Capture(_channel.Player.ReadView(), full);
            if (frame is null)
            {
                Volatile.Write(ref _observationPending, 0);
                continue;
            }
            Enqueue(new PlayerMessage(PlayerMessageKind.Observation, 0, PlayerObservationFrameCodec.Encode(frame)));
            lastPublication = _time.GetTimestamp();
        }
    }

    private void Enqueue(PlayerMessage message)
    {
        if (!_responses.Writer.TryWrite(message)) throw new IOException("The player response queue is full.");
    }

    private static PlayerMessage Refusal(ulong requestId, PlayerRequestRefusal refusal) =>
        new(PlayerMessageKind.Refusal, requestId, new[] { (byte)refusal });

    private static void RequireEmpty(PlayerMessage message)
    {
        if (!message.Payload.IsEmpty) throw new FormatException("This player message must have an empty payload.");
    }
}
