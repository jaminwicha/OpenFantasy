using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.WorldStorage.GatheringWorldFixture;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;
using static OpenFantasy.Hosting.Tests.WorldStorage.ProductionUpgradeFixture;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ProductionUpgradeRecoveryTests
{
    [Theory]
    [InlineData("outcome")]
    [InlineData("request")]
    [InlineData("missing-definition")]
    [InlineData("wrong-definition-role")]
    [InlineData("missing-old-receipt")]
    [InlineData("unscheduled-checkpoint")]
    [InlineData("legacy-head")]
    [InlineData("extra-schema-object")]
    public void EditedMigrationOrRetainedHistoryCannotPassRecovery(string mutation)
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        WorldSnapshot old = Create();
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(source.Path, old))
        {
            old = authority.Commit(Batch(old, Swing(old))).Candidate;
        }
        using (SqliteWorldAuthority.UpgradeGatheringToProduction(source.Path, destination.Path, Request(old))) { }
        string sql = mutation switch
        {
            "outcome" => "UPDATE world_commits SET outcomes=x'00' WHERE kind=3",
            "request" => "UPDATE world_commits SET accepted_batch=x'00' WHERE kind=3",
            "missing-definition" => "DELETE FROM world_occupied_identity WHERE kind=5",
            "wrong-definition-role" => "UPDATE world_occupied_identity SET kind=4 WHERE kind=5",
            "missing-old-receipt" => "DELETE FROM world_command_outcomes",
            "unscheduled-checkpoint" => "INSERT INTO world_checkpoints SELECT sequence,advancing_steps,state,hash FROM world_head",
            "legacy-head" => "UPDATE world_head SET state=x'" + Convert.ToHexString(WorldSnapshotCodec.Encode(old))
                + "',hash=x'" + Convert.ToHexString(old.ContentHash.ToByteArray()) + "'",
            "extra-schema-object" => "CREATE VIEW unexpected AS SELECT sequence FROM world_commits",
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        destination.Execute(sql);
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(destination.Path));
        using SqliteWorldAuthority original = SqliteWorldAuthority.OpenExisting(source.Path);
        Assert.Equal(old.ContentHash, original.InspectForHost().State.ContentHash);
    }

    [Fact]
    public void HistoricalCauseAndChangedHeadCannotBeAdmittedAsFreshMigrationIdentity()
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        WorldSnapshot state = Create();
        WorldActionCommand action = Swing(state);
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(source.Path, state))
        {
            state = authority.Commit(Batch(state, action)).Candidate;
        }
        Assert.Throws<InvalidOperationException>(() => SqliteWorldAuthority.UpgradeGatheringToProduction(source.Path,
            destination.Path, Request(state, action.CommandId)));
        Assert.False(File.Exists(destination.Path));
        var correct = Request(state);
        var wrong = new WorldProductionMigration(correct.CauseId, correct.WorldId, correct.BranchId,
            correct.ExpectedRevision, correct.ExpectedAdvancingSteps, ContentHash.Compute("different-head"u8),
            correct.SourceAdmissionVersion, correct.TargetAdmissionVersion, correct.Production);
        Assert.Throws<InvalidOperationException>(() => SqliteWorldAuthority.UpgradeGatheringToProduction(source.Path, destination.Path, wrong));
        Assert.False(File.Exists(destination.Path));
    }

    [Fact]
    public void FailedSchemaTransactionRollsBackMetadataAndBothTablesOnTheStagingCopy()
    {
        using var files = new StorageFiles();
        WorldSnapshot state = Create();
        using (SqliteWorldAuthority.CreateNew(files.Path, state)) { }
        using (WorldDatabase database = WorldDatabase.Open(files.Path, create: false))
        {
            PreparedWorldMigration prepared = new WorldMigrationPreparation().Prepare(state, state.IdentityIndex,
                state.ContentHash, Request(state), _ => false);
            Assert.Throws<IOException>(() => database.UpgradeProductionSchema(prepared, false,
                at => { if (at == WorldProductionUpgradeBoundary.BeforeCommit) throw new IOException("rollback schema migration"); }));
            Assert.Equal(2, database.SchemaVersion);
            Assert.Equal(1, database.Inspect().ForeignKeys);
            Assert.Empty(database.ReadJournal());
            Assert.Empty(database.ReadIdentities());
        }
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(state.ContentHash, reopened.InspectForHost().State.ContentHash);
    }

    [Fact]
    public void SchemaRebuildPreservesUnsignedRowsAndForeignKeysExactly()
    {
        using var files = new StorageFiles();
        WorldSnapshot state = Create();
        ulong high = (ulong)long.MaxValue + 9;
        byte[] highBytes = "old unsigned row"u8.ToArray();
        using (WorldDatabase database = WorldDatabase.Open(files.Path, create: true))
        {
            database.Initialize(WorldSnapshotCodec.Encode(state));
            using (var transaction = database.BeginWrite())
            {
                database.WriteCommit(new(high, WorldCommitKind.AdvancingStep, Id(99_000), high, highBytes, highBytes,
                    state.ContentHash, ContentHash.Compute(highBytes)), transaction);
                database.WriteCommandOutcome(new(Id(99_001), Id(99_002), high, highBytes, highBytes), transaction);
                database.ReserveIdentity(new(Id(99_002), high, WorldIdentityKind.Command), transaction);
                transaction.Commit();
            }
            PreparedWorldMigration migration = new WorldMigrationPreparation().Prepare(state, state.IdentityIndex,
                state.ContentHash, Request(state), identity => database.ContainsIdentity(identity));
            database.UpgradeProductionSchema(migration, false, null);
            Assert.Equal(3, database.SchemaVersion);
            Assert.Equal(1, database.Inspect().ForeignKeys);
        }
        // This is a substrate fixture with deliberately noncontiguous high counters, not an accepted gameplay journal.
        using WorldDatabase restored = WorldDatabase.Open(files.Path, create: false);
        StoredWorldCommit highRow = restored.ReadJournal().Single(commit => commit.Sequence == high);
        Assert.Equal(high, highRow.AdvancingSteps);
        Assert.Equal(highBytes, highRow.AcceptedBatch);
        StoredWorldCommandOutcome command = Assert.Single(restored.ReadCommandOutcomes());
        Assert.Equal(high, command.Sequence);
        Assert.Equal(highBytes, command.Receipt);
        Assert.Equal(high, restored.ReadIdentities().Single(identity => identity.Identity == Id(99_002)).Sequence);
    }
}
