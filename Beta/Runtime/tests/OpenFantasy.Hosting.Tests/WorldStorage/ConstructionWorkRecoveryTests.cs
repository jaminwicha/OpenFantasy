using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ConstructionWorkRecoveryTests
{
    [Fact]
    public void Ordinary_work_reopens_at_the_exact_partial_step_and_installs_one_funded_object()
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        var fixture = new ConstructionUpgradeFixture();
        WorldSnapshot original = fixture.CreateSource(source.Path, completeQuest: true);
        EntityId player = original.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        byte[] questBytes = QuestCodecV1.Encode(original.Quests!);
        byte[] progressionBytes = ProgressionCodecV1.Encode(original.Progression!);
        byte[] partialBytes;
        WorldStepBatch retainedBatch;
        WorldActionCommand retainedCommand;
        WorldActionReceipt retainedReceipt;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.UpgradeQuestsToConstruction(
            source.Path, destination.Path, fixture.Request(original)))
        {
            WorldSnapshot world = authority.InspectForHost().State;
            world = fixture.Act(authority, world, player, new CloseDialogueIntent(world.Dialogues.Single().DialogueId));
            world = AdvanceUntil(authority, fixture, world, 40);
            Assert.Equal(40UL, Project(world).Progress.CompletedUnits);
            Assert.Empty(world.Space.Structures);
            Assert.Empty(world.Production!.Batches);

            ConstructionProject project = Project(world);
            world = fixture.Act(authority, world, player, new SpeakToActorIntent(project.BuilderActorId,
                world.Space.GetActor(project.BuilderActorId).Revision));
            byte[] heldBytes = WorldSnapshotCodec.Encode(world);
            var controller = new LocalNpcController();
            for (int step = 0; step < 12; step++)
            {
                Assert.Empty(controller.SelectMovement(world));
                Assert.Empty(controller.SelectActions(world, () => throw new InvalidOperationException("Hold allocated work.")));
            }
            Assert.Equal(heldBytes, WorldSnapshotCodec.Encode(authority.InspectForHost().State));
            world = fixture.Act(authority, world, player, new CloseDialogueIntent(world.Dialogues.Single().DialogueId));
            retainedBatch = NextBatch(fixture, world);
            retainedCommand = Assert.Single(retainedBatch.Actions);
            WorldStepDecision decision = authority.Commit(retainedBatch);
            retainedReceipt = Assert.Single(decision.Receipts);
            Assert.Equal(WorldActionRejection.None, retainedReceipt.Rejection);
            Assert.Equal(41UL, Project(decision.Candidate).Progress.CompletedUnits);
            partialBytes = WorldSnapshotCodec.Encode(decision.Candidate);
        }

        byte[] completedBytes;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(destination.Path))
        {
            WorldSnapshot world = authority.InspectForHost().State;
            Assert.Equal(partialBytes, WorldSnapshotCodec.Encode(world));
            WorldActionReceipt receipt = Assert.IsType<WorldActionReceipt>(authority.FindReceipt(retainedCommand,
                ConstructionUpgradeFixture.Delivery(world, retainedCommand.ActorId)));
            Assert.Equal(WorldReceiptCodecV1.Encode(retainedReceipt), WorldReceiptCodecV1.Encode(receipt));
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(retainedBatch));
            Assert.Equal(partialBytes, WorldSnapshotCodec.Encode(authority.InspectForHost().State));
            world = AdvanceUntil(authority, fixture, world, 200);
            ConstructionProject complete = Project(world);
            Assert.True(complete.IsCompleted);
            Assert.Equal(questBytes, QuestCodecV1.Encode(world.Quests!));
            Assert.Equal(progressionBytes, ProgressionCodecV1.Encode(world.Progression!));
            Assert.Single(world.Space.Structures);
            Assert.Single(world.Production!.Batches);
            Assert.DoesNotContain(world.Inventory.Custody.Lots, lot => lot.ReservedBy == complete.ReservationId);
            TransformedLot guard = Assert.Single(world.Production.Lots, lot => lot.ReservedBy == complete.ReservationId);
            Assert.Equal(1UL, guard.Quantity);
            Assert.Equal(complete.Installed!.GuardLotId, guard.LotId);
            Assert.Equal(3UL, Assert.Single(world.Production.Lots, lot => lot.ReservedBy is null).Quantity);
            completedBytes = WorldSnapshotCodec.Encode(world);
        }

        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(destination.Path))
        {
            WorldSnapshot world = authority.InspectForHost().State;
            Assert.Equal(completedBytes, WorldSnapshotCodec.Encode(world));
            var controller = new LocalNpcController();
            EntityId builder = Project(world).BuilderActorId;
            Assert.True(new LocalConstructionController().NeedsReturnHome(world, world.GetResident(builder)));
            for (int step = 0; step < 64; step++)
            {
                Assert.Empty(controller.SelectActions(world, fixture.NextIdentity));
                world = authority.Commit(fixture.Batch(world, [], controller.SelectMovement(world))).Candidate;
            }
            Assert.False(new LocalConstructionController().NeedsReturnHome(world, world.GetResident(builder)));
            Assert.Equal(200UL, Project(world).Progress.CompletedUnits);
            Assert.Single(world.Production!.Batches);
            Assert.Single(world.Space.Structures);
            Assert.Equal(questBytes, QuestCodecV1.Encode(world.Quests!));
            Assert.Equal(progressionBytes, ProgressionCodecV1.Encode(world.Progression!));
        }
        using SqliteWorldAuthority preserved = SqliteWorldAuthority.OpenExisting(source.Path);
        Assert.Equal(original.ContentHash, preserved.InspectForHost().State.ContentHash);
    }

    [Theory]
    [InlineData((int)AuthorityCommitBoundary.BeforeCommit)]
    [InlineData((int)AuthorityCommitBoundary.AfterCommit)]
    public void Interrupted_final_work_step_recovers_all_material_geometry_and_work_together(int boundaryValue)
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        var fixture = new ConstructionUpgradeFixture();
        WorldSnapshot original = fixture.CreateSource(source.Path, completeQuest: true);
        EntityId player = original.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        WorldStepBatch finalBatch;
        byte[] before;
        var boundary = (AuthorityCommitBoundary)boundaryValue;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.UpgradeQuestsToConstruction(
            source.Path, destination.Path, fixture.Request(original)))
        {
            WorldSnapshot world = authority.InspectForHost().State;
            world = fixture.Act(authority, world, player, new CloseDialogueIntent(world.Dialogues.Single().DialogueId));
            world = AdvanceUntil(authority, fixture, world, 199);
            before = WorldSnapshotCodec.Encode(world);
            finalBatch = NextBatch(fixture, world);
            Assert.Single(finalBatch.Actions);
            Assert.Throws<IOException>(() => authority.Commit(finalBatch, at =>
            {
                if (at == boundary) throw new IOException("interrupted final joinery transaction");
            }));
        }
        using SqliteWorldAuthority recovered = SqliteWorldAuthority.OpenExisting(destination.Path);
        WorldSnapshot reopened = recovered.InspectForHost().State;
        if (boundary == AuthorityCommitBoundary.BeforeCommit)
        {
            Assert.Equal(before, WorldSnapshotCodec.Encode(reopened));
            Assert.Empty(reopened.Space.Structures);
            Assert.Empty(reopened.Production!.Batches);
            reopened = recovered.Commit(finalBatch).Candidate;
        }
        else
        {
            Assert.Throws<AuthorityDispatchException>(() => recovered.Commit(finalBatch));
        }
        Assert.Equal(200UL, Project(reopened).Progress.CompletedUnits);
        Assert.True(Project(reopened).IsCompleted);
        Assert.Single(reopened.Space.Structures);
        Assert.Single(reopened.Production!.Batches);
        Assert.Equal(2, reopened.Production.Lots.Count);
        Assert.DoesNotContain(reopened.Inventory.Custody.Lots, lot => lot.ReservedBy == Project(reopened).ReservationId);
    }

    private static ConstructionProject Project(WorldSnapshot world) => world.Construction!.Projects.Single();

    private static WorldSnapshot AdvanceUntil(SqliteWorldAuthority authority, ConstructionUpgradeFixture fixture,
        WorldSnapshot world, ulong target)
    {
        for (int step = 0; Project(world).Progress.CompletedUnits < target; step++)
        {
            Assert.True(step < 512, $"Ordinary builder failed to reach work {target}; current {Project(world).Progress.CompletedUnits}.");
            WorldStepDecision decision = authority.Commit(NextBatch(fixture, world));
            Assert.All(decision.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
            world = decision.Candidate;
        }
        return world;
    }

    private static WorldStepBatch NextBatch(ConstructionUpgradeFixture fixture, WorldSnapshot world)
    {
        var controller = new LocalNpcController();
        return fixture.Batch(world, controller.SelectActions(world, fixture.NextIdentity), controller.SelectMovement(world));
    }
}
