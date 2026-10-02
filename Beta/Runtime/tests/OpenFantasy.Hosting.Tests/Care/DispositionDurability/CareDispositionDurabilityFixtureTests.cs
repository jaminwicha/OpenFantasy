using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Care.DispositionContinuation;
using OpenFantasy.Hosting.Tests.Care.Dispositions;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care.DispositionDurability;

/// <summary>Exports real transactions and checks rollback/lost-reply boundaries before external process interruption.</summary>
public sealed class CareDispositionDurabilityFixtureTests
{
    [Fact]
    public void Closure_and_pending_archive_publications_recover_exactly_at_both_transaction_boundaries()
    {
        string? requested = Environment.GetEnvironmentVariable("OPENFANTASY_CARE_DISPOSITION_DURABILITY_FIXTURES");
        Assert.SkipWhen(requested is null, "A fresh durability fixture directory is required.");
        string root = Path.GetFullPath(requested);
        Assert.False(Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any());
        Directory.CreateDirectory(root);
        var ordinary = new CareDispositionJournalFixture(humanHelper: true);
        using var pending = new StorageFiles();
        using (var authority = SqliteWorldAuthority.CreateNew(pending.Path, ordinary.Genesis))
            ordinary.RequestAfterDefeat(authority);
        using var pressure = new DispositionContinuationFixture();
        File.WriteAllBytes(Path.Combine(root, "ordinary.genesis.ofc"), WorldSnapshotCodec.Encode(ordinary.Genesis));
        File.WriteAllBytes(Path.Combine(root, "pressure.genesis.ofc"), WorldSnapshotCodec.Encode(pressure.Genesis));
        List<object> cases = [];

        foreach (bool decline in new[] { false, true })
        {
            using var files = new StorageFiles();
            File.Copy(pending.Path, files.Path);
            using var authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(ordinary.Genesis));
            Export(decline ? "decline" : "cancel", "ordinary", authority,
                () => Close(authority, ordinary.Journey.Commands, ordinary.Journey.Patient, ordinary.Journey.Helper, decline));
        }
        using (var files = pressure.CopyPending())
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(pressure.Genesis)))
            Export("cut-pending", "pressure", authority, () => pressure.CutPendingWithRefusedRemedy(authority, out _));
        foreach (bool decline in new[] { false, true })
        {
            using var files = pressure.CopyPending();
            using var authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(pressure.Genesis));
            pressure.CutPendingWithRefusedRemedy(authority, out _);
            Export(decline ? "archived-decline" : "archived-cancel", "pressure", authority,
                () => Close(authority, pressure.Journey.Commands, pressure.Journey.Patient, pressure.Journey.Helper, decline));
        }
        File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(new
        {
            Kind = "care-disposition-durability-fixtures-v1", ProfileVersion = 38, Cases = cases,
            TransactionFaults = 10,
            Calibration = "Fresh declared human-helper care and finite 255 one-HP-bite resource pressure; no lived-save rewrite."
        }, new JsonSerializerOptions { WriteIndented = true }));

        void Export(string label, string genesisKind, SqliteWorldAuthority authority, Func<WorldStepDecision> publish)
        {
            WorldSnapshot before = authority.ReadStateForHost();
            Assert.NotNull(before.Care!.Pending);
            string beforePath = Path.Combine(root, label + ".before.sqlite");
            string afterPath = Path.Combine(root, label + ".after.sqlite");
            authority.BackupForHost(beforePath);
            WorldStepDecision decision = publish();
            WorldSnapshot after = decision.Candidate;
            authority.BackupForHost(afterPath);
            WorldStepBatch batch;
            using (var database = WorldDatabase.Open(afterPath, create: false))
                batch = WorldStepCodec.Decode(database.ReadJournal().Last().AcceptedBatch);
            Assert.Single(batch.Actions);
            Assert.Empty(batch.Movement);
            Assert.Equal(before.Calendar, after.Calendar);
            Assert.Equal(before.AdvancingSteps, after.AdvancingSteps);
            Assert.Equal(before.Progression!.ContentHash, after.Progression!.ContentHash);
            Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
            Assert.Equal(before.Pacing!.ContentHash, after.Pacing!.ContentHash);
            Assert.Equal(before.EncounterOutcome!.ContentHash, after.EncounterOutcome!.ContentHash);
            Assert.Equal(before.Care.TotalTreatments, after.Care!.TotalTreatments);
            Assert.Equal(before.Production!.TotalConsumedItemCount, after.Production!.TotalConsumedItemCount);
            var receipt = Assert.Single(decision.Receipts);
            if (label == "cut-pending")
            {
                Assert.NotNull(decision.SealedMaterialCareSegment);
                Assert.Equal(WorldActionRejection.RevisionConflict, receipt.Rejection);
                Assert.Equivalent(before.Care.Pending, after.Care.Pending, strict: true);
            }
            else
            {
                Assert.Null(decision.SealedMaterialCareSegment);
                Assert.Equal(WorldActionRejection.None, receipt.Rejection);
                Assert.Null(after.Care.Pending);
                CareDisposition closure = Assert.Single(after.Care.Dispositions);
                Assert.Equal(label.EndsWith("decline", StringComparison.Ordinal)
                    ? CareDispositionKind.HelperDeclined : CareDispositionKind.PatientCancelled, closure.Kind);
                Assert.Equal(before.Care.TotalDispositions + 1, after.Care.TotalDispositions);
            }
            byte[] genesis = File.ReadAllBytes(Path.Combine(root, genesisKind + ".genesis.ofc"));
            foreach (AuthorityCommitBoundary boundary in Enum.GetValues<AuthorityCommitBoundary>())
            {
                using var interrupted = new StorageFiles();
                File.Copy(beforePath, interrupted.Path);
                using (var crashing = SqliteWorldAuthority.OpenExisting(interrupted.Path, genesis))
                {
                    Assert.Throws<IOException>(() => crashing.Commit(batch, at =>
                    {
                        if (at == boundary) throw new IOException("Declared transaction interruption.");
                    }));
                    Assert.Throws<InvalidOperationException>(() => crashing.InspectForHost());
                    Assert.Throws<InvalidOperationException>(() => crashing.Commit(batch));
                }
                bool committed = boundary == AuthorityCommitBoundary.AfterCommit;
                RequireSame(committed ? afterPath : beforePath, interrupted.Path);
                using (var recovered = SqliteWorldAuthority.OpenExisting(interrupted.Path, genesis))
                {
                    Assert.Equal(committed ? after.ContentHash : before.ContentHash, recovered.ReadStateForHost().ContentHash);
                    if (!committed) Assert.Equal(after.ContentHash, recovered.Commit(batch).Candidate.ContentHash);
                    var command = Assert.Single(batch.Actions);
                    Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(
                        recovered.FindReceipt(command, ConstructionUpgradeFixture.Delivery(recovered.ReadStateForHost(), command.ActorId)))));
                    Assert.True(recovered.IsIdentityOccupiedForHost(command.CommandId));
                    Assert.True(recovered.IsIdentityOccupiedForHost(receipt.ResultId));
                }
                RequireSame(afterPath, interrupted.Path);
            }
            File.WriteAllBytes(Path.Combine(root, label + ".step.bin"), WorldStepCodec.Encode(batch));
            cases.Add(new
            {
                Label = label, Sequence = after.Revision, Kind = batch.Kind.ToString(), GenesisKind = genesisKind,
                Genesis = genesisKind + ".genesis.ofc", Before = label + ".before.sqlite", After = label + ".after.sqlite",
                Step = label + ".step.bin", BeforeHash = before.ContentHash.Value, AfterHash = after.ContentHash.Value,
                Receipt = Convert.ToHexString(WorldReceiptCodecV1.Encode(receipt)),
                ArchiveCount = authority.InspectForHost().Journal.MaterialCareArchiveCount
            });
        }
    }

    private static WorldStepDecision Close(SqliteWorldAuthority authority,
        EncounterJourneyWorldFixture commands, EntityId patient, EntityId helper, bool decline)
    {
        WorldSnapshot state = authority.ReadStateForHost();
        var pending = Assert.IsType<CareAcknowledgment>(state.Care!.Pending);
        PendingCareDispositionIntent intent = decline
            ? new DeclinePendingCareIntent(pending.AcknowledgmentId, pending.EncounterId, state.Care.Revision)
            : new CancelPendingCareIntent(pending.AcknowledgmentId, pending.EncounterId, state.Care.Revision);
        return authority.Commit(commands.Batch(state, WorldBatchKind.Care,
            [commands.Command(state, decline ? helper : patient, intent)]));
    }

    internal static void RequireSame(string expected, string actual)
    {
        using var left = WorldDatabase.Open(expected, create: false);
        using var right = WorldDatabase.Open(actual, create: false);
        WorldBackupComparison.RequireExactCopy(left, right);
    }
}
