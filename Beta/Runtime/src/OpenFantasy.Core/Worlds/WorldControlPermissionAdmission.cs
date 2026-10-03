using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;

namespace OpenFantasy.Core.Worlds;

/// <summary>Explicit fresh permission profile; loading a prior world never grants this interpretation.</summary>
public static class WorldControlPermissionAdmission
{
    public const ushort ProfileVersion = 39;
    public static ContentHash RulesPin => WorldControlPermissionState.RulesPin;
    public static WorldSnapshot CreateFresh(WorldSnapshot basis)
    {
        ArgumentNullException.ThrowIfNull(basis);
        if (basis.ProfileVersion != WorldCareDispositionAdmission.ProfileVersion || basis.Revision != 0
            || basis.AdvancingSteps != 0 || basis.HasReadingHold || basis.PendingExcavation is not null
            || basis.Care is not { Revision: 0, Ordinary: { Revision: 0 } }
            || basis.Coordination is not { Anchor: null }
            || basis.EncounterLifecycle is not { Phase: EncounterLifecyclePhase.Exploration, EncounterCount: 0 }
            || basis.Controllers!.Assignments.Any(row => row.Revision != 0))
            throw new ArgumentException("Control permissions require an unplayed complete care-disposition world.", nameof(basis));
        var admission = ContentHash.Compute(CanonicalEncodingV1.Frame("world.control-permission-admission.v1",
            basis.ContentHash.ToByteArray(), RulesPin.ToByteArray()));
        var world = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle,
            care: basis.Care, resourceHistory: WorldResourceHistory.CreateFresh(admission, basis.Progression!),
            coordination: basis.Coordination, retreat: basis.Retreat, encounterOutcome: basis.EncounterOutcome,
            controllers: basis.Controllers, recovery: basis.Recovery, controlPermissions: WorldControlPermissionState.Empty);
        _ = WorldSnapshotCodec.Encode(world);
        return world;
    }

    internal static void Validate(WorldSnapshot world)
    {
        if (world.ControlPermissions is not { } permissions) return;
        if (world.Care?.SupportsDispositions != true || world.Controllers is null || world.Recovery is null
            || (UInt128)permissions.Revision + world.Controllers.Assignments.Aggregate((UInt128)0, (sum, row) => sum + row.Revision) > world.Revision
            || world.Revision == 0 && permissions.Slots.Count != 0)
            throw new ArgumentException("Permission state requires its full profile and earned world revisions.");
        var humans = world.Controllers.Assignments.Select(row => row.ActorId).ToHashSet();
        foreach (var slot in permissions.Slots)
        {
            if (!humans.Contains(slot.OwnerActorId) || !humans.Contains(slot.GranteeActorId) || slot.ChangedSequence > world.Revision)
                throw new ArgumentException("Permission slots require distinct admitted human home actors and committed sequences.");
            if (slot.Terms is not { } terms) continue;
            if (terms.WorldId != world.WorldId || terms.BranchId != world.BranchId
                || !world.Inventory.CanControl(new(terms.OwnerPrincipalId, terms.OwnerSessionId), terms.OwnerActorId)
                || !world.Inventory.CanControl(new(terms.GranteePrincipalId, terms.GranteeSessionId), terms.GranteeActorId))
                throw new ArgumentException("Permission terms must retain both original home-actor entitlements.");
        }
    }
}
