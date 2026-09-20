using System.Buffers.Binary;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Construction;

/// <summary>Bounded canonical physical state, restored only against its exact retained construction owner.</summary>
public static class ConstructionPhysicalCodecV1
{
    public const int MaximumEncodedByteLength = 256 * 1024;
    private const int MaximumMemberCount = ConstructionSnapshot.MaximumProjects * ConstructionDefinition.MaximumMembers;
    private const int MaximumMemberByteLength = 256;

    public static byte[] Encode(ConstructionPhysicalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ReadOnlyMemory<byte>[] members = snapshot.Members.Select(member => (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame(
            "construction.physical.member.v1", member.StructureId.ToByteArray(), Number(member.MemberIndex),
            Number(member.Revision), Number(member.ConditionParts), Signed(member.Displacement.X),
            Signed(member.Displacement.Y), Signed(member.Displacement.Z), Number((ulong)member.Disposition))).ToArray();
        byte[] bytes = CanonicalEncodingV1.Frame("construction.physical.v1", snapshot.ConstructionHash.ToByteArray(),
            snapshot.WorldId.ToByteArray(), snapshot.BranchId.ToByteArray(), Number(snapshot.Revision),
            CanonicalEncodingV1.Frame("construction.physical.members.v1", members));
        if (bytes.Length > MaximumEncodedByteLength)
        {
            throw new ArgumentException("The physical construction snapshot exceeds its retained byte bound.", nameof(snapshot));
        }
        return bytes;
    }

    public static ConstructionPhysicalSnapshot Decode(ReadOnlySpan<byte> bytes, ConstructionSnapshot expectedConstruction)
    {
        ArgumentNullException.ThrowIfNull(expectedConstruction);
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 6);
            reader.Tag("construction.physical.v1"u8);
            if (!reader.Exact(32).SequenceEqual(expectedConstruction.ContentHash.ToByteArray()))
            {
                throw new FormatException("The physical state belongs to a different retained construction snapshot.");
            }
            EntityId world = reader.Identity();
            EntityId branch = reader.Identity();
            if (world != expectedConstruction.WorldId || branch != expectedConstruction.BranchId)
            {
                throw new FormatException("The physical state belongs to a different world or branch.");
            }
            ulong revision = reader.Number();
            var members = new WorldFrameReader(reader.Field(MaximumEncodedByteLength), MaximumEncodedByteLength,
                MaximumMemberCount + 1);
            members.Tag("construction.physical.members.v1"u8);
            List<ConstructionMemberState> values = [];
            while (members.Remaining != 0)
            {
                var member = new WorldFrameReader(members.Field(MaximumMemberByteLength), MaximumMemberByteLength, 9);
                member.Tag("construction.physical.member.v1"u8);
                EntityId structure = member.Identity();
                uint ordinal = checked((uint)member.Number());
                ulong memberRevision = member.Number();
                uint condition = checked((uint)member.Number());
                var displacement = new SubunitPosition(member.Signed(), member.Signed(), member.Signed());
                var disposition = (ConstructionMemberDisposition)checked((int)member.Number());
                member.Complete();
                if (!Enum.IsDefined(disposition))
                {
                    throw new FormatException("The physical member disposition is unsupported.");
                }
                values.Add(new ConstructionMemberState(structure, ordinal, memberRevision, condition, displacement, disposition));
            }
            members.Complete();
            reader.Complete();
            var restored = new ConstructionPhysicalSnapshot(expectedConstruction, revision, values);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The physical construction snapshot is not canonical.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The physical construction snapshot violates its retained ownership or bounds.", error);
        }
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);

    private static byte[] Signed(long value)
    {
        byte[] bytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }
}
