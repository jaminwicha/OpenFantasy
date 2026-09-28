using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ExcavationContinuationStaticOwnerTests
{
    [Fact]
    public void Actually_funded_and_completed_construction_retains_its_backing_and_support_across_two_cuts()
    {
        var fixture = new ConstructedSupportWorldFixture();
        using var files = new StorageFiles();
        var world = fixture.Actions.CreateSource(files.Path, completeQuest: true, genesis: fixture.Genesis);
        using var authority = SqliteWorldAuthority.OpenExisting(files.Path);
        world = fixture.Actions.Act(authority, world, fixture.Player,
            new CloseDialogueIntent(Assert.Single(world.Dialogues).DialogueId));
        world = fixture.BuildPlatform(authority, world);
        Assert.NotNull(Assert.Single(world.Construction!.Projects).Installed);
        Assert.Single(world.Space.Structures);
        var state = MaterialExcavationState.Capture(world);
        for (int index = 0; index < 2; index++)
        {
            state = state.PlanContinue(TestContext.Current.CancellationToken).ApplyTo(state);
            Assert.Single(state.Production.Continuation!.ConstructionBatches);
            Assert.Equal(world.Construction.ContentHash, state.Construction.ContentHash);
            Assert.Equal(world.Space.StructureSupport, state.Space.StructureSupport);
            Assert.Equal(world.Space.GroundSupport, state.Space.GroundSupport);
            Assert.Equal(world.Inventory.ContentHash, state.Inventory.ContentHash);
            Assert.Equal(world.Production!.Lots.Select(lot =>
                (lot.LotId, lot.CustodianId, lot.Definition, lot.Output, lot.Revision, lot.Protection, lot.ReservedBy)),
                state.Production.Lots.Select(lot =>
                    (lot.LotId, lot.CustodianId, lot.Definition, lot.Output, lot.Revision, lot.Protection, lot.ReservedBy)));
            byte[] bytes = MaterialExcavationCodecV1.Encode(state);
            Assert.Equal(bytes, MaterialExcavationCodecV1.Encode(MaterialExcavationCodecV1.Decode(bytes,
                new(state), TestContext.Current.CancellationToken)));
        }
    }

    [Fact]
    public void Generic_reserved_delivery_cannot_mutate_the_captured_construction_owner()
    {
        var journey = new MaterialExcavationFixture();
        journey.Cut();
        journey.Gather();
        var state = journey.State;
        var project = Assert.Single(state.Construction.Projects);
        var recipe = project.Definition.MaterialTransform;
        var item = recipe.ItemFor(recipe.Inputs[0]);
        var lot = journey.Inventory.Custody.Lots.First(value => value.CustodianId == journey.Bag
            && value.Definition == item && value.ReservedBy is null && value.Protection == LotProtection.None);
        var delivery = new MaterialSourceDeliveryPlanner().Plan(journey.Inventory, journey.Production,
            ConstructionUpgradeFixture.Delivery(journey.SourceWorld, journey.Actor), journey.Actor, journey.NextId(),
            new(journey.Bag, journey.Inventory.Custody.GetStorage(journey.Bag).Revision,
                project.ProjectCustodianId, item, 1, project.ReservationId, [new(lot.LotId, lot.Revision, 1)]), journey.Occupied);
        Assert.Equal(WorldActionRejection.None, delivery.Rejection);
        Assert.Contains(delivery.CandidateInventory.Custody.Lots, value => value.ReservedBy == project.ReservationId);
        var refusal = Assert.Throws<ArgumentException>(() => state.PlanReconcile(delivery.CandidateInventory,
            delivery.CandidateProduction, TestContext.Current.CancellationToken));
        Assert.Contains("Static construction", refusal.Message);
        Assert.Same(state, journey.State);
        Assert.DoesNotContain(state.Inventory.Custody.Lots, value => value.ReservedBy == project.ReservationId);
        // An ordinary transfer of the same unreserved stock remains valid and does not change funding.
        journey.Transfer(lot.LotId, journey.OtherBag, 1);
        Assert.Equal(state.Construction.ContentHash, journey.State.Construction.ContentHash);
    }
}
