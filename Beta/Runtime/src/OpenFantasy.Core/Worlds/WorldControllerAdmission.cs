using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Explicit fresh selection of durable session decisions. Character ownership, bodies, resources and
/// simulation time retain their existing owners. A lived world requires an explicit migration.
/// </summary>
public static class WorldControllerAdmission
{
    public const ushort ProfileVersion = 36;

    public static WorldSnapshot CreateFresh(WorldSnapshot basis)
    {
        ArgumentNullException.ThrowIfNull(basis);
        if (basis.ProfileVersion != WorldEncounterOutcomeAdmission.ProfileVersion || basis.Revision != 0
            || basis.AdvancingSteps != 0 || basis.HasReadingHold || basis.Care?.Pending is not null
            || basis.EncounterOutcome is not { FinalizedCount: 0, Last: null } outcome || outcome.Acknowledgments.Count != 0
            || basis.EncounterLifecycle is not { Phase: EncounterLifecyclePhase.Exploration, EncounterCount: 0 }
            || basis.Coordination is not { Anchor: null })
            throw new ArgumentException("Controller decisions require an unplayed outcome-capable world.", nameof(basis));
        var controllers = WorldControllerState.Fresh(basis.Residents
            .Where(resident => resident.ControllerKind == ResidentControllerKind.Player).Select(resident => resident.ActorId));
        ContentHash admission = ContentHash.Compute(CanonicalEncodingV1.Frame("world.controller-admission.v1",
            basis.ContentHash.ToByteArray(), WorldControllerState.RulesPin.ToByteArray()));
        var world = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle,
            care: basis.Care, resourceHistory: WorldResourceHistory.CreateFresh(admission, basis.Progression!),
            coordination: basis.Coordination, retreat: basis.Retreat, encounterOutcome: basis.EncounterOutcome,
            controllers: controllers);
        _ = WorldSnapshotCodec.Encode(world);
        return world;
    }

    internal static void Validate(WorldSnapshot world)
    {
        if (world.Controllers is not { } controllers) return;
        if (world.EncounterOutcome is null || world.Coordination?.Markets is null
            || !controllers.Assignments.Select(row => row.ActorId).SequenceEqual(world.Residents
                .Where(resident => resident.ControllerKind == ResidentControllerKind.Player).Select(resident => resident.ActorId).Order())
            || controllers.Assignments.Aggregate((UInt128)0, (sum, row) => sum + row.Revision) > world.Revision
            || controllers.Assignments.Any(row => row.Revision > world.Revision
                || row.DisconnectId == world.WorldId || row.DisconnectId == world.BranchId
                || world.Inventory.Controls.Count(binding => binding.ActorId == row.ActorId) != 1))
            throw new ArgumentException("Controller assignments require the complete human roster and its permanent entitlements.");
    }
}
