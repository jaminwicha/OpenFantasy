using System.Diagnostics;
using System.Net.WebSockets;

namespace OpenFantasy.Hosting.Networking;

/// <summary>
/// Ordered binary WebSocket messages with bounded fragmentation. The caller owns connection admission,
/// TLS and credentials; wrapping an open socket grants no world or actor access.
/// </summary>
public sealed class WebSocketPlayerTransport : PlayerMessageTransport
{
    private const int ReceiveBufferBytes = 8192;
    public const int MaximumReceiveParts = 1024;
    public static readonly TimeSpan ReassemblyLimit = TimeSpan.FromSeconds(5);
    private readonly WebSocket _socket;

    public WebSocketPlayerTransport(WebSocket socket, int maximumMessageBytes = PlayerMessageCodec.MaximumMessageBytes)
        : base(maximumMessageBytes)
    {
        ArgumentNullException.ThrowIfNull(socket);
        if (socket.State != WebSocketState.Open) throw new ArgumentException("An open WebSocket is required.", nameof(socket));
        _socket = socket;
    }

    protected override async ValueTask SendMessageAsync(byte[] message, CancellationToken cancellationToken)
    {
        try { await _socket.SendAsync(message.AsMemory(), WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    protected override async ValueTask<byte[]?> ReceiveMessageAsync(CancellationToken cancellationToken)
    {
        try { return await ReassembleAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            // A canceled socket operation may have consumed a prefix. Never resume at that suffix.
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async ValueTask<byte[]?> ReassembleAsync(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[Math.Min(ReceiveBufferBytes, MaximumMessageBytes)];
        using var content = new MemoryStream();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        long? started = null;
        for (int part = 0; part < MaximumReceiveParts; part++)
        {
            ValueWebSocketReceiveResult received = await _socket.ReceiveAsync(buffer.AsMemory(), deadline.Token).ConfigureAwait(false);
            if (started is { } began && Stopwatch.GetElapsedTime(began) >= ReassemblyLimit)
                throw new InvalidDataException("The player message exceeded its reassembly deadline.");
            if (received.MessageType == WebSocketMessageType.Close)
            {
                if (started is not null) throw new InvalidDataException("The peer closed inside a player message.");
                return null;
            }
            if (received.MessageType != WebSocketMessageType.Binary || content.Length + received.Count > MaximumMessageBytes)
                throw new InvalidDataException("The peer sent a nonbinary or oversized player message.");
            if (started is null)
            {
                started = Stopwatch.GetTimestamp();
                deadline.CancelAfter(ReassemblyLimit);
            }
            content.Write(buffer, 0, received.Count);
            if (received.EndOfMessage) return content.ToArray();
        }
        throw new InvalidDataException("The player message exceeded its receive-part limit.");
    }

    protected override void CloseTransport()
    {
        _socket.Abort();
        _socket.Dispose();
    }
}
