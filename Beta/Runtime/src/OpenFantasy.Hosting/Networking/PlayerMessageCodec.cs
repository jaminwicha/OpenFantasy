using System.Buffers.Binary;

namespace OpenFantasy.Hosting.Networking;

/// <summary>Versioned big-endian framing. Semantic payloads keep their own codecs and permission checks.</summary>
public static class PlayerMessageCodec
{
    public const ushort Version = 1;
    public const int HeaderBytes = 20;
    public const int MaximumPayloadBytes = 1024 * 1024;
    public const int MaximumMessageBytes = HeaderBytes + MaximumPayloadBytes;
    private static ReadOnlySpan<byte> Magic => "OFWP"u8;

    public static byte[] Encode(PlayerMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        byte[] bytes = new byte[HeaderBytes + message.Payload.Length];
        Magic.CopyTo(bytes);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(4), Version);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(6), (ushort)message.Kind);
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(8), message.RequestId);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), (uint)message.Payload.Length);
        message.Payload.CopyTo(bytes.AsSpan(HeaderBytes));
        return bytes;
    }

    public static PlayerMessage Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderBytes || bytes.Length > MaximumMessageBytes || !bytes[..4].SequenceEqual(Magic))
            throw new FormatException("Invalid player message framing.");
        if (BinaryPrimitives.ReadUInt16BigEndian(bytes[4..]) != Version)
            throw new FormatException("Unsupported player protocol version.");
        var kind = (PlayerMessageKind)BinaryPrimitives.ReadUInt16BigEndian(bytes[6..]);
        if (!Enum.IsDefined(kind) || BinaryPrimitives.ReadUInt32BigEndian(bytes[16..]) != bytes.Length - HeaderBytes)
            throw new FormatException("Invalid player message kind or payload length.");
        return new PlayerMessage(kind, BinaryPrimitives.ReadUInt64BigEndian(bytes[8..]), bytes[HeaderBytes..]);
    }
}
