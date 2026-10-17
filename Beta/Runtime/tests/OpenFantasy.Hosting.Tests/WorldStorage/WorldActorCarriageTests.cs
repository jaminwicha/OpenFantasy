using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class WorldActorCarriageTests
{
    [Fact]
    public void Actual_movement_retains_cargo_and_a_later_gift_changes_only_the_new_projection()
    {
        using var files = new StorageFiles();
        var genesis = WorldServiceFixture.Genesis();
        var actor = WorldServiceFixture.Id(10);
        var recipient = WorldServiceFixture.Id(11);
        var initial = WorldActorCarriage.Capture(genesis, actor);
        var originalLot = Assert.Single(initial.CountedLots);
        Assert.Equal(6UL, originalLot.Quantity);
        Assert.Throws<ArgumentException>(() => WorldActorCarriage.Capture(genesis, WorldServiceFixture.Id(999)));
        WorldSnapshot final;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            var moved = authority.Commit(WorldServiceFixture.Step(genesis, cause: 901,
                movement: [new(WorldServiceFixture.Delivery, actor, -32, 0)])).Candidate;
            Assert.Equal(0, genesis.Space.GetActor(actor).Feet.Cell.X);
            Assert.Equal(-1, moved.Space.GetActor(actor).Feet.Cell.X);
            var afterMove = WorldActorCarriage.Capture(moved, actor);
            Assert.Equal(initial.WorldId, afterMove.WorldId);
            Assert.Equal(initial.BranchId, afterMove.BranchId);
            Assert.Equal(actor, afterMove.ActorId);
            Assert.Equal(moved.Revision, afterMove.WorldRevision);
            Assert.Equal(initial.CustodianIds, afterMove.CustodianIds);
            Assert.Same(moved.Inventory.Custody.GetLot(originalLot.LotId), Assert.Single(afterMove.CountedLots));
            Assert.Equal(AuthorityEncodingV1.Encode(genesis.Inventory), AuthorityEncodingV1.Encode(moved.Inventory));
            initial.ValidateCurrent(moved);

            var gift = authority.Commit(WorldServiceFixture.Step(moved, cause: 902,
                actions: [WorldServiceFixture.Give(moved, command: 903, quantity: 2)]));
            Assert.Equal(WorldActionRejection.None, Assert.Single(gift.Receipts).Rejection);
            final = gift.Candidate;
            Assert.Throws<InvalidOperationException>(() => initial.ValidateCurrent(final));
            var senderCargo = WorldActorCarriage.Capture(final, actor);
            var recipientCargo = WorldActorCarriage.Capture(final, recipient);
            var retained = Assert.Single(senderCargo.CountedLots);
            var delivered = Assert.Single(recipientCargo.CountedLots);
            Assert.Equal(4UL, retained.Quantity);
            Assert.Equal(2UL, delivered.Quantity);
            Assert.Equal(originalLot.Source.SourceId, retained.Source.SourceId);
            Assert.Equal(originalLot.Source.SourceId, delivered.Source.SourceId);
            Assert.Equal(originalLot.Source.Start, delivered.Source.Start);
            Assert.Equal(delivered.Source.End, retained.Source.Start);
            Assert.Equal(originalLot.Source.End, retained.Source.End);
            Assert.Equal(6UL, Assert.Single(initial.CountedLots).Quantity);
            Assert.Equal(6UL, Assert.Single(afterMove.CountedLots).Quantity);
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
        Assert.Equal(final.ContentHash, reopened.ReadStateForHost().ContentHash);
        Assert.Equal(4UL, Assert.Single(WorldActorCarriage.Capture(reopened.ReadStateForHost(), actor).CountedLots).Quantity);
        Assert.Equal(2UL, Assert.Single(WorldActorCarriage.Capture(reopened.ReadStateForHost(), recipient).CountedLots).Quantity);
    }

    [Fact]
    public void Funded_project_stock_travels_with_builder_but_installed_backing_does_not()
    {
        using var files = new StorageFiles();
        var fixture = new StructuralPolicyWorldFixture();
        var actions = fixture.Stairs.Actions;
        var supplied = actions.CreateSource(files.Path, completeQuest: true, genesis: fixture.Genesis);
        var project = supplied.Construction!.Projects.Single();
        var before = WorldActorCarriage.Capture(supplied, project.BuilderActorId);
        var stock = Assert.Single(before.CountedLots, lot => lot.CustodianId == project.ProjectCustodianId);
        Assert.Equal(6UL, stock.Quantity);
        Assert.Equal(project.ReservationId, stock.ReservedBy);
        Assert.Contains(project.ProjectCustodianId, before.CustodianIds);
        UInt128 initialLoad = supplied.StructuralPolicy!.Masses.ActorLoad(supplied, project.BuilderActorId);
        WorldSnapshot final;
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            var world = actions.Act(authority, supplied, fixture.Stairs.Player,
                new CloseDialogueIntent(supplied.Dialogues.Single().DialogueId));
            var controller = new LocalNpcController();
            for (int step = 0; !world.Construction!.Projects.Single().IsCompleted; step++)
            {
                Assert.True(step < 128);
                var result = authority.Commit(actions.Batch(world,
                    controller.SelectActions(world, actions.NextIdentity), controller.SelectMovement(world)));
                Assert.All(result.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
                world = result.Candidate;
            }
            final = world;
        }
        var completed = final.Construction!.Projects.Single();
        var installed = completed.Installed!;
        var after = WorldActorCarriage.Capture(final, project.BuilderActorId);
        Assert.Throws<InvalidOperationException>(() => before.ValidateCurrent(final));
        Assert.DoesNotContain(after.CountedLots, lot => lot.CustodianId == project.ProjectCustodianId);
        Assert.DoesNotContain(after.ProducedLots, lot => lot.LotId == installed.GuardLotId);
        var offcuts = Assert.Single(after.ProducedLots);
        Assert.Equal(project.ProjectCustodianId, offcuts.CustodianId);
        Assert.Equal(3UL, offcuts.Quantity);
        Assert.Null(offcuts.ReservedBy);
        Assert.Equal(1UL, final.Production!.Lots.Single(lot => lot.LotId == installed.GuardLotId).Quantity);
        UInt128 remainingLoad = final.StructuralPolicy!.Masses.ActorLoad(final, project.BuilderActorId);
        Assert.Equal((UInt128)270_000, initialLoad - remainingLoad);
        Assert.Equal(6UL, stock.Quantity);
        Assert.Empty(before.ProducedLots);
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal(final.ContentHash, reopened.ReadStateForHost().ContentHash);
        var restored = WorldActorCarriage.Capture(reopened.ReadStateForHost(), project.BuilderActorId);
        after.ValidateCurrent(reopened.ReadStateForHost());
        Assert.Equal(after.CustodianIds, restored.CustodianIds);
        Assert.Equal(after.ProducedLots.Select(lot => (lot.LotId, lot.CustodianId, lot.Quantity, lot.Output)),
            restored.ProducedLots.Select(lot => (lot.LotId, lot.CustodianId, lot.Quantity, lot.Output)));
    }
}
