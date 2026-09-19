using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using static OpenFantasy.Core.Worlds.WorldSnapshotCodecV1;
using static OpenFantasy.Core.Worlds.WorldSpatialCodecV1;

namespace OpenFantasy.Core.Worlds;

/// <summary>Direct complete state whose grounded actors may depend on installed construction for support.</summary>
public static class WorldSnapshotCodecV11
{
    public static byte[] Encode(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.ProfileVersion != 11 || world.Excavation is null || world.Construction is null
            || world.Space.GroundSupport != GroundSupportMode.TerrainAndConstruction)
        {
            throw new ArgumentException("This codec requires the construction-supported excavation world profile.", nameof(world));
        }
        byte[] bytes = CanonicalEncodingV1.Frame("world.snapshot", Number(11), AuthorityEncodingV1.Encode(world.Inventory),
            WorldSpatialCodecV2.Encode(world.Space), EncodeResidents(world.Residents), world.AdmissionVersion.ToByteArray(),
            Signed(world.Calendar.Days), Number((ulong)world.Calendar.MillisecondsWithinDay), Number(world.Revision),
            Number(world.AdvancingSteps), EncodeDialogues(world.Dialogues), GatheringCodecV1.Encode(world.Gathering!),
            ProductionCodecV1.Encode(world.Production!), ProgressionCodecV1.Encode(world.Progression!),
            QuestCodecV1.Encode(world.Quests!), ConstructionCodecV1.Encode(world.Construction), ExcavationCodecV1.Encode(world.Excavation));
        if (bytes.Length > WorldSnapshotCodec.MaximumStateByteLength)
        {
            throw new ArgumentException("The construction-supported world exceeds its retained storage bound.", nameof(world));
        }
        return bytes;
    }

    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, WorldSnapshotCodec.MaximumStateByteLength, 17);
            reader.Tag("world.snapshot"u8);
            if (reader.Number() != 11)
            {
                throw new FormatException("Unsupported construction-supported world version.");
            }
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
            reader.Complete();

            // Grounded actor admission needs the retained construction's actual support surfaces.
            LocalSpatialSnapshot space = WorldSpatialCodecV2.Decode(spatialBytes, construction.GetStructures(supportsActors: true));
            var restored = new WorldSnapshot(inventory, space, residents, admission, calendar, revision, advancingSteps,
                dialogues, gathering, production, progression, quests, construction, excavation: excavation);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The construction-supported world is not canonical.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The construction-supported world violates its admission invariants.", error);
        }
    }
}
