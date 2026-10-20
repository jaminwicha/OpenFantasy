using System.Text;
using OpenFantasy.Core.Cognition;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// The cognition interpretation, profile 42: a strict successor of collective withdrawal (41) whose root adds one
/// typed cognition record per resident. It stays internal and non-default until NPC-01 is accepted; saves made with it
/// before then are development fixtures. Fresh selection never upgrades a lived save, so an empty facet always means
/// that nothing has been acquired since an unplayed genesis.
/// </summary>
public static class WorldCognitionAdmission
{
    public const ushort ProfileVersion = 42;

    /// <summary>
    /// Pins the laws this interpretation replays: which committed outcomes give whom evidence, the admitted perception
    /// rule, evidence retention, wake suppression, decision service and deferral memory. A law change needs a new
    /// interpretation once profile 42 is frozen; a planner change never does.
    /// </summary>
    public static readonly ContentHash RulesPin = ContentHash.Compute(CanonicalEncodingV1.Frame("world.cognition-laws.v2",
        Encoding.UTF8.GetBytes("evidence:item-received,quest-delivery-to-giver,gather-outcome,source-in-sight-4-cells;own-project-completion-wakes-builder"),
        Encoding.UTF8.GetBytes("retention:newer-source-observation-and-gather-outcome-replace-uncited-same-subject,cited-kept"),
        Encoding.UTF8.GetBytes("wake:evidence-at-resulting-step;suppression:reading-hold,encounter,paid-continuation"),
        Encoding.UTF8.GetBytes("decision:recorded-actor-action,due-unsuppressed-wake,unheld-advancing-step,fallback-within-100-steps,"
            + "supported-by-own-evidence-and-project;inspection:sight-before-revision"),
        Encoding.UTF8.GetBytes("deferral:current-reason-beside-intention,reason-change-remembered,no-intention-defers-nothing")));

    public static WorldSnapshot CreateFresh(WorldSnapshot basis)
    {
        ArgumentNullException.ThrowIfNull(basis);
        if (basis.ProfileVersion != WorldPartyWithdrawalAdmission.ProfileVersion || basis.Revision != 0
            || basis.AdvancingSteps != 0 || basis.HasReadingHold || basis.PendingExcavation is not null
            || basis.Coordination?.Anchor is not null
            || basis.Retreat is not { Active: null, AcceptedCount: 0, Last: null, Withdrawal: { Revision: 0 } }
            || basis.EncounterLifecycle is not { Phase: EncounterLifecyclePhase.Exploration, EncounterCount: 0 })
            throw new ArgumentException("Cognition fresh admission requires an unplayed collective-withdrawal world.");
        var admission = ContentHash.Compute(CanonicalEncodingV1.Frame("world.cognition-admission.v1",
            basis.ContentHash.ToByteArray(), RulesPin.ToByteArray()));
        var cognition = new CognitionSnapshot(basis.WorldId, basis.BranchId, 0,
            basis.Residents.Select(resident => ActorCognition.Empty(resident.ActorId)));
        var world = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle,
            care: basis.Care, resourceHistory: WorldResourceHistory.CreateFresh(admission, basis.Progression!),
            coordination: basis.Coordination, retreat: basis.Retreat, encounterOutcome: basis.EncounterOutcome,
            controllers: basis.Controllers, recovery: basis.Recovery, controlPermissions: basis.ControlPermissions,
            effectiveControl: basis.EffectiveControl, cognition: cognition);
        _ = WorldSnapshotCodec.Encode(world);
        return world;
    }

    internal static void Validate(WorldSnapshot world)
    {
        if (world.Cognition is not { } cognition) return;
        if (world.Retreat?.Withdrawal is null || world.EffectiveControl is null)
            throw new ArgumentException("Cognition succeeds the complete collective-withdrawal interpretation.");
        if (cognition.WorldId != world.WorldId || cognition.BranchId != world.BranchId
            || !cognition.Actors.Select(actor => actor.ActorId).SequenceEqual(world.Residents.Select(resident => resident.ActorId))
            || world.Revision == 0 && cognition.Revision != 0)
            throw new ArgumentException("Every resident of this world and branch owns exactly one cognition record.");
        foreach (ActorCognition actor in cognition.Actors)
        {
            if (actor.Evidence.Any(entry => entry.AcquiredAt > world.Calendar
                    || entry.CounterpartActorId is { } counterpart && world.Residents.All(resident => resident.ActorId != counterpart))
                || actor.Memory.Any(entry => entry.At > world.Calendar)
                || actor.Intention is { } intention && intention.AdoptedAt > world.Calendar
                || actor.Deferral is { } deferral && deferral.Since > world.Calendar)
                throw new ArgumentException("Cognition records only what residents acquired and decided before this instant.");
        }
    }
}
