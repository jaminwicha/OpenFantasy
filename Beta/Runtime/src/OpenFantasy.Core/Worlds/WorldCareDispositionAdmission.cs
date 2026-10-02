using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;

namespace OpenFantasy.Core.Worlds;

/// <summary>Fresh, explicit admission of terminal pending-care decisions; opening a lived save never selects it.</summary>
public static class WorldCareDispositionAdmission
{
    public const ushort ProfileVersion = 38;
    public static ContentHash RulesPin => CareDispositionPolicy.RulesPin;

    public static WorldSnapshot CreateFresh(WorldSnapshot basis)
    {
        ArgumentNullException.ThrowIfNull(basis);
        if (basis.ProfileVersion != WorldRecoveryAdmission.ProfileVersion || basis.Revision != 0
            || basis.AdvancingSteps != 0 || basis.HasReadingHold || basis.PendingExcavation is not null
            || basis.Care is not { Revision: 0, Ordinary: { Revision: 0 } ordinary }
            || basis.Coordination is not { Anchor: null }
            || basis.EncounterLifecycle is not { Phase: EncounterLifecyclePhase.Exploration, EncounterCount: 0 }
            || basis.Controllers!.Assignments.Any(value => value.Revision != 0))
            throw new ArgumentException("Care disposition admission requires an unplayed recovery world.", nameof(basis));

        CareSnapshot care = CareSnapshot.CreateWithDispositions(basis.WorldId, basis.BranchId, 0,
            basis.Care.Remedies, basis.Care.Agreements, [], [], [], ordinary);
        ContentHash admission = ContentHash.Compute(CanonicalEncodingV1.Frame("world.care-disposition-admission.v1",
            basis.ContentHash.ToByteArray(), RulesPin.ToByteArray(), care.ContentHash.ToByteArray()));
        var world = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle,
            care: care, resourceHistory: WorldResourceHistory.CreateFresh(admission, basis.Progression!),
            coordination: basis.Coordination, retreat: basis.Retreat, encounterOutcome: basis.EncounterOutcome,
            controllers: basis.Controllers, recovery: basis.Recovery);
        _ = WorldSnapshotCodec.Encode(world);
        return world;
    }

    internal static void Validate(WorldSnapshot world)
    {
        if (world.Care?.SupportsDispositions == true && (world.Recovery is null || world.Controllers is null
            || world.EncounterOutcome is null || world.Coordination?.Markets is null))
            throw new ArgumentException("Care dispositions require the complete recovery-world interpretation.");
    }
}
