using System.Threading.Channels;

namespace OpenFantasy.Hosting.Networking;

/// <summary>Two bounded ordered endpoints carrying the same bytes as the WebSocket adapter.</summary>
public sealed class InMemoryPlayerTransport : PlayerMessageTransport
{
    public const int MaximumQueuedMessages = 8;
    private readonly Channel<byte[]> _incoming;
    private readonly Channel<byte[]> _outgoing;

    private InMemoryPlayerTransport(Channel<byte[]> incoming, Channel<byte[]> outgoing, int maximumMessageBytes)
        : base(maximumMessageBytes)
    {
        _incoming = incoming;
        _outgoing = outgoing;
    }

    public static (InMemoryPlayerTransport Client, InMemoryPlayerTransport Server) CreatePair(
        int maximumMessageBytes = PlayerMessageCodec.MaximumMessageBytes)
    {
        Channel<byte[]> toServer = CreateQueue();
        Channel<byte[]> toClient = CreateQueue();
        return (new(toClient, toServer, maximumMessageBytes), new(toServer, toClient, maximumMessageBytes));
    }

    protected override ValueTask SendMessageAsync(byte[] message, CancellationToken cancellationToken) =>
        _outgoing.Writer.WriteAsync(message, cancellationToken);

    protected override async ValueTask<byte[]?> ReceiveMessageAsync(CancellationToken cancellationToken)
    {
        try { return await _incoming.Reader.ReadAsync(cancellationToken).ConfigureAwait(false); }
        catch (ChannelClosedException) { return null; }
    }

    protected override void CloseTransport()
    {
        _outgoing.Writer.TryComplete();
        _incoming.Writer.TryComplete();
    }

    private static Channel<byte[]> CreateQueue() => Channel.CreateBounded<byte[]>(new BoundedChannelOptions(MaximumQueuedMessages)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = true,
        AllowSynchronousContinuations = false
    });
}
