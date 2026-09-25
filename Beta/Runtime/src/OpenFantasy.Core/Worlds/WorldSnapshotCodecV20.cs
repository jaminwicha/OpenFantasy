using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using static OpenFantasy.Core.Worlds.WorldSnapshotCodecV1;
using static OpenFantasy.Core.Worlds.WorldSpatialCodecV1;

namespace OpenFantasy.Core.Worlds;

/// <summary>Complete contained world with source-backed detailed liquid placement at its exact clock boundary.</summary>
public static class WorldSnapshotCodecV20
{
    public static byte[] Encode(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.ProfileVersion != 20 || world.Reservoirs is null || world.ReservoirSeals is null || world.LiquidCells is null)
        {
            throw new ArgumentException("This codec requires the detailed liquid-cell world profile.", nameof(world));
        }
        byte[] bytes = CanonicalEncodingV1.Frame("world.snapshot", Number(20), AuthorityEncodingV1.Encode(world.Inventory),
            EncodeComponents(world.Space, 1), EncodeResidents(world.Residents), world.AdmissionVersion.ToByteArray(),
            Signed(world.Calendar.Days), Number((ulong)world.Calendar.MillisecondsWithinDay), Number(world.Revision),
            Number(world.AdvancingSteps), EncodeDialogues(world.Dialogues), GatheringCodecV1.Encode(world.Gathering!),
            ProductionCodecV1.Encode(world.Production!), ProgressionCodecV1.Encode(world.Progression!),
            QuestCodecV1.Encode(world.Quests!), ConstructionCodecV1.Encode(world.Construction!),
            LiquidReservoirCodecV1.Encode(world.Reservoirs), ReservoirSealPolicyCodecV1.Encode(world.ReservoirSeals),
            LiquidCellStateCodecV1.Encode(world.LiquidCells));
        if (bytes.Length > WorldSnapshotCodec.MaximumStateByteLength)
        {
            throw new ArgumentException("The detailed liquid-cell world exceeds its retained storage bound.", nameof(world));
        }
        return bytes;
    }

    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, WorldSnapshotCodec.MaximumStateByteLength, 19);
            reader.Tag("world.snapshot"u8);
            if (reader.Number() != 20)
            {
                throw new FormatException("Unsupported detailed liquid-cell world version.");
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
            LiquidReservoirState reservoirs = LiquidReservoirCodecV1.Decode(reader.Field(LiquidReservoirCodecV1.MaximumEncodedByteLength));
            ReservoirSealPolicy seals = ReservoirSealPolicyCodecV1.Decode(reader.Field(ReservoirSealPolicyCodecV1.MaximumEncodedByteLength));
            LiquidCellState cells = LiquidCellStateCodecV1.Decode(reader.Field(LiquidCellStateCodecV1.MaximumEncodedByteLength), reservoirs);
            reader.Complete();
            // Installed colliders belong to retained construction, never a duplicated spatial payload.
            var components = DecodeComponents(spatialBytes, 1);
            var space = new LocalSpatialSnapshot(components.Scope, components.Regions, components.Actors, construction.GetStructures());
            var restored = new WorldSnapshot(inventory, space, residents, admission, calendar, revision, advancingSteps,
                dialogues, gathering, production, progression, quests, construction, reservoirs: reservoirs,
                reservoirSeals: seals, liquidCells: cells);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The detailed liquid-cell world is not canonical.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The detailed liquid-cell world violates its complete admission invariants.", error);
        }
    }
}
