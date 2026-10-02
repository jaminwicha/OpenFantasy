using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care.Dispositions;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Dispositions;

public sealed class CareDecisionHostTests
{
    [Fact]
    public async Task Invalid_next_player_cannot_push_automatic_treatment_ahead_of_an_already_queued_patient_cancellation()
    {
        using var files = new StorageFiles();
        var fixture = new CareDispositionJournalFixture(humanObserver: true);
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var defeated = fixture.DefeatWithHelperNearby(authority);
        var time = new ManualSessionTimeProvider();
        await using var host = new LocalWorldSession(authority, fixture.Journey.Patient,
            ConstructionUpgradeFixture.Delivery(defeated, fixture.Journey.Patient), time);
        await using var bystander = await host.ConnectPlayerAsync(fixture.Journey.Observer,
            ConstructionUpgradeFixture.Delivery(defeated, fixture.Journey.Observer));
        host.SetReady(true);
        bystander.SetReady(true);
        var acknowledgment = host.SubmitAction(fixture.Journey.Commands.Command(defeated, fixture.Journey.Patient,
            Assert.Single(host.ReadView().Care!.Offers).Intent));
        await Tick(time);
        Assert.Equal(WorldActionRejection.None, (await acknowledgment).Rejection);
        // Patient was last served, so ordinary rotation would next choose the other queued player.
        var pending = authority.ReadStateForHost();
        var request = pending.Care!.Pending!;
        var invalidCommand = fixture.Journey.Commands.Command(pending, fixture.Journey.Observer,
            new DeclinePendingCareIntent(request.AcknowledgmentId, request.EncounterId, pending.Care.Revision));
        var cancellationCommand = fixture.Journey.Commands.Command(pending, fixture.Journey.Patient,
            host.ReadView().Care!.Decision!.Cancellation!);
        var invalid = bystander.SubmitAction(invalidCommand);
        var cancel = host.SubmitAction(cancellationCommand);
        await Until(() => bystander.Pending?.Command.CommandId == invalidCommand.CommandId
            && host.Player.Pending?.Command.CommandId == cancellationCommand.CommandId);
        await Tick(time);
        // Publication completes before the async caller continuation necessarily resumes. Await it
        // under the existing five-second fixture bound, without advancing another simulation tick.
        await Until(() => cancel.IsCompleted);
        Assert.True(cancel.IsCompletedSuccessfully);
        Assert.Equal(WorldActionRejection.None, (await cancel).Rejection);
        Assert.False(invalid.IsCompleted);
        Assert.Empty(authority.ReadStateForHost().Care!.Treatments);
        Assert.Single(authority.ReadStateForHost().Care!.Dispositions);
        await Tick(time);
        Assert.Equal(WorldActionRejection.Unavailable, (await invalid).Rejection);
        Assert.Equal(0, authority.ReadStateForHost().Progression!.GetActor(fixture.Journey.Patient).CurrentHp);
        Assert.Equal(pending.Inventory.ContentHash, authority.ReadStateForHost().Inventory.ContentHash);
    }

    [Fact]
    public async Task Queued_patient_cancellation_precedes_automatic_treatment_and_exact_retry_retains_its_receipt()
    {
        using var files = new StorageFiles();
        var fixture = new CareDispositionJournalFixture();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var pending = fixture.RequestAfterDefeat(authority);
        var time = new ManualSessionTimeProvider();
        await using var host = new LocalWorldSession(authority, fixture.Journey.Patient,
            ConstructionUpgradeFixture.Delivery(pending, fixture.Journey.Patient), time);
        var intent = Assert.IsType<CancelPendingCareIntent>(host.ReadView().Care!.Decision!.Cancellation);
        Assert.Null(host.ReadView().Care!.Decision!.Treatment);
        Assert.Null(host.ReadView().Care!.Decision!.Decline);
        var command = fixture.Journey.Commands.Command(pending, fixture.Journey.Patient, intent);
        host.SetReady(true);
        Task<WorldActionReceipt> action = host.SubmitAction(command);
        Task<WorldActionReceipt> duplicate = host.SubmitAction(command);
        await Tick(time);
        Assert.Equal(WorldActionRejection.None, (await action).Rejection);
        Assert.Equal(WorldReceiptCodecV1.Encode(await action), WorldReceiptCodecV1.Encode(await duplicate));
        host.SetReady(false);
        var closed = authority.ReadStateForHost();
        Assert.Equal(pending.Revision + 1, closed.Revision);
        Assert.Equal(pending.Progression!.ContentHash, closed.Progression!.ContentHash);
        Assert.Equal(pending.Inventory.ContentHash, closed.Inventory.ContentHash);
        Assert.Equal(pending.Calendar, closed.Calendar);
        Assert.Equal(pending.EncounterOutcome!.ContentHash, closed.EncounterOutcome!.ContentHash);
        Assert.Empty(closed.Care!.Treatments);
        Assert.Single(closed.Care.Dispositions);
        Assert.Equal(LocalCareStatus.Cancelled, host.ReadView().Care!.Status);
        Assert.Equal(LocalCareStatus.Cancelled, PlayerViewCodec.Decode(PlayerViewCodec.Encode(host.ReadView())).Care!.Status);
        await Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(closed.ContentHash, authority.ReadStateForHost().ContentHash);
        Assert.Equal(WorldReceiptCodecV1.Encode(await action), WorldReceiptCodecV1.Encode(await host.SubmitAction(command)));
        Assert.True((await host.VerifyRecordedHistoryAsync()).ReproducesHead);
        await host.StopAsync();
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal(closed.ContentHash, reopened.ReadStateForHost().ContentHash);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invalid_terminal_traffic_gets_a_receipt_but_does_not_starve_host_treatment(bool wrongActor)
    {
        using var files = new StorageFiles();
        var fixture = new CareDispositionJournalFixture();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var pending = fixture.RequestAfterDefeat(authority);
        var request = pending.Care!.Pending!;
        var time = new ManualSessionTimeProvider();
        await using var host = new LocalWorldSession(authority, fixture.Journey.Patient,
            ConstructionUpgradeFixture.Delivery(pending, fixture.Journey.Patient), time);
        WorldActionIntent intent = wrongActor
            ? new DeclinePendingCareIntent(request.AcknowledgmentId, request.EncounterId, pending.Care.Revision)
            : new CancelPendingCareIntent(request.AcknowledgmentId, request.EncounterId, pending.Care.Revision + 1);
        host.SetReady(true);
        var action = host.SubmitAction(fixture.Journey.Commands.Command(pending, fixture.Journey.Patient, intent));
        await Tick(time);
        Assert.Equal(wrongActor ? WorldActionRejection.NotAuthorized : WorldActionRejection.RevisionConflict,
            (await action).Rejection);
        var treated = authority.ReadStateForHost();
        Assert.Equal(pending.Revision + 2, treated.Revision);
        Assert.Single(treated.Care!.Treatments);
        Assert.Empty(treated.Care.Dispositions);
        Assert.Null(treated.Care.Pending);
        Assert.True(treated.Progression!.GetActor(fixture.Journey.Patient).CurrentHp > 0);
        Assert.Equal(pending.Calendar, treated.Calendar);
        Assert.Equal(LocalCareStatus.Treated, host.ReadView().Care!.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Human_helper_receives_its_own_explicit_choice_and_is_never_automatically_conscripted(bool treat)
    {
        using var files = new StorageFiles();
        var fixture = new CareDispositionJournalFixture(humanHelper: true);
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var defeated = fixture.DefeatWithHelperNearby(authority);
        var time = new ManualSessionTimeProvider();
        await using var host = new LocalWorldSession(authority, fixture.Journey.Patient,
            ConstructionUpgradeFixture.Delivery(defeated, fixture.Journey.Patient), time);
        await using var helper = await host.ConnectPlayerAsync(fixture.Journey.Helper,
            ConstructionUpgradeFixture.Delivery(defeated, fixture.Journey.Helper));
        var acknowledgment = Assert.Single(host.ReadView().Care!.Offers);
        Assert.True(acknowledgment.CanAcknowledge);
        host.SetReady(true);
        var request = host.SubmitAction(fixture.Journey.Commands.Command(defeated, fixture.Journey.Patient, acknowledgment.Intent));
        await Tick(time);
        Assert.Equal(WorldActionRejection.None, (await request).Rejection);
        var pending = authority.ReadStateForHost();
        await Tick(time);
        Assert.Equal(pending.ContentHash, authority.ReadStateForHost().ContentHash);
        Assert.Null(new LocalCareController().SelectTreatment(pending, authority.IsIdentityOccupiedForHost));
        var decision = helper.ReadView().Care!.Decision!;
        Assert.Null(decision.Cancellation);
        Assert.NotNull(decision.Decline);
        Assert.NotNull(decision.Treatment);
        Assert.Null(host.ReadView().Care!.Decision!.Treatment);
        var restored = PlayerViewCodec.Decode(PlayerViewCodec.Encode(helper.ReadView()));
        Assert.Equal(decision, restored.Care!.Decision);
        helper.SetReady(true);
        WorldActionIntent choice = treat ? decision.Treatment! : decision.Decline!;
        var action = helper.SubmitAction(fixture.Journey.Commands.Command(pending, fixture.Journey.Helper, choice));
        await Tick(time);
        Assert.Equal(WorldActionRejection.None, (await action).Rejection);
        var closed = authority.ReadStateForHost();
        Assert.Null(closed.Care!.Pending);
        Assert.Equal(treat ? 1 : 0, closed.Care.Treatments.Count);
        Assert.Equal(treat ? 0 : 1, closed.Care.Dispositions.Count);
        Assert.Equal(treat, closed.Progression!.GetActor(fixture.Journey.Patient).CurrentHp > 0);
        Assert.True((await host.VerifyRecordedHistoryAsync()).ReproducesHead);
    }

    [Fact]
    public void A_later_real_defeat_in_the_same_episode_reports_current_care_eligibility_instead_of_the_previous_treatment()
    {
        using var files = new StorageFiles();
        var fixture = new CareDispositionJournalFixture();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        fixture.Journey.CraftDose(authority);
        var pending = fixture.RequestAfterDefeat(authority);
        EntityId episode = pending.EncounterActions!.Scope.EncounterId;
        var state = fixture.Journey.Act(authority, fixture.Journey.Helper,
            Assert.IsType<AdministerRemedyIntent>(WorldCareActions.PrepareRemedy(pending)), WorldBatchKind.Care);
        var automatic = new LocalEncounterController();
        for (int step = 0; state.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome; step++)
        {
            Assert.True(step < 800);
            var commands = automatic.SelectChoices(state, fixture.Journey.Commands.NextId).ToList();
            if (WorldEncounterActions.HasRequiredDecisions(state)) commands.Add(fixture.Journey.Commands.Wait(state, fixture.Journey.Patient));
            var kind = commands.Count != 0 ? WorldBatchKind.EncounterDecision
                : WorldEncounterActions.DueActors(state).Count == 0 ? WorldBatchKind.TacticalAdvance : WorldBatchKind.EncounterResolution;
            state = fixture.Journey.Commit(authority, kind, commands).Candidate;
        }
        Assert.Equal(episode, state.EncounterActions!.Scope.EncounterId);
        Assert.Equal(0, state.Progression!.GetActor(fixture.Journey.Patient).CurrentHp);
        var view = new LocalWorldView(state, fixture.Journey.Patient);
        Assert.Null(view.Care!.Closure);
        var offer = Assert.Single(view.Care.Offers);
        Assert.True(state.Care!.CanAcknowledgeDefeat(fixture.Journey.Patient, state.EncounterActions.LastResolution!.WorldSequence));
        Assert.Equal(WorldCareActions.EvaluateAcknowledgment(state, fixture.Journey.Patient, offer.Intent), offer.Rejection);
        // This actual second defeat occurs before the previous helper work has recovered. The new
        // request is chronologically eligible, but its current helper constraint must remain visible.
        Assert.True(state.EncounterLifecycle.GetDebt(fixture.Journey.Helper).RemainingDelay > 0);
        Assert.Equal(WorldActionRejection.Unavailable, offer.Rejection);
        Assert.False(offer.CanAcknowledge);
        Assert.Equal(1UL, state.Care.TotalAcknowledgments);
        Assert.Equal(1UL, state.Care.TotalTreatments);
        Assert.Null(state.Care.Pending);
    }

    [Fact]
    public async Task Returning_incapacitated_patient_regains_control_unready_and_can_cancel_after_expiry()
    {
        using var files = new StorageFiles();
        var fixture = new CareDispositionJournalFixture();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var pending = fixture.RequestAfterDefeat(authority);
        var time = new ManualSessionTimeProvider();
        var delivery = ConstructionUpgradeFixture.Delivery(pending, fixture.Journey.Patient);
        await using var host = new LocalWorldSession(authority, fixture.Journey.Patient, delivery, time);
        await host.CompleteNetworkAdmissionAsync(host.Player, TestContext.Current.CancellationToken, () => { });
        await host.Player.ConfirmTransportDisconnectedAsync(WorldControllerDisconnectReason.TransportLost);
        await Tick(time, TimeSpan.FromSeconds(30));
        Assert.Equal(WorldControllerMode.Autonomous, authority.ReadStateForHost().Controllers!.Get(fixture.Journey.Patient).Mode);
        Assert.Equal(pending.Care!.ContentHash, authority.ReadStateForHost().Care!.ContentHash);
        await using var returning = await host.ConnectPlayerAsync(fixture.Journey.Patient, delivery);
        await host.CompleteNetworkAdmissionAsync(returning, TestContext.Current.CancellationToken, () => { });
        Assert.Equal(WorldControllerMode.Human, authority.ReadStateForHost().Controllers!.Get(fixture.Journey.Patient).Mode);
        Assert.Equal(LocalWorldRunState.Paused, returning.ReadRunState());
        var restored = authority.ReadStateForHost();
        Assert.Equal(pending.Care.ContentHash, restored.Care!.ContentHash);
        Assert.Equal(0, returning.ReadView().Progression!.Hp);
        var choice = returning.ReadView().Care!.Decision!.Cancellation!;
        var command = fixture.Journey.Commands.Command(restored, fixture.Journey.Patient, choice);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => returning.SubmitAction(command));
        await Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(restored.ContentHash, authority.ReadStateForHost().ContentHash);
        returning.SetReady(true);
        var action = returning.SubmitAction(command);
        await Tick(time);
        Assert.Equal(WorldActionRejection.None, (await action).Rejection);
        Assert.Single(authority.ReadStateForHost().Care!.Dispositions);
        Assert.Equal(0, returning.ReadView().Progression!.Hp);
    }
}
