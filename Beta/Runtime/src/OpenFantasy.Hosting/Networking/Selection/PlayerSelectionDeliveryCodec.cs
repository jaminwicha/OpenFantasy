using System.Buffers.Binary;
using OpenFantasy.Core;

namespace OpenFantasy.Hosting.Networking.Selection;

/// <summary>Confirms that a selection lifetime's Resume basis completed the bounded transport send.</summary>
public static class PlayerSelectionDeliveryCodec
{
    public const int ByteLength = 1 + EntityId.ByteLength + sizeof(ulong);

    public static byte[] Encode(EntityId streamId, ulong revision)
    {
        Validate(streamId, revision);
        byte[] bytes = new byte[ByteLength];
        bytes[0] = 1;
        streamId.WriteBytes(bytes.AsSpan(1));
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(1 + EntityId.ByteLength), revision);
        return bytes;
    }

    public static (EntityId StreamId, ulong Revision) Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != ByteLength || bytes[0] != 1)
            throw new FormatException("Invalid selected observation delivery proof.");
        EntityId streamId = EntityId.FromBytes(bytes.Slice(1, EntityId.ByteLength));
        ulong revision = BinaryPrimitives.ReadUInt64BigEndian(bytes[(1 + EntityId.ByteLength)..]);
        Validate(streamId, revision);
        return (streamId, revision);
    }

    private static void Validate(EntityId streamId, ulong revision)
    {
        if (streamId == default || revision == 0)
            throw new FormatException("A delivered selection requires a stream and positive revision.");
    }
}
