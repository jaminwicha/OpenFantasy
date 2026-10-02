using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Recovery;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care.Dispositions;

public sealed class CareDispositionJournalTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Patient_or_helper_closes_the_real_request_without_healing_and_exact_receipt_survives_a_real_cut(bool decline)
    {
        using var files = new StorageFiles();
        var fixture = new CareDispositionJournalFixture();
        WorldSnapshot final;
        WorldActionCommand command;
        WorldActionReceipt receipt;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            WorldSnapshot pending = fixture.RequestAfterDefeat(authority);
            var acknowledgment = pending.Care!.Pending!;
            EntityId actor = decline ? fixture.Journey.Helper : fixture.Journey.Patient;
            PendingCareDispositionIntent intent = decline
                ? new DeclinePendingCareIntent(acknowledgment.AcknowledgmentId, acknowledgment.EncounterId, pending.Care.Revision)
                : new CancelPendingCareIntent(acknowledgment.AcknowledgmentId, acknowledgment.EncounterId, pending.Care.Revision);
            command = fixture.Journey.Commands.Command(pending, actor, intent);
            var selectedRemedy = fixture.Journey.Commands.Command(pending, fixture.Journey.Helper,
                Assert.IsType<AdministerRemedyIntent>(WorldCareActions.PrepareRemedy(pending)));
            var decision = fixture.Journey.Commit(authority, WorldBatchKind.Care, [command]);
            receipt = Assert.Single(decision.Receipts);
            var closed = decision.Candidate;
            AssertUnchangedNonCare(pending, closed);
            Assert.Null(closed.Care!.Pending);
            Assert.Empty(closed.Care.Treatments);
            var disposition = Assert.Single(closed.Care.Dispositions);
            Assert.Equal((command.CommandId, actor, acknowledgment.AcknowledgmentId),
                (disposition.CommandId, disposition.DecidingActorId, disposition.AcknowledgmentId));
            Assert.Equal(decline ? CareDispositionKind.HelperDeclined : CareDispositionKind.PatientCancelled, disposition.Kind);
            Assert.Equal(EncounterLifecyclePhase.Outcome, closed.EncounterLifecycle!.Phase);
            Assert.Equal(0, closed.Progression!.GetActor(fixture.Journey.Patient).CurrentHp);
            Assert.False(closed.Care.CanAcknowledgeDefeat(fixture.Journey.Patient, acknowledgment.Defeat.WorldSequence));

            var late = authority.Commit(fixture.Journey.Commands.Batch(closed, WorldBatchKind.Care, [selectedRemedy]));
            Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(late.Receipts).Rejection);
            AssertUnchangedNonCare(closed, late.Candidate);
            Assert.Equal(closed.Care.ContentHash, late.Candidate.Care!.ContentHash);
            var retry = new AcknowledgeCareIntent(acknowledgment.AgreementId, acknowledgment.EncounterId,
                late.Candidate.EncounterLifecycle!.Revision, late.Candidate.EncounterActions!.ContentHash);
            Assert.Equal(WorldActionRejection.Unavailable, WorldCareActions.EvaluateAcknowledgment(late.Candidate,
                fixture.Journey.Patient, retry));

            fixture.Journey.DisconnectAndComplete(authority);
            final = fixture.Journey.ArchiveBeforeRescue(authority);
            Assert.Equal(1UL, final.Care!.TotalDispositions);
            Assert.Empty(final.Care.Dispositions);
            Assert.Equal(1UL, final.Care.Continuation!.ArchivedDispositions);
            Assert.False(final.Care.CanAcknowledgeDefeat(fixture.Journey.Patient, acknowledgment.Defeat.WorldSequence));
            Assert.Empty(final.Care.Continuation.PatientWitnesses);
            Assert.Equal(command.CommandId, Assert.Single(final.Care.Continuation.ClosedDefeatWitnesses).Disposition!.CommandId);
            Assert.True(authority.IsIdentityOccupiedForHost(command.CommandId));
            Assert.True(authority.IsIdentityOccupiedForHost(disposition.DispositionId));
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(
            reopened.FindReceipt(command, ConstructionUpgradeFixture.Delivery(final, command.ActorId)))));
        Assert.True(reopened.ReadMaterialCareArchiveForHost(0).Segment.Continued.Care!.SupportsDispositions);
    }

    [Theory]
    [InlineData("wrong-actor", WorldActionRejection.NotAuthorized)]
    [InlineData("wrong-request", WorldActionRejection.Unavailable)]
    [InlineData("wrong-episode", WorldActionRejection.Unavailable)]
    [InlineData("stale-care", WorldActionRejection.RevisionConflict)]
    public void Refused_disposition_is_durable_and_preserves_the_pending_request(string contrast, WorldActionRejection rejection)
    {
        using var files = new StorageFiles();
        var fixture = new CareDispositionJournalFixture();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var pending = fixture.RequestAfterDefeat(authority);
        var acknowledgment = pending.Care!.Pending!;
        var intent = new CancelPendingCareIntent(contrast == "wrong-request" ? fixture.Journey.Commands.NextId() : acknowledgment.AcknowledgmentId,
            contrast == "wrong-episode" ? fixture.Journey.Commands.NextId() : acknowledgment.EncounterId,
            pending.Care.Revision + (contrast == "stale-care" ? 1UL : 0UL));
        var actor = contrast == "wrong-actor" ? fixture.Journey.Helper : fixture.Journey.Patient;
        var command = fixture.Journey.Commands.Command(pending, actor, intent);
        var decision = authority.Commit(fixture.Journey.Commands.Batch(pending, WorldBatchKind.Care, [command]));
        Assert.Equal(rejection, Assert.Single(decision.Receipts).Rejection);
        Assert.Equal(pending.Care.ContentHash, decision.Candidate.Care!.ContentHash);
        AssertUnchangedNonCare(pending, decision.Candidate);
        Assert.True(authority.IsIdentityOccupiedForHost(command.CommandId));
    }

    [Fact]
    public void Treatment_wins_over_the_original_cancellation_and_cannot_be_closed_twice()
    {
        using var files = new StorageFiles();
        var fixture = new CareDispositionJournalFixture();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var pending = fixture.RequestAfterDefeat(authority);
        var request = pending.Care!.Pending!;
        var cancellation = fixture.Journey.Commands.Command(pending, fixture.Journey.Patient,
            new CancelPendingCareIntent(request.AcknowledgmentId, request.EncounterId, pending.Care.Revision));
        var treated = fixture.Journey.Act(authority, fixture.Journey.Helper,
            Assert.IsType<AdministerRemedyIntent>(WorldCareActions.PrepareRemedy(pending)), WorldBatchKind.Care);
        Assert.Single(treated.Care!.Treatments);
        Assert.True(treated.Progression!.GetActor(fixture.Journey.Patient).CurrentHp > 0);
        var late = authority.Commit(fixture.Journey.Commands.Batch(treated, WorldBatchKind.Care, [cancellation]));
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(late.Receipts).Rejection);
        Assert.Equal(treated.Care.ContentHash, late.Candidate.Care!.ContentHash);
        Assert.Empty(late.Candidate.Care.Dispositions);
        AssertUnchangedNonCare(treated, late.Candidate);
    }

    private static void AssertUnchangedNonCare(WorldSnapshot before, WorldSnapshot after)
    {
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(before.Calendar, after.Calendar);
        Assert.Equal(before.AdvancingSteps, after.AdvancingSteps);
        Assert.Same(before.Space, after.Space);
        Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
        Assert.Equal(before.Progression!.ContentHash, after.Progression!.ContentHash);
        Assert.Equal(before.ResourceHistory!.ContentHash, after.ResourceHistory!.ContentHash);
        Assert.Equal(before.Pacing!.ContentHash, after.Pacing!.ContentHash);
        Assert.Equal(before.EncounterOutcome!.ContentHash, after.EncounterOutcome!.ContentHash);
        Assert.Equal(before.EncounterLifecycle!.Phase, after.EncounterLifecycle!.Phase);
        Assert.Equal(before.EncounterLifecycle.Debts, after.EncounterLifecycle.Debts);
        Assert.Equal(before.EncounterActions!.ContentHash, after.EncounterActions!.ContentHash);
        Assert.Same(before.Care!.Ordinary, after.Care!.Ordinary);
    }
}

internal sealed class CareDispositionJournalFixture
{
    internal CareDispositionJournalFixture(bool humanHelper = false, bool humanObserver = false)
    {
        Journey = new RecoveryJournalFixture(humanObserver);
        WorldSnapshot recovery = humanHelper ? HumanHelperRecovery() : Journey.Genesis;
        Genesis = WorldCareDispositionAdmission.CreateFresh(recovery);
    }

    internal RecoveryJournalFixture Journey { get; }
    internal WorldSnapshot Genesis { get; }

    private WorldSnapshot HumanHelperRecovery()
    {
        WorldSnapshot basis = Journey.Legacy;
        // A separately declared unplayed calibration, never replacement of a lived root or its controller owner.
        ContentHash admission = ContentHash.Compute(basis.ContentHash.ToByteArray().Concat(Journey.Helper.ToByteArray()).ToArray());
        var retreat = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle,
            care: basis.Care, resourceHistory: new WorldResourceHistory(admission, new ActorResourceHistory(basis.Progression!, 0, []), []),
            coordination: basis.Coordination, retreat: basis.Retreat);
        var outcome = WorldEncounterOutcomeAdmission.CreateCalibratedFresh(retreat,
            new EncounterOutcomeCalibration(new Dictionary<EntityId, int>(), [Journey.Helper]));
        var controllers = WorldControllerAdmission.CreateFresh(outcome);
        var definitions = Journey.Genesis.Care!.Ordinary!.Definitions
            .Where(value => value.DeliveryMode == RestorativeDeliveryMode.IncapacitatedAssistance).ToArray();
        var permissions = Journey.Genesis.Care.Ordinary.Permissions
            .Where(value => definitions.Any(definition => definition.DefinitionId == value.UseDefinitionId));
        return WorldRecoveryAdmission.CreateFresh(controllers, definitions, permissions);
    }

    internal WorldSnapshot RequestAfterDefeat(SqliteWorldAuthority authority)
    {
        var state = DefeatWithHelperNearby(authority);
        var agreement = Assert.Single(state.Care!.Agreements);
        var intent = new AcknowledgeCareIntent(agreement.AgreementId, state.EncounterActions!.Scope.EncounterId,
            state.EncounterLifecycle!.Revision, state.EncounterActions.ContentHash);
        Assert.Equal(WorldActionRejection.None, WorldCareActions.EvaluateAcknowledgment(state, Journey.Patient, intent));
        return Journey.Act(authority, Journey.Patient, intent, WorldBatchKind.Care);
    }

    internal WorldSnapshot DefeatWithHelperNearby(SqliteWorldAuthority authority)
    {
        var state = authority.ReadStateForHost();
        var scenario = state.EncounterLifecycle!.GetScenario(Journey.FirstScenario);
        Journey.WalkTo(authority, Journey.Patient, Journey.Beside(state, Journey.Patient, scenario.TargetActorId));
        state = authority.ReadStateForHost();
        Journey.WalkTo(authority, Journey.Helper, Journey.Beside(state, Journey.Helper, Journey.Patient));
        state = Journey.EnterAndFight(authority, Journey.FirstScenario, patientWaits: true);
        Assert.Equal(0, state.Progression!.GetActor(Journey.Patient).CurrentHp);
        return state;
    }
}
