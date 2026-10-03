using System.Buffers.Binary;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>Fixed-width, big-endian witness payload, bounded to fit retained ordinary action envelopes.</summary>
public static class WorldControlWitnessCodecV1
{
    public const int MaximumByteLength = 256;
    private const int PayloadLength = 169;
    public static byte[] Encode(WorldControlWitness witness)
    {
        ArgumentNullException.ThrowIfNull(witness);
        byte[] payload = new byte[PayloadLength];
        payload[0] = 1;
        int offset = 1;
        foreach (var id in new[] { witness.OwnerActorId, witness.GranteeActorId, witness.GrantCommandId,
            witness.AcceptanceCommandId, witness.AssignmentId, witness.DisconnectId })
        { id.ToByteArray().CopyTo(payload, offset); offset += 16; }
        foreach (ulong value in new[] { witness.GrantRevision, (ulong)witness.Capabilities, witness.AssignmentRevision,
            witness.AcquiredSequence, witness.OwnerControlRevision })
        { BinaryPrimitives.WriteUInt64BigEndian(payload.AsSpan(offset), value); offset += 8; }
        witness.TermsHash.ToByteArray().CopyTo(payload, offset);
        return CanonicalEncodingV1.Frame("world.control-witness.v1", payload);
    }
    public static WorldControlWitness Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumByteLength, 2);
            reader.Tag("world.control-witness.v1"u8);
            var payload = reader.Exact(PayloadLength);
            reader.Complete();
            if (payload[0] != 1) throw new FormatException("Unsupported control witness version.");
            var ids = new EntityId[6];
            int offset = 1;
            for (int index = 0; index < ids.Length; index++, offset += 16) ids[index] = EntityId.FromBytes(payload.Slice(offset, 16));
            var values = new ulong[5];
            for (int index = 0; index < values.Length; index++, offset += 8) values[index] = BinaryPrimitives.ReadUInt64BigEndian(payload[offset..]);
            var hash = new ContentHash(Convert.ToHexString(payload.Slice(offset, 32)).ToLowerInvariant());
            var result = new WorldControlWitness(ids[0], ids[1], ids[2], ids[3], values[0], (WorldControlCapabilities)values[1],
                hash, ids[4], values[2], values[3], ids[5], values[4]);
            if (!Encode(result).AsSpan().SequenceEqual(bytes)) throw new FormatException("Noncanonical control witness.");
            return result;
        }
        catch (ArgumentException error) { throw new FormatException("Invalid control witness.", error); }
    }
}
