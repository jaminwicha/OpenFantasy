namespace OpenFantasy.Hosting.Networking;

public enum PlayerMessageKind : ushort
{
    Hello = 1,
    Welcome = 2,
    Readiness = 3,
    Movement = 4,
    Command = 5,
    Receipt = 6,
    Observation = 7,
    Refusal = 8,
    Goodbye = 9,
    Ping = 10,
    Pong = 11,
    Query = 12,
    QueryResult = 13
}

/// <summary>A transport envelope, not authentication, a world command or a durable receipt.</summary>
public sealed class PlayerMessage
{
    private readonly byte[] _payload;

    public PlayerMessage(PlayerMessageKind kind, ulong requestId, ReadOnlySpan<byte> payload)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (payload.Length > PlayerMessageCodec.MaximumPayloadBytes)
            throw new ArgumentException("The player message exceeds its payload limit.", nameof(payload));
        Kind = kind;
        RequestId = requestId;
        _payload = payload.ToArray();
    }

    public PlayerMessageKind Kind { get; }
    public ulong RequestId { get; }
    public ReadOnlySpan<byte> Payload => _payload;
}
