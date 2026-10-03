using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;

namespace OpenFantasy.Core.Worlds;

public static class WorldDelegatedControlAdmission
{
    public const ushort ProfileVersion = 40;
    public static ContentHash RulesPin => WorldEffectiveControlState.RulesPin;
    public static WorldSnapshot CreateFresh(WorldSnapshot basis)
    {
        ArgumentNullException.ThrowIfNull(basis);
        if (basis.ProfileVersion != WorldControlPermissionAdmission.ProfileVersion || basis.Revision != 0
            || basis.AdvancingSteps != 0 || basis.HasReadingHold || basis.PendingExcavation is not null
            || basis.ControlPermissions is not { Revision: 0 }
            || basis.Care is not { Revision: 0, Ordinary: { Revision: 0 } }
            || basis.EncounterLifecycle is not { Phase: EncounterLifecyclePhase.Exploration, EncounterCount: 0 }
            || basis.Controllers!.Assignments.Any(row => row.Revision != 0) || basis.Coordination is not { Anchor: null })
            throw new ArgumentException("Effective control requires a fresh explicit permission world.", nameof(basis));
        var admission = ContentHash.Compute(CanonicalEncodingV1.Frame("world.delegated-control-admission.v1", basis.ContentHash.ToByteArray(), RulesPin.ToByteArray()));
        var world = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle,
            care: basis.Care, resourceHistory: WorldResourceHistory.CreateFresh(admission, basis.Progression!),
            coordination: basis.Coordination, retreat: basis.Retreat, encounterOutcome: basis.EncounterOutcome,
            controllers: basis.Controllers, recovery: basis.Recovery, controlPermissions: basis.ControlPermissions,
            effectiveControl: WorldEffectiveControlState.Empty);
        _ = WorldSnapshotCodec.Encode(world);
        return world;
    }
    internal static void Validate(WorldSnapshot world)
    {
        IEnumerable<WorldActionCommand> retained = world.EncounterActions?.Pending.AsEnumerable() ?? [];
        if (world.Retreat?.Active is { } retreat)
        {
            retained = retained.Append(retreat.Command);
        }
        foreach (var command in retained)
        {
            if (command.ControlWitness is { } witness && (world.EffectiveControl is null || witness.AcquiredSequence >= world.Revision
                || !WorldActorCommandAuthority.ValidateRecordedEvidence(world.Inventory, command)))
                throw new ArgumentException("Accepted delegated work requires historical attribution in the explicit effective-control profile.");
        }
        if (world.EffectiveControl is not { } effective) return;
        if (world.ControlPermissions is null || world.Controllers is null || effective.Revision > world.Revision
            || world.Revision == 0 && effective.Slots.Count != 0)
            throw new ArgumentException("Effective control requires its complete permission profile and earned revisions.");
        UInt128 requiredBoundaries = world.ControlPermissions.Revision;
        foreach (var controller in world.Controllers.Assignments)
            requiredBoundaries += controller.Revision;
        // Every acquisition is its own isolated commit. Releases may share a permission or controller
        // change, so counting all effective mutations would reject lawful atomic invalidations.
        foreach (var slot in effective.Slots)
            requiredBoundaries += ((UInt128)slot.Revision + 1) / 2;
        if (requiredBoundaries > world.Revision)
            throw new ArgumentException("Controller, permission and acquisition history requires more committed boundaries than this root records.");
        foreach (var slot in effective.Slots)
        {
            if (slot.ChangedSequence > world.Revision || world.Controllers!.Assignments.All(row => row.ActorId != slot.ActorId))
                throw new ArgumentException("Effective control must refer to an admitted original actor and committed boundary.");
            if (slot.Witness is { } witness)
            {
                var home = world.Inventory.Controls.FirstOrDefault(row => row.ActorId == witness.GranteeActorId);
                if (home is null || !WorldActorCommandAuthority.IsEligibleAssignment(world, witness)
                    || !WorldActorCommandAuthority.ValidateRecordedEvidence(world.Inventory, slot.ActorId,
                        new(home.PrincipalId, home.SessionId), witness, witness.Capabilities))
                    throw new ArgumentException("An active assignment requires its current exact grant and original expired episode.");
            }
        }
    }
}
