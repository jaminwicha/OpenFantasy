using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care.DispositionContinuation;

public sealed class CareDispositionContinuationJournalTests(DispositionContinuationFixture fixture)
    : IClassFixture<DispositionContinuationFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pending_request_crosses_a_real_cut_then_cancel_or_decline_and_recut_reopen_exactly(bool decline)
    {
        using var files = fixture.CopyPending();
        WorldSnapshot final;
        WorldActionCommand terminalCommand;
        WorldActionReceipt terminalReceipt;
        WorldActionCommand refusedCommand;
        WorldActionReceipt refusedReceipt;
        CareDisposition terminal;
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis)))
        {
            var cut = fixture.CutPendingWithRefusedRemedy(authority, out refusedCommand);
            refusedReceipt = Assert.Single(cut.Receipts);
            var carried = cut.Candidate;
            Assert.Equal(1UL, carried.Care!.TotalAcknowledgments);
            Assert.Equal(0UL, carried.Care.TotalTreatments);
            Assert.Equal(0UL, carried.Care.TotalDispositions);
            Assert.Equal(0UL, carried.Care.Continuation!.ArchivedAcknowledgments);
            Assert.Equivalent(fixture.Request, Assert.Single(carried.Care.Acknowledgments), strict: true);
            Assert.Equivalent(fixture.Request, carried.Care.Continuation.Pending, strict: true);
            Assert.Empty(carried.Care.Continuation.PatientWitnesses);
            Assert.Empty(carried.Care.Continuation.ClosedDefeatWitnesses);

            EntityId actor = decline ? fixture.Journey.Helper : fixture.Journey.Patient;
            PendingCareDispositionIntent intent = decline
                ? new DeclinePendingCareIntent(fixture.Request.AcknowledgmentId, fixture.Request.EncounterId, carried.Care.Revision)
                : new CancelPendingCareIntent(fixture.Request.AcknowledgmentId, fixture.Request.EncounterId, carried.Care.Revision);
            terminalCommand = fixture.Journey.Commands.Command(carried, actor, intent);
            var closed = fixture.Journey.Commit(authority, WorldBatchKind.Care, [terminalCommand]);
            terminalReceipt = Assert.Single(closed.Receipts);
            terminal = Assert.Single(closed.Candidate.Care!.Dispositions);
            Assert.Null(closed.Candidate.Care.Pending);
            Assert.Null(closed.SealedMaterialCareSegment);
            Assert.Equal(carried.Revision + 1, terminal.WorldSequence);
            Assert.Equal(decline ? CareDispositionKind.HelperDeclined : CareDispositionKind.PatientCancelled, terminal.Kind);
            Assert.Equal(fixture.Request.AcknowledgmentId, terminal.AcknowledgmentId);
            Assert.Equal(actor, terminal.DecidingActorId);
            Assert.Equal(carried.Inventory.ContentHash, closed.Candidate.Inventory.ContentHash);
            Assert.Equal(carried.Production!.ContentHash, closed.Candidate.Production!.ContentHash);
            Assert.Equal(carried.Progression!.ContentHash, closed.Candidate.Progression!.ContentHash);
            Assert.Equal(carried.Calendar, closed.Candidate.Calendar);
            Assert.Equal(carried.AdvancingSteps, closed.Candidate.AdvancingSteps);
            Assert.Equal(carried.Pacing!.ContentHash, closed.Candidate.Pacing!.ContentHash);
            final = fixture.CompleteAndRecut(authority);
            AssertFinalClosure(final, fixture.Request, terminal, totalAcknowledgments: 1, totalTreatments: 0);
            Assert.Empty(final.Care!.Continuation!.PatientWitnesses);
            Assert.Equal(2UL, authority.InspectForHost().Journal.MaterialCareArchiveCount);
        }
        byte[] bytes = File.ReadAllBytes(files.Path);
        using (var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis)))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
            AssertFinalClosure(reopened.ReadStateForHost(), fixture.Request, terminal, 1, 0);
            AssertReceiptAndIdentities(reopened, fixture.RequestCommand, fixture.RequestReceipt, fixture.Request.AcknowledgmentId);
            AssertReceiptAndIdentities(reopened, refusedCommand, refusedReceipt);
            AssertReceiptAndIdentities(reopened, terminalCommand, terminalReceipt, terminal.DispositionId);
            var first = reopened.ReadMaterialCareArchiveForHost(0);
            Assert.Equal(WorldSnapshotCodec.Encode(fixture.Pending), first.Segment.Encode());
            Assert.Equivalent(fixture.Request, first.Segment.Continued.Care!.Pending, strict: true);
            var second = reopened.ReadMaterialCareArchiveForHost(1);
            AssertFinalClosure(second.Segment.Continued, fixture.Request, terminal, 1, 0);
            Assert.Equal(final.ContentHash, second.SourceHeadHash);
            Assert.Equal(final.Revision, second.SourceHeadRevision);
            Assert.InRange(WorldSnapshotCodec.Encode(final).Length, 1, WorldSnapshotCodec.MaximumStateByteLength);
            Assert.InRange(final.Care!.Continuation!.ToCommitment().Length, 1, CareCodecV5.MaximumContinuationByteLength);
        }
        Assert.Equal(bytes, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public void Later_disposition_archives_the_latest_closure_without_replacing_the_earlier_real_treatment()
    {
        using var files = fixture.CopyPending();
        WorldSnapshot final;
        CareTreatment treatment;
        CareAcknowledgment laterRequest;
        CareDisposition terminal;
        WorldActionCommand treatmentCommand;
        WorldActionReceipt treatmentReceipt;
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            var carried = fixture.CutPendingWithRefusedRemedy(authority, out _).Candidate;
            treatmentCommand = fixture.Journey.Commands.Command(carried, fixture.Journey.Helper,
                Assert.IsType<AdministerRemedyIntent>(WorldCareActions.PrepareRemedy(carried)));
            var treated = fixture.Journey.Commit(authority, WorldBatchKind.Care, [treatmentCommand]);
            treatmentReceipt = Assert.Single(treated.Receipts);
            treatment = Assert.Single(treated.Candidate.Care!.Treatments);
            Assert.Equal(40, treated.Candidate.Progression!.GetActor(fixture.Journey.Patient).CurrentHp);
            Assert.Equal(1UL, treated.Candidate.Production!.TotalConsumedItemCount);
            var defeatedAgain = fixture.FightToDefeat(authority);
            Assert.True(WorldActorAvailability.CanAct(defeatedAgain, fixture.Journey.Helper));
            EntityId helperStorage = defeatedAgain.GetResident(fixture.Journey.Helper).InventoryCustodianId;
            var remedyItem = Assert.Single(defeatedAgain.Care!.Remedies).Item;
            Assert.DoesNotContain(defeatedAgain.Inventory.Custody.Lots,
                value => value.CustodianId == helperStorage && value.Definition == remedyItem);
            Assert.Equal(1UL, Assert.Single(defeatedAgain.Production!.Lots,
                value => value.CustodianId == helperStorage && value.Definition == remedyItem).Quantity);
            fixture.Acknowledge(authority);
            var pending = authority.ReadStateForHost();
            laterRequest = Assert.IsType<CareAcknowledgment>(pending.Care!.Pending);
            Assert.NotEqual(fixture.Request.AcknowledgmentId, laterRequest.AcknowledgmentId);
            Assert.True(laterRequest.Defeat.WorldSequence > treatment.WorldSequence);
            var closed = fixture.Journey.Act(authority, fixture.Journey.Patient,
                new CancelPendingCareIntent(laterRequest.AcknowledgmentId, laterRequest.EncounterId, pending.Care.Revision), WorldBatchKind.Care);
            terminal = Assert.Single(closed.Care!.Dispositions);
            final = fixture.CompleteAndRecut(authority);
            AssertFinalClosure(final, laterRequest, terminal, totalAcknowledgments: 2, totalTreatments: 1);
            Assert.Equivalent(treatment, Assert.Single(final.Care!.Continuation!.PatientWitnesses).Treatment, strict: true);
            Assert.Equal(1UL, final.Production!.TotalConsumedItemCount);
            Assert.Equal(0, final.Progression!.GetActor(fixture.Journey.Patient).CurrentHp);
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        AssertFinalClosure(reopened.ReadStateForHost(), laterRequest, terminal, 2, 1);
        Assert.Equivalent(treatment, Assert.Single(reopened.ReadStateForHost().Care!.Continuation!.PatientWitnesses).Treatment, strict: true);
        AssertReceiptAndIdentities(reopened, treatmentCommand, treatmentReceipt, treatment.TreatmentId, treatment.ConsumptionId);
        Assert.True(reopened.IsIdentityOccupiedForHost(laterRequest.CommandId));
        Assert.True(reopened.IsIdentityOccupiedForHost(laterRequest.AcknowledgmentId));
        Assert.True(reopened.IsIdentityOccupiedForHost(terminal.CommandId));
        Assert.True(reopened.IsIdentityOccupiedForHost(terminal.DispositionId));
    }

    private static void AssertFinalClosure(WorldSnapshot state, CareAcknowledgment request, CareDisposition terminal,
        ulong totalAcknowledgments, ulong totalTreatments)
    {
        CareSnapshot care = state.Care!;
        Assert.Equal((ushort)38, state.ProfileVersion);
        Assert.Null(care.Pending);
        Assert.Empty(care.Acknowledgments);
        Assert.Empty(care.Dispositions);
        Assert.Empty(care.Treatments);
        Assert.Equal(totalAcknowledgments, care.TotalAcknowledgments);
        Assert.Equal(totalTreatments, care.TotalTreatments);
        Assert.Equal(1UL, care.TotalDispositions);
        Assert.Equal(totalAcknowledgments + totalTreatments + 1 + care.Ordinary!.Revision, care.Revision);
        Assert.Equal(totalAcknowledgments, care.Continuation!.ArchivedAcknowledgments);
        Assert.Equal(totalTreatments, care.Continuation.ArchivedTreatments);
        Assert.Equal(1UL, care.Continuation.ArchivedDispositions);
        var witness = Assert.Single(care.Continuation.ClosedDefeatWitnesses);
        Assert.Equivalent(request, witness.Acknowledgment, strict: true);
        Assert.Equivalent(terminal, witness.Disposition, strict: true);
        Assert.Null(witness.Treatment);
        Assert.Equal(terminal.WorldSequence, witness.TerminalSequence);
        Assert.False(care.CanAcknowledgeDefeat(Assert.IsType<EntityId>(request.Defeat.TargetActorId), request.Defeat.WorldSequence));
    }

    private static void AssertReceiptAndIdentities(SqliteWorldAuthority authority, WorldActionCommand command,
        WorldActionReceipt expected, params EntityId[] created)
    {
        Assert.Equal(WorldReceiptCodecV1.Encode(expected), WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(
            authority.FindReceipt(command, ConstructionUpgradeFixture.Delivery(authority.ReadStateForHost(), command.ActorId)))));
        Assert.True(authority.IsIdentityOccupiedForHost(command.CommandId));
        Assert.True(authority.IsIdentityOccupiedForHost(expected.ResultId));
        Assert.All(created, identity => Assert.True(authority.IsIdentityOccupiedForHost(identity)));
    }
}
