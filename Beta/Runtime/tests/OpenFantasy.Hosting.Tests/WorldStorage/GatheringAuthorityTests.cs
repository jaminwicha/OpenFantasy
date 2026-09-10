using System.Diagnostics;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.WorldStorage.GatheringWorldFixture;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class GatheringAuthorityTests(ITestOutputHelper output)
{
    [Fact]
    public void LegacyBatchCannotDiscardTheNewProfileAndForgedLegacyHeadCannotReplaceReplay()
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Create();
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            var legacy = new WorldStepBatch(Id(90_000), genesis.WorldId, genesis.BranchId, 0, 0,
                WorldBatchKind.AdvancingStep, [], []);
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(legacy));
            Assert.Equal(genesis.ContentHash, authority.InspectForHost().State.ContentHash);
            authority.Commit(Batch(genesis, Swing(genesis)));
        }
        byte[] unrelated = WorldSnapshotCodecV1.Encode(Genesis());
        files.Execute("UPDATE world_head SET state=x'" + Convert.ToHexString(unrelated)
            + "',hash=x'" + Convert.ToHexString(ContentHash.Compute(unrelated).ToByteArray()) + "'");
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path));
    }

    [Fact]
    public void GatheringCommitRestoresExactRequestReceiptSourceAndRemainingWork()
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Create();
        WorldActionCommand action = Swing(genesis);
        byte[] stateBytes;
        byte[] receiptBytes;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            WorldStepDecision decision = authority.Commit(Batch(genesis, action));
            WorldSnapshot current = decision.Candidate;
            Assert.Equal(CommandReceiptStatus.Applied, decision.Receipts.Single().Status);
            Assert.Equal((ushort)2, current.ProfileVersion);
            Assert.Equal(4, Node(current).RemainingSwings);
            Assert.Equal(1UL, Node(current).NextUnissuedUnit);
            var issued = current.Inventory.Custody.Lots.Single(lot => lot.Source.SourceId == NodeSourceId);
            Assert.Equal(0UL, issued.Source.Start);
            Assert.Equal(1UL, issued.Source.End);
            Assert.Equal(1UL, issued.Quantity);
            Assert.True(authority.IsIdentityOccupiedForHost(issued.LotId));
            Assert.Equal(610U, current.Gathering!.GetActorWork(Id(10)).Effort);
            stateBytes = WorldSnapshotCodec.Encode(current);
            receiptBytes = WorldReceiptCodecV1.Encode(decision.Receipts.Single());
            Assert.Equal(receiptBytes, WorldReceiptCodecV1.Encode(authority.FindReceipt(action, Delivery)!));
            Assert.Equal(1UL, authority.InspectForHost().State.Revision);
        }
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(stateBytes, WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
        Assert.Equal(receiptBytes, WorldReceiptCodecV1.Encode(reopened.FindReceipt(action, Delivery)!));
        Assert.Equal(1UL, reopened.InspectForHost().Journal.CommandOutcomeCount);
        Assert.Throws<AuthorityDispatchException>(() => reopened.FindReceipt(new WorldActionCommand(action.CommandId,
            action.PrincipalId, action.OriginSessionId, action.ActorId, action.WorldId, action.BranchId,
            action.ExpectedActorRevision, new GatherIntent(NodeId, 99, ToolLotId, 0)), Delivery));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LostReplyBeforeOrAfterCommitNeverDuplicatesGathering(bool afterCommit)
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Create();
        WorldActionCommand action = Swing(genesis);
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            Assert.Throws<IOException>(() => authority.Commit(Batch(genesis, action), boundary =>
            {
                if (boundary == (afterCommit ? AuthorityCommitBoundary.AfterCommit : AuthorityCommitBoundary.BeforeCommit))
                {
                    throw new IOException("Injected lost gathering reply.");
                }
            }));
            Assert.Throws<InvalidOperationException>(() => authority.FindReceipt(action, Delivery));
        }
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        WorldActionReceipt? prior = reopened.FindReceipt(action, Delivery);
        Assert.Equal(afterCommit, prior is not null);
        WorldSnapshot recovered = reopened.InspectForHost().State;
        Assert.Equal(afterCommit ? 1UL : 0UL, Node(recovered).NextUnissuedUnit);
        if (prior is null)
        {
            recovered = reopened.Commit(Batch(recovered, action)).Candidate;
        }
        Assert.Equal(1UL, Node(recovered).NextUnissuedUnit);
        Assert.Equal(4, Node(recovered).RemainingSwings);
        Assert.Equal(1UL, reopened.InspectForHost().Journal.CommandOutcomeCount);
    }

    [Fact]
    public void FullStorageRecordsRejectionWithoutDamageIssuanceOrEffortCost()
    {
        using var files = new StorageFiles();
        WorldSnapshot state = Create(effort: 400, storageCapacity: 7);
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, state))
        {
            WorldStepDecision decision = authority.Commit(Batch(state, Swing(state)));
            state = decision.Candidate;
            Assert.Equal(CommandReceiptStatus.Rejected, decision.Receipts.Single().Status);
            Assert.Equal(0UL, Node(state).NextUnissuedUnit);
            Assert.Equal(5, Node(state).RemainingSwings);
            Assert.Equal(410U, state.Gathering!.GetActorWork(Id(10)).Effort);
            Assert.DoesNotContain(state.Inventory.Custody.Lots, lot => lot.Source.SourceId == NodeSourceId);
        }
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(state), WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
    }

    [Fact]
    public void DepletionRegrowthAndSecondGenerationRetainMonotonicSourceRightsAcrossReplay()
    {
        using var files = new StorageFiles();
        WorldSnapshot state = Create();
        var elapsed = Stopwatch.StartNew();
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, state))
        {
            for (ulong swing = 0; swing < 5; swing++)
            {
                while (state.Gathering!.GetActorWork(Id(10)).Effort < 400)
                {
                    state = authority.Commit(Batch(state)).Candidate;
                }
                WorldStepDecision decision = authority.Commit(Batch(state, Swing(state, swing)));
                Assert.Equal(CommandReceiptStatus.Applied, decision.Receipts.Single().Status);
                state = decision.Candidate;
            }
            Assert.Equal(0, Node(state).RemainingSwings);
            Assert.Equal(5UL, Node(state).NextUnissuedUnit);
            Assert.Equal(GatherNodeVisibility.Stump, Node(state).VisibilityAt(state.AdvancingSteps));
        }
        double firstRun = elapsed.Elapsed.TotalMilliseconds;
        elapsed.Restart();
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(state), WorldSnapshotCodec.Encode(authority.InspectForHost().State));
            ulong due = Node(state).RespawnAtAdvancingStep!.Value;
            while (state.AdvancingSteps + 1 < due)
            {
                state = authority.Commit(Batch(state)).Candidate;
            }
            Assert.Equal(0, Node(state).RemainingSwings);
            Assert.Equal(GatherNodeVisibility.Absent, Node(state).VisibilityAt(state.AdvancingSteps));
            state = authority.Commit(Batch(state)).Candidate;
            Assert.Equal(due, state.AdvancingSteps);
            Assert.Equal(5, Node(state).RemainingSwings);
            Assert.Equal(5UL, Node(state).NextUnissuedUnit);
            WorldStepDecision harvested = authority.Commit(Batch(state, Swing(state, 5)));
            Assert.Equal(CommandReceiptStatus.Applied, harvested.Receipts.Single().Status);
            state = harvested.Candidate;
            Assert.Equal(6UL, Node(state).NextUnissuedUnit);
            var lots = state.Inventory.Custody.Lots.Where(lot => lot.Source.SourceId == NodeSourceId)
                .OrderBy(lot => lot.Source.Start).ToArray();
            Assert.Equal(6, lots.Length);
            Assert.Equal(Enumerable.Range(0, 6).Select(value => (ulong)value), lots.Select(lot => lot.Source.Start));
            Assert.Equal(Enumerable.Range(1, 6).Select(value => (ulong)value), lots.Select(lot => lot.Source.End));
        }
        double regrowthRun = elapsed.Elapsed.TotalMilliseconds;
        elapsed.Restart();
        using (SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(state), WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
        }
        output.WriteLine("Finite gathering trace: steps={0}; stateBytes={1}; firstDepletionMs={2:F3}; regrowthMs={3:F3}; finalReplayMs={4:F3}; databaseBytes={5}",
            state.AdvancingSteps, WorldSnapshotCodec.Encode(state).Length, firstRun, regrowthRun,
            elapsed.Elapsed.TotalMilliseconds, new FileInfo(files.Path).Length);
    }
}
