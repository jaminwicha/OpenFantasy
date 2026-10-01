using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Explicit fresh selection of the retreat interpretation (profile 34): an unplayed local-market world (profile 33)
/// plus the pinned retreat law. It never upgrades a lived world, never reinterprets a profile-33 root and grants no
/// position, health, time or material. Structural validation authenticates no history.
/// </summary>
public static class WorldEncounterRetreatAdmission
{
    public const ushort ProfileVersion = 34;

    public static WorldSnapshot CreateFresh(WorldSnapshot marketGenesis)
    {
        ArgumentNullException.ThrowIfNull(marketGenesis);
        if (marketGenesis.ProfileVersion != WorldMarketAdmission.ProfileVersion || marketGenesis.Revision != 0
            || marketGenesis.AdvancingSteps != 0 || marketGenesis.HasReadingHold
            || marketGenesis.Coordination is not { Anchor: null, PaidExchange: { } paid, Proposals: { } proposals, Markets: { } markets } coordination
            || paid.Open.Count != 0 || paid.Outcomes.Count != 0 || proposals.Open.Count != 0 || proposals.Outcomes.Count != 0
            || coordination.ConsentedMaterial!.Transfers.Count != 0
            || marketGenesis.EncounterLifecycle is not { Phase: EncounterLifecyclePhase.Exploration, EncounterCount: 0 }
            || marketGenesis.Space.GroundSupport != GroundSupportMode.TerrainOnly)
        {
            throw new ArgumentException("Retreat is admitted only into an unplayed fresh local-market genesis.", nameof(marketGenesis));
        }
        EncounterRetreatState retreat = EncounterRetreatState.Fresh();
        ContentHash admission = ContentHash.Compute(CanonicalEncodingV1.Frame("world.retreat-admission.v1",
            marketGenesis.ContentHash.ToByteArray(), retreat.RulesPin.ToByteArray()));
        WorldSnapshot basis = marketGenesis;
        var world = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle,
            care: basis.Care, resourceHistory: WorldResourceHistory.CreateFresh(admission, basis.Progression!),
            coordination: new WorldCoordinatedContinuation(coordination.Sources,
                consentedMaterial: new WorldConsentedMaterialHistory([]), proposals: new WorldMaterialProposalBook([], []),
                paidExchange: WorldPaidExchangeBook.Empty, markets: markets),
            retreat: retreat);
        _ = WorldSnapshotCodec.Encode(world);
        return world;
    }

    /// <summary>
    /// The retreat owner belongs only to the complete local-market family on terrain ground. A committed continuation
    /// names the current active episode's living participant, whose feet equal its walked prefix; before its first
    /// sample exactly one Action timing choice pins the command, afterwards no timing choice remains for that actor.
    /// </summary>
    internal static void Validate(WorldSnapshot world)
    {
        if (world.Retreat is not { } retreat) return;
        if (world.Coordination?.Markets is null || world.EncounterLifecycle is not { } lifecycle
            || world.Space.GroundSupport != GroundSupportMode.TerrainOnly)
        {
            throw new ArgumentException("Retreat requires the complete local-market world on terrain ground.");
        }
        if (retreat.Last is { } last && (last.WorldSequence > world.Revision
            || world.Residents.All(value => value.ActorId != last.ActorId)))
        {
            throw new ArgumentException("A retreat record names a resident within this world's committed time.");
        }
        if (retreat.Active is not { } active) return;
        if (lifecycle.Phase != EncounterLifecyclePhase.Active || world.EncounterActions is not { } actions
            || world.Pacing?.Encounter is not { } timing || active.EncounterId != actions.Scope.EncounterId
            || active.Command.WorldId != world.WorldId || active.Command.BranchId != world.BranchId
            || !WorldEncounterActions.CanParticipate(world, active.ActorId)
            || world.Space.GetActor(active.ActorId).Feet != active.ExpectedFeet)
        {
            throw new ArgumentException("A committed retreat names the active episode's living participant at its walked position.");
        }
        EncounterDecision? decision = timing.Decisions.SingleOrDefault(value => value.ActorId == active.ActorId);
        bool pinned = decision is not null && decision.Kind == EncounterDecisionKind.Action
            && decision.DecisionId == active.Command.CommandId && decision.ActionPin == active.Command.ContentHash;
        if (active.HasStarted ? decision is not null : !pinned)
        {
            throw new ArgumentException("A committed retreat is either its exact queued choice or a standing continuation.");
        }
    }

    /// <summary>The exact queued, not yet started, retreat timing choice, if any.</summary>
    internal static bool IsQueuedRetreatChoice(WorldSnapshot world, EncounterDecision decision) =>
        world.Retreat?.Active is { HasStarted: false } active && decision.ActorId == active.ActorId
        && decision.DecisionId == active.Command.CommandId && decision.ActionPin == active.Command.ContentHash;
}
