using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Geography;

/// <summary>Authored finite source stock; optional produced stock is earned by the actual craft planner.</summary>
internal sealed class LiquidCellPlannerFixture
{
    internal static EntityId Id(ulong value) => new(0x6c697163656c6c73, value);
    internal static readonly EntityId Reservoir = Id(10), Store = Id(11), Source = Id(12), Owner = Id(13),
        Budget = Id(14), SourceLot = Id(15), Client = Id(16), Session = Id(17);
    internal LiquidReservoirState Reservoirs { get; }
    internal EntityId? ProducedLot { get; }

    internal LiquidCellPlannerFixture(IEnumerable<ReservoirColumn> columns, long spillZ, ulong units,
        ulong microlitresPerUnit = 100_000_000, ulong producedUnits = 0)
    {
        var scope = new TerrainScope(Id(1), Id(2), Id(3));
        var basin = new WatershedPlanner().Prepare(new(scope, Id(4), new(0, 0, 1, 1), new(0, 0), 0, Id(5)),
            TestContext.Current.CancellationToken);
        var network = new WatershedNetworkPlanner().Prepare(Id(6), [basin],
            [WatershedOutletClosure.ToTerminal(basin.BasinId, 1)], TestContext.Current.CancellationToken);
        var geometry = new ReservoirGeometry(scope, Reservoir, Store, spillZ, columns);
        var water = new CountedItemDefinition(Id(20), ContentHash.Compute("cell-planner-water"u8));
        var material = new ItemPhysicalBinding(water, new(new PackageId("openfantasy.liquidcelltests"), Id(21),
            "cell_water", new(1), new(microlitresPerUnit), new(0)));
        CountedSourceLot[] lots = units == 0 ? Array.Empty<CountedSourceLot>()
            : [new CountedSourceLot(SourceLot, Store, water, new(Source, 0, units), 0)];
        var inventory = new CountedAuthorityState(new(scope.WorldId, scope.BranchId, 0,
            [new(Store, Math.Max(1UL, units), 0)], lots), Math.Max(1UL, units), [Owner], [new(Client, Session, Owner)],
            [new(Store, Owner, CustodyDepositRule.OwnerOnly)], [new(Source, water, Math.Max(1UL, units))], [Budget]);
        ProductionSnapshot? production = null;
        if (producedUnits > 0)
        {
            var physicalRecipe = new MaterialRecipeDefinition(material.Physical.Package, Id(22), "repack_cell_water", new(1), null,
                new MaterialCatalog([material.Physical]), [new(RecipeMaterialRole.Input, material.Physical.Pin(), producedUnits),
                    new(RecipeMaterialRole.Output, material.Physical.Pin(), producedUnits)]);
            var recipe = new CraftRecipeDefinition(Id(23), physicalRecipe, [material], [new(water, 0, material.Physical.MassPerUnit)]);
            production = new(scope.WorldId, scope.BranchId, 0, [recipe], [], []);
            var craft = new InstantCraftPlanner().Plan(inventory, production, new(Client, Session), Owner, Id(24),
                new(recipe.DefinitionId, recipe.Version, 0, Store, 0, [new(IngredientLotKind.Source, SourceLot, 0, producedUnits)]));
            Assert.Equal(WorldActionRejection.None, craft.Rejection);
            inventory = craft.CandidateInventory;
            production = craft.CandidateProduction;
            ProducedLot = Assert.Single(production.Lots).LotId;
        }
        var request = new WatershedRunoffRequest(network, new(1, 1, 0), inventory, production, Budget, Owner,
            new(0, 0), new(0, 1), material, [WatershedRunoffInput.Dry(basin.BasinId)],
            [new(basin.BasinId, Store, geometry.Capacity)]);
        var capacity = WatershedRunoffCapacityState.Create(new WatershedRunoffPlanner().Prepare(request,
            TestContext.Current.CancellationToken));
        capacity = capacity.PlanCancel(Budget).ApplyTo(capacity);
        Reservoirs = LiquidReservoirState.Create(capacity, [geometry]);
    }

    internal static LiquidCellKey Cell(long x, long y = 0, long z = 0) => new(Reservoir, new(x, y, z));

    internal static LiquidCellPortion Raw(LiquidCellKey cell, ulong start, ulong end) =>
        new(cell, LiquidStockOrigin.RawSource, Source, start, end);

    internal LiquidCellState Place(IEnumerable<LiquidCellPortion> portions, ulong downward = 10, ulong lateral = 10) =>
        LiquidCellState.Create(Reservoirs, new(downward, lateral), new(0, 0), portions);

    internal LiquidCellState PlaceAll(LiquidCellKey cell, ulong downward = 10, ulong lateral = 10) => Place(
        Reservoirs.Capacity.Inventory.Custody.Lots.Select(l => new LiquidCellPortion(cell, LiquidStockOrigin.RawSource,
                l.Source.SourceId, l.Source.Start, l.Source.End))
            .Concat(Reservoirs.Capacity.Production?.Lots.Select(l => new LiquidCellPortion(cell, LiquidStockOrigin.ProducedLot,
                l.LotId, 0, l.Quantity)) ?? []), downward, lateral);
}
