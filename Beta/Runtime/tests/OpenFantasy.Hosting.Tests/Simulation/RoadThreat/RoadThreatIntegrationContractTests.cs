using System.Text;
using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Simulation.RoadThreat;
using OpenFantasy.Hosting.Tests.Simulation.Retreat;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.RoadThreat;

/// <summary>
/// Declared fresh calibrations exercise otherwise distant or difficult combinations. They place the chief and Wren
/// beside the farm's player before creating a save; the victory calibration starts the chief wounded at one HP,
/// retaining all source statistics and abilities. A new admission and initial resource baseline describe that genesis.
/// Every dialogue, encounter, injury, acknowledgment, treatment and refusal thereafter is an actual committed operation.
/// These are boundary proofs, not production placement, ordinary victory balance or invented played history.
/// </summary>
public sealed class RoadThreatIntegrationContractTests
{
    [Fact]
    public async Task A_wounded_host_initiator_can_lose_but_neither_survivor_nor_incapacitated_initiator_can_release()
    {
        WorldSnapshot genesis = Calibration("road-victory-contract", woundedChief: true);
        RoadThreatBinding threat = RoadThreatJourney.Threat(genesis);
        using var files = new SessionFiles();
        var world = new RetreatJourneyWorld(files.Path, genesis, reopen: false);
        try
        {
            await world.Tick();
            Assert.Equal(1UL, world.State.EncounterLifecycle!.EncounterCount);
            await RoadThreatJourney.AwaitPlayerChoice(world);
            Assert.Equal(WorldActionRejection.None, (await world.Submit(RoadThreatJourney.Strike(world, threat.ChiefActorId))).Rejection);
            await world.TickUntil(() => WorldEncounterActions.IsOutcome(world.State), 600, "chief incapacitation");
            Assert.Equal(0, world.State.Progression!.GetActor(threat.ChiefActorId).CurrentHp);
            Assert.True(world.State.Progression.GetActor(world.Player).CurrentHp > 0);
            Assert.Equal(genesis.Progression!.GetActor(threat.ChiefActorId).Stats,
                world.State.Progression.GetActor(threat.ChiefActorId).Stats);
            WorldSnapshot outcome = world.State;
            await world.StopAsync();

            // A complete-journal reopen keeps the real lethal hit; it does not manufacture a release or rewards.
            using (SqliteWorldAuthority owner = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis)))
            {
                Assert.Equal(WorldSnapshotCodec.Encode(outcome), WorldSnapshotCodec.Encode(owner.ReadStateForHost()));
                foreach ((EntityId actor, WorldActionRejection rejection) in new[]
                {
                    (world.Player, WorldActionRejection.NotAuthorized),
                    (threat.ChiefActorId, WorldActionRejection.Unavailable)
                })
                {
                    WorldSnapshot before = owner.ReadStateForHost();
                    WorldActionCommand command = Command(before, actor, new ReleaseEncounterIntent(
                        before.EncounterActions!.Scope.EncounterId, before.EncounterActions.ContentHash));
                    WorldStepDecision decision = owner.Commit(Batch(before, WorldBatchKind.EncounterLifecycle, command));
                    Assert.Equal(rejection, Assert.Single(decision.Receipts).Rejection);
                    Assert.True(WorldEncounterActions.IsOutcome(decision.Candidate));
                    Assert.Equal(before.Progression!.ContentHash, decision.Candidate.Progression!.ContentHash);
                    Assert.Equal(before.Space.Actors, decision.Candidate.Space.Actors);
                    Assert.Equal(before.Space.Regions, decision.Candidate.Space.Regions);
                }
                outcome = owner.ReadStateForHost();
            }
            world = new RetreatJourneyWorld(files.Path, genesis, reopen: true);
            Assert.Equal(outcome.ContentHash, world.State.ContentHash);
            world.Session.SetMovement(0, WorldMovementInput.DisplacementPerStep);
            for (int index = 0; index < 12; index++) await world.Tick();
            Assert.Equal(outcome.ContentHash, world.State.ContentHash);
            Assert.False(LocalRoadThreatController.CanInitiate(world.State, threat.Scenario));
        }
        finally { await world.StopAsync(); }
    }

    [Fact]
    public async Task An_actual_reading_hold_defers_a_ready_in_reach_chief_until_the_cutoff_after_closing()
    {
        WorldSnapshot genesis = Calibration("road-reading-contract");
        RoadThreatBinding threat = RoadThreatJourney.Threat(genesis);
        EntityId player = Player(genesis);
        EntityId wren = Wren(genesis);
        using var files = new SessionFiles();
        // The calibrated Host schedules a real, authenticated Speak before handing the save to the local session.
        // This supplies a reachable reading boundary; it does not claim that an ordinary input outruns hostile entry.
        using (SqliteWorldAuthority owner = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            Assert.True(LocalRoadThreatController.CanInitiate(genesis, threat.Scenario));
            WorldActionCommand speak = Command(genesis, player, new SpeakToActorIntent(wren, genesis.Space.GetActor(wren).Revision));
            Assert.Equal(WorldActionRejection.None,
                Assert.Single(owner.Commit(Batch(genesis, WorldBatchKind.AdvancingStep, speak)).Receipts).Rejection);
        }
        await using var world = new RetreatJourneyWorld(files.Path, genesis, reopen: true);
        WorldSnapshot reading = world.State;
        Assert.True(reading.HasReadingHold);
        Assert.True(WorldActorAvailability.CanAct(reading, threat.ChiefActorId));
        Assert.Equal(SpatialInteraction.Reachable, reading.Space.QueryInteraction(threat.ChiefActorId, player, threat.Scenario.EntryRangeSubunits));
        Assert.False(LocalRoadThreatController.CanInitiate(reading, threat.Scenario));
        world.Session.SetMovement(0, WorldMovementInput.DisplacementPerStep);
        for (int index = 0; index < 12; index++) await world.Tick();
        Assert.Equal(reading.ContentHash, world.State.ContentHash);
        Assert.Equal(WorldActionRejection.None, (await world.SubmitOffer(FarmSemanticAction.CloseDialogue)).Rejection);
        Assert.False(world.State.HasReadingHold);
        Assert.Equal(0UL, world.State.EncounterLifecycle!.EncounterCount);
        Assert.True(LocalRoadThreatController.CanInitiate(world.State, threat.Scenario));
        await world.Tick();
        Assert.Equal((1UL, threat.ScenarioId), (world.State.EncounterLifecycle!.EncounterCount, world.State.EncounterLifecycle.ScenarioId));
        Assert.Equal(reading.Space.GetActor(player).Feet, world.Feet(player));
    }

    [Fact]
    public async Task Genuine_aftermath_care_and_its_resumed_encounter_hold_back_a_nearby_chief()
    {
        WorldSnapshot genesis = Calibration("road-care-contract");
        RoadThreatBinding threat = RoadThreatJourney.Threat(genesis);
        using var files = new SessionFiles();
        var world = new RetreatJourneyWorld(files.Path, genesis, reopen: false);
        try
        {
            // The player's explicit lifecycle selection has its documented priority over hostile advice.
            Assert.Equal(WorldActionRejection.None, (await world.EnterFirstWolf()).Rejection);
            for (int choices = 0; !WorldEncounterActions.IsOutcome(world.State); choices++)
            {
                Assert.True(choices < 20, "Waiting must reach the ordinary wolf's real defeat boundary.");
                await world.TickUntil(() => WorldEncounterActions.IsOutcome(world.State)
                    || world.Session.ReadView().EncounterActions is { CanWait: true }, 600, "wait choice");
                if (WorldEncounterActions.IsOutcome(world.State)) break;
                var actions = world.Session.ReadView().EncounterActions!;
                Assert.Equal(WorldActionRejection.None, (await world.Submit(world.PlayerCommand(
                    new QueueEncounterWaitIntent(actions.EncounterId, actions.TimingRevision), RetreatJourneyWorld.NewId()))).Rejection);
            }
            Assert.Equal(0, world.State.Progression!.GetActor(world.Player).CurrentHp);
            var offer = Assert.Single(world.Session.ReadView().Care!.Offers);
            Assert.True(offer.CanAcknowledge);
            Assert.Equal(WorldActionRejection.None, (await world.Submit(world.PlayerCommand(offer.Intent, RetreatJourneyWorld.NewId()))).Rejection);
            WorldSnapshot pending = world.State;
            Assert.NotNull(pending.Care!.Pending);
            Assert.False(LocalRoadThreatController.CanInitiate(pending, threat.Scenario));
            Assert.Equal(SpatialInteraction.Reachable,
                pending.Space.QueryInteraction(threat.ChiefActorId, world.Player, threat.Scenario.EntryRangeSubunits));
            await world.StopAsync();
            world = new RetreatJourneyWorld(files.Path, genesis, reopen: true);
            Assert.Equal(pending.ContentHash, world.State.ContentHash);
            world.Session.SetMovement(0, WorldMovementInput.DisplacementPerStep);
            await world.Tick();
            WorldSnapshot treated = world.State;
            Assert.Null(treated.Care!.Pending);
            Assert.Equal(pending.Care.Treatments.Count + 1, treated.Care.Treatments.Count);
            Assert.Equal(pending.EncounterLifecycle!.EncounterCount, treated.EncounterLifecycle!.EncounterCount);
            Assert.Equal(pending.Space.GetActor(world.Player).Feet, world.Feet(world.Player));
            Assert.True(treated.Progression!.GetActor(world.Player).CurrentHp > 0);
            Assert.Equal(pending.Production!.ConsumedItems.Count + 1, treated.Production!.ConsumedItems.Count);
            // The wolf is still alive: aftermath treatment resumes this same encounter, not exploration.
            // The nearby chief cannot replace either the pending care boundary or its resumed fight.
            Assert.Equal(EncounterLifecyclePhase.Active, treated.EncounterLifecycle.Phase);
            Assert.Equal(pending.EncounterLifecycle.ScenarioId, treated.EncounterLifecycle.ScenarioId);
            Assert.False(WorldEncounterActions.IsOutcome(treated));
            Assert.True(WorldActorAvailability.CanAct(treated, threat.ChiefActorId));
            Assert.False(LocalRoadThreatController.CanInitiate(treated, threat.Scenario));
            for (int index = 0; index < 6; index++) await world.Tick();
            Assert.Equal(pending.EncounterLifecycle.EncounterCount, world.State.EncounterLifecycle!.EncounterCount);
            Assert.Equal(pending.EncounterLifecycle.ScenarioId, world.State.EncounterLifecycle.ScenarioId);
            Assert.Equal(pending.Space.GetActor(world.Player).Feet, world.Feet(world.Player));
            Assert.Equal(treated.Care.Treatments.Count, world.State.Care!.Treatments.Count);
        }
        finally { await world.StopAsync(); }
    }

    [Fact]
    public async Task A_saved_host_entry_retries_exactly_but_conflicting_content_and_player_control_are_refused()
    {
        WorldSnapshot genesis = Calibration("road-entry-identity-contract");
        RoadThreatBinding threat = RoadThreatJourney.Threat(genesis);
        using var files = new SessionFiles();
        await using (var world = new RetreatJourneyWorld(files.Path, genesis, reopen: false))
        {
            await world.Tick();
            Assert.Equal((1UL, threat.ScenarioId), (world.State.EncounterLifecycle!.EncounterCount, world.State.EncounterLifecycle.ScenarioId));
        }
        WorldActionCommand entry;
        using (WorldDatabase database = WorldDatabase.Open(files.Path, create: false))
        {
            entry = Assert.Single(database.ReadJournal(0).SelectMany(commit => WorldStepCodec.Decode(commit.AcceptedBatch).Actions),
                command => command.Intent is EnterEncounterIntent);
        }
        using SqliteWorldAuthority owner = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
        WorldSnapshot before = owner.ReadStateForHost();
        ActorControlBinding control = before.Inventory.Controls.Single(value => value.ActorId == threat.ChiefActorId);
        var delivery = new TrustedCommandDelivery(control.PrincipalId, control.SessionId);
        WorldActionReceipt original = Assert.IsType<WorldActionReceipt>(owner.FindReceipt(entry, delivery));
        Assert.Equal(WorldActionRejection.None, original.Rejection);
        Assert.Equal(WorldReceiptCodecV1.Encode(original), WorldReceiptCodecV1.Encode(owner.FindReceipt(entry, delivery)!));
        var conflicting = new WorldActionCommand(entry.CommandId, entry.PrincipalId, entry.OriginSessionId, entry.ActorId,
            entry.WorldId, entry.BranchId, entry.ExpectedActorRevision + 1, entry.Intent);
        Assert.Throws<AuthorityDispatchException>(() => owner.FindReceipt(conflicting, delivery));
        Assert.Throws<AuthorityDispatchException>(() => owner.Commit(Batch(before, WorldBatchKind.EncounterLifecycle, conflicting)));
        ActorControlBinding player = before.Inventory.Controls.Single(value => value.ActorId == Player(before));
        Assert.Throws<AuthorityDispatchException>(() => owner.FindReceipt(entry, new TrustedCommandDelivery(player.PrincipalId, player.SessionId)));
        Assert.Equal(before.ContentHash, owner.ReadStateForHost().ContentHash);
        Assert.Equal(WorldReceiptCodecV1.Encode(original), WorldReceiptCodecV1.Encode(owner.FindReceipt(entry, delivery)!));
    }

    private static EntityId Player(WorldSnapshot state) => state.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
    private static EntityId Wren(WorldSnapshot state) => state.Quests!.Definitions.Select(value => value.GiverActorId).Distinct().Single();

    private static WorldActionCommand Command(WorldSnapshot state, EntityId actor, WorldActionIntent intent)
    {
        ActorControlBinding control = state.Inventory.Controls.Single(value => value.ActorId == actor);
        return new WorldActionCommand(RetreatJourneyWorld.NewId(), control.PrincipalId, control.SessionId, actor,
            state.WorldId, state.BranchId, state.Space.GetActor(actor).Revision, intent);
    }

    private static WorldStepBatch Batch(WorldSnapshot state, WorldBatchKind kind, params WorldActionCommand[] actions) =>
        new(RetreatJourneyWorld.NewId(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps, kind, [], actions, state.ProfileVersion);

    private static WorldSnapshot Calibration(string seed, bool woundedChief = false)
    {
        WorldSnapshot source = RoadThreatJourney.Genesis(seed);
        EntityId player = Player(source);
        EntityId chief = RoadThreatJourney.Threat(source).ChiefActorId;
        EntityId helper = Wren(source);
        SubunitPosition feet = source.Space.GetActor(player).Feet;
        LocalSpatialSnapshot? space = null;
        foreach ((long x, long y) in new (long, long)[] { (256, 0), (0, 256), (-256, 0), (0, -256) })
        {
            try
            {
                space = source.Space.WithActors(source.Space.Actors.Select(actor => actor.ActorId == chief || actor.ActorId == helper
                    ? new ActorPresence(actor.Binding, actor.Condition, actor.Scope,
                        actor.ActorId == chief ? feet.Offset(x, y, 0) : feet.Offset(-x, -y, 0), actor.Revision, actor.HomePlaceId)
                    : actor));
                if (space.QueryInteraction(chief, player, 400) == SpatialInteraction.Reachable
                    && space.QueryInteraction(helper, player, 256) == SpatialInteraction.Reachable) break;
                space = null;
            }
            catch (ArgumentException) { space = null; }
        }
        Assert.NotNull(space);
        ProgressionSnapshot old = source.Progression!;
        var progression = new ProgressionSnapshot(old.WorldId, old.BranchId, 0, old.Profiles, old.Curves,
            old.Actors.Select(actor => woundedChief && actor.ActorId == chief
                ? new ActorProgressionState(actor.ActorId, 0, actor.Profile, actor.Curve, actor.Level, actor.Experience, 1, actor.CurrentMp)
                : actor), old.Currency);
        ContentHash admission = ContentHash.Compute(Encoding.UTF8.GetBytes("declared-road-boundary-calibration:" + seed)
            .Concat(source.ContentHash.ToByteArray()).Concat(progression.ContentHash.ToByteArray()).ToArray());
        var result = new WorldSnapshot(source.Inventory, space, source.Residents, admission, source.Calendar, 0, 0,
            gathering: source.Gathering, production: source.Production, progression: progression, quests: source.Quests,
            construction: source.Construction, reservoirs: source.Reservoirs, reservoirSeals: source.ReservoirSeals,
            liquidCells: source.LiquidCells, excavation: source.Excavation, pacing: source.Pacing,
            encounterLifecycle: source.EncounterLifecycle, care: source.Care,
            resourceHistory: new WorldResourceHistory(admission, new ActorResourceHistory(progression, 0, []), []),
            coordination: source.Coordination, retreat: source.Retreat);
        Assert.Equal(WorldSnapshotCodec.Encode(result), WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(result))));
        return result;
    }
}
