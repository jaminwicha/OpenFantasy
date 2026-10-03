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
/// Complete outcome-continuation world (profile 35): exactly the profile-34 fields in the same order and frames,
/// followed by the outcome owner as the final field. Profile-34 roots keep their own codec and bytes and never decode
/// here; a profile-35 root never decodes as profile 34.
/// </summary>
public static class WorldSnapshotCodecV35
{
    private const int MaximumBindingByteLength = 512;
    private const int MaximumCoordinationByteLength = CareCodecV2.MaximumEncodedByteLength
        + WorldSourceIssuanceBaseline.MaximumEncodedByteLength + MaximumBindingByteLength
        + WorldConsentedMaterialHistory.MaximumByteLength + WorldMaterialProposalBook.MaximumByteLength
        + WorldPaidExchangeBook.MaximumByteLength + WorldMarketBook.MaximumByteLength + 192;

    public static byte[] Encode(WorldSnapshot world) => EncodeComplete(world, WorldEncounterOutcomeAdmission.ProfileVersion);

    internal static byte[] EncodeComplete(WorldSnapshot world, ushort version)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (version is not 35 and not 36 and not 37 and not 38 and not 39 and not 40 || world.ProfileVersion != version || world.Retreat is null || world.EncounterOutcome is null
            || world.Coordination is not { ConsentedMaterial: { } history, Proposals: { } proposals, PaidExchange: { } paid, Markets: { } markets } coordination
            || world.Care is null || world.EncounterLifecycle is null || world.Pacing is null || world.Reservoirs is null
            || world.ReservoirSeals is null || world.LiquidCells?.Topology is null || world.Excavation is null)
        {
            throw new ArgumentException("This codec requires the complete outcome-continuation world profile.", nameof(world));
        }
        WorldMaterialCareArchiveAnchor? anchor = coordination.Anchor;
        byte[] liquidBytes = LiquidCellStateCodecV2.Encode(world.LiquidCells);
        byte[] pacingBytes = WorldPacingCodecV1.Encode(world.Pacing);
        byte[] actionBytes = EncounterLifecycleCodecV1.Encode(world.EncounterLifecycle);
        byte[] careBytes = version is WorldCareDispositionAdmission.ProfileVersion or WorldControlPermissionAdmission.ProfileVersion or WorldDelegatedControlAdmission.ProfileVersion ? CareCodecV5.Encode(world.Care)
            : version == WorldRecoveryAdmission.ProfileVersion ? CareCodecV4.Encode(world.Care)
            : anchor is null ? CareCodecV2.Encode(world.Care) : CareCodecV3.Encode(world.Care);
        byte[] coordinationBytes = CanonicalEncodingV1.Frame("world.coordinated-market-care.v1", careBytes,
            coordination.Sources.Encode(), anchor?.EncodeBinding() ?? [], WorldConsentedMaterialCodec.Encode(history),
            WorldMaterialProposalCodec.Encode(proposals), WorldPaidExchangeCodec.Encode(paid), WorldMarketCodec.Encode(markets));
        byte[] resourceBytes = WorldResourceHistoryCodec.Encode(world.ResourceHistory!);
        byte[] retreatBytes = EncounterRetreatCodecV1.Encode(world.Retreat);
        byte[] outcomeBytes = EncounterOutcomeCodecV1.Encode(world.EncounterOutcome);
        List<ReadOnlyMemory<byte>> fields = [Number(version),
            AuthorityEncodingV1.Encode(world.Inventory),
            EncodeComponents(world.Space, 1), EncodeResidents(world.Residents), world.AdmissionVersion.ToByteArray(),
            Signed(world.Calendar.Days), Number((ulong)world.Calendar.MillisecondsWithinDay), Number(world.Revision),
            Number(world.AdvancingSteps), EncodeDialogues(world.Dialogues), GatheringCodecV1.Encode(world.Gathering!),
            anchor is null ? ProductionCodecV2.Encode(world.Production!) : ProductionCodecV11.Encode(world.Production!),
            ProgressionCodecV1.Encode(world.Progression!), QuestCodecV1.Encode(world.Quests!),
            ConstructionCodecV1.Encode(world.Construction!),
            anchor is null ? LiquidReservoirCodecV2.Encode(world.Reservoirs) : LiquidReservoirCodecV3.Encode(world.Reservoirs),
            ReservoirSealPolicyCodecV1.Encode(world.ReservoirSeals), liquidBytes, ExcavationCodecV2.Encode(world.Excavation),
            pacingBytes, actionBytes, coordinationBytes, resourceBytes, retreatBytes, outcomeBytes];
        byte[] controllerBytes = version is WorldControllerAdmission.ProfileVersion or WorldRecoveryAdmission.ProfileVersion or WorldCareDispositionAdmission.ProfileVersion or WorldControlPermissionAdmission.ProfileVersion or WorldDelegatedControlAdmission.ProfileVersion
            ? WorldControllerCodecV1.Encode(world.Controllers!) : [];
        if (version is WorldControllerAdmission.ProfileVersion or WorldRecoveryAdmission.ProfileVersion or WorldCareDispositionAdmission.ProfileVersion or WorldControlPermissionAdmission.ProfileVersion or WorldDelegatedControlAdmission.ProfileVersion) fields.Add(controllerBytes);
        if (version is WorldRecoveryAdmission.ProfileVersion or WorldCareDispositionAdmission.ProfileVersion or WorldControlPermissionAdmission.ProfileVersion or WorldDelegatedControlAdmission.ProfileVersion)
            fields.Add(WorldRecoveryPolicy.RulesPin.ToByteArray());
        if (version is WorldCareDispositionAdmission.ProfileVersion or WorldControlPermissionAdmission.ProfileVersion or WorldDelegatedControlAdmission.ProfileVersion) fields.Add(WorldCareDispositionAdmission.RulesPin.ToByteArray());
        byte[] permissionBytes = version is WorldControlPermissionAdmission.ProfileVersion or WorldDelegatedControlAdmission.ProfileVersion ? WorldControlPermissionCodecV1.Encode(world.ControlPermissions!) : [];
        if (version is WorldControlPermissionAdmission.ProfileVersion or WorldDelegatedControlAdmission.ProfileVersion) fields.Add(permissionBytes);
        byte[] effectiveBytes = version == WorldDelegatedControlAdmission.ProfileVersion ? WorldEffectiveControlCodecV1.Encode(world.EffectiveControl!) : [];
        if (version == WorldDelegatedControlAdmission.ProfileVersion) fields.Add(effectiveBytes);
        byte[] bytes = CanonicalEncodingV1.Frame("world.snapshot", fields.ToArray());
        if ((long)bytes.Length - liquidBytes.Length - pacingBytes.Length - actionBytes.Length - coordinationBytes.Length
            - resourceBytes.Length - retreatBytes.Length - outcomeBytes.Length
            + LiquidCellStateCodecV2.MaximumEncodedByteLength + WorldPacingCodecV1.MaximumEncodedByteLength
            + EncounterLifecycleCodecV1.MaximumEncodedByteLength + MaximumCoordinationByteLength
            + WorldResourceHistoryCodec.MaximumEncodedByteLength + EncounterRetreatCodecV1.MaximumEncodedByteLength
            + EncounterOutcomeCodecV1.MaximumEncodedByteLength
            + (version is WorldControllerAdmission.ProfileVersion or WorldRecoveryAdmission.ProfileVersion or WorldCareDispositionAdmission.ProfileVersion or WorldControlPermissionAdmission.ProfileVersion or WorldDelegatedControlAdmission.ProfileVersion ? WorldControllerCodecV1.MaximumStateByteLength - controllerBytes.Length : 0)
            + (version is WorldControlPermissionAdmission.ProfileVersion or WorldDelegatedControlAdmission.ProfileVersion ? WorldControlPermissionCodecV1.MaximumStateByteLength - permissionBytes.Length : 0)
            + (version == WorldDelegatedControlAdmission.ProfileVersion ? WorldEffectiveControlCodecV1.MaximumStateByteLength - effectiveBytes.Length : 0)
            > WorldSnapshotCodec.MaximumStateByteLength)
        {
            throw new ArgumentException("The complete outcome-continuation world exceeds its retained storage bound.", nameof(world));
        }
        return bytes;
    }

    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes, WorldMaterialCareArchiveAnchor? anchor = null) =>
        DecodeComplete(bytes, anchor, WorldEncounterOutcomeAdmission.ProfileVersion);

    internal static WorldSnapshot DecodeComplete(ReadOnlySpan<byte> bytes, WorldMaterialCareArchiveAnchor? anchor, ushort version)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, WorldSnapshotCodec.MaximumStateByteLength,
                version == WorldDelegatedControlAdmission.ProfileVersion ? 31
                : version == WorldControlPermissionAdmission.ProfileVersion ? 30
                : version == WorldCareDispositionAdmission.ProfileVersion ? 29
                : version == WorldRecoveryAdmission.ProfileVersion ? 28 : version == WorldControllerAdmission.ProfileVersion ? 27 : 26);
            reader.Tag("world.snapshot"u8);
            if (version is not 35 and not 36 and not 37 and not 38 and not 39 and not 40 || reader.Number() != version)
            {
                throw new FormatException("Unsupported complete outcome-continuation world version.");
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
            EncounterRetreatState retreat = EncounterRetreatCodecV1.Decode(reader.Field(EncounterRetreatCodecV1.MaximumEncodedByteLength));
            EncounterOutcomeState outcome = EncounterOutcomeCodecV1.Decode(reader.Field(EncounterOutcomeCodecV1.MaximumEncodedByteLength));
            WorldControllerState? controllers = version is WorldControllerAdmission.ProfileVersion or WorldRecoveryAdmission.ProfileVersion or WorldCareDispositionAdmission.ProfileVersion or WorldControlPermissionAdmission.ProfileVersion or WorldDelegatedControlAdmission.ProfileVersion
                ? WorldControllerCodecV1.Decode(reader.Field(WorldControllerCodecV1.MaximumStateByteLength)) : null;
            WorldRecoveryPolicy? recovery = null;
            if (version is WorldRecoveryAdmission.ProfileVersion or WorldCareDispositionAdmission.ProfileVersion or WorldControlPermissionAdmission.ProfileVersion or WorldDelegatedControlAdmission.ProfileVersion)
            {
                if (!reader.Exact(32).SequenceEqual(WorldRecoveryPolicy.RulesPin.ToByteArray()))
                    throw new FormatException("The recovery root requires its exact admitted interpretation.");
                recovery = WorldRecoveryPolicy.AssistedIncapacity;
            }
            if (version is WorldCareDispositionAdmission.ProfileVersion or WorldControlPermissionAdmission.ProfileVersion or WorldDelegatedControlAdmission.ProfileVersion
                && !reader.Exact(32).SequenceEqual(WorldCareDispositionAdmission.RulesPin.ToByteArray()))
                throw new FormatException("The care disposition root requires its exact admitted interpretation.");
            WorldControlPermissionState? permissions = version is WorldControlPermissionAdmission.ProfileVersion or WorldDelegatedControlAdmission.ProfileVersion
                ? WorldControlPermissionCodecV1.Decode(reader.Field(WorldControlPermissionCodecV1.MaximumStateByteLength)) : null;
            WorldEffectiveControlState? effective = version == WorldDelegatedControlAdmission.ProfileVersion
                ? WorldEffectiveControlCodecV1.Decode(reader.Field(WorldEffectiveControlCodecV1.MaximumStateByteLength)) : null;
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
                throw new FormatException("The outcome-continuation world requires its exact joint decode context.");
            }

            ProductionSnapshot production = anchor is null ? ProductionCodecV2.Decode(productionBytes)
                : ProductionCodecV11.Decode(productionBytes, anchor.Production);
            LiquidReservoirState reservoirs = anchor is null ? LiquidReservoirCodecV2.Decode(reservoirBytes)
                : LiquidReservoirCodecV3.Decode(reservoirBytes, anchor.Geography);
            CareSnapshot care = version is WorldCareDispositionAdmission.ProfileVersion or WorldControlPermissionAdmission.ProfileVersion or WorldDelegatedControlAdmission.ProfileVersion
                ? anchor is null ? CareCodecV5.Decode(careBytes) : CareCodecV5.Decode(careBytes, anchor.Care)
                : version == WorldRecoveryAdmission.ProfileVersion
                ? anchor is null ? CareCodecV4.Decode(careBytes) : CareCodecV4.Decode(careBytes, anchor.Care)
                : anchor is null ? CareCodecV2.Decode(careBytes) : CareCodecV3.Decode(careBytes, anchor.Care);
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
                coordination: new WorldCoordinatedContinuation(sources, anchor, history, proposals, paid, markets),
                retreat: retreat, encounterOutcome: outcome, controllers: controllers, recovery: recovery, controlPermissions: permissions, effectiveControl: effective);
            if (!EncodeComplete(restored, version).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The complete outcome-continuation world is not canonical.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The complete outcome-continuation world violates its complete admission invariants.", error);
        }
    }
}
