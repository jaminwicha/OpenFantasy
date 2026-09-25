using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Client;

public sealed class EncounterActionOfferTests
{
    [Fact]
    public void Explicit_scenario_target_captures_episode_count_target_and_actor_revisions_without_farm_actions()
    {
        var fixture = new EncounterJourneyWorldFixture();
        var view = View(fixture, fixture.Genesis);
        var factory = Offers(fixture);
        Assert.DoesNotContain(FarmSemanticAction.EnterEncounter, factory.Create(view).Keys);
        var selection = new FarmTargetSelection(fixture.Wren);
        selection.Refresh(view);
        HashSet<EntityId> seen = [];
        for (int index = 0; index < view.Actors.Count + view.GatheringNodes.Count + view.Worksite!.ExcavationTargets.Count; index++)
        {
            selection.Cycle();
            if (selection.Selected!.Kind == FarmTargetKind.Encounter) seen.Add(selection.Selected.Id);
        }
        Assert.Contains(fixture.WolfFirst, seen);
        Assert.Contains(fixture.WolfSecond, seen);
        byte[] before = WorldSnapshotCodec.Encode(fixture.Genesis);
        foreach (EntityId wolf in seen)
        {
            var offers = factory.Create(view, Target(view, wolf));
            FarmActionOffer offer = Assert.Single(offers).Value;
            Assert.Equal(FarmSemanticAction.EnterEncounter, offer.Action);
            Assert.True(offer.CanExecute);
            var intent = Assert.IsType<EnterEncounterIntent>(offer.Intent);
            var entry = Assert.Single(view.EncounterLifecycle!.Entries, entry => entry.TargetActorId == wolf);
            Assert.Equal((entry.ScenarioId, entry.EncounterId, view.EncounterLifecycle.EncounterCount, entry.TargetRevision),
                (intent.ScenarioId, intent.EncounterId, intent.ExpectedEncounterCount, intent.ExpectedTargetRevision));
            Assert.Equal(view.Actors.Single(actor => actor.ActorId == fixture.Player).PresenceRevision,
                offer.ToCommand(fixture.NextId()).ExpectedActorRevision);
        }
        Assert.Equal(before, WorldSnapshotCodec.Encode(fixture.Genesis));
        selection.Clear();
        Assert.Contains(FarmSemanticAction.Speak, factory.Create(view, selection.Selected).Keys);

        WorldSnapshot ordinary = FarmWorldAdmission.CreateEmbedded("encounter-offer-legacy", "Traveler");
        EntityId player = ordinary.Residents.Single(actor => actor.ControllerKind == ResidentControllerKind.Player).ActorId;
        Assert.Null(EncounterActionOffers.Create(new LocalWorldView(ordinary, player),
            FarmActionScope.From(fixture.Delivery, view), Target(view, fixture.WolfFirst)));
    }

    [Fact]
    public void Active_choices_capture_only_visible_grants_and_wheel_never_updates_the_inspected_timing()
    {
        var fixture = new EncounterJourneyWorldFixture();
        WorldSnapshot active = Active(fixture);
        var factory = Offers(fixture);
        LocalWorldView view = View(fixture, active);
        var offers = factory.Create(view, Target(view, fixture.WolfFirst));
        Assert.Equal(new[] { FarmSemanticAction.Strike, FarmSemanticAction.Wait }, offers.Keys.Order().ToArray());
        FarmActionOffer strike = offers[FarmSemanticAction.Strike];
        var attack = Assert.IsType<QueueEncounterAbilityIntent>(strike.Intent);
        Assert.True(strike.CanExecute);
        Assert.Equal((view.Encounter!.EncounterId, view.Encounter.TimingRevision, fixture.Strike, fixture.WolfFirst),
            (attack.EncounterId, attack.ExpectedTimingRevision, attack.AbilityDefinitionId, attack.TargetActorId));
        var wait = Assert.IsType<QueueEncounterWaitIntent>(offers[FarmSemanticAction.Wait].Intent);
        Assert.Equal(view.Encounter.TimingRevision, wait.ExpectedTimingRevision);
        var bystander = factory.Create(view, Target(view, fixture.Wren))[FarmSemanticAction.Strike];
        Assert.False(bystander.CanExecute);
        Assert.Equal(fixture.Wren, bystander.TargetId);
        Assert.Null(bystander.Intent);

        var wheel = new FarmQuickWheelModel(offers);
        wheel.Select(1);
        Assert.Equal(fixture.WolfFirst, wheel.Selected!.PinnedTarget!.Id);
        Assert.Equal(FarmTargetKind.Encounter, wheel.Selected.PinnedTarget.Kind);
        WorldSnapshot npcChosen = Apply(fixture, active, WorldBatchKind.EncounterDecision,
            fixture.Wait(active, fixture.WolfFirst)).Candidate;
        Assert.True(npcChosen.Pacing!.Encounter!.Revision > attack.ExpectedTimingRevision);
        wheel.Refresh(target => factory.Create(View(fixture, npcChosen), target));
        Assert.False(wheel.Selected.Offer.CanExecute);
        Assert.Null(wheel.Selected.Offer.Intent);
        Assert.Same(attack, strike.Intent);
        WorldStepDecision stale = Apply(fixture, npcChosen, WorldBatchKind.EncounterDecision, strike.ToCommand(fixture.NextId()));
        Assert.Equal(WorldActionRejection.RevisionConflict, Assert.Single(stale.Receipts).Rejection);
        Assert.Empty(stale.Candidate.EncounterActions!.Pending);
        Assert.Equal(active.Progression!.ContentHash, stale.Candidate.Progression!.ContentHash);
    }

    [Fact]
    public void Actual_outcome_release_retains_recovery_blocks_farm_offers_and_still_allows_the_other_confrontation()
    {
        var fixture = new EncounterJourneyWorldFixture();
        var factory = Offers(fixture);
        WorldSnapshot state = Active(fixture);
        for (int boundary = 0; state.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome; boundary++)
        {
            Assert.True(boundary < 1_000, "The source-backed duel did not reach its finite outcome bound.");
            var timing = state.Pacing!.Encounter!;
            var due = WorldEncounterActions.DueActors(state);
            WorldActionCommand[] choices = due.Where(actor => !timing.Decisions.Any(choice => choice.ActorId == actor.ActorId))
                .Select(actor => fixture.Attack(state, actor.ActorId, actor.ActorId == fixture.Player ? fixture.WolfFirst : fixture.Player)).ToArray();
            WorldBatchKind kind = choices.Length != 0 ? WorldBatchKind.EncounterDecision
                : due.Count != 0 ? WorldBatchKind.EncounterResolution : WorldBatchKind.TacticalAdvance;
            state = Apply(fixture, state, kind, choices).Candidate;
        }
        Assert.True(state.Progression!.GetActor(fixture.Player).CurrentHp > 0);
        ulong debt = state.EncounterLifecycle!.GetDebt(fixture.Player).RemainingDelay;
        Assert.True(debt > 0);
        FarmActionOffer release = Assert.Single(factory.Create(View(fixture, state), Target(View(fixture, state), fixture.WolfFirst))).Value;
        Assert.Equal(FarmSemanticAction.ReleaseEncounter, release.Action);
        var intent = Assert.IsType<ReleaseEncounterIntent>(release.Intent);
        Assert.Equal(state.EncounterActions!.ContentHash, intent.ExpectedActionHash);
        Assert.Equal(state.Pacing!.Encounter!.EncounterId, intent.EncounterId);
        WorldStepDecision released = Apply(fixture, state, WorldBatchKind.EncounterLifecycle, release.ToCommand(fixture.NextId()));
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(released.Receipts).Status);
        state = released.Candidate;
        Assert.Equal(debt, state.EncounterLifecycle!.GetDebt(fixture.Player).RemainingDelay);
        var offers = factory.Create(View(fixture, state));
        foreach (var offer in offers.Values.Where(offer => offer.Action != FarmSemanticAction.CloseDialogue))
        {
            Assert.False(offer.CanExecute);
            Assert.Null(offer.Intent);
            Assert.Contains("recovering", offer.BlockingReason!, StringComparison.Ordinal);
        }
        var other = factory.Create(View(fixture, state), Target(View(fixture, state), fixture.WolfSecond));
        Assert.True(Assert.Single(other).Value.CanExecute); // Entry retains the debt; it does not spend ordinary readiness.
        for (int interval = 0; state.EncounterLifecycle!.GetDebt(fixture.Player).RemainingDelay != 0; interval++)
        {
            Assert.True(interval < 10);
            state = Apply(fixture, state, WorldBatchKind.AdvancingStep).Candidate;
        }
        Assert.True(factory.Create(View(fixture, state))[FarmSemanticAction.Speak].CanExecute);
    }

    [Fact]
    public async Task Real_client_entry_and_wait_are_deduplicated_and_stale_presentations_are_not_replaced()
    {
        var fixture = new EncounterJourneyWorldFixture();
        using var files = new SessionFiles();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var clock = new ManualSessionTimeProvider();
        var world = new LocalWorldSession(authority, fixture.Player, fixture.Delivery, clock);
        await using var session = new FarmClientSession(world, fixture.Delivery, fixture.Wren, Wood(fixture.Genesis), null);
        world.SetReady(true);
        LocalWorldView initial = world.ReadView();
        FarmTarget selected = Target(initial, fixture.WolfFirst);
        FarmActionOffer entry = session.CreateOffers(initial, selected)[FarmSemanticAction.EnterEncounter];
        Task<WorldActionReceipt> entered = session.SubmitOffer(entry);
        Assert.Same(entered, session.SubmitOffer(entry));
        await Tick(clock);
        Assert.Equal(CommandReceiptStatus.Applied, (await entered).Status);
        Assert.All(session.CreateOffers(world.ReadView(), selected).Values, offer => Assert.False(offer.CanExecute));
        Assert.Same(entered, session.SubmitOffer(entry));
        await Tick(clock);
        await Tick(clock);
        Assert.Equal(EncounterLifecyclePhase.Active, world.ReadView().EncounterLifecycle!.Phase);
        FarmActionOffer unused = session.CreateOffers(world.ReadView(), selected)[FarmSemanticAction.Wait];
        Assert.True(unused.CanExecute);
        await Tick(clock); // The host chooses the real wolf's bite; that advances the timing revision only.
        Task<WorldActionReceipt> stale = session.SubmitOffer(unused);
        await Tick(clock);
        Assert.Equal(WorldActionRejection.RevisionConflict, (await stale).Rejection);
        Assert.Same(stale, session.SubmitOffer(unused));
        FarmActionOffer wait = session.CreateOffers(world.ReadView(), selected)[FarmSemanticAction.Wait];
        Task<WorldActionReceipt> waiting = session.SubmitOffer(wait);
        Assert.Same(waiting, session.SubmitOffer(wait));
        await Tick(clock);
        Assert.Equal(CommandReceiptStatus.Applied, (await waiting).Status);
        Assert.Same(waiting, session.SubmitOffer(wait));
        Assert.Equal(4UL, authority.InspectForHost().Journal.CommandOutcomeCount); // Entry, actual NPC choice, stale Wait, fresh Wait.
    }

    [Fact]
    public async Task Explicit_encounter_open_attaches_Wren_and_reopens_the_same_profile_without_migrating_an_ordinary_save()
    {
        using var files = new SessionFiles();
        EntityId wren;
        await using (FarmClientSession first = await FarmClientSession.OpenEncounterAsync(files.Path,
            EncounterJourneyWorldFixture.SeedText, EncounterJourneyWorldFixture.PlayerName))
        {
            LocalWorldView view = first.World.ReadView();
            wren = first.WrenActorId;
            Assert.Equal(4, view.Actors.Count);
            Assert.Equal("Wren", view.Actors.Single(actor => actor.ActorId == wren).DisplayName);
            Assert.Equal(2, view.EncounterLifecycle!.Entries.Count);
            Assert.False(view.IsInEncounter);
        }
        await using (FarmClientSession restored = await FarmClientSession.OpenEncounterAsync(files.Path,
            EncounterJourneyWorldFixture.SeedText, EncounterJourneyWorldFixture.PlayerName))
            Assert.Equal(wren, restored.WrenActorId);
        using var ordinaryFiles = new SessionFiles();
        WorldSnapshot ordinary = FarmWorldAdmission.CreateEmbedded("ordinary-encounter-selection", "Traveler");
        using (SqliteWorldAuthority.CreateNew(ordinaryFiles.Path, ordinary)) { }
        await Assert.ThrowsAsync<InvalidDataException>(() => FarmClientSession.OpenEncounterAsync(ordinaryFiles.Path,
            EncounterJourneyWorldFixture.SeedText, EncounterJourneyWorldFixture.PlayerName));
        using var unchanged = SqliteWorldAuthority.OpenExisting(ordinaryFiles.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(ordinary), WorldSnapshotCodec.Encode(unchanged.ReadStateForHost()));
    }

    private static LocalWorldView View(EncounterJourneyWorldFixture fixture, WorldSnapshot state) => new(state, fixture.Player);
    private static EntityId Wood(WorldSnapshot state) => state.Gathering!.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Oak)
        .Definition.YieldDefinition.DefinitionId;
    private static FarmActionOffers Offers(EncounterJourneyWorldFixture fixture) => new(fixture.Delivery, fixture.Wren, Wood(fixture.Genesis), null);
    private static FarmTarget Target(LocalWorldView view, EntityId actorId)
    {
        var actor = view.Actors.Single(actor => actor.ActorId == actorId);
        return new(actor.ActorId, actor.DisplayName, FarmTargetKind.Encounter, actor.Feet);
    }
    private static WorldSnapshot Active(EncounterJourneyWorldFixture fixture)
    {
        WorldSnapshot state = Apply(fixture, fixture.Genesis, WorldBatchKind.EncounterLifecycle,
            fixture.Enter(fixture.Genesis, fixture.FirstScenario)).Candidate;
        for (int stage = 0; stage < 2; stage++) state = Apply(fixture, state, WorldBatchKind.EncounterLifecycle).Candidate;
        Assert.Equal(EncounterLifecyclePhase.Active, state.EncounterLifecycle!.Phase);
        return state;
    }
    private static WorldStepDecision Apply(EncounterJourneyWorldFixture fixture, WorldSnapshot state,
        WorldBatchKind kind, params WorldActionCommand[] actions) => new WorldStepProcessor().Decide(state, fixture.Batch(state, kind, actions));
}
