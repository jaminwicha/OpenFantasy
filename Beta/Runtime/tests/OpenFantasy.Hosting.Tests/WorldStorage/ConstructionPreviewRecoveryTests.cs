using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ConstructionPreviewRecoveryTests
{
    [Theory]
    [InlineData((int)AuthorityCommitBoundary.BeforeCommit)]
    [InlineData((int)AuthorityCommitBoundary.AfterCommit)]
    public void Warm_preview_is_unearned_until_the_same_current_world_transaction_commits(int boundaryValue)
    {
        using var files = new StorageFiles();
        var fixture = new ConstructionUpgradeFixture();
        var genesis = AdventureWorldAdmission.CreateEmbedded(AdventureWorldAdmission.SeedText, ["Rowan"]);
        var supplied = fixture.CreateSource(files.Path, completeQuest: true, genesis: genesis);
        WorldStepBatch batch;
        WorldStepDecision preview;
        byte[] before;
        var boundary = (AuthorityCommitBoundary)boundaryValue;
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            var player = supplied.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player).ActorId;
            var world = fixture.Act(authority, supplied, player,
                new CloseDialogueIntent(supplied.Dialogues.Single().DialogueId));
            var controller = new LocalNpcController();
            for (int step = 0; ; step++)
            {
                Assert.True(step < 512, "The actual funded builder must reach the first work selection.");
                batch = fixture.Batch(world, controller.SelectActions(world, fixture.NextIdentity), controller.SelectMovement(world));
                if (batch.Actions.Any(command => command.Intent is WorkConstructionIntent)) break;
                world = authority.Commit(batch).Candidate;
            }
            Assert.Equal(0UL, world.Construction!.Projects.Single().Progress.CompletedUnits);
            before = WorldSnapshotCodec.Encode(world);
            preview = authority.Preview(batch);
            var repeated = authority.Preview(batch);
            Assert.Equal(WorldSnapshotCodec.Encode(preview.Candidate), WorldSnapshotCodec.Encode(repeated.Candidate));
            Assert.Equal(1UL, preview.Candidate.Construction!.Projects.Single().Progress.CompletedUnits);
            Assert.Equal(before, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            foreach (var command in batch.Actions)
                Assert.Null(authority.FindReceipt(command, ConstructionUpgradeFixture.Delivery(world, command.ActorId)));
            Assert.Throws<IOException>(() => authority.Commit(batch, at =>
            {
                if (at == boundary) throw new IOException("interrupt warmed construction publication");
            }));
        }
        using var recovered = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
        var durable = recovered.ReadStateForHost();
        if (boundary == AuthorityCommitBoundary.BeforeCommit)
        {
            Assert.Equal(before, WorldSnapshotCodec.Encode(durable));
            foreach (var command in batch.Actions)
                Assert.Null(recovered.FindReceipt(command, ConstructionUpgradeFixture.Delivery(durable, command.ActorId)));
            durable = recovered.Commit(batch).Candidate;
        }
        Assert.Equal(WorldSnapshotCodec.Encode(preview.Candidate), WorldSnapshotCodec.Encode(durable));
        Assert.Throws<AuthorityDispatchException>(() => recovered.Commit(batch));
        foreach (var command in batch.Actions)
        {
            var receipt = Assert.IsType<WorldActionReceipt>(recovered.FindReceipt(command,
                ConstructionUpgradeFixture.Delivery(durable, command.ActorId)));
            Assert.Equal(WorldReceiptCodecV1.Encode(preview.Receipts.Single(value => value.CommandId == command.CommandId)),
                WorldReceiptCodecV1.Encode(receipt));
        }
        Assert.Equal(WorldSnapshotCodec.Encode(durable),
            WorldSnapshotCodec.Encode(recovered.ReadRecordedStateForHost(durable.Revision).State));
        Assert.Equal(1UL, durable.Construction!.Projects.Single().Progress.CompletedUnits);
        Assert.Empty(durable.Space.Structures);
        Assert.Empty(durable.Production!.Batches);
    }
}
