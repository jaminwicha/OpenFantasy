using System.Buffers.Binary;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed class DurableResourceArchiveTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Actual_care_reopens_at_defeat_acknowledgment_treatment_and_resumed_attack(bool crafted)
    {
        using var files = new StorageFiles();
        var fixture = new CareWorldFixture(crafted);
        WorldSnapshot defeated;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            defeated = fixture.PrepareDefeat(new(authority));
        }

        var defeat = defeated.EncounterActions!.LastResolution!;
        Assert.Equal(0U, defeat.TargetHpAfter);
        WorldActionCommand acknowledgment;
        WorldActionReceipt acknowledgmentReceipt;
        WorldSnapshot acknowledged;
        using (var authority = ReopenExact(files.Path, defeated))
        {
            acknowledgment = fixture.Acknowledge(defeated);
            var batch = fixture.Batch(defeated, WorldBatchKind.Care, [acknowledgment]);
            var before = WorldSnapshotCodec.Encode(defeated);
            var preview = authority.Preview(batch);
            Assert.NotNull(preview.Candidate.Care!.Pending);
            Assert.Equal(before, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            var accepted = authority.Commit(batch);
            acknowledged = accepted.Candidate;
            acknowledgmentReceipt = Assert.Single(accepted.Receipts);
            Assert.Equal(CommandReceiptStatus.Applied, acknowledgmentReceipt.Status);
            Assert.Equal(preview.Candidate.ContentHash, acknowledged.ContentHash);
            Assert.Equal(defeated.Progression!.ContentHash, acknowledged.Progression!.ContentHash);
            Assert.Equal(defeated.Production!.ContentHash, acknowledged.Production!.ContentHash);
            Assert.Equal(defeated.Calendar, acknowledged.Calendar);
            Assert.Equal(defeated.EncounterLifecycle!.TacticalTicks, acknowledged.EncounterLifecycle!.TacticalTicks);
        }

        WorldActionCommand administer;
        WorldStepBatch treatmentBatch;
        WorldActionReceipt treatmentReceipt;
        WorldSnapshot treated;
        using (var authority = ReopenExact(files.Path, acknowledged))
        {
            AssertReceipt(authority, acknowledgment, fixture, acknowledgmentReceipt);
            Assert.NotNull(authority.ReadStateForHost().Care!.Pending);
            Assert.Empty(authority.ReadStateForHost().Care!.Treatments);
            Assert.Empty(authority.ReadStateForHost().Production!.ConsumedItems);
            administer = fixture.Administer(acknowledged);
            treatmentBatch = fixture.Batch(acknowledged, WorldBatchKind.Care, [administer]);
            var preview = authority.Preview(treatmentBatch);
            Assert.Equal(acknowledged.ContentHash, authority.ReadStateForHost().ContentHash);
            ExportCrashFixture(authority, crafted ? "crafted-care" : "raw-care", acknowledged, treatmentBatch, preview);
            var accepted = authority.Commit(treatmentBatch);
            ExportAcceptedDatabase(authority, crafted ? "crafted-care" : "raw-care");
            treated = accepted.Candidate;
            treatmentReceipt = Assert.Single(accepted.Receipts);
            Assert.Equal(CommandReceiptStatus.Applied, treatmentReceipt.Status);
            Assert.Equal(preview.Candidate.ContentHash, treated.ContentHash);
            Assert.Equal(40, treated.Progression!.GetActor(fixture.Player).CurrentHp);
            Assert.Null(treated.Care!.Pending);
            Assert.Single(treated.Care.Treatments);
            var consumed = Assert.Single(treated.Production!.ConsumedItems);
            Assert.Equal(administer.CommandId, consumed.CommandId);
            Assert.Equal(1UL, consumed.Ingredient.Quantity);
            Assert.Equal(defeat.TargetHpAfter, treated.EncounterActions!.LastResolution!.TargetHpAfter);
            Assert.Equal(EncounterLifecyclePhase.Active, treated.EncounterLifecycle!.Phase);
            Assert.True(treated.Pacing!.Encounter!.GetActor(fixture.Helper).RemainingDelay > 0);
            Assert.Equal(acknowledged.Calendar, treated.Calendar);
            Assert.Equal(acknowledged.EncounterLifecycle!.TacticalTicks, treated.EncounterLifecycle.TacticalTicks);
        }

        WorldSnapshot attacked;
        using (var authority = ReopenExact(files.Path, treated))
        {
            AssertReceipt(authority, acknowledgment, fixture, acknowledgmentReceipt);
            AssertReceipt(authority, administer, fixture, treatmentReceipt);
            var beforeRetry = WorldSnapshotCodec.Encode(authority.ReadStateForHost());
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(treatmentBatch));
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(
                fixture.Batch(treated, WorldBatchKind.Care, [administer])));
            Assert.Equal(beforeRetry, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            Assert.Single(authority.ReadStateForHost().Production!.ConsumedItems);
            var session = new CareWorldSession(authority);
            attacked = treated;
            ulong resourceRevision = treated.Progression!.Revision;
            for (int step = 0; attacked.Progression!.Revision == resourceRevision; step++)
            {
                Assert.True(step < 128, "Resumed damage must occur within the existing readiness bound.");
                attacked = fixture.NextCombatBoundary(session, playerAttacks: true);
            }
            Assert.Equal(treated.Care!.ContentHash, attacked.Care!.ContentHash);
            Assert.Equal(treated.Production!.ContentHash, attacked.Production!.ContentHash);
            Assert.IsType<WorldEncounterResourceEvidence>(attacked.ResourceHistory!.Evidence[^1]);
        }
        using (var authority = ReopenExact(files.Path, attacked))
        {
            AssertReceipt(authority, administer, fixture, treatmentReceipt);
            Assert.Equal(treated.ContentHash, authority.ReadRecordedStateForHost(treated.Revision).StateHash);
            Assert.Equal(acknowledged.ContentHash, authority.ReadRecordedStateForHost(acknowledged.Revision).StateHash);
            Assert.Equal(defeated.ContentHash, authority.ReadRecordedStateForHost(defeated.Revision).StateHash);
        }
        using var database = WorldDatabase.Open(files.Path, create: false);
        Assert.Equal(7, database.SchemaVersion);
        Assert.Equal(2, database.ReadJournal().Count(row => row.Kind == WorldCommitKind.Care));
    }

    [Fact]
    public void Actual_520_resource_changes_publish_two_archives_and_refuse_closed_save_corruption()
    {
        var fixture = new CareWorldFixture(rewardExperience: 1000, strikeMana: 1, enduranceProfile: true);
        var expected = new CareWorldSession(fixture.Genesis);
        var rewarded = fixture.CompleteQuest(expected);
        var close = fixture.Command(rewarded, fixture.Player,
            new CloseDialogueIntent(rewarded.Dialogues.Single().DialogueId));
        CareWorldFixture.Commit(expected, fixture.Batch(rewarded, WorldBatchKind.HeldInteraction, [close]));
        var final = fixture.BeginCombat(expected);
        for (int step = 0; final.Progression!.Revision < 520; step++)
        {
            Assert.True(step < 4000, "The existing actual attack/readiness journey must stay bounded.");
            final = fixture.NextCombatBoundary(expected, playerAttacks: true);
        }
        Assert.Equal(2, expected.Archives.Count);
        Assert.Equal(8, final.ResourceHistory!.History.Batches.Count);

        using var files = new StorageFiles();
        WorldStepBatch? publicationBatch = null;
        WorldSnapshot? beforePublication = null;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            int archives = 0;
            foreach (var batch in expected.Batches)
            {
                var before = authority.ReadStateForHost();
                var seal = expected.Archives.SingleOrDefault(value => value.Reference.BoundarySequence == before.Revision);
                if (seal is not null)
                {
                    var preview = authority.Preview(batch);
                    Assert.Equal(seal.Encode(), Assert.IsType<WorldResourceArchiveSegment>(preview.SealedResourceSegment).Encode());
                    Assert.Equal(before.ContentHash, authority.ReadStateForHost().ContentHash);
                    Assert.Equal((ulong)archives, authority.InspectForHost().Journal.ResourceArchiveCount);
                    if (archives == 0) ExportCrashFixture(authority, "archive", before, batch, preview);
                    publicationBatch = batch;
                    beforePublication = before;
                }
                var accepted = authority.Commit(batch);
                if (seal is not null)
                {
                    Assert.Equal(seal.Encode(), Assert.IsType<WorldResourceArchiveSegment>(accepted.SealedResourceSegment).Encode());
                    if (archives == 0) ExportAcceptedDatabase(authority, "archive");
                    archives++;
                    Assert.Equal((ulong)archives, authority.InspectForHost().Journal.ResourceArchiveCount);
                    Assert.Equal((ulong)expected.Archives.Take(archives).Sum(value => value.Encode().LongLength),
                        authority.InspectForHost().Journal.ResourceArchiveBytes);
                }
                else Assert.Null(accepted.SealedResourceSegment);
            }
            Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            Assert.Equal(final.Quests!.ContentHash, rewarded.Quests!.ContentHash);
            Assert.True(final.Progression!.GetActor(fixture.Player).CurrentMp < rewarded.Progression!.GetActor(fixture.Player).CurrentMp);
        }

        byte[] closed = File.ReadAllBytes(files.Path);
        Assert.Equal(2L, ReadArchiveCount(files.Path));
        using (var authority = ReopenExact(files.Path, final))
        {
            WorldResourceArchiveAnchor? anchor = null;
            for (int index = 0; index < expected.Archives.Count; index++)
            {
                var original = expected.Archives[index];
                var retrieved = authority.ReadResourceArchiveForHost((ulong)index);
                Assert.Equal(original.Reference, retrieved.Segment.Reference);
                Assert.Equal(original.Encode(), retrieved.Segment.Encode());
                Assert.Equal(original.Reference.BoundarySequence + 1, retrieved.PublicationSequence);
                Assert.Equal(final.Revision, retrieved.SourceHeadRevision);
                Assert.Equal(final.ContentHash, retrieved.SourceHeadHash);
                var boundary = WorldSnapshotCodec.Decode(retrieved.Segment.Encode(), anchor);
                Assert.Equal(ActorResourceHistory.MaximumBatches, boundary.ResourceHistory!.History.Batches.Count);
                Assert.Equal(boundary.ContentHash, retrieved.Segment.Reference.BoundaryHash);
                anchor = retrieved.Segment.Anchor;
            }
            Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(
                WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(final), anchor)));
            Assert.Throws<ArgumentOutOfRangeException>(() => authority.ReadResourceArchiveForHost(2));
            Assert.Equal(beforePublication!.ContentHash,
                authority.ReadRecordedStateForHost(beforePublication.Revision).StateHash);
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(publicationBatch!));
            Assert.Equal(2UL, authority.InspectForHost().Journal.ResourceArchiveCount);
            Assert.Equal(final.ContentHash, authority.ReadStateForHost().ContentHash);

            var closing = WorldSnapshotCodec.Decode(expected.Archives[0].Encode());
            var oldAttack = closing.ResourceHistory!.Evidence.OfType<WorldEncounterResourceEvidence>().First();
            Assert.False(final.IdentityIndex.Contains(oldAttack.CommandId));
            var control = final.Inventory.Controls.Single(value => value.ActorId == oldAttack.Request.ActorId);
            var reused = new WorldActionCommand(oldAttack.CommandId, control.PrincipalId, control.SessionId,
                control.ActorId, final.WorldId, final.BranchId, final.Space.GetActor(control.ActorId).Revision,
                new QueueEncounterWaitIntent(final.EncounterActions!.Scope.EncounterId, final.Pacing!.Encounter!.Revision));
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(
                fixture.Batch(final, WorldBatchKind.EncounterDecision, [reused])));
            Assert.Equal(final.ContentHash, authority.ReadStateForHost().ContentHash);
        }
        Assert.Equal(closed, File.ReadAllBytes(files.Path));

        // Each counterexample modifies a copy of the same closed accepted save. No regeneration or
        // schema repair may substitute for the archive bytes that were actually committed.
        foreach (var mutation in ArchiveMutations())
        {
            using var corrupted = new StorageFiles();
            File.Copy(files.Path, corrupted.Path);
            corrupted.Execute(mutation);
            byte[] altered = File.ReadAllBytes(corrupted.Path);
            Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(corrupted.Path));
            Assert.Equal(altered, File.ReadAllBytes(corrupted.Path));
        }
        using (var orphan = new StorageFiles())
        {
            File.Copy(files.Path, orphan.Path);
            InsertUnscheduledArchive(orphan.Path, rewarded);
            byte[] altered = File.ReadAllBytes(orphan.Path);
            var failure = Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(orphan.Path));
            Assert.Contains("unscheduled", failure.Message);
            Assert.Equal(altered, File.ReadAllBytes(orphan.Path));
        }
    }

    [Theory]
    [InlineData(25, 6)]
    [InlineData(24, 7)]
    public void Care_profile_and_schema_require_their_exact_pair(int profile, int schema)
    {
        using var files = new StorageFiles();
        var genesis = profile == 25 ? new CareWorldFixture().Genesis : new EncounterJourneyWorldFixture().Genesis;
        using (var database = WorldDatabase.Open(files.Path, create: true, schemaVersion: schema))
        {
            database.Initialize(WorldSnapshotCodec.Encode(genesis));
        }
        byte[] closed = File.ReadAllBytes(files.Path);
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path));
        Assert.Throws<InvalidDataException>(() => RecordedWorldReader.ReadForHost(files.Path, 0));
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public void Fresh_care_database_cannot_import_an_actual_pending_obligation_or_treatment()
    {
        var fixture = new CareWorldFixture();
        var session = new CareWorldSession(fixture.Genesis);
        var defeated = fixture.PrepareDefeat(session);
        var acknowledged = CareWorldFixture.Commit(session, fixture.Batch(defeated, WorldBatchKind.Care,
            [fixture.Acknowledge(defeated)])).Candidate;
        var treated = CareWorldFixture.Commit(session, fixture.Batch(acknowledged, WorldBatchKind.Care,
            [fixture.Administer(acknowledged)])).Candidate;
        foreach (var earned in new[] { defeated, acknowledged, treated })
        {
            using var files = new StorageFiles();
            byte[] bytes = WorldSnapshotCodec.Encode(earned);
            Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));
            Assert.Throws<ArgumentException>(() => SqliteWorldAuthority.CreateNew(files.Path, earned));
            Assert.False(File.Exists(files.Path));
            using (var database = WorldDatabase.Open(files.Path, create: true, schemaVersion: 7))
            {
                database.Initialize(bytes);
            }
            byte[] closed = File.ReadAllBytes(files.Path);
            Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path));
            Assert.Equal(closed, File.ReadAllBytes(files.Path));
        }
    }

    private static SqliteWorldAuthority ReopenExact(string path, WorldSnapshot expected)
    {
        var authority = SqliteWorldAuthority.OpenExisting(path);
        try
        {
            Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            return authority;
        }
        catch
        {
            authority.Dispose();
            throw;
        }
    }

    private static void AssertReceipt(SqliteWorldAuthority authority, WorldActionCommand command,
        CareWorldFixture fixture, WorldActionReceipt expected)
    {
        var receipt = Assert.IsType<WorldActionReceipt>(authority.FindReceipt(command, fixture.DeliveryFor(command.ActorId)));
        Assert.Equal(WorldReceiptCodecV1.Encode(expected), WorldReceiptCodecV1.Encode(receipt));
    }

    private static long ReadArchiveCount(string path)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM world_resource_segments";
        return (long)command.ExecuteScalar()!;
    }

    private static IEnumerable<string> ArchiveMutations()
    {
        yield return "DELETE FROM world_resource_segments WHERE ordinal=x'0000000000000000'";
        yield return "UPDATE world_resource_segments SET state=zeroblob(length(state)) WHERE ordinal=x'0000000000000000'";
        yield return "UPDATE world_resource_segments SET boundary_hash=zeroblob(32) WHERE ordinal=x'0000000000000000'";
        yield return "UPDATE world_resource_segments SET world_id=zeroblob(16) WHERE ordinal=x'0000000000000000'";
        yield return "UPDATE world_resource_segments SET ordinal=x'0000000000000003' WHERE ordinal=x'0000000000000001'";
    }

    private static void InsertUnscheduledArchive(string path, WorldSnapshot actualEarlierRoot)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadWrite, Pooling = false
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO world_resource_segments "
            + "(world_id,branch_id,ordinal,boundary_sequence,publication_sequence,boundary_hash,state) "
            + "VALUES($world,$branch,$ordinal,$boundary,$publication,$hash,$state)";
        command.Parameters.AddWithValue("$world", actualEarlierRoot.WorldId.ToByteArray());
        command.Parameters.AddWithValue("$branch", actualEarlierRoot.BranchId.ToByteArray());
        command.Parameters.AddWithValue("$ordinal", Number(2));
        command.Parameters.AddWithValue("$boundary", Number(actualEarlierRoot.Revision));
        command.Parameters.AddWithValue("$publication", Number(actualEarlierRoot.Revision + 1));
        command.Parameters.AddWithValue("$hash", actualEarlierRoot.ContentHash.ToByteArray());
        command.Parameters.AddWithValue("$state", WorldSnapshotCodec.Encode(actualEarlierRoot));
        Assert.Equal(1, command.ExecuteNonQuery());
    }

    private static byte[] Number(ulong value)
    {
        byte[] bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    private static void ExportCrashFixture(SqliteWorldAuthority authority, string fixtureName, WorldSnapshot before,
        WorldStepBatch batch, WorldStepDecision expected)
    {
        if (Environment.GetEnvironmentVariable("OPENFANTASY_DURABLE_ARCHIVE_EVIDENCE") is not { Length: > 0 } root)
            return;
        string directory = Path.Combine(root, fixtureName);
        Directory.CreateDirectory(directory);
        // The owning connection takes the consistent committed backup under its gate. A second
        // connection cannot read an exclusive writer, and copying its live file can omit the WAL.
        authority.BackupForHost(Path.Combine(directory, "before.sqlite3"));
        File.WriteAllBytes(Path.Combine(directory, "before.ofc"), WorldSnapshotCodec.Encode(before));
        File.WriteAllBytes(Path.Combine(directory, "batch.ofc"), WorldStepCodec.Encode(batch));
        File.WriteAllBytes(Path.Combine(directory, "after.ofc"), WorldSnapshotCodec.Encode(expected.Candidate));
        ulong count = authority.InspectForHost().Journal.ResourceArchiveCount;
        File.WriteAllText(Path.Combine(directory, "metadata.json"), JsonSerializer.Serialize(new
        {
            fixture = fixtureName,
            beforeRevision = before.Revision,
            beforeHash = Convert.ToHexString(before.ContentHash.ToByteArray()).ToLowerInvariant(),
            afterRevision = expected.Candidate.Revision,
            afterHash = Convert.ToHexString(expected.Candidate.ContentHash.ToByteArray()).ToLowerInvariant(),
            archiveCountBefore = count,
            archiveCountAfter = count + (expected.SealedResourceSegment is null ? 0UL : 1UL),
            newArchiveOrdinal = expected.SealedResourceSegment?.Reference.Ordinal
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void ExportAcceptedDatabase(SqliteWorldAuthority authority, string fixtureName)
    {
        if (Environment.GetEnvironmentVariable("OPENFANTASY_DURABLE_ARCHIVE_EVIDENCE") is not { Length: > 0 } root)
            return;
        authority.BackupForHost(Path.Combine(root, fixtureName, "after.sqlite3"));
    }
}
