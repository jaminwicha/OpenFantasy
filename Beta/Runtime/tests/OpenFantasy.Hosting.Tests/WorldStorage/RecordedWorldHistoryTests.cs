using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class RecordedWorldHistoryTests
{
    [Fact]
    public void EveryRecordedBoundaryMatchesOriginalBytesInReverseOrderAfterRestart()
    {
        using var files = new StorageFiles();
        WorldSnapshot state = Genesis();
        var originals = new List<byte[]> { WorldSnapshotCodec.Encode(state) };
        WorldActionCommand give = Give(state, quantity: 2);
        byte[] receipt;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, state))
        {
            WorldStepDecision transfer = authority.Commit(Step(state, actions: [give],
                movement: [new(Delivery, Id(10), 32, 0)]));
            state = transfer.Candidate;
            receipt = WorldReceiptCodecV1.Encode(Assert.Single(transfer.Receipts));
            originals.Add(WorldSnapshotCodec.Encode(state));
            for (ulong index = 1; index < 4; index++)
            {
                state = authority.Commit(Step(state, 300 + index,
                    movement: [new(Delivery, Id(10), 32, 0)])).Candidate;
                originals.Add(WorldSnapshotCodec.Encode(state));
            }
        }
        byte[] fileBefore = File.ReadAllBytes(files.Path);
        using (SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            WorldInspection head = reopened.InspectForHost();
            foreach (int index in new[] { 4, 0, 3, 1, 2, 1 })
            {
                RecordedWorldState past = reopened.ReadRecordedStateForHost((ulong)index);
                Assert.Equal(originals[index], WorldSnapshotCodec.Encode(past.State));
                Assert.Equal(state.ContentHash, past.SourceHeadHash);
                Assert.Equal(4UL, past.SourceHeadRevision);
                Assert.Equal((ulong)index, past.State.Revision);
                Assert.Equal(past.State.ContentHash, past.StateHash);
            }
            Assert.Same(head.State, reopened.ReadStateForHost());
            Assert.Equal(head.Journal, reopened.InspectForHost().Journal);
            Assert.Equal(receipt, WorldReceiptCodecV1.Encode(reopened.FindReceipt(give, Delivery)!));
            Assert.Equal(4UL, reopened.ReadStateForHost().Inventory.Custody.GetStoredQuantity(Id(60)));
            Assert.Equal(2UL, reopened.ReadStateForHost().Inventory.Custody.GetStoredQuantity(Id(61)));
        }
        Assert.Equal(fileBefore, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public void SameInstantCommitsKeepTheirDistinctDialogueStatesAndPreparedHead()
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Genesis();
        using SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var speak = new WorldActionCommand(Id(201), Delivery.PrincipalId, Delivery.SessionId, Id(10),
            genesis.WorldId, genesis.BranchId, 0, new SpeakToActorIntent(Id(11), 0));
        WorldSnapshot held = authority.Commit(Step(genesis, actions: [speak])).Candidate;
        RecordedWorldState prepared = authority.ReadRecordedStateForHost(1);
        var close = new WorldActionCommand(Id(202), Delivery.PrincipalId, Delivery.SessionId, Id(10),
            held.WorldId, held.BranchId, held.Space.GetActor(Id(10)).Revision,
            new CloseDialogueIntent(held.Dialogues[0].DialogueId));
        WorldSnapshot closed = authority.Commit(Step(held, 301, [close])).Candidate;
        RecordedWorldState later = authority.ReadRecordedStateForHost(2);
        RecordedWorldState heldAgain = authority.ReadRecordedStateForHost(1);
        Assert.Equal(held.Calendar, closed.Calendar);
        Assert.True(prepared.State.HasReadingHold);
        Assert.False(later.State.HasReadingHold);
        Assert.Equal(1UL, prepared.SourceHeadRevision);
        Assert.Equal(held.ContentHash, prepared.SourceHeadHash);
        Assert.Equal(2UL, heldAgain.SourceHeadRevision);
        Assert.Equal(closed.ContentHash, heldAgain.SourceHeadHash);
        Assert.Equal(prepared.StateHash, heldAgain.StateHash);
        Assert.Equal(WorldSnapshotCodec.Encode(held), WorldSnapshotCodec.Encode(heldAgain.State));
        Assert.Equal(WorldSnapshotCodec.Encode(closed), WorldSnapshotCodec.Encode(later.State));
    }

    [Fact]
    public void GenesisAndInvalidFutureQueriesCannotChangeOrDisableTheAuthority()
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Genesis();
        using SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        RecordedWorldState zero = authority.ReadRecordedStateForHost(0);
        Assert.Equal(WorldSnapshotCodec.Encode(genesis), WorldSnapshotCodec.Encode(zero.State));
        Assert.Equal(0UL, zero.SourceHeadRevision);
        Assert.Throws<ArgumentOutOfRangeException>(() => authority.ReadRecordedStateForHost(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => authority.ReadRecordedStateForHost(ulong.MaxValue));
        Assert.Equal(1UL, authority.Commit(Step(genesis)).Candidate.Revision);
        Assert.Equal(0UL, zero.State.Revision);
    }

    [Fact]
    public void RejectedActionBoundaryIsReconstructedWithoutChangingItsTerminalReceipt()
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Genesis();
        WorldActionCommand excessive = Give(genesis, quantity: 7);
        using SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        WorldStepDecision rejected = authority.Commit(Step(genesis, actions: [excessive]));
        Assert.Equal(CommandReceiptStatus.Rejected, Assert.Single(rejected.Receipts).Status);
        RecordedWorldState past = authority.ReadRecordedStateForHost(1);
        Assert.Equal(genesis.Inventory.ContentHash, past.State.Inventory.ContentHash);
        Assert.Equal(rejected.Candidate.Calendar, past.State.Calendar);
        Assert.Equal(rejected.Candidate.ContentHash, past.StateHash);
        Assert.Equal(CommandReceiptStatus.Rejected, authority.FindReceipt(excessive, Delivery)!.Status);
        Assert.Equal(1UL, authority.InspectForHost().Journal.CommandOutcomeCount);
    }

    [Theory]
    [InlineData("UPDATE world_commits SET outcomes=x'01' WHERE sequence=x'0000000000000002'")]
    [InlineData("DELETE FROM world_occupied_identity")]
    [InlineData("UPDATE world_command_outcomes SET receipt=x'01'")]
    [InlineData("DELETE FROM world_checkpoints")]
    public void EvenGenesisInspectionRejectsCorruptLaterEvidence(string mutation)
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Genesis();
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            WorldSnapshot first = authority.Commit(Step(genesis, actions: [Give(genesis)])).Candidate;
            authority.Commit(Step(first, 301));
        }
        // Corrupt the closed save, then exercise history replay itself, before normal readiness.
        files.Execute(mutation);
        using WorldDatabase database = WorldDatabase.Open(files.Path, create: false);
        Assert.Throws<InvalidDataException>(() => new WorldJournalRecovery().ReadRecordedState(database, 0));
    }

    [Fact]
    public void UncertainCommitAndDisposedAuthorityCannotPrepareHistory()
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Genesis();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        try
        {
            Assert.Throws<IOException>(() => authority.Commit(Step(genesis), _ => throw new IOException("injected")));
            Assert.Throws<InvalidOperationException>(() => authority.ReadRecordedStateForHost(0));
        }
        finally
        {
            authority.Dispose();
        }
        Assert.Throws<ObjectDisposedException>(() => authority.ReadRecordedStateForHost(0));
    }
}
