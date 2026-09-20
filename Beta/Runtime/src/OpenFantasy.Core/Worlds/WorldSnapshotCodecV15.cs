using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using static OpenFantasy.Core.Worlds.WorldSnapshotCodecV1;
using static OpenFantasy.Core.Worlds.WorldSpatialCodecV1;

namespace OpenFantasy.Core.Worlds;

/// <summary>Complete physical world with immutable load and atomic settlement policies.</summary>
public static class WorldSnapshotCodecV15
{
    public static byte[] Encode(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.ProfileVersion != 15 || world.StructuralPolicy is null || world.ConstructionPhysical is null
            || world.StructuralSettlementPolicy is null)
        {
            throw new ArgumentException("This codec requires the structural settlement world profile.", nameof(world));
        }
        byte[] bytes = CanonicalEncodingV1.Frame("world.snapshot", Number(15), AuthorityEncodingV1.Encode(world.Inventory),
            WorldSpatialCodecV3.Encode(world.Space), EncodeResidents(world.Residents), world.AdmissionVersion.ToByteArray(),
            Signed(world.Calendar.Days), Number((ulong)world.Calendar.MillisecondsWithinDay), Number(world.Revision),
            Number(world.AdvancingSteps), EncodeDialogues(world.Dialogues), GatheringCodecV1.Encode(world.Gathering!),
            ProductionCodecV1.Encode(world.Production!), ProgressionCodecV1.Encode(world.Progression!),
            QuestCodecV1.Encode(world.Quests!), ConstructionCodecV1.Encode(world.Construction!), ExcavationCodecV1.Encode(world.Excavation!),
            ConstructionPhysicalCodecV1.Encode(world.ConstructionPhysical), ConstructionLoadPolicyCodecV1.Encode(world.StructuralPolicy),
            StructuralSettlementPolicyCodecV1.Encode(world.StructuralSettlementPolicy));
        if (bytes.Length > WorldSnapshotCodec.MaximumStateByteLength)
        {
            throw new ArgumentException("The structural settlement world exceeds its retained storage bound.", nameof(world));
        }
        return bytes;
    }

    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, WorldSnapshotCodec.MaximumStateByteLength, 20);
            reader.Tag("world.snapshot"u8);
            if (reader.Number() != 15) { throw new FormatException("Unsupported structural settlement world version."); }
            CountedAuthorityState inventory = AuthorityEncodingV1.Decode(reader.Field(AuthorityEncodingV1.MaximumStateByteLength));
            ReadOnlySpan<byte> spatialBytes = reader.Field(WorldSnapshotCodec.MaximumStateByteLength);
            List<WorldResident> residents = DecodeResidents(reader.Field(MaximumResidentsByteLength));
            var admission = new ContentHash(Convert.ToHexString(reader.Exact(32)).ToLowerInvariant());
            WorldInstant calendar = ReadInstant(ref reader);
            ulong revision = reader.Number();
            ulong advancingSteps = reader.Number();
            List<DialogueSession> dialogues = DecodeDialogues(reader.Field(MaximumDialoguesByteLength));
            GatheringSnapshot gathering = GatheringCodecV1.Decode(reader.Field(GatheringCodecV1.MaximumSnapshotByteLength));
            ProductionSnapshot production = ProductionCodecV1.Decode(reader.Field(ProductionCodecV1.MaximumSnapshotByteLength));
            ProgressionSnapshot progression = ProgressionCodecV1.Decode(reader.Field(ProgressionCodecV1.MaximumSnapshotByteLength));
            QuestSnapshot quests = QuestCodecV1.Decode(reader.Field(QuestCodecV1.MaximumSnapshotByteLength));
            ConstructionSnapshot construction = ConstructionCodecV1.Decode(reader.Field(ConstructionCodecV1.MaximumSnapshotByteLength));
            ExcavationSnapshot excavation = ExcavationCodecV1.Decode(reader.Field(ExcavationCodecV1.MaximumEncodedByteLength));
            ConstructionPhysicalSnapshot physical = ConstructionPhysicalCodecV1.Decode(
                reader.Field(ConstructionPhysicalCodecV1.MaximumEncodedByteLength), construction);
            ReadOnlySpan<byte> policyBytes = reader.Field(ConstructionLoadPolicyCodecV1.MaximumEncodedByteLength);
            // The enclosing root and its journal own this pin; no derived model is retained.
            ConstructionLoadPolicy policy = ConstructionLoadPolicyCodecV1.Decode(policyBytes, ContentHash.Compute(policyBytes));
            StructuralSettlementPolicy settlement = StructuralSettlementPolicyCodecV1.Decode(
                reader.Field(WorldSnapshotCodec.MaximumStateByteLength));
            reader.Complete();
            LocalSpatialSnapshot space = WorldSpatialCodecV3.Decode(spatialBytes, physical.GetStructures());
            var restored = new WorldSnapshot(inventory, space, residents, admission, calendar, revision, advancingSteps,
                dialogues, gathering, production, progression, quests, construction, excavation: excavation,
                constructionPhysical: physical, structuralPolicy: policy, structuralSettlementPolicy: settlement);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The structural settlement world is not canonical.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The structural settlement world violates its complete admission invariants.", error);
        }
    }
}
