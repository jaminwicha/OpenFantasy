using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Worlds.Controllers;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.Controllers.Conservative;

public sealed class ConservativeControllerHostTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Human_and_unexpired_allowance_never_choose_even_with_a_ready_bystander(bool disconnected)
    {
        await using var fixture = await ConservativeControllerFixture.Open();
        var joined = await fixture.Join();
        await fixture.Connect(fixture.Observer);
        if (disconnected) await fixture.LoseTransport(joined);
        WorldSnapshot held = fixture.State;
        await fixture.Tick(disconnected ? TimeSpan.FromMilliseconds(29_950) : TimeSpan.FromHours(1));
        Assert.Equal(held.ContentHash, fixture.State.ContentHash);
        Assert.Equal(disconnected ? WorldControllerMode.ReconnectAllowance : WorldControllerMode.Human, fixture.Control.Mode);
        Assert.Empty(fixture.State.Pacing!.Encounter!.Decisions);
        Assert.Null(fixture.State.Retreat!.Active);
        Assert.True(WorldEncounterActions.HasRequiredDecisions(fixture.State));
    }

    [Fact]
    public async Task Expiry_with_zero_ready_ports_changes_control_only_until_a_human_bystander_is_ready()
    {
        await using var fixture = await ConservativeControllerFixture.Open();
        var joined = await fixture.Join();
        var observer = await fixture.Connect(fixture.Observer, ready: false);
        await fixture.LoseTransport(joined);
        WorldSnapshot lost = fixture.State;
        await fixture.Expire();
        ControllerJournalTests.AssertGameplayUnchanged(lost, fixture.State);
        WorldSnapshot autonomous = fixture.State;
        await fixture.Tick(TimeSpan.FromHours(1));
        Assert.Equal(autonomous.ContentHash, fixture.State.ContentHash);
        Assert.Empty(fixture.State.Pacing!.Encounter!.Decisions);
        observer.SetReady(true);
        await fixture.Until(() => fixture.State.Retreat!.Active is not null);
        Assert.Equal(fixture.Actor, fixture.State.Retreat!.Active!.ActorId);
        Assert.Equal(EncounterActorControl.RequiredHuman, fixture.State.Pacing!.Encounter!.GetActor(fixture.Actor).Control);
        Assert.Equal(WorldControllerMode.Human, fixture.State.Controllers!.Get(fixture.Observer).Mode);
        Assert.DoesNotContain(fixture.State.Pacing!.Encounter!.Decisions, row => row.ActorId == fixture.Observer);
    }

    [Fact]
    public async Task Autonomous_retreat_uses_the_ordinary_legal_route_and_charges_its_first_physical_sample()
    {
        await using var fixture = await ConservativeControllerFixture.Open();
        var joined = await fixture.Join();
        await fixture.Connect(fixture.Observer);
        await fixture.LoseTransport(joined);
        WorldSnapshot before = fixture.State;
        var advice = WorldEncounterRetreatAdvisor.Advise(before, fixture.Actor);
        Assert.True(advice.IsFound);
        await fixture.Expire();
        await fixture.Until(() => fixture.State.Retreat!.Active is not null);
        var accepted = fixture.State.Retreat!.Active!;
        Assert.Equal(fixture.Actor, accepted.ActorId);
        Assert.Equal(advice.Destination, accepted.Intent.Destination);
        Assert.Equal(advice.Route.ToArray(), accepted.Intent.Route.ToArray());
        Assert.Equal(WorldActionRejection.None, WorldEncounterRetreats.EvaluateQueue(before, fixture.Actor, accepted.Intent));
        Assert.Equal(0, accepted.SamplesTaken);
        Assert.Equal(0UL, accepted.ReadinessCharged);
        Assert.Equal(before.Space.Actors.Select(row => (row.ActorId, row.Feet, row.Revision)),
            fixture.State.Space.Actors.Select(row => (row.ActorId, row.Feet, row.Revision)));
        Assert.Equal(before.Calendar, fixture.State.Calendar);
        Assert.Equal(before.Progression!.ContentHash, fixture.State.Progression!.ContentHash);
        WorldSnapshot held = fixture.State;
        for (int cutoff = 0; cutoff < 4; cutoff++) await fixture.Tick();
        Assert.Equal(held.ContentHash, fixture.State.ContentHash);
        Assert.Equal(1UL, fixture.State.Retreat!.AcceptedCount);
        Assert.Single(fixture.State.Pacing!.Encounter!.Decisions, row => row.ActorId == fixture.Actor);
        Assert.Empty(new LocalAutonomousEncounterController().SelectChoices(fixture.State, [], fixture.Commands.NextId));

        var opponent = await fixture.Connect(fixture.Opponent);
        await fixture.ChooseWaits(opponent);
        await fixture.Until(() => fixture.State.Space.GetActor(fixture.Actor).Feet != accepted.Origin);
        var after = fixture.State;
        ulong sampleCharge = EncounterRetreatRules.SampleDelay(held.Pacing!.Encounter!.GetActor(fixture.Actor).NormalizedSpeed);
        Assert.Equal(QueueEncounterRetreatIntent.Endpoint(accepted.Origin, accepted.Intent.Route.Take(1)),
            after.Space.GetActor(fixture.Actor).Feet);
        Assert.Equal(sampleCharge, after.Retreat!.Active?.ReadinessCharged ?? after.Retreat.Last!.ReadinessCharged);
        Assert.Equal(1, after.Retreat.Active?.SamplesTaken ?? after.Retreat.Last!.SamplesTaken);
        Assert.Equal(before.Calendar, after.Calendar);
        Assert.Equal(before.Progression!.ContentHash, after.Progression!.ContentHash);
        Assert.Equal(1UL, after.Retreat.AcceptedCount);
        Assert.Empty(new LocalAutonomousEncounterController().SelectChoices(after, [], fixture.Commands.NextId));
    }

    [Fact]
    public async Task A_party_blocked_retreat_falls_back_to_paid_wait_and_preserves_every_human_hold()
    {
        await using var fixture = await ConservativeControllerFixture.Open(party: true);
        var joined = await fixture.Join();
        await fixture.Connect(fixture.Observer);
        Assert.False(WorldEncounterRetreats.IsSupportedSide(fixture.State, fixture.Actor));
        WorldSnapshot before = fixture.State;
        await fixture.LoseTransport(joined);
        await fixture.Expire();
        await fixture.Until(() => fixture.State.Pacing!.Encounter!.Decisions.Any(row => row.ActorId == fixture.Actor));
        var chosen = Assert.Single(fixture.State.Pacing!.Encounter!.Decisions, row => row.ActorId == fixture.Actor);
        Assert.Equal(EncounterDecisionKind.Wait, chosen.Kind);
        Assert.Null(fixture.State.Retreat!.Active);
        Assert.Equal(0UL, fixture.State.Retreat.AcceptedCount);
        Assert.Equal(before.Progression!.ContentHash, fixture.State.Progression!.ContentHash);
        Assert.Equal(before.Space.Actors.Select(row => (row.ActorId, row.Feet, row.Revision)),
            fixture.State.Space.Actors.Select(row => (row.ActorId, row.Feet, row.Revision)));
        WorldSnapshot held = fixture.State;
        for (int cutoff = 0; cutoff < 3; cutoff++) await fixture.Tick();
        Assert.Equal(held.ContentHash, fixture.State.ContentHash);
        Assert.DoesNotContain(fixture.State.Pacing!.Encounter!.Decisions, row => row.ActorId == fixture.Ally || row.ActorId == fixture.Opponent);

        var opponent = await fixture.Connect(fixture.Opponent);
        var ally = await fixture.Connect(fixture.Ally!.Value);
        await fixture.ChooseWaits(opponent, ally);
        await fixture.Until(() => fixture.State.Pacing!.Encounter!.GetActor(fixture.Actor).RemainingDelay > 0);
        var timing = fixture.State.Pacing!.Encounter!;
        Assert.Equal(timing.GetActor(fixture.Actor).StandardDelay, timing.GetActor(fixture.Actor).RemainingDelay);
        Assert.DoesNotContain(timing.Decisions, row => row.ActorId == fixture.Actor);
        Assert.Equal(EncounterActionResolutionStatus.Wait, fixture.State.EncounterActions!.LastResolution!.Status);
        Assert.Equal(fixture.Actor, fixture.State.EncounterActions!.LastResolution!.ActorId);
        Assert.Equal(before.Calendar, fixture.State.Calendar);
        Assert.Equal(before.Progression!.ContentHash, fixture.State.Progression!.ContentHash);
    }

    [Fact]
    public async Task Rejoin_at_a_safe_autonomous_boundary_restores_before_any_fresh_choice()
    {
        await using var fixture = await ConservativeControllerFixture.Open();
        var joined = await fixture.Join();
        var observer = await fixture.Connect(fixture.Observer, ready: false);
        await fixture.LoseTransport(joined);
        await fixture.Expire();
        WorldSnapshot autonomous = fixture.State;
        var returned = await fixture.Join();
        Assert.Equal(WorldControllerMode.Human, fixture.Control.Mode);
        Assert.False(returned.Channel.Player.Ready);
        ControllerJournalTests.AssertGameplayUnchanged(autonomous, fixture.State);
        observer.SetReady(true);
        WorldSnapshot restored = fixture.State;
        await fixture.Tick(TimeSpan.FromMinutes(1));
        Assert.Equal(restored.ContentHash, fixture.State.ContentHash);
        Assert.Empty(fixture.State.Pacing!.Encounter!.Decisions);
        Assert.Null(fixture.State.Retreat!.Active);
        Assert.Equal(LocalWorldRunState.Paused, returned.Channel.Player.ReadRunState());
    }

    [Fact]
    public async Task An_accepted_human_wait_survives_expiry_and_rejoin_without_replacement_or_automatic_readiness()
    {
        await using var fixture = await ConservativeControllerFixture.Open();
        var joined = await fixture.Join();
        await fixture.Connect(fixture.Observer);
        joined.Channel.Player.SetReady(true);
        var wait = fixture.Commands.Wait(fixture.State, fixture.Actor);
        Task<WorldActionReceipt> pending = joined.Channel.Player.SubmitAction(wait);
        await fixture.Tick();
        WorldActionReceipt receipt = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
        await fixture.LoseTransport(joined);
        await fixture.Expire();
        var retained = Assert.Single(fixture.State.Pacing!.Encounter!.Decisions, row => row.ActorId == fixture.Actor);
        Assert.Equal(wait.CommandId, retained.DecisionId);
        Assert.Null(fixture.State.Retreat!.Active);
        var returned = await fixture.Join();
        Assert.Equal(WorldControllerMode.Autonomous, fixture.Control.Mode);
        returned.Channel.Player.SetReady(true);
        Assert.False(returned.Channel.Player.Ready);
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt),
            WorldReceiptCodecV1.Encode(await returned.Channel.Player.SubmitAction(wait)));
        var opponent = await fixture.Connect(fixture.Opponent);
        await fixture.ChooseWaits(opponent);
        await fixture.Until(() => fixture.Control.Mode == WorldControllerMode.Human);
        Assert.False(returned.Channel.Player.Ready);
        Assert.Equal(LocalWorldRunState.Paused, returned.Channel.Player.ReadRunState());
        Assert.True(fixture.State.Pacing!.Encounter!.GetActor(fixture.Actor).RemainingDelay > 0);
        Assert.DoesNotContain(fixture.State.Pacing!.Encounter!.Decisions, row => row.ActorId == fixture.Actor);
        Assert.Equal(0UL, fixture.State.Retreat!.AcceptedCount);
    }

    [Fact]
    public async Task A_zero_ready_return_finishes_no_new_choice_for_another_autonomous_human()
    {
        await using var fixture = await ConservativeControllerFixture.Open();
        var first = await fixture.Join();
        var opponent = await fixture.Join(fixture.Opponent);
        first.Channel.Player.SetReady(true);
        var wait = fixture.Commands.Wait(fixture.State, fixture.Actor);
        Task<WorldActionReceipt> pending = first.Channel.Player.SubmitAction(wait);
        await fixture.Tick();
        Assert.Equal(WorldActionRejection.None,
            (await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Rejection);
        await fixture.LoseTransport(first);
        await fixture.LoseTransport(opponent);
        await fixture.Expire();
        Assert.Equal(WorldControllerMode.Autonomous, fixture.State.Controllers!.Get(fixture.Opponent).Mode);
        var returned = await fixture.Join();
        Assert.False(WorldControllerBoundaries.CanRestoreHuman(fixture.State, fixture.Actor));
        returned.Channel.Player.SetReady(true);
        Assert.False(returned.Channel.Player.Ready);
        WorldSnapshot held = fixture.State;
        for (int cutoff = 0; cutoff < 4; cutoff++) await fixture.Tick();
        Assert.Equal(held.ContentHash, fixture.State.ContentHash);
        Assert.Equal(wait.CommandId, Assert.Single(fixture.State.Pacing!.Encounter!.Decisions).DecisionId);
        Assert.Null(fixture.State.Retreat!.Active);
        Assert.True(WorldEncounterActions.HasRequiredDecisions(fixture.State));
    }

    [Fact]
    public async Task Ordinary_autonomous_choice_and_controller_events_replay_to_the_identical_saved_root()
    {
        await using var fixture = await ConservativeControllerFixture.Open();
        var joined = await fixture.Join();
        await fixture.Connect(fixture.Observer);
        await fixture.LoseTransport(joined);
        await fixture.Expire();
        await fixture.Until(() => fixture.State.Retreat!.Active is not null);
        WorldSnapshot selected = fixture.State;
        WorldActionCommand command = selected.Retreat!.Active!.Command;
        Assert.True((await fixture.Host.VerifyRecordedHistoryAsync()).ReproducesHead);
        byte[] expected = WorldSnapshotCodec.Encode(selected);
        await fixture.Host.StopAsync();
        using (var database = WorldDatabase.Open(fixture.Path, false))
        {
            WorldStepBatch[] batches = database.ReadJournal().Select(row => WorldStepCodec.Decode(row.AcceptedBatch)).ToArray();
            Assert.Contains(batches, batch => batch.ControllerEvent?.Kind == WorldControllerEventKind.Disconnected);
            Assert.Contains(batches, batch => batch.ControllerEvent?.Kind == WorldControllerEventKind.AllowanceExpired);
            var decisionBatch = Assert.Single(batches, batch => batch.Actions.Any(row => row.CommandId == command.CommandId));
            Assert.Equal(WorldBatchKind.EncounterDecision, decisionBatch.Kind);
            Assert.IsType<QueueEncounterRetreatIntent>(Assert.Single(decisionBatch.Actions).Intent);
            Assert.DoesNotContain(batches.SelectMany(batch => batch.Actions), row => row.ActorId == fixture.Actor
                && row.Intent is QueueEncounterAbilityIntent);
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(fixture.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal(expected, WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.NotNull(reopened.FindReceipt(command, fixture.Delivery(fixture.Actor)));
    }

    [Fact]
    public async Task Selector_reserves_a_proposed_retreat_and_does_not_replace_a_selected_choice_for_the_same_actor()
    {
        await using var fixture = await ConservativeControllerFixture.Open();
        var joined = await fixture.Join();
        await fixture.LoseTransport(joined);
        await fixture.Expire(); // No ready port: selection can be inspected without committing it.
        var controller = new LocalAutonomousEncounterController();
        var ownWait = fixture.Commands.Wait(fixture.State, fixture.Actor);
        Assert.Empty(controller.SelectChoices(fixture.State, [ownWait], fixture.Commands.NextId));
        var advice = WorldEncounterRetreatAdvisor.Advise(fixture.State, fixture.Opponent);
        Assert.True(advice.IsFound);
        var timing = fixture.State.Pacing!.Encounter!;
        var otherRetreat = fixture.Commands.Command(fixture.State, fixture.Opponent,
            new QueueEncounterRetreatIntent(timing.EncounterId, timing.Revision, advice.Destination, advice.Route));
        Assert.Equal(WorldActionRejection.None, WorldEncounterRetreats.EvaluateQueue(fixture.State, fixture.Opponent,
            (QueueEncounterRetreatIntent)otherRetreat.Intent));
        var proposed = Assert.Single(controller.SelectChoices(fixture.State, [otherRetreat], fixture.Commands.NextId));
        Assert.Equal(fixture.Actor, proposed.ActorId);
        Assert.IsType<QueueEncounterWaitIntent>(proposed.Intent);
        Assert.Empty(fixture.State.Pacing!.Encounter!.Decisions);
        Assert.Null(fixture.State.Retreat!.Active);
    }
}
