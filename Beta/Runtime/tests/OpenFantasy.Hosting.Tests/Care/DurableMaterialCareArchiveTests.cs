using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>Reuses one actual finite-supply journey across durable recovery adversaries.</summary>
public sealed class DurableMaterialCareArchiveTests(DurableMaterialCareJourney journey)
    : IClassFixture<DurableMaterialCareJourney>
{
    [Fact]
    public void Actual_finite_doses_cross_multiple_joint_cuts_and_reopen_with_exact_receipts()
    {
        using var files = journey.CopyFinal();
        byte[] databaseBytes = File.ReadAllBytes(files.Path);
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            var state = authority.ReadStateForHost();
            Assert.Equal(journey.FinalBytes, WorldSnapshotCodec.Encode(state));
            Assert.Equal((ushort)27, state.ProfileVersion);
            Assert.Equal(80UL, state.Inventory.Custody.GetLot(journey.PotionLot).Quantity);
            Assert.Equal(journey.InjuredHp + 520 * 40, state.Progression!.GetActor(journey.Player).CurrentHp);
            var usage = authority.InspectForHost().Journal;
            Assert.True(usage.MaterialCareArchiveCount >= 3);
            Assert.Equal(0UL, usage.ResourceArchiveCount);
            Assert.Equal(0UL, usage.ResourceArchiveBytes);
            Assert.True(usage.MaterialCareArchiveBytes > 0);
            for (ulong ordinal = 0; ordinal < usage.MaterialCareArchiveCount; ordinal++)
            {
                var actual = authority.ReadMaterialCareArchiveForHost(ordinal);
                Assert.Equal(ordinal, actual.Segment.Reference.Ordinal);
                Assert.Equal(actual.Segment.Reference.BoundarySequence + 1, actual.PublicationSequence);
                Assert.Equal(state.Revision, actual.SourceHeadRevision);
                Assert.Equal(state.ContentHash, actual.SourceHeadHash);
            }
            var command = Assert.Single(journey.CutBatch.Actions);
            var receipt = Assert.IsType<WorldActionReceipt>(authority.FindReceipt(command,
                new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId)));
            Assert.Equal(journey.CutReceipt, WorldReceiptCodecV1.Encode(receipt));
            Assert.True(authority.IsIdentityOccupiedForHost(command.CommandId));
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(journey.CutBatch));
            Assert.Equal(journey.FinalBytes, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            Assert.Equal(journey.CutBefore.ContentHash,
                authority.ReadRecordedStateForHost(journey.CutBefore.Revision).StateHash);
        }
        Assert.Equal(databaseBytes, File.ReadAllBytes(files.Path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_cut_receipt_identity_archive_and_head_commit_or_roll_back_together(bool afterCommit)
    {
        using var files = journey.CopyBeforeCut();
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Throws<IOException>(() => authority.Commit(journey.CutBatch, boundary =>
            {
                if (boundary == (afterCommit ? AuthorityCommitBoundary.AfterCommit : AuthorityCommitBoundary.BeforeCommit))
                    throw new IOException("Injected loss at the coordinated publication boundary.");
            }));
            Assert.Throws<InvalidOperationException>(() => authority.ReadStateForHost());
        }
        using var recovered = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(afterCommit ? journey.CutAfterBytes : WorldSnapshotCodec.Encode(journey.CutBefore),
            WorldSnapshotCodec.Encode(recovered.ReadStateForHost()));
        Assert.Equal(journey.CutOrdinal + (afterCommit ? 1UL : 0UL),
            recovered.InspectForHost().Journal.MaterialCareArchiveCount);
        var command = Assert.Single(journey.CutBatch.Actions);
        Assert.Equal(afterCommit, recovered.IsIdentityOccupiedForHost(command.CommandId));
        var receipt = recovered.FindReceipt(command,
            new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId));
        if (afterCommit)
        {
            Assert.NotNull(receipt);
            Assert.Equal(journey.CutReceipt, WorldReceiptCodecV1.Encode(receipt));
            Assert.Throws<AuthorityDispatchException>(() => recovered.Commit(journey.CutBatch));
        }
        else
        {
            Assert.Null(receipt);
            Assert.Equal(journey.CutAfterBytes, WorldSnapshotCodec.Encode(recovered.Commit(journey.CutBatch).Candidate));
        }
    }

    [Theory]
    [InlineData("DELETE FROM world_material_care_segments WHERE ordinal=(SELECT MAX(ordinal) FROM world_material_care_segments)")]
    [InlineData("UPDATE world_material_care_segments SET state=zeroblob(length(state)) WHERE ordinal=(SELECT MAX(ordinal) FROM world_material_care_segments)")]
    [InlineData("UPDATE world_material_care_segments SET boundary_hash=zeroblob(32) WHERE ordinal=(SELECT MAX(ordinal) FROM world_material_care_segments)")]
    [InlineData("UPDATE world_material_care_segments SET world_id=zeroblob(16) WHERE ordinal=(SELECT MAX(ordinal) FROM world_material_care_segments)")]
    [InlineData("UPDATE world_material_care_segments SET ordinal=x'000000000000ffff' WHERE ordinal=(SELECT MAX(ordinal) FROM world_material_care_segments)")]
    public void Later_archive_damage_blocks_recovery_and_selection_of_an_earlier_archive(string mutation)
    {
        using var files = journey.CopyFinal();
        files.Execute(mutation);
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path));
        using var database = WorldDatabase.Open(files.Path, create: false);
        Assert.Throws<InvalidDataException>(() => new WorldJournalRecovery().ReadMaterialCareArchive(database, 0));
        Assert.Throws<InvalidDataException>(() => new WorldJournalRecovery().ReadRecordedState(database, 1));
    }
}

/// <summary>All damage and doses come from accepted world actions; only fresh fixture statistics differ.</summary>
public sealed class DurableMaterialCareJourney : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "openfantasy-joint-" + Guid.NewGuid().ToString("N"));
    private string FinalPath => Path.Combine(_directory, "final.sqlite3");
    private string BeforePath => Path.Combine(_directory, "before.sqlite3");
    private string AfterPath => Path.Combine(_directory, "after.sqlite3");

    public DurableMaterialCareJourney()
    {
        Directory.CreateDirectory(_directory);
        var fixture = new OrdinaryCareArchiveFixture(materialCareStress: true);
        Player = fixture.Journey.Player;
        PotionLot = fixture.Journey.PotionLot;
        var genesis = WorldCoordinatedAdmission.CreateFresh(fixture.Genesis);
        using var authority = SqliteWorldAuthority.CreateNew(Path.Combine(_directory, "working.sqlite3"), genesis);
        var session = new CareWorldSession(authority);
        var state = new PairedRewardBoundaryJourney(fixture.Journey).Prepare(session);
        var close = state.Dialogues.Select(dialogue => fixture.Journey.Command(state, dialogue.ListenerActorId,
            new CloseDialogueIntent(dialogue.DialogueId))).ToArray();
        state = CareWorldFixture.Commit(session, fixture.Journey.Batch(state, WorldBatchKind.HeldInteraction, close)).Candidate;
        InjuredHp = state.Progression!.GetActor(Player).CurrentHp;
        Assert.True(state.Progression.GetActor(Player).Stats.MaxHp - InjuredHp >= 520 * 40);
        Assert.Equal(600UL, state.Inventory.Custody.GetLot(PotionLot).Quantity);
        for (int dose = 0; dose < 520; dose++)
        {
            var before = state;
            var command = fixture.Journey.Command(before, fixture.Journey.Helper, fixture.Aid(before));
            var batch = fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep, [command]);
            var preview = authority.Preview(batch);
            var receipt = Assert.Single(preview.Receipts);
            Assert.True(receipt.Status == CommandReceiptStatus.Applied,
                $"Dose {dose}, world {before.Revision}, active uses {before.Care!.Ordinary!.Uses.Count}, "
                + $"joint cut {preview.SealedMaterialCareSegment is not null}: {receipt.Rejection}");
            Assert.Equal(before.ContentHash, authority.ReadStateForHost().ContentHash);
            // Capture a later cut after the material stream has exceeded its original finite window,
            // rather than only the early resource-pressure boundary from combat preparation.
            bool capture = CutBatch is null && before.Care!.TotalOrdinaryUses >= 256
                && preview.SealedMaterialCareSegment is not null;
            if (capture)
            {
                authority.BackupForHost(BeforePath);
                CutBefore = before;
                CutBatch = batch;
                CutOrdinal = preview.SealedMaterialCareSegment!.Reference.Ordinal;
                CutAfterBytes = WorldSnapshotCodec.Encode(preview.Candidate);
                CutReceipt = WorldReceiptCodecV1.Encode(Assert.Single(preview.Receipts));
            }
            var accepted = CareWorldFixture.Commit(session, batch);
            Assert.Equal(preview.Candidate.ContentHash, accepted.Candidate.ContentHash);
            if (accepted.SealedMaterialCareSegment is { } archive)
            {
                Assert.Equal(WorldSnapshotCodec.Encode(before), archive.Encode());
                Assert.Equal(before.Revision, archive.Reference.BoundarySequence);
            }
            state = accepted.Candidate;
            if (capture) authority.BackupForHost(AfterPath);
            Assert.Equal(InjuredHp + (dose + 1) * 40, state.Progression!.GetActor(Player).CurrentHp);
        }
        Assert.NotNull(CutBatch);
        FinalBytes = WorldSnapshotCodec.Encode(state);
        authority.BackupForHost(FinalPath);
        Export();
    }

    internal EntityId Player { get; }
    internal EntityId PotionLot { get; }
    internal int InjuredHp { get; }
    internal WorldSnapshot CutBefore { get; private set; } = null!;
    internal WorldStepBatch CutBatch { get; private set; } = null!;
    internal ulong CutOrdinal { get; private set; }
    internal byte[] CutAfterBytes { get; private set; } = [];
    internal byte[] CutReceipt { get; private set; } = [];
    internal byte[] FinalBytes { get; }

    internal StorageFiles CopyFinal() => Copy(FinalPath);
    internal StorageFiles CopyBeforeCut() => Copy(BeforePath);

    private static StorageFiles Copy(string path)
    {
        var files = new StorageFiles();
        File.Copy(path, files.Path);
        return files;
    }

    private void Export()
    {
        if (Environment.GetEnvironmentVariable("OPENFANTASY_COORDINATED_EVIDENCE") is not { Length: > 0 } root) return;
        Directory.CreateDirectory(root);
        File.Copy(BeforePath, Path.Combine(root, "before.sqlite3"), overwrite: true);
        File.Copy(FinalPath, Path.Combine(root, "final.sqlite3"), overwrite: true);
        File.Copy(AfterPath, Path.Combine(root, "after.sqlite3"), overwrite: true);
        File.WriteAllBytes(Path.Combine(root, "before.ofc"), WorldSnapshotCodec.Encode(CutBefore));
        File.WriteAllBytes(Path.Combine(root, "batch.ofc"), WorldStepCodec.Encode(CutBatch));
        File.WriteAllBytes(Path.Combine(root, "after.ofc"), CutAfterBytes);
        File.WriteAllBytes(Path.Combine(root, "final.ofc"), FinalBytes);
        File.WriteAllText(Path.Combine(root, "metadata.json"), JsonSerializer.Serialize(new
        {
            actualDoses = 520, freshPotionSupply = 600, remainingPotions = 80,
            profileVersion = 27, beforeRevision = CutBefore.Revision,
            beforeHash = CutBefore.ContentHash.Value, cutOrdinal = CutOrdinal,
            note = "Explicit fresh stress statistics; damage and all restoration earned by actual accepted actions."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
