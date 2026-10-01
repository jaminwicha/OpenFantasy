using OpenFantasy.Core.Care;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using static OpenFantasy.Core.Worlds.WorldSnapshotCodecV1;
using static OpenFantasy.Core.Worlds.WorldSpatialCodecV1;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Complete local-market world (profile 33). Its coordination frame carries the market owner after the
/// paid-quote owner; every other owner keeps the paid world's frames on both sides of a cut. Profile-32
/// roots keep their own codec and bytes and never decode here.
/// </summary>
public static class WorldSnapshotCodecV33
{
    private const int MaximumBindingByteLength = 512;
    private const int MaximumCoordinationByteLength = CareCodecV2.MaximumEncodedByteLength
        + WorldSourceIssuanceBaseline.MaximumEncodedByteLength + MaximumBindingByteLength
        + WorldConsentedMaterialHistory.MaximumByteLength + WorldMaterialProposalBook.MaximumByteLength
        + WorldPaidExchangeBook.MaximumByteLength + WorldMarketBook.MaximumByteLength + 192;

    public static byte[] Encode(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.ProfileVersion != WorldMarketAdmission.ProfileVersion
            || world.Coordination is not { ConsentedMaterial: { } history, Proposals: { } proposals, PaidExchange: { } paid, Markets: { } markets } coordination
            || world.Care is null || world.EncounterLifecycle is null || world.Pacing is null || world.Reservoirs is null
            || world.ReservoirSeals is null || world.LiquidCells?.Topology is null || world.Excavation is null)
        {
            throw new ArgumentException("This codec requires the complete local-market world profile.", nameof(world));
        }
        WorldMaterialCareArchiveAnchor? anchor = coordination.Anchor;
        byte[] liquidBytes = LiquidCellStateCodecV2.Encode(world.LiquidCells);
        byte[] pacingBytes = WorldPacingCodecV1.Encode(world.Pacing);
        byte[] actionBytes = EncounterLifecycleCodecV1.Encode(world.EncounterLifecycle);
        byte[] careBytes = anchor is null ? CareCodecV2.Encode(world.Care) : CareCodecV3.Encode(world.Care);
        byte[] coordinationBytes = CanonicalEncodingV1.Frame("world.coordinated-market-care.v1", careBytes,
            coordination.Sources.Encode(), anchor?.EncodeBinding() ?? [], WorldConsentedMaterialCodec.Encode(history),
            WorldMaterialProposalCodec.Encode(proposals), WorldPaidExchangeCodec.Encode(paid), WorldMarketCodec.Encode(markets));
        byte[] resourceBytes = WorldResourceHistoryCodec.Encode(world.ResourceHistory!);
        byte[] bytes = CanonicalEncodingV1.Frame("world.snapshot", Number(WorldMarketAdmission.ProfileVersion),
            AuthorityEncodingV1.Encode(world.Inventory),
            EncodeComponents(world.Space, 1), EncodeResidents(world.Residents), world.AdmissionVersion.ToByteArray(),
            Signed(world.Calendar.Days), Number((ulong)world.Calendar.MillisecondsWithinDay), Number(world.Revision),
            Number(world.AdvancingSteps), EncodeDialogues(world.Dialogues), GatheringCodecV1.Encode(world.Gathering!),
            anchor is null ? ProductionCodecV2.Encode(world.Production!) : ProductionCodecV11.Encode(world.Production!),
            ProgressionCodecV1.Encode(world.Progression!), QuestCodecV1.Encode(world.Quests!),
            ConstructionCodecV1.Encode(world.Construction!),
            anchor is null ? LiquidReservoirCodecV2.Encode(world.Reservoirs) : LiquidReservoirCodecV3.Encode(world.Reservoirs),
            ReservoirSealPolicyCodecV1.Encode(world.ReservoirSeals), liquidBytes, ExcavationCodecV2.Encode(world.Excavation),
            pacingBytes, actionBytes, coordinationBytes, resourceBytes);
        if ((long)bytes.Length - liquidBytes.Length - pacingBytes.Length - actionBytes.Length - coordinationBytes.Length - resourceBytes.Length
            + LiquidCellStateCodecV2.MaximumEncodedByteLength + WorldPacingCodecV1.MaximumEncodedByteLength
            + EncounterLifecycleCodecV1.MaximumEncodedByteLength + MaximumCoordinationByteLength
            + WorldResourceHistoryCodec.MaximumEncodedByteLength > WorldSnapshotCodec.MaximumStateByteLength)
        {
            throw new ArgumentException("The complete local-market world exceeds its retained storage bound.", nameof(world));
        }
        return bytes;
    }

    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes, WorldMaterialCareArchiveAnchor? anchor = null)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, WorldSnapshotCodec.MaximumStateByteLength, 24);
            reader.Tag("world.snapshot"u8);
            if (reader.Number() != WorldMarketAdmission.ProfileVersion)
            {
                throw new FormatException("Unsupported complete local-market world version.");
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
            ReadOnlySpan<byte> productionBytes = reader.Field(ProductionCodecV2.MaximumSnapshotByteLength);
            ProgressionSnapshot progression = ProgressionCodecV1.Decode(reader.Field(ProgressionCodecV1.MaximumSnapshotByteLength));
            QuestSnapshot quests = QuestCodecV1.Decode(reader.Field(QuestCodecV1.MaximumSnapshotByteLength));
            ConstructionSnapshot construction = ConstructionCodecV1.Decode(reader.Field(ConstructionCodecV1.MaximumSnapshotByteLength));
            ReadOnlySpan<byte> reservoirBytes = reader.Field(LiquidReservoirCodecV2.MaximumEncodedByteLength);
            ReservoirSealPolicy seals = ReservoirSealPolicyCodecV1.Decode(reader.Field(ReservoirSealPolicyCodecV1.MaximumEncodedByteLength));
            ReadOnlySpan<byte> liquidBytes = reader.Field(LiquidCellStateCodecV2.MaximumEncodedByteLength);
            ExcavationSnapshot excavation = ExcavationCodecV2.Decode(reader.Field(ExcavationCodecV2.MaximumEncodedByteLength));
            WorldPacingState pacing = WorldPacingCodecV1.Decode(reader.Field(WorldPacingCodecV1.MaximumEncodedByteLength));
            EncounterLifecycleSnapshot lifecycle = EncounterLifecycleCodecV1.Decode(
                reader.Field(EncounterLifecycleCodecV1.MaximumEncodedByteLength), pacing.WorldId, pacing.BranchId);
            ReadOnlySpan<byte> coordinationBytes = reader.Field(MaximumCoordinationByteLength);
            ReadOnlySpan<byte> resourceBytes = reader.Field(WorldResourceHistoryCodec.MaximumEncodedByteLength);
            reader.Complete();

            var joint = new WorldFrameReader(coordinationBytes, MaximumCoordinationByteLength, 8);
            joint.Tag("world.coordinated-market-care.v1"u8);
            ReadOnlySpan<byte> careBytes = joint.Field(CareCodecV2.MaximumEncodedByteLength);
            var sources = WorldSourceIssuanceBaseline.Decode(joint.Field(WorldSourceIssuanceBaseline.MaximumEncodedByteLength));
            ReadOnlySpan<byte> binding = joint.Field(MaximumBindingByteLength);
            var history = WorldConsentedMaterialCodec.Decode(joint.Field(WorldConsentedMaterialHistory.MaximumByteLength));
            var proposals = WorldMaterialProposalCodec.Decode(joint.Field(WorldMaterialProposalBook.MaximumByteLength));
            var paid = WorldPaidExchangeCodec.Decode(joint.Field(WorldPaidExchangeBook.MaximumByteLength));
            var markets = WorldMarketCodec.Decode(joint.Field(WorldMarketBook.MaximumByteLength));
            joint.Complete();
            // The stored binding names the boundary; only the supplied actual anchor provides its context.
            if (binding.IsEmpty != (anchor is null) || anchor is not null && !binding.SequenceEqual(anchor.EncodeBinding()))
            {
                throw new FormatException("The local-market world requires its exact joint decode context.");
            }

            ProductionSnapshot production = anchor is null ? ProductionCodecV2.Decode(productionBytes)
                : ProductionCodecV11.Decode(productionBytes, anchor.Production);
            LiquidReservoirState reservoirs = anchor is null ? LiquidReservoirCodecV2.Decode(reservoirBytes)
                : LiquidReservoirCodecV3.Decode(reservoirBytes, anchor.Geography);
            CareSnapshot care = anchor is null ? CareCodecV2.Decode(careBytes) : CareCodecV3.Decode(careBytes, anchor.Care);
            WorldResourceHistory resources = anchor is null ? WorldResourceHistoryCodec.Decode(resourceBytes)
                : WorldResourceHistoryCodec.DecodeCoordinated(resourceBytes, anchor);
            // Installed colliders belong to retained construction, never a duplicated spatial payload.
            var components = DecodeComponents(spatialBytes, 1);
            var topology = TerrainLiquidTopology.Create(reservoirs, components.Regions, seals);
            LiquidCellState cells = LiquidCellStateCodecV2.Decode(liquidBytes, reservoirs, topology);
            var space = new LocalSpatialSnapshot(components.Scope, components.Regions, components.Actors, construction.GetStructures());
            var restored = new WorldSnapshot(inventory, space, residents, admission, calendar, revision, advancingSteps,
                dialogues, gathering, production, progression, quests, construction, reservoirs: reservoirs,
                reservoirSeals: seals, liquidCells: cells, excavation: excavation, pacing: pacing,
                encounterLifecycle: lifecycle, care: care, resourceHistory: resources,
                coordination: new WorldCoordinatedContinuation(sources, anchor, history, proposals, paid, markets));
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The complete local-market world is not canonical.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The complete local-market world violates its complete admission invariants.", error);
        }
    }
}
