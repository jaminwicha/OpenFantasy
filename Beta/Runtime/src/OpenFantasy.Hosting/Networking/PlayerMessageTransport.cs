namespace OpenFantasy.Hosting.Networking;

/// <summary>
/// Bounded complete-message I/O shared by local and socket connections. One send and one receive may
/// overlap; competing sends or receives are refused instead of accumulating an unbounded waiter queue.
/// Disposal cancels outstanding I/O and aborts the connection; it is not a gameplay disconnect decision.
/// </summary>
public abstract class PlayerMessageTransport : IAsyncDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private int _disposed;
    private int _sending;
    private int _receiving;

    protected PlayerMessageTransport(int maximumMessageBytes)
    {
        if (maximumMessageBytes < PlayerMessageCodec.HeaderBytes
            || maximumMessageBytes > PlayerMessageCodec.MaximumMessageBytes)
            throw new ArgumentOutOfRangeException(nameof(maximumMessageBytes));
        MaximumMessageBytes = maximumMessageBytes;
    }

    public int MaximumMessageBytes { get; }

    public async ValueTask SendAsync(ReadOnlyMemory<byte> message, CancellationToken cancellationToken = default)
    {
        if (message.Length > MaximumMessageBytes)
            throw new InvalidDataException("The transport message exceeds its byte limit.");
        Enter(ref _sending);
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
            // Both adapters capture the caller's bytes before asynchronous delivery begins.
            await SendMessageAsync(message.ToArray(), linked.Token).ConfigureAwait(false);
        }
        finally { Volatile.Write(ref _sending, 0); }
    }

    /// <summary>Returns null for orderly peer end-of-stream; abrupt socket failure remains an I/O error.</summary>
    public async ValueTask<byte[]?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        Enter(ref _receiving);
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
            byte[]? message = await ReceiveMessageAsync(linked.Token).ConfigureAwait(false);
            if (message?.Length > MaximumMessageBytes)
                throw new InvalidDataException("The peer exceeded the transport message limit.");
            return message;
        }
        catch (InvalidDataException)
        {
            // Never reinterpret an unread malformed message suffix as another logical message.
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
        finally { Volatile.Write(ref _receiving, 0); }
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _lifetime.Cancel();
            CloseTransport();
            _lifetime.Dispose();
        }
        return ValueTask.CompletedTask;
    }

    protected abstract ValueTask SendMessageAsync(byte[] message, CancellationToken cancellationToken);
    protected abstract ValueTask<byte[]?> ReceiveMessageAsync(CancellationToken cancellationToken);
    protected abstract void CloseTransport();

    private void Enter(ref int operation)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.CompareExchange(ref operation, 1, 0) != 0)
            throw new InvalidOperationException("Only one operation in each transport direction may be active.");
    }
}
