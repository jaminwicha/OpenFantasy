using System.Buffers.Binary;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking.Selection;

/// <summary>Version 1, three exact identities, generation and a length-delimited canonical immutable witness.</summary>
public static class PlayerSelectionCodec
{
    public const int HeaderBytes = 59;
    public const int MaximumBodyBytes = HeaderBytes + WorldControlWitnessCodecV1.MaximumByteLength;
    public static byte[] Encode(LocalActorSelection selection)
    {
        Validate(selection);
        byte[] witness = selection.ControlWitness is { } value ? WorldControlWitnessCodecV1.Encode(value) : [];
        byte[] bytes = new byte[HeaderBytes + witness.Length];
        bytes[0] = 1;
        selection.PortId.WriteBytes(bytes.AsSpan(1)); selection.HomeActorId.WriteBytes(bytes.AsSpan(17));
        selection.ActorId.WriteBytes(bytes.AsSpan(33));
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(49), selection.Generation);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(57), checked((ushort)witness.Length));
        witness.CopyTo(bytes, HeaderBytes);
        return bytes;
    }
    public static LocalActorSelection Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < HeaderBytes || bytes.Length > MaximumBodyBytes || bytes[0] != 1)
            throw new FormatException("Invalid actor selection header.");
        int length = BinaryPrimitives.ReadUInt16BigEndian(bytes[57..]);
        if (bytes.Length != HeaderBytes + length) throw new FormatException("Invalid actor selection witness length.");
        var result = new LocalActorSelection(EntityId.FromBytes(bytes.Slice(1, 16)), EntityId.FromBytes(bytes.Slice(17, 16)),
            EntityId.FromBytes(bytes.Slice(33, 16)), BinaryPrimitives.ReadUInt64BigEndian(bytes[49..]),
            length == 0 ? null : WorldControlWitnessCodecV1.Decode(bytes[HeaderBytes..]));
        Validate(result);
        if (!bytes.SequenceEqual(Encode(result))) throw new FormatException("Noncanonical actor selection.");
        return result;
    }
    internal static void Validate(LocalActorSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.PortId == default || selection.HomeActorId == default || selection.ActorId == default
            || (selection.ControlWitness is null) != (selection.ActorId == selection.HomeActorId)
            || selection.ControlWitness is { } witness && (selection.Generation == 0
                || witness.OwnerActorId != selection.ActorId || witness.GranteeActorId != selection.HomeActorId))
            throw new FormatException("Actor selection contradicts its home, generation or witness.");
    }
}
