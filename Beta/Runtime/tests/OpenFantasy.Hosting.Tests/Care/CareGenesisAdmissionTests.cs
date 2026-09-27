using OpenFantasy.Core;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed class CareGenesisAdmissionTests
{
    [Fact]
    public void Structurally_conserved_crafted_stock_with_zero_production_revision_is_not_fresh_genesis()
    {
        var fixture = new CareWorldFixture(craftedDose: true);
        var fresh = fixture.Genesis;
        var recipe = fresh.Production!.Recipes.Single(value => value.Outputs.Any(term => value.ItemFor(term) == fixture.Potion));
        var berries = fresh.Inventory.Custody.GetLot(fixture.BerryLot);
        var stock = fresh.Inventory.Custody.GetStorage(fixture.HelperStorage);
        var crafted = new InstantCraftPlanner().Plan(fresh.Inventory, fresh.Production, fixture.DeliveryFor(fixture.Helper),
            fixture.Helper, new EntityId(0x636172652d67656e, 1), new CraftRequest(recipe.DefinitionId, recipe.Version,
                fresh.Production.Revision, fixture.HelperStorage, stock.Revision,
                [new IngredientRequest(IngredientLotKind.Source, berries.LotId, berries.Revision, 3)]));
        Assert.Equal(WorldActionRejection.None, crafted.Rejection);
        Assert.Single(crafted.CandidateProduction.Batches);
        Assert.Single(crafted.CandidateProduction.Lots);

        // The transformation's real retired inputs and live output remain conserved. Structural
        // production restoration permits this revision, but new-world authority must not import it.
        var resetProduction = new ProductionSnapshot(fresh.WorldId, fresh.BranchId, 0,
            crafted.CandidateProduction.Recipes, crafted.CandidateProduction.Batches,
            crafted.CandidateProduction.Lots, crafted.CandidateProduction.ConsumedItems);
        resetProduction.ValidateForAuthority(crafted.CandidateInventory);
        var oldCapacity = fresh.Reservoirs!.Capacity;
        Assert.Empty(oldCapacity.Pending);
        var domain = oldCapacity.Domain;
        var closedReservation = Assert.Single(oldCapacity.Closed);
        var dryRequest = new WatershedRunoffRequest(domain.Network, domain.Mapping, crafted.CandidateInventory,
            resetProduction, closedReservation, domain.OwnerActorId, fresh.Calendar,
            fresh.Calendar.AddMilliseconds(1), domain.Material,
            domain.Network.Basins.Select(basin => WatershedRunoffInput.Dry(basin.BasinId)), domain.Terminals);
        var capacity = WatershedRunoffCapacityState.Create(new WatershedRunoffPlanner().Prepare(dryRequest,
            TestContext.Current.CancellationToken));
        capacity = capacity.PlanCancel(closedReservation).ApplyTo(capacity);
        var reservoirs = LiquidReservoirState.Create(capacity, fresh.Reservoirs.Geometries);
        var topology = TerrainLiquidTopology.Create(reservoirs, fresh.Space.Regions, fresh.ReservoirSeals!);
        var cells = LiquidCellState.Create(reservoirs, fresh.LiquidCells!.Profile, fresh.Calendar,
            fresh.LiquidCells.Portions, topology);
        var imported = new WorldSnapshot(crafted.CandidateInventory, fresh.Space, fresh.Residents,
            fresh.AdmissionVersion, fresh.Calendar, 0, 0, gathering: fresh.Gathering,
            production: resetProduction, progression: fresh.Progression, quests: fresh.Quests,
            construction: fresh.Construction, reservoirs: reservoirs, excavation: fresh.Excavation,
            reservoirSeals: fresh.ReservoirSeals, liquidCells: cells, pacing: fresh.Pacing,
            encounterLifecycle: fresh.EncounterLifecycle, care: fresh.Care, resourceHistory: fresh.ResourceHistory);
        byte[] bytes = WorldSnapshotCodec.Encode(imported);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));
        Assert.Equal(0UL, imported.Revision);
        Assert.Equal(0UL, imported.Production!.Revision);
        Assert.Equal(0UL, imported.Care!.Revision);
        Assert.Equal(0UL, imported.Progression!.Revision);
        Assert.Empty(imported.ResourceHistory!.History.Batches);
        Assert.Empty(imported.Production.ConsumedItems);
        Assert.Equal(1UL, imported.Production.GetStoredQuantity(fixture.HelperStorage));
        Assert.Equal(3UL, imported.Inventory.Custody.GetLot(fixture.BerryLot).Quantity);

        using var files = new StorageFiles();
        var refused = Assert.Throws<ArgumentException>(() => SqliteWorldAuthority.CreateNew(files.Path, imported));
        Assert.Contains("unearned material", refused.Message);
        Assert.False(File.Exists(files.Path));
        using (var database = WorldDatabase.Open(files.Path, create: true, schemaVersion: 7))
        {
            database.Initialize(bytes);
        }
        byte[] closed = File.ReadAllBytes(files.Path);
        var failedRecovery = Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path));
        Assert.Contains("unearned material", Assert.IsType<ArgumentException>(failedRecovery.InnerException).Message);
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
    }
}
