using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Threading.Channels;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking.Client;

/// <summary>
/// Owns one already-protected participant transport. Commands retain their exact caller-authored
/// identity and content; this client neither reconnects nor retries world actions.
/// </summary>
public sealed partial class PlayerProtocolClient : IAsyncDisposable
{
    public static readonly TimeSpan StartupLimit = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan SendLimit = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan MovementRenewalInterval = TimeSpan.FromMilliseconds(50);
    public const int OutputCapacity = 32;

    private readonly object _gate = new();
    private readonly PlayerMessageTransport _transport;
    private readonly TimeProvider _time;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Channel<PlayerMessage> _output = Channel.CreateBounded<PlayerMessage>(
        new BoundedChannelOptions(OutputCapacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly PlayerObservationReplica _replica;
    private readonly TaskCompletionSource<bool> _firstFull = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Task _completion;
    private Exception? _failure;
    private bool _closed;
    private bool _ready;
    private bool _fullRequested;
    private ulong _lastRequestId = 1;
    private int _movementX;
    private int _movementY;
    private long _lastMovement;
    private long _controlWindow;
    private int _controlCount;

    private PlayerProtocolClient(PlayerMessageTransport transport, PlayerWelcome welcome, TimeProvider time)
    {
        _transport = transport;
        Welcome = welcome;
        _time = time;
        _controlWindow = time.GetTimestamp();
        _replica = new PlayerObservationReplica(welcome.Compatibility.WorldId, welcome.Compatibility.BranchId, welcome.ActorId);
        // Subscription is not a recovery attempt: a missing initial basis must still request a full frame.
        lock (_gate) EnqueueLocked(id => new PlayerMessage(PlayerMessageKind.Observation, id, []));
        _completion = RunAsync();
    }

    public PlayerWelcome Welcome { get; }
    public Task Completion => _completion;
    public LocalWorldView ReadView() => _replica.ReadView()
        ?? throw new InvalidOperationException("The first full observation has not arrived.");

    /// <summary>
    /// Takes ownership of the transport, including failed admission. The token bounds this attempt
    /// only; a successfully returned connection has an independent lifetime and begins unready.
    /// </summary>
    public static async Task<PlayerProtocolClient> ConnectAsync(PlayerMessageTransport transport,
        PlayerConnectionInvitation invitation, CancellationToken token = default, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(invitation);
        time ??= TimeProvider.System;
        long started = time.GetTimestamp();
        using var deadline = new CancellationTokenSource(StartupLimit, time);
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
        PlayerProtocolClient? client = null;
        try
        {
            byte[] credential = invitation.ExportCredential();
            PlayerMessage hello;
            try { hello = PlayerHandshakeCodec.Hello(1, invitation.Compatibility, credential); }
            finally { CryptographicOperations.ZeroMemory(credential); }
            await SendBoundedAsync(transport, hello, time, attempt.Token).ConfigureAwait(false);
            CheckStartup();
            byte[]? bytes = await transport.ReceiveAsync(attempt.Token).ConfigureAwait(false);
            CheckStartup();
            if (bytes is null) throw new EndOfStreamException("The peer closed before admission.");
            PlayerMessage message = PlayerMessageCodec.Decode(bytes);
            if (message.Kind != PlayerMessageKind.Welcome || message.RequestId != 1)
                throw new FormatException("Admission requires the welcome for request 1.");
            PlayerWelcome welcome = PlayerHandshakeCodec.ReadWelcome(message);
            if (welcome.Compatibility != invitation.Compatibility || welcome.ActorId != invitation.ActorId)
                throw new FormatException("The welcome does not match the invitation.");
            client = new PlayerProtocolClient(transport, welcome, time);
            await client._firstFull.Task.WaitAsync(attempt.Token).ConfigureAwait(false);
            CheckStartup();
            lock (client._gate) client.ThrowIfClosedLocked();
            return client;
        }
        catch (Exception error)
        {
            if (client is not null) await client.DisposeAsync().ConfigureAwait(false);
            else await transport.DisposeAsync().ConfigureAwait(false);
            if (!token.IsCancellationRequested && (deadline.IsCancellationRequested || time.GetElapsedTime(started) >= StartupLimit))
                throw new TimeoutException("The player connection did not receive its first full view within the startup deadline.", error);
            throw;
        }

        void CheckStartup()
        {
            token.ThrowIfCancellationRequested();
            if (deadline.IsCancellationRequested || time.GetElapsedTime(started) >= StartupLimit)
                throw new TimeoutException("The player startup deadline elapsed.");
        }
    }

    public void SetReady(bool ready)
    {
        lock (_gate)
        {
            ThrowIfClosedLocked();
            if (_ready == ready) return;
            EnqueueLocked(id => PlayerGameplayCodec.Readiness(id, ready));
            _ready = ready;
            if (!ready) { _movementX = 0; _movementY = 0; }
        }
    }

    /// <summary>Each call is a fresh input sample; held movement is never renewed autonomously.</summary>
    public void SetMovement(int x, int y)
    {
        if (!(x == 0 && y == 0 || x == 0 && y is -32 or 32 || y == 0 && x is -32 or 32))
            throw new ArgumentOutOfRangeException(nameof(x), "Movement must be one cardinal 32-unit sample or stop.");
        lock (_gate)
        {
            ThrowIfClosedLocked();
            long now = _time.GetTimestamp();
            if (x == _movementX && y == _movementY
                && (x == 0 && y == 0 || _time.GetElapsedTime(_lastMovement, now) < MovementRenewalInterval)) return;
            EnqueueLocked(id => PlayerGameplayCodec.Movement(id, x, y));
            _movementX = x;
            _movementY = y;
            _lastMovement = now;
        }
    }

    public void RequestFullObservation()
    {
        lock (_gate)
        {
            ThrowIfClosedLocked();
            RequestFullLocked();
        }
    }

    private void RequestFullLocked()
    {
        if (_fullRequested) return;
        EnqueueLocked(id => new PlayerMessage(PlayerMessageKind.Observation, id, []));
        _fullRequested = true;
    }

    // Callers assign identities and publish output under this same lock, preserving wire order.
    private ulong EnqueueLocked(Func<ulong, PlayerMessage> create)
    {
        ThrowIfClosedLocked();
        long now = _time.GetTimestamp();
        if (_time.GetElapsedTime(_controlWindow, now) >= TimeSpan.FromSeconds(1))
        {
            _controlWindow = now;
            _controlCount = 0;
        }
        if (_lastRequestId == ulong.MaxValue || _controlCount >= 128)
        {
            var failure = new IOException("The player connection exhausted its bounded output budget.");
            CloseLocked(failure);
            throw failure;
        }
        ulong id = _lastRequestId + 1;
        PlayerMessage message = create(id);
        if (!_output.Writer.TryWrite(message))
        {
            var failure = new IOException("The player output queue is full.");
            CloseLocked(failure);
            throw failure;
        }
        _lastRequestId = id;
        _controlCount++;
        return id;
    }

    private void ThrowIfClosedLocked()
    {
        if (!_closed) return;
        if (_failure is { } failure) ExceptionDispatchInfo.Capture(failure).Throw();
        throw new ObjectDisposedException(nameof(PlayerProtocolClient));
    }

    private void CloseLocked(Exception? failure)
    {
        if (_closed) return;
        _closed = true;
        _failure = failure;
        Exception ended = failure ?? new ObjectDisposedException(nameof(PlayerProtocolClient));
        _firstFull.TrySetException(ended);
        _action?.Source.TrySetException(ended);
        _query?.Source.TrySetException(ended);
        _ping?.Source.TrySetException(ended);
        _action = null;
        _query = null;
        _ping = null;
        _output.Writer.TryComplete();
        _lifetime.Cancel();
    }

    private async Task RunAsync()
    {
        Task receive = ReceiveLoopAsync();
        Task send = SendLoopAsync();
        try
        {
            Task ended = await Task.WhenAny(receive, send).ConfigureAwait(false);
            await ended.ConfigureAwait(false);
            lock (_gate)
            {
                if (!_closed) CloseLocked(new EndOfStreamException("The player connection ended."));
            }
        }
        catch (Exception error)
        {
            lock (_gate) CloseLocked(error);
        }
        finally
        {
            try { await _transport.DisposeAsync().ConfigureAwait(false); }
            catch (Exception error) { lock (_gate) _failure ??= error; }
            try { await Task.WhenAll(receive, send).ConfigureAwait(false); }
            catch { /* The first connection failure is retained; cancellation joins both owners. */ }
            _lifetime.Dispose();
        }
        if (_failure is { } failure) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private async Task SendLoopAsync()
    {
        await foreach (PlayerMessage message in _output.Reader.ReadAllAsync(_lifetime.Token).ConfigureAwait(false))
            await SendBoundedAsync(_transport, message, _time, _lifetime.Token).ConfigureAwait(false);
    }

    private static async Task SendBoundedAsync(PlayerMessageTransport transport, PlayerMessage message,
        TimeProvider time, CancellationToken token)
    {
        long started = time.GetTimestamp();
        using var deadline = new CancellationTokenSource(SendLimit, time);
        using var send = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
        try
        {
            await transport.SendAsync(PlayerMessageCodec.Encode(message), send.Token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (deadline.IsCancellationRequested || time.GetElapsedTime(started) >= SendLimit)
                throw new TimeoutException("The player send deadline elapsed.");
        }
        catch (OperationCanceledException error) when (!token.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new TimeoutException("The player send deadline elapsed.", error);
        }
    }

    /// <summary>Joins connection shutdown; prior connection errors remain observable through Completion.</summary>
    public async ValueTask DisposeAsync()
    {
        lock (_gate) CloseLocked(null);
        try { await _completion.ConfigureAwait(false); }
        catch { /* Explicit cleanup does not rethrow a previously reported connection failure. */ }
    }
}
