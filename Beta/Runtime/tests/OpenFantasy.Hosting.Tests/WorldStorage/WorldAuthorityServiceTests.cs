using System.Diagnostics;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class WorldAuthorityServiceTests(ITestOutputHelper output)
{
    [Fact]
    public void CommittedMovementTimeInventoryAndReceiptsSurviveExactReplay()
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Genesis();
        WorldActionCommand give = Give(genesis);
        byte[] committed;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            Assert.Null(authority.FindReceipt(give, Delivery));
            WorldStepDecision decision = authority.Commit(Step(genesis, actions: [give],
                movement: [new WorldMovementInput(Delivery, Id(10), 32, 0)]));
            WorldActionReceipt receipt = Assert.Single(decision.Receipts);
            Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
            Assert.Equal(6UL, decision.Candidate.Inventory.Custody.GetStoredQuantity(Id(61)));
            Assert.Equal(0UL, decision.Candidate.Inventory.Custody.GetStoredQuantity(Id(60)));
            Assert.Equal(1UL, decision.Candidate.Revision);
            Assert.Equal(1UL, decision.Candidate.AdvancingSteps);
            Assert.Equal(32L, decision.Candidate.Space.GetActor(Id(10)).Feet.X);
            Assert.Equal(genesis.Calendar.AddMilliseconds(1800), decision.Candidate.Calendar);
            committed = WorldSnapshotCodecV1.Encode(decision.Candidate);
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(authority.FindReceipt(give, Delivery)!));
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(Step(genesis, actions: [give])));
        }
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(committed, WorldSnapshotCodecV1.Encode(reopened.InspectForHost().State));
        Assert.Equal(CommandReceiptStatus.Applied, reopened.FindReceipt(give, Delivery)!.Status);
        Assert.Equal(1UL, reopened.InspectForHost().Journal.CommandOutcomeCount);
    }

    [Fact]
    public void DialogueHoldAndCloseAreDurableWithoutRetrospectiveTime()
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Genesis();
        var speak = new WorldActionCommand(Id(201), Id(100), Id(110), Id(10), genesis.WorldId,
            genesis.BranchId, 0, new SpeakToActorIntent(Id(11), 0));
        WorldSnapshot held;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            held = authority.Commit(Step(genesis, actions: [speak])).Candidate;
            Assert.True(held.HasReadingHold);
        }
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.True(reopened.InspectForHost().State.HasReadingHold);
        var close = new WorldActionCommand(Id(202), Id(100), Id(110), Id(10), held.WorldId, held.BranchId,
            held.Space.GetActor(Id(10)).Revision, new CloseDialogueIntent(held.Dialogues[0].DialogueId));
        WorldSnapshot closed = reopened.Commit(Step(held, 301, [close])).Candidate;
        Assert.False(closed.HasReadingHold);
        Assert.Equal(held.Calendar, closed.Calendar);
        Assert.Equal(held.AdvancingSteps, closed.AdvancingSteps);
        Assert.Equal(2UL, closed.Revision);
    }

    [Fact]
    public void SchedulerPreviewDoesNotReservePublishOrAcknowledgeAnything()
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Genesis();
        WorldActionCommand command = Give(genesis);
        WorldStepBatch batch = Step(genesis, actions: [command]);
        using SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        WorldStepDecision preview = authority.Preview(batch);
        Assert.Equal(1UL, preview.Candidate.Revision);
        Assert.Equal(0UL, authority.ReadStateForHost().Revision);
        Assert.Null(authority.FindReceipt(command, Delivery));
        Assert.False(authority.IsIdentityOccupiedForHost(command.CommandId));
        Assert.True(authority.IsIdentityOccupiedForHost(Id(10)));
        WorldStepDecision committed = authority.Commit(batch);
        Assert.Equal(WorldSnapshotCodecV1.Encode(preview.Candidate), WorldSnapshotCodecV1.Encode(committed.Candidate));
        Assert.True(authority.IsIdentityOccupiedForHost(command.CommandId));
        Assert.Throws<AuthorityDispatchException>(() => authority.Preview(batch));
    }

    [Fact]
    public void PartialTransferLineageAndTerminalRejectionsRemainDurable()
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Genesis();
        WorldActionCommand partial = Give(genesis, quantity: 2);
        EntityId child = EntityId.DeriveSpawned(partial.CommandId, 0);
        WorldActionCommand excessive;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            WorldStepDecision first = authority.Commit(Step(genesis, actions: [partial]));
            Assert.Contains(first.Identities, identity => identity.Identity == child
                && identity.Kind == WorldCommittedIdentityKind.SpawnedEntity);
            Assert.Equal(4UL, first.Candidate.Inventory.Custody.GetLot(Id(70)).Quantity);
            Assert.Equal(2UL, first.Candidate.Inventory.Custody.GetLot(child).Quantity);
            excessive = Give(first.Candidate, command: 201, quantity: 5);
            WorldStepDecision second = authority.Commit(Step(first.Candidate, cause: 301, actions: [excessive]));
            Assert.Equal(CommandReceiptStatus.Rejected, Assert.Single(second.Receipts).Status);
            Assert.Equal(first.Candidate.Inventory.ContentHash, second.Candidate.Inventory.ContentHash);
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(Step(second.Candidate, cause: 300)));
            Assert.Equal(2UL, authority.ReadStateForHost().Revision);
        }
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.True(reopened.IsIdentityOccupiedForHost(child));
        Assert.Equal(4UL, reopened.ReadStateForHost().Inventory.Custody.GetLot(Id(70)).Quantity);
        Assert.Equal(2UL, reopened.ReadStateForHost().Inventory.Custody.GetLot(child).Quantity);
        Assert.Equal(CommandReceiptStatus.Applied, reopened.FindReceipt(partial, Delivery)!.Status);
        Assert.Equal(CommandReceiptStatus.Rejected, reopened.FindReceipt(excessive, Delivery)!.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UncertainCommitRequiresReopenAndExactReceiptRecovery(bool afterCommit)
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Genesis();
        WorldActionCommand command = Give(genesis);
        WorldStepBatch batch = Step(genesis, actions: [command]);
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            Assert.Throws<IOException>(() => authority.Commit(batch, boundary =>
            {
                if ((boundary == AuthorityCommitBoundary.AfterCommit) == afterCommit)
                {
                    throw new IOException("Injected lost operation reply.");
                }
            }));
            Assert.Throws<InvalidOperationException>(() => authority.InspectForHost());
            Assert.Throws<InvalidOperationException>(() => authority.FindReceipt(command, Delivery));
        }
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        if (afterCommit)
        {
            Assert.NotNull(reopened.FindReceipt(command, Delivery));
            Assert.Equal(1UL, reopened.InspectForHost().State.Revision);
        }
        else
        {
            Assert.Null(reopened.FindReceipt(command, Delivery));
            Assert.Equal(0UL, reopened.InspectForHost().State.Revision);
            Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(reopened.Commit(batch).Receipts).Status);
        }
    }

    [Fact]
    public void CurrentAuthorityAndExactCommandBytesGateReceiptLookup()
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Genesis();
        WorldActionCommand original = Give(genesis);
        using SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        authority.Commit(Step(genesis, actions: [original]));
        Assert.Throws<AuthorityDispatchException>(() => authority.FindReceipt(original, new TrustedCommandDelivery(Id(101), Id(111))));
        Assert.Throws<AuthorityDispatchException>(() => authority.FindReceipt(original, new TrustedCommandDelivery(Id(100), Id(999))));
        Assert.Throws<AuthorityDispatchException>(() => authority.FindReceipt(Give(genesis, quantity: 5), Delivery));
        var wrongBranch = new WorldActionCommand(original.CommandId, original.PrincipalId, original.OriginSessionId,
            original.ActorId, original.WorldId, Id(997), original.ExpectedActorRevision, original.Intent);
        Assert.Throws<AuthorityDispatchException>(() => authority.FindReceipt(wrongBranch, Delivery));
        var freshOldOrigin = new WorldActionCommand(Id(999), Id(100), Id(998), Id(10), genesis.WorldId,
            genesis.BranchId, 0, new SpeakToActorIntent(Id(11), 0));
        Assert.Throws<AuthorityDispatchException>(() => authority.FindReceipt(freshOldOrigin, Delivery));
        Assert.Throws<AuthorityDispatchException>(() => authority.Commit(Step(authority.InspectForHost().State,
            cause: 301, actions: [freshOldOrigin])));
        Assert.Equal(1UL, authority.InspectForHost().State.Revision);
    }

    [Theory]
    [InlineData("UPDATE world_command_outcomes SET receipt=x'01'")]
    [InlineData("UPDATE world_commits SET outcomes=x'01'")]
    [InlineData("DELETE FROM world_occupied_identity")]
    [InlineData("DELETE FROM world_checkpoints")]
    [InlineData("UPDATE world_head SET advancing_steps=x'0000000000000000'")]
    [InlineData("INSERT INTO world_checkpoints SELECT sequence,advancing_steps,state,hash FROM world_head")]
    public void SemanticallyCorruptJournalTablesRejectBeforeReadiness(string mutation)
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Genesis();
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            authority.Commit(Step(genesis, actions: [Give(genesis)]));
        }
        files.Execute(mutation);
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path));
    }

    [Fact]
    public void GenesisCannotDisguiseAnExistingWorldRevisionAsCommitZero()
    {
        using var files = new StorageFiles();
        WorldSnapshot initial = Genesis();
        var advanced = new WorldSnapshot(initial.Inventory, initial.Space, initial.Residents,
            initial.AdmissionVersion, initial.Calendar, 1, 1);
        Assert.Throws<ArgumentException>(() => SqliteWorldAuthority.CreateNew(files.Path, advanced));
        Assert.False(File.Exists(files.Path));
    }

    [Fact]
    public void CheckpointsAndRecoverySafetyLimitsHaveExplicitBoundaries()
    {
        var empty = new WorldJournalUsage(0, 0, 0, 0, 0);
        Assert.False(WorldJournalPolicy.Advance(empty, 5999, 5999, 0, 1, 1).WriteCheckpoint);
        Assert.True(WorldJournalPolicy.Advance(empty, 6000, 6000, 0, 1, 1).WriteCheckpoint);
        Assert.False(WorldJournalPolicy.Advance(empty, 9999, 20, 0, 1, 1).WriteCheckpoint);
        Assert.True(WorldJournalPolicy.Advance(empty, 10000, 20, 0, 1, 1).WriteCheckpoint);
        Assert.Throws<WorldRecoveryLimitException>(() => WorldJournalPolicy.Advance(empty,
            WorldJournalPolicy.MaximumCommitCount + 1, 0, 0, 1, 1));
        Assert.Throws<WorldRecoveryLimitException>(() => WorldJournalPolicy.Advance(empty,
            1, 0, WorldJournalPolicy.MaximumCommandOutcomeCount + 1, 1, 1));
        Assert.Throws<WorldRecoveryLimitException>(() => WorldJournalPolicy.Advance(empty,
            1, 0, 0, WorldJournalPolicy.MaximumAcceptedJournalBytes, 1));
    }

    [Fact]
    public void SixThousandActualCommitsWriteAndReplayTheirScheduledCheckpoint()
    {
        using var files = new StorageFiles();
        WorldSnapshot state = Genesis();
        int stateBytes = WorldSnapshotCodecV1.Encode(state).Length;
        byte[]? beforeCheckpoint = null;
        var elapsed = Stopwatch.StartNew();
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, state))
        {
            for (ulong step = 0; step < 6000; step++)
            {
                state = authority.Commit(Step(state, cause: 10_000 + step)).Candidate;
                if (state.Revision == 5999)
                {
                    beforeCheckpoint = WorldSnapshotCodec.Encode(state);
                }
            }
            Assert.Equal(6000UL, authority.InspectForHost().Journal.LastCheckpointSequence);
            Assert.Equal(6000UL, authority.InspectForHost().Journal.LastCheckpointSteps);
        }
        double commitMilliseconds = elapsed.Elapsed.TotalMilliseconds;
        elapsed.Restart();
        double recoveryMilliseconds;
        double historyMilliseconds;
        using (SqliteWorldAuthority recovered = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodecV1.Encode(state), WorldSnapshotCodecV1.Encode(recovered.InspectForHost().State));
            Assert.Equal(6000UL, recovered.InspectForHost().Journal.LastCheckpointSequence);
            recoveryMilliseconds = elapsed.Elapsed.TotalMilliseconds;
            elapsed.Restart();
            Assert.Equal(beforeCheckpoint, WorldSnapshotCodec.Encode(recovered.ReadRecordedStateForHost(5999).State));
            historyMilliseconds = elapsed.Elapsed.TotalMilliseconds;
        }
        long fileBytes = new FileInfo(files.Path).Length;
        output.WriteLine("Small-fixture full replay: commits=6000; genesisBytes={0}; commitMilliseconds={1:F3}; recoveryMilliseconds={2:F3}; databaseBytes={3}; historyMilliseconds={4:F3}",
            stateBytes, commitMilliseconds, recoveryMilliseconds, fileBytes, historyMilliseconds);
        files.Execute("DELETE FROM world_checkpoints WHERE sequence!=x'0000000000000000'");
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path));
    }
}
