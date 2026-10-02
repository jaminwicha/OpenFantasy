using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care.DispositionCapacity;

public sealed class CareDispositionPressureJournalTests(ITestOutputHelper output)
{
    [Fact]
    public void Repeated_real_dispositions_reach_resource_capacity_before_the_disposition_limit_and_reopen_exactly()
    {
        using var files = new StorageFiles();
        var journey = new DispositionPressureJourney();
        WorldSnapshot closing;
        WorldSnapshot final;
        WorldStepBatch? cutBatch = null;
        WorldActionReceipt? cutReceipt = null;
        const int cycles = ActorResourceHistory.MaximumBatches / 2;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, journey.Genesis))
        {
            Assert.Equal(129UL, journey.HelperDoses(journey.Genesis));
            journey.Approach(authority);
            for (int cycle = 0; cycle < cycles; cycle++)
            {
                journey.DefeatAndClose(authority, decline: cycle % 2 != 0);
                var recovery = journey.PrepareRecovery(authority);
                var before = authority.ReadStateForHost();
                Assert.Equal((ulong)(cycle + 1), before.Care!.TotalDispositions);
                Assert.Equal((ulong)cycle, before.Care.TotalOrdinaryUses);
                Assert.Equal(0UL, before.Care.TotalTreatments);
                Assert.Equal(2 * cycle + 1, before.ResourceHistory!.History.Batches.Count);
                Assert.Equal(0UL, authority.InspectForHost().Journal.MaterialCareArchiveCount);
                if (cycle == cycles - 1)
                {
                    cutBatch = recovery;
                    closing = before;
                    var preview = authority.Preview(cutBatch);
                    Assert.Equal(WorldSnapshotCodec.Encode(closing),
                        Assert.IsType<WorldMaterialCareArchiveSegment>(preview.SealedMaterialCareSegment).Encode());
                    Assert.Equal(closing.ContentHash, authority.ReadStateForHost().ContentHash);
                    var cut = authority.Commit(cutBatch);
                    cutReceipt = Assert.Single(cut.Receipts);
                    Assert.Equal(WorldActionRejection.None, cutReceipt.Rejection);
                    final = cut.Candidate;
                    Assert.Equal(preview.Candidate.ContentHash, final.ContentHash);
                    Assert.Equal(WorldSnapshotCodec.Encode(closing), cut.SealedMaterialCareSegment!.Encode());
                    Assert.Equal(1UL, authority.InspectForHost().Journal.MaterialCareArchiveCount);
                    VerifyBoundary(closing, final, journey, cycles);
                    break;
                }
                var restored = authority.Commit(recovery);
                Assert.Equal(WorldActionRejection.None, Assert.Single(restored.Receipts).Rejection);
                Assert.Null(restored.SealedMaterialCareSegment);
                Assert.Equal(1, restored.Candidate.Progression!.GetActor(journey.Patient).CurrentHp);
            }
            final = authority.ReadStateForHost();
        }
        byte[] saved = File.ReadAllBytes(files.Path);
        using (var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(journey.Genesis)))
        {
            var restored = reopened.ReadStateForHost();
            Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(restored));
            var archive = reopened.ReadMaterialCareArchiveForHost(0);
            closing = reopened.ReadRecordedStateForHost(archive.Segment.Reference.BoundarySequence).State;
            VerifyBoundary(closing, restored, journey, cycles);
            Assert.Equal(WorldSnapshotCodec.Encode(closing), archive.Segment.Encode());
            Assert.Equal(final.ContentHash, archive.SourceHeadHash);
            Assert.Equal(final.Revision, archive.SourceHeadRevision);
            byte[] continued = WorldSnapshotCodecV38.Encode(restored);
            Assert.Equal(continued, WorldSnapshotCodecV38.Encode(WorldSnapshotCodecV38.Decode(continued, archive.Segment.Anchor)));
            Assert.InRange(continued.Length, 1, WorldSnapshotCodec.MaximumStateByteLength);
            Assert.InRange(CareCodecV5.Encode(restored.Care!).Length, 1, CareCodecV5.MaximumEncodedByteLength);
            Assert.InRange(restored.Care!.Continuation!.ToCommitment().Length, 1, CareCodecV5.MaximumContinuationByteLength);
            AssertReceipt(reopened, journey.FirstRequest!, journey.FirstRequestReceipt!);
            AssertReceipt(reopened, journey.FirstDisposition!, journey.FirstDispositionReceipt!);
            AssertReceipt(reopened, Assert.Single(Assert.IsType<WorldStepBatch>(cutBatch).Actions),
                Assert.IsType<WorldActionReceipt>(cutReceipt));
            Assert.True(reopened.IsIdentityOccupiedForHost(EntityId.DeriveSpawned(journey.FirstRequest!.CommandId, 0)));
            Assert.True(reopened.IsIdentityOccupiedForHost(EntityId.DeriveSpawned(journey.FirstDisposition!.CommandId, 0)));
            Assert.True(reopened.IsIdentityOccupiedForHost(journey.LatestRequest!.AcknowledgmentId));
            Assert.True(reopened.IsIdentityOccupiedForHost(journey.LatestDisposition!.DispositionId));
            output.WriteLine($"Actual pressure: dispositions={cycles}, closing resource batches={closing.ResourceHistory!.History.Batches.Count}, "
                + $"closing frame bytes={WorldSnapshotCodec.Encode(closing).Length}, continued frame bytes={continued.Length}, "
                + $"care continuation bytes={restored.Care.Continuation.ToCommitment().Length}. This is not a maximum whole-world frame.");
        }
        Assert.Equal(saved, File.ReadAllBytes(files.Path));
    }

    private static void VerifyBoundary(WorldSnapshot closing, WorldSnapshot final, DispositionPressureJourney journey, int cycles)
    {
        Assert.Equal(ActorResourceHistory.MaximumBatches - 1, closing.ResourceHistory!.History.Batches.Count);
        Assert.Equal(cycles, closing.Care!.Dispositions.Count);
        Assert.True(closing.Care.Dispositions.Count < CareSnapshot.MaximumDispositions);
        Assert.Equal(cycles, closing.Care.Acknowledgments.Count);
        Assert.Equal(cycles / 2, closing.Care.Dispositions.Count(value => value.Kind == CareDispositionKind.PatientCancelled));
        Assert.Equal(cycles / 2, closing.Care.Dispositions.Count(value => value.Kind == CareDispositionKind.HelperDeclined));
        Assert.Empty(closing.Care.Treatments);
        Assert.Equal((ulong)cycles, final.Care!.TotalAcknowledgments);
        Assert.Equal((ulong)cycles, final.Care.TotalDispositions);
        Assert.Equal((ulong)cycles, final.Care.TotalOrdinaryUses);
        Assert.Equal(0UL, final.Care.TotalTreatments);
        Assert.Equal((ulong)(cycles * 3), final.Care.Revision);
        Assert.Empty(final.Care.Acknowledgments);
        Assert.Empty(final.Care.Dispositions);
        Assert.Empty(final.Care.Continuation!.PatientWitnesses);
        Assert.Equal((ulong)cycles, final.Care.Continuation.ArchivedDispositions);
        Assert.Equal((ulong)(cycles - 1), final.Care.Continuation.ArchivedOrdinaryUses);
        Assert.Equivalent(journey.LatestRequest, Assert.Single(final.Care.Continuation.ClosedDefeatWitnesses).Acknowledgment, strict: true);
        Assert.Equivalent(journey.LatestDisposition, Assert.Single(final.Care.Continuation.ClosedDefeatWitnesses).Disposition, strict: true);
        Assert.Equal(1, final.Progression!.GetActor(journey.Patient).CurrentHp);
        Assert.Equal(1UL, journey.HelperDoses(final));
        Assert.Equal((ulong)cycles, final.Production!.TotalConsumedItemCount);
        Assert.Single(final.ResourceHistory!.History.Batches);
        Assert.Single(final.Care.Ordinary!.Uses);
        Assert.Equal(closing.Revision, final.ResourceHistory.History.BasisWorldSequence);
        Assert.Equal(closing.Progression!.Revision + 1, final.Progression.Revision);
    }

    private static void AssertReceipt(SqliteWorldAuthority authority, WorldActionCommand command, WorldActionReceipt receipt)
    {
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(
            authority.FindReceipt(command, ConstructionUpgradeFixture.Delivery(authority.ReadStateForHost(), command.ActorId)))));
        Assert.True(authority.IsIdentityOccupiedForHost(command.CommandId));
        Assert.True(authority.IsIdentityOccupiedForHost(receipt.ResultId));
    }
}
