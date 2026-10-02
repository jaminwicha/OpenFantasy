using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.EncounterOutcome;
using OpenFantasy.Hosting.Tests.Worlds.Controllers;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.Controllers.Aftermath;

public sealed class AutonomousAftermathTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Human_and_grace_preserve_the_exact_outcome_and_care_choice(bool lost)
    {
        await using var fixture = await AutonomousAftermathFixture.Open();
        var joined = await fixture.Join();
        await fixture.ConnectObserver();
        if (lost) await fixture.Lose(joined);
        WorldSnapshot held = fixture.State;
        await fixture.Tick(lost ? TimeSpan.FromMilliseconds(29_950) : TimeSpan.FromHours(1));
        Assert.Equal(held.ContentHash, fixture.State.ContentHash);
        Assert.Null(fixture.State.Care!.Pending);
        Assert.Empty(fixture.State.EncounterOutcome!.Acknowledgments);
        Assert.Null(new LocalAutonomousAftermathController().SelectChoice(fixture.State, _ => false));
    }

    [Fact]
    public async Task Legal_autonomous_care_has_separate_patient_and_helper_receipts_and_one_real_paid_dose()
    {
        await using var fixture = await AutonomousAftermathFixture.Open();
        var joined = await fixture.Join();
        var observer = await fixture.ConnectObserver(ready: false);
        await fixture.Lose(joined);
        WorldSnapshot lost = fixture.State;
        await fixture.Tick(TimeSpan.FromSeconds(30));
        ControllerJournalTests.AssertGameplayUnchanged(lost, fixture.State);
        WorldSnapshot expired = fixture.State;
        await fixture.Tick(TimeSpan.FromHours(1));
        Assert.Equal(expired.ContentHash, fixture.State.ContentHash);
        var controller = new LocalAutonomousAftermathController();
        WorldActionCommand selected = Assert.IsType<WorldActionCommand>(controller.SelectChoice(expired, _ => false));
        var intent = Assert.IsType<AcknowledgeCareIntent>(selected.Intent);
        Assert.Equal(fixture.Agreement.AgreementId, intent.AgreementId);
        Assert.Equal(fixture.Actor, selected.ActorId);
        Assert.Equal(fixture.Delivery(fixture.Actor).PrincipalId, selected.PrincipalId);
        Assert.Equal(WorldActionCodec.Encode(selected), WorldActionCodec.Encode(controller.SelectChoice(expired, _ => false)!));
        Assert.Null(controller.SelectChoice(expired, identity => identity == selected.CommandId));

        observer.SetReady(true);
        await fixture.Tick();
        WorldSnapshot requested = fixture.State;
        Assert.Equal(selected.CommandId, requested.Care!.Pending!.CommandId);
        Assert.Equal(EntityId.DeriveSpawned(selected.CommandId, 0), requested.Care.Pending.AcknowledgmentId);
        Assert.Equal(expired.Revision + 1, requested.Revision);
        Assert.Equal(expired.Inventory.ContentHash, requested.Inventory.ContentHash);
        Assert.Equal(expired.Production!.ContentHash, requested.Production!.ContentHash);
        Assert.Equal(0, requested.Progression!.GetActor(fixture.Actor).CurrentHp);
        Assert.Empty(requested.Care.Treatments);
        Assert.Empty(requested.EncounterOutcome!.Acknowledgments);
        Assert.Equal(WorldActionRejection.None, fixture.Authority.FindReceipt(selected, fixture.Delivery(fixture.Actor))!.Rejection);

        var treatment = new LocalCareController().SelectTreatment(requested, fixture.Authority.IsIdentityOccupiedForHost)!;
        await fixture.Tick();
        WorldSnapshot treated = fixture.State;
        Assert.Equal(requested.Revision + 1, treated.Revision);
        Assert.Equal(EncounterLifecyclePhase.Active, treated.EncounterLifecycle!.Phase);
        Assert.Equal(requested.EncounterActions!.Scope.EncounterId, treated.EncounterActions!.Scope.EncounterId);
        Assert.Null(treated.Care!.Pending);
        Assert.True(treated.Progression!.GetActor(fixture.Actor).CurrentHp > 0);
        Assert.Equal(treatment.CommandId, Assert.Single(treated.Care.Treatments).CommandId);
        var consumed = Assert.Single(treated.Production!.ConsumedItems);
        Assert.Equal(treatment.CommandId, consumed.CommandId);
        Assert.Equal(1UL, consumed.Ingredient.Quantity);
        Assert.True(treated.Pacing!.Encounter!.GetActor(fixture.Agreement.HelperActorId).RemainingDelay > 0);
        Assert.Equal(expired.Calendar, treated.Calendar);
        Assert.Equal(expired.Space.GetActor(fixture.Actor).Feet, treated.Space.GetActor(fixture.Actor).Feet);
        Assert.Equal(WorldControllerMode.Autonomous, fixture.Mode);
        Assert.True((await fixture.Host.VerifyRecordedHistoryAsync()).ReproducesHead);
    }

    [Fact]
    public async Task A_player_controlled_helper_is_not_conscripted_and_incapacity_is_not_automatically_terminalized()
    {
        await using var fixture = await AutonomousAftermathFixture.Open(humanHelper: true);
        var joined = await fixture.Join();
        await fixture.ConnectObserver();
        Assert.Equal(WorldActionRejection.None, WorldCareActions.EvaluateAcknowledgment(fixture.State, fixture.Actor, fixture.CareIntent()));
        await fixture.Lose(joined);
        await fixture.Tick(TimeSpan.FromSeconds(30));
        WorldSnapshot held = fixture.State;
        for (int count = 0; count < 4; count++) await fixture.Tick();
        Assert.Equal(held.ContentHash, fixture.State.ContentHash);
        Assert.Equal(0, fixture.State.Progression!.GetActor(fixture.Actor).CurrentHp);
        Assert.Null(fixture.State.Care!.Pending);
        Assert.Empty(fixture.State.EncounterOutcome!.Acknowledgments);
        Assert.Equal(0UL, fixture.State.EncounterOutcome.FinalizedCount);
        Assert.Equal(EncounterLifecyclePhase.Outcome, fixture.State.EncounterLifecycle!.Phase);
    }

    [Fact]
    public async Task Living_acceptance_and_world_completion_are_separate_and_neutral_after_rejoin()
    {
        await using var fixture = await AutonomousAftermathFixture.Open(defeated: false);
        var joined = await fixture.Join();
        await fixture.ConnectObserver();
        await fixture.Lose(joined);
        await fixture.Tick(TimeSpan.FromSeconds(30));
        WorldSnapshot accepted = fixture.State;
        Assert.True(accepted.EncounterOutcome!.HasAcknowledged(fixture.Actor));
        Assert.Equal(0UL, accepted.EncounterOutcome.FinalizedCount);
        Assert.Equal(EncounterLifecyclePhase.Outcome, accepted.EncounterLifecycle!.Phase);
        var before = new LocalWorldView(accepted, fixture.Actor).EncounterOutcome!;
        Assert.Equal(LocalOutcomeAcknowledgmentMode.ControllerAcceptance, before.AcknowledgmentMode);
        await fixture.Tick();
        WorldSnapshot completed = fixture.State;
        Assert.Equal(accepted.Revision + 1, completed.Revision);
        Assert.Equal(1UL, completed.EncounterOutcome!.FinalizedCount);
        Assert.Equal(EncounterLifecyclePhase.Exploration, completed.EncounterLifecycle!.Phase);
        Assert.Equal(accepted.Progression!.ContentHash, completed.Progression!.ContentHash);
        Assert.Equal(accepted.Inventory.ContentHash, completed.Inventory.ContentHash);
        Assert.Equal(accepted.Calendar, completed.Calendar);
        var returned = await fixture.Join();
        Assert.Equal(WorldControllerMode.Human, fixture.Mode);
        Assert.False(returned.Channel.Player.Ready);
        var view = returned.Channel.Player.ReadView().EncounterOutcome!;
        Assert.True(view.ObserverHasRead);
        Assert.Equal(LocalOutcomeAcknowledgmentMode.ControllerAcceptance, view.AcknowledgmentMode);
        Assert.Equal(LocalOutcomeStage.Completed, view.Stage);
        Assert.True((await fixture.Host.VerifyRecordedHistoryAsync()).ReproducesHead);
    }

    [Fact]
    public async Task A_living_autonomous_acceptance_does_not_satisfy_the_other_required_human()
    {
        await using var fixture = await AutonomousAftermathFixture.Open(defeated: false, humanOpponent: true);
        var joined = await fixture.Join();
        await fixture.ConnectObserver();
        await fixture.Lose(joined);
        await fixture.Tick(TimeSpan.FromSeconds(30));
        Assert.True(fixture.State.EncounterOutcome!.HasAcknowledged(fixture.Actor));
        Assert.False(fixture.State.EncounterOutcome.HasAcknowledged(fixture.Opponent));
        WorldSnapshot held = fixture.State;
        await fixture.Tick(TimeSpan.FromHours(1));
        Assert.Equal(held.ContentHash, fixture.State.ContentHash);
        await using var other = await fixture.Host.ConnectPlayerAsync(fixture.Opponent, fixture.Delivery(fixture.Opponent));
        other.SetReady(true);
        var determination = WorldEncounterOutcomes.Determine(fixture.State)!;
        var command = fixture.Commands.Command(fixture.State, fixture.Opponent,
            new AcknowledgeEncounterOutcomeIntent(determination.EncounterId, determination.ActionHash));
        Task<WorldActionReceipt> pending = other.SubmitAction(command);
        await fixture.Tick();
        Assert.Equal(WorldActionRejection.None, (await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Rejection);
        Assert.Equal(0UL, fixture.State.EncounterOutcome!.FinalizedCount);
        await fixture.Tick();
        Assert.Equal(1UL, fixture.State.EncounterOutcome!.FinalizedCount);
    }

    [Fact]
    public async Task Autonomous_care_preserves_another_humans_queued_outcome_request_and_never_returns_the_wrong_receipt()
    {
        await using var fixture = await AutonomousAftermathFixture.Open(humanOpponent: true);
        var joined = await fixture.Join();
        await fixture.Lose(joined);
        await fixture.Tick(TimeSpan.FromSeconds(30));
        await using var other = await fixture.Host.ConnectPlayerAsync(fixture.Opponent, fixture.Delivery(fixture.Opponent));
        other.SetReady(true);
        var outcome = WorldEncounterOutcomes.Determine(fixture.State)!;
        var command = fixture.Commands.Command(fixture.State, fixture.Opponent,
            new AcknowledgeEncounterOutcomeIntent(outcome.EncounterId, outcome.ActionHash));
        Task<WorldActionReceipt> pending = other.SubmitAction(command);
        await fixture.Tick();
        Assert.NotNull(fixture.State.Care!.Pending);
        Assert.False(pending.IsCompleted);
        await fixture.Tick();
        Assert.Equal(EncounterLifecyclePhase.Active, fixture.State.EncounterLifecycle!.Phase);
        Assert.False(pending.IsCompleted);
        await fixture.Tick();
        WorldActionReceipt refused = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(command.CommandId, refused.CommandId);
        Assert.Equal(command.ContentHash, refused.CommandHash);
        Assert.Equal(WorldActionRejection.Unavailable, refused.Rejection);
        Assert.Equal(WorldReceiptCodecV1.Encode(refused), WorldReceiptCodecV1.Encode(
            fixture.Authority.FindReceipt(command, fixture.Delivery(fixture.Opponent))!));
        Assert.Empty(fixture.State.EncounterOutcome!.Acknowledgments);
        Assert.Single(fixture.State.Care!.Treatments);
    }

    [Fact]
    public async Task A_saved_autonomous_request_reopens_exactly_and_the_helper_spends_only_one_dose()
    {
        await using var fixture = await AutonomousAftermathFixture.Open();
        var joined = await fixture.Join();
        await fixture.ConnectObserver();
        await fixture.Lose(joined);
        await fixture.Tick(TimeSpan.FromSeconds(30));
        WorldSnapshot pending = fixture.State;
        Assert.NotNull(pending.Care!.Pending);
        await fixture.Host.StopAsync();
        var restored = SqliteWorldAuthority.OpenExisting(fixture.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal(WorldSnapshotCodec.Encode(pending), WorldSnapshotCodec.Encode(restored.ReadStateForHost()));
        var time = new ManualSessionTimeProvider();
        await using var host = new LocalWorldSession(restored, fixture.Actor, fixture.Delivery(fixture.Actor), time);
        await host.Player.DisposeAsync();
        await using var observer = await host.ConnectPlayerAsync(fixture.Observer, fixture.Delivery(fixture.Observer));
        observer.SetReady(true);
        await SessionFixture.Until(() => time.HasPendingTimer);
        await LiveControllerHostTests.Tick(time);
        WorldSnapshot treated = restored.ReadStateForHost();
        Assert.Null(treated.Care!.Pending);
        Assert.Single(treated.Care.Treatments);
        Assert.Equal(1UL, Assert.Single(treated.Production!.ConsumedItems).Ingredient.Quantity);
        Assert.Equal(pending.Revision + 1, treated.Revision);
        Assert.True((await host.VerifyRecordedHistoryAsync()).ReproducesHead);
    }
}
