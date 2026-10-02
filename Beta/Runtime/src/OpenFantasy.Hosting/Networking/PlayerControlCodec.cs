using System.Buffers.Binary;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking;

/// <summary>Versioned transient control proof. No field is a world sequence or durable world value.</summary>
public static class PlayerControlCodec
{
    public const int ByteLength = 66;

    public static byte[] Encode(PlayerControlStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        Validate(status);
        byte[] bytes = new byte[ByteLength];
        bytes[0] = 1;
        bytes[1] = (byte)status.State;
        status.PortId.WriteBytes(bytes.AsSpan(2));
        status.ActorId.WriteBytes(bytes.AsSpan(18));
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(34), status.Generation);
        status.ObservationStreamId.WriteBytes(bytes.AsSpan(42));
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(58), status.ObservationRevision);
        return bytes;
    }

    public static PlayerControlStatus Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != ByteLength || bytes[0] != 1) throw new FormatException("Invalid player control status.");
        var status = new PlayerControlStatus((PlayerControlState)bytes[1], EntityId.FromBytes(bytes.Slice(2, 16)),
            EntityId.FromBytes(bytes.Slice(18, 16)), BinaryPrimitives.ReadUInt64BigEndian(bytes[34..]),
            EntityId.FromBytes(bytes.Slice(42, 16)), BinaryPrimitives.ReadUInt64BigEndian(bytes[58..]));
        Validate(status);
        return status;
    }

    private static void Validate(PlayerControlStatus status)
    {
        if (!Enum.IsDefined(status.State) || status.PortId == default || status.ObservationStreamId == default
            || status.ObservationRevision == 0
            || (status.State == PlayerControlState.Pending && status.Generation != 0)
            || (status.State == PlayerControlState.Restored && status.Generation != 1)
            || status.Generation > 1)
            throw new FormatException("Invalid player control binding.");
    }
}
