using Microsoft.Data.Sqlite;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class RecordedWorldReaderTests
{
    [Fact]
    public void ClosedReaderReturnsExactHistoryAndPreparationPinsWithoutChangingTheSave()
    {
        using var files = new StorageFiles();
        WorldSnapshot[] history = CreateCommittedWorld(files.Path);
        WorldSnapshot head = history[^1];
        byte[] closedBytes = File.ReadAllBytes(files.Path);

        foreach (int index in new[] { 1, 2, 0, 1 })
        {
            RecordedWorldState recorded = RecordedWorldReader.ReadForHost(files.Path, (ulong)index);
            AssertRecorded(recorded, history[index], head);
            Assert.Equal(closedBytes, File.ReadAllBytes(files.Path));
        }

        RecordedWorldState prepared = RecordedWorldReader.ReadForHost(files.Path, 1);
        WorldSnapshot later;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            later = authority.Commit(Step(head, 302,
                movement: [new(Delivery, Id(10), 32, 0)])).Candidate;
        }

        // The lease has ended, but the prepared result still describes the head it verified.
        AssertRecorded(prepared, history[1], head);
        byte[] laterClosedBytes = File.ReadAllBytes(files.Path);
        AssertRecorded(RecordedWorldReader.ReadForHost(files.Path, 1), history[1], later);
        AssertRecorded(RecordedWorldReader.ReadForHost(files.Path, later.Revision), later, later);
        Assert.Equal(laterClosedBytes, File.ReadAllBytes(files.Path));
    }

    [Theory]
    [InlineData("UPDATE world_commits SET outcomes=x'01' WHERE sequence=x'0000000000000002'")]
    [InlineData("UPDATE world_command_outcomes SET receipt=x'01' WHERE sequence=x'0000000000000002'")]
    [InlineData("DELETE FROM world_occupied_identity WHERE sequence=x'0000000000000002'")]
    [InlineData("INSERT INTO world_checkpoints SELECT sequence,advancing_steps,state,hash FROM world_head")]
    [InlineData("UPDATE world_head SET advancing_steps=x'0000000000000000'")]
    public void GenesisReadRejectsLaterCorruptionWithoutRepairingTheFileOrRetainingTheLease(string mutation)
    {
        using var files = new StorageFiles();
        WorldSnapshot[] history = CreateCommittedWorld(files.Path);
        // Every mutation targets evidence after genesis. The copied checkpoint is well formed,
        // but no checkpoint is scheduled at this second advancing step.
        files.Execute(mutation);
        byte[] corruptedBytes = File.ReadAllBytes(files.Path);

        Assert.Throws<InvalidDataException>(() => RecordedWorldReader.ReadForHost(files.Path, 0));
        Assert.Equal(corruptedBytes, File.ReadAllBytes(files.Path));

        // Opening the storage boundary alone does not replay the deliberately corrupt trace.
        // Its successful exclusive lease proves the failed reader disposed its own lease.
        using (WorldDatabase database = WorldDatabase.Open(files.Path, create: false))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(history[0]), database.ReadGenesis());
        }
        Assert.Equal(corruptedBytes, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public void FutureRevisionRefusalPreservesTheSaveAndReleasesItsLease()
    {
        using var files = new StorageFiles();
        WorldSnapshot[] history = CreateCommittedWorld(files.Path);
        byte[] closedBytes = File.ReadAllBytes(files.Path);

        foreach (ulong revision in new[] { history[^1].Revision + 1, ulong.MaxValue })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RecordedWorldReader.ReadForHost(files.Path, revision));
            Assert.Equal(closedBytes, File.ReadAllBytes(files.Path));
            AssertRecorded(RecordedWorldReader.ReadForHost(files.Path, 0), history[0], history[^1]);
        }
        Assert.Equal(closedBytes, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public void MissingFileRefusalCreatesNoSaveOrSidecarAndDoesNotPreventExplicitCreation()
    {
        using var files = new StorageFiles();
        string directory = Path.GetDirectoryName(files.Path)!;
        Assert.Empty(Directory.GetFiles(directory));

        Assert.Throws<FileNotFoundException>(() => RecordedWorldReader.ReadForHost(files.Path, 0));
        Assert.False(File.Exists(files.Path));
        Assert.Empty(Directory.GetFiles(directory));

        WorldSnapshot genesis = Genesis();
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            Assert.Equal(genesis.ContentHash, authority.ReadStateForHost().ContentHash);
        }
        byte[] closedBytes = File.ReadAllBytes(files.Path);
        AssertRecorded(RecordedWorldReader.ReadForHost(files.Path, 0), genesis, genesis);
        Assert.Equal(closedBytes, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public void LiveAuthorityExcludesTheReaderUntilItsExclusiveLeaseEnds()
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Genesis();
        WorldSnapshot committed;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            SqliteException refused = Assert.Throws<SqliteException>(() => RecordedWorldReader.ReadForHost(files.Path, 0));
            Assert.Contains(refused.SqliteErrorCode, new[] { 5, 6 });
            Assert.Equal(genesis.ContentHash, authority.ReadStateForHost().ContentHash);
            committed = authority.Commit(Step(genesis, actions: [Give(genesis)])).Candidate;
        }

        byte[] closedBytes = File.ReadAllBytes(files.Path);
        AssertRecorded(RecordedWorldReader.ReadForHost(files.Path, 0), genesis, committed);
        Assert.Equal(closedBytes, File.ReadAllBytes(files.Path));
    }

    private static WorldSnapshot[] CreateCommittedWorld(string path)
    {
        WorldSnapshot genesis = Genesis();
        using SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(path, genesis);
        WorldStepDecision first = authority.Commit(Step(genesis, actions: [Give(genesis, quantity: 2)]));
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(first.Receipts).Status);
        WorldStepDecision second = authority.Commit(Step(first.Candidate, 301,
            actions: [Give(first.Candidate, command: 201, quantity: 1)]));
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(second.Receipts).Status);
        return [genesis, first.Candidate, second.Candidate];
    }

    private static void AssertRecorded(RecordedWorldState actual, WorldSnapshot expected, WorldSnapshot sourceHead)
    {
        Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(actual.State));
        Assert.Equal(expected.ContentHash, actual.StateHash);
        Assert.Equal(sourceHead.Revision, actual.SourceHeadRevision);
        Assert.Equal(sourceHead.ContentHash, actual.SourceHeadHash);
    }
}
