using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;

namespace OpenFantasy.Hosting.Tests.Simulation.PublicMaterialDescriptions;

public sealed class ProducedLotAvailabilityTests
{
    [Theory]
    [InlineData(LotProtection.None, false, true)]
    [InlineData(LotProtection.Favorite, false, false)]
    [InlineData(LotProtection.UserReserved, false, false)]
    [InlineData(LotProtection.None, true, false)]
    public void Home_transformed_stock_reports_actual_protection_and_reservation(
        LotProtection protection, bool reserved, bool available)
    {
        var gathering = GatheringWorldFixture.Create();
        var admitted = new WorldProductionMigrationProcessor().Decide(gathering, ProductionUpgradeFixture.Request(gathering)).Candidate;
        var result = new WorldStepProcessor().Decide(admitted,
            GatheringWorldFixture.Batch(admitted, ProductionUpgradeFixture.Craft(admitted)));
        Assert.Equal(WorldActionRejection.None, Assert.Single(result.Receipts).Rejection);
        var crafted = result.Candidate;
        var produced = Assert.Single(crafted.Production!.Lots);
        var inventory = crafted.Inventory;
        EntityId? reservation = reserved ? WorldServiceFixture.Id(98_001) : null;
        if (reservation is { } id)
            inventory = new CountedAuthorityState(inventory.Custody, inventory.AdmittedUnitCapacity,
                inventory.Actors, inventory.Controls, inventory.Policies, inventory.Sources,
                inventory.ReservationIds.Append(id));
        // A presentation calibration over actual crafted provenance, not a gameplay protection transition.
        var production = new ProductionSnapshot(crafted.WorldId, crafted.BranchId, crafted.Production.Revision,
            crafted.Production.Recipes, crafted.Production.Batches,
            [new TransformedLot(produced.LotId, produced.CustodianId, produced.Definition,
                produced.Output, produced.Revision, protection, reservation)]);
        var state = new WorldSnapshot(inventory, crafted.Space, crafted.Residents, crafted.AdmissionVersion,
            crafted.Calendar, crafted.Revision, crafted.AdvancingSteps, crafted.Dialogues, crafted.Gathering, production);
        var view = new LocalWorldView(state, WorldServiceFixture.Id(10));
        var lot = Assert.Single(view.PlayerInventory.Lots, item => item.LotId == produced.LotId);
        Assert.Equal(IngredientLotKind.Transformed, lot.Provenance);
        Assert.Equal(produced.Quantity, lot.Quantity);
        Assert.Equal(available, lot.AvailableForDelivery);
        var replica = PlayerViewCodec.Decode(PlayerViewCodec.EncodeWithMaterialDescriptions(view));
        Assert.Equal(lot, Assert.Single(replica.PlayerInventory.Lots, item => item.LotId == produced.LotId));
    }
}
