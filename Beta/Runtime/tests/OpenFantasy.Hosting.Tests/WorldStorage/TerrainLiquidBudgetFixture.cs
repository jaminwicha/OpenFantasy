using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

/// <summary>
/// Independently funded one-microlitre units in known, vented terrain channels. Optional produced
/// water comes from a real craft plan; that case is a supplied revision-one root, not fresh migration.
/// </summary>
internal sealed class TerrainLiquidBudgetFixture
{
    internal static EntityId Id(ulong value) => new(0x746572726c627564, value);
    internal static readonly EntityId Reservoir = Id(10), Store = Id(11), Source = Id(12),
        Budget = Id(14), SourceLot = Id(15), OtherReservoir = Id(30), OtherStore = Id(31),
        OtherSource = Id(32), OtherSourceLot = Id(35);
    internal ExcavationWorldFixture SourceFixture { get; } = new();
    internal ConstructionUpgradeFixture Actions => SourceFixture.Actions;
    internal WorldSnapshot Basis { get; }
    internal LiquidReservoirState Reservoirs { get; }
    internal ReservoirSealPolicy Seals { get; }
    internal LiquidCellState Cells { get; }
    internal EntityId? ProducedLot { get; }
    internal ulong TotalUnits { get; }

    internal TerrainLiquidBudgetFixture(ulong primaryUnits, ulong secondaryUnits = 0,
        ulong producedUnits = 0, bool fragmented = false)
    {
        if (primaryUnits == 0 || producedUnits > primaryUnits || fragmented && (producedUnits != 0 || secondaryUnits != 0))
        {
            throw new ArgumentException("The fixture requires funded positive primary stock and an unambiguous placement.");
        }
        WorldSnapshot original = SourceFixture.Basis;
        EntityId owner = SourceFixture.Player;
        TotalUnits = checked(primaryUnits + secondaryUnits);
        var water = new CountedItemDefinition(Id(20), ContentHash.Compute("terrain-liquid-budget-water-v1"u8));
        var material = new ItemPhysicalBinding(water, new(new PackageId("openfantasy.terrainliquidbudget"),
            Id(21), "water", new(1), new(1), new(0)));
        var storages = original.Inventory.Custody.Storages.Append(new(Store, primaryUnits, 0));
        var lots = original.Inventory.Custody.Lots.Append(new(SourceLot, Store, water, new(Source, 0, primaryUnits), 0));
        var policies = original.Inventory.Policies.Append(new(Store, owner, CustodyDepositRule.OwnerOnly));
        var sources = original.Inventory.Sources.Append(new(Source, water, primaryUnits));
        if (secondaryUnits != 0)
        {
            storages = storages.Append(new(OtherStore, secondaryUnits, 0));
            lots = lots.Append(new(OtherSourceLot, OtherStore, water, new(OtherSource, 0, secondaryUnits), 0));
            policies = policies.Append(new(OtherStore, owner, CustodyDepositRule.OwnerOnly));
            sources = sources.Append(new(OtherSource, water, secondaryUnits));
        }
        var inventory = new CountedAuthorityState(new(original.WorldId, original.BranchId, 0, storages, lots),
            checked(original.Inventory.AdmittedUnitCapacity + TotalUnits), original.Inventory.Actors,
            original.Inventory.Controls, policies, sources, original.Inventory.ReservationIds.Append(Budget));
        ProductionSnapshot production = original.Production!;
        if (producedUnits != 0)
        {
            var physicalRecipe = new MaterialRecipeDefinition(material.Physical.Package, Id(22), "repack_water", new(1), null,
                new MaterialCatalog([material.Physical]),
                [new(RecipeMaterialRole.Input, material.Physical.Pin(), producedUnits),
                    new(RecipeMaterialRole.Output, material.Physical.Pin(), producedUnits)]);
            var recipe = new CraftRecipeDefinition(Id(23), physicalRecipe, [material],
                [new(water, 0, material.Physical.MassPerUnit)]);
            production = new(original.WorldId, original.BranchId, 0, production.Recipes.Append(recipe), [], []);
            var craft = new InstantCraftPlanner().Plan(inventory, production,
                ConstructionUpgradeFixture.Delivery(original, owner), owner, Id(24),
                new(recipe.DefinitionId, recipe.Version, 0, Store, 0,
                    [new(IngredientLotKind.Source, SourceLot, 0, producedUnits)]));
            Assert.Equal(WorldActionRejection.None, craft.Rejection);
            inventory = craft.CandidateInventory;
            production = craft.CandidateProduction;
            ProducedLot = Assert.Single(production.Lots).LotId;
        }

        TerrainRegionSnapshot retained = original.Space.Regions[0];
        CellCoordinate[] holes = Enumerable.Range(10, 3).Select(x => new CellCoordinate(x, 0, 0))
            .Concat(secondaryUnits == 0 ? [] : Enumerable.Range(16, 3).Select(x => new CellCoordinate(x, 0, 0))).ToArray();
        var channel = new StratifiedTerrainGenerator().Generate(original.Space.Scope, retained.Seed, retained.Rules,
            new(new(9, -1, -2), 11, 3, 5)).Excavate(0, holes);
        Assert.All(holes, cell =>
        {
            Assert.False(channel.GetCell(cell).IsSolid);
            Assert.False(channel.GetCell(new(cell.X, cell.Y, 1)).IsSolid);
            Assert.True(channel.GetCell(new(cell.X, cell.Y, -1)).IsSolid);
        });
        var space = new LocalSpatialSnapshot(original.Space.Scope, original.Space.Regions.Append(channel), original.Space.Actors);
        Basis = new(inventory, space, original.Residents, original.AdmissionVersion, original.Calendar,
            producedUnits == 0 ? 0UL : 1UL, 0, gathering: original.Gathering, production: production,
            progression: original.Progression, quests: original.Quests, construction: original.Construction);

        var basin = new WatershedPlanner().Prepare(new(space.Scope, Id(4), new(0, 0, 1, 1),
            new(0, 0), 0, Id(5)), TestContext.Current.CancellationToken);
        var network = new WatershedNetworkPlanner().Prepare(Id(6), [basin],
            [WatershedOutletClosure.ToTerminal(basin.BasinId, 1)], TestContext.Current.CancellationToken);
        var primaryGeometry = new ReservoirGeometry(space.Scope, Reservoir, Store, 1,
            [new(10, 0, 0), new(11, 0, 0), new(12, 0, 0)]);
        var geometries = new List<ReservoirGeometry> { primaryGeometry };
        if (secondaryUnits != 0)
        {
            geometries.Add(new(space.Scope, OtherReservoir, OtherStore, 1,
                [new(16, 0, 0), new(17, 0, 0), new(18, 0, 0)]));
        }
        var request = new WatershedRunoffRequest(network, new(1, 1, 0), inventory, production, Budget, owner,
            Basis.Calendar, Basis.Calendar.AddMilliseconds(1), material,
            [WatershedRunoffInput.Dry(basin.BasinId)], [new(basin.BasinId, Store, primaryGeometry.Capacity)]);
        var capacity = WatershedRunoffCapacityState.Create(new WatershedRunoffPlanner().Prepare(request,
            TestContext.Current.CancellationToken));
        capacity = capacity.PlanCancel(Budget).ApplyTo(capacity);
        Reservoirs = LiquidReservoirState.Create(capacity, geometries);
        Seals = new(water, new[] { retained.Rules.Materials.Surface, retained.Rules.Materials.Soil,
            retained.Rules.Materials.Stone }.Distinct().Select(item => new ReservoirSealRule(item, 1000)));
        TerrainLiquidTopology topology = TerrainLiquidTopology.Create(Reservoirs, space.Regions, Seals);
        var portions = new List<LiquidCellPortion>();
        if (fragmented)
        {
            for (ulong index = 0; index < primaryUnits; index++)
            {
                portions.Add(new(Cell(index % 2 == 0 ? 10 : 12), LiquidStockOrigin.RawSource, Source, index, index + 1));
            }
        }
        else
        {
            portions.AddRange(inventory.Custody.Lots.Where(lot => lot.CustodianId == Store || lot.CustodianId == OtherStore)
                .Select(lot => new LiquidCellPortion(lot.CustodianId == Store ? Cell(10) : OtherCell(16),
                    LiquidStockOrigin.RawSource, lot.Source.SourceId, lot.Source.Start, lot.Source.End)));
            portions.AddRange(production.Lots.Where(lot => lot.CustodianId == Store)
                .Select(lot => new LiquidCellPortion(Cell(10), LiquidStockOrigin.ProducedLot, lot.LotId, 0, lot.Quantity)));
        }
        Cells = LiquidCellState.Create(Reservoirs, new(1, 1), Basis.Calendar, portions, topology);
    }

    internal WorldSnapshot CreateFreshGenesis() => WorldTerrainLiquidAdmission.CreateGenesis(Basis, Reservoirs,
        Seals, new([SourceFixture.Site]), Cells);

    internal WorldSnapshot CreateSuppliedRoot() => new(Basis.Inventory, Basis.Space, Basis.Residents,
        Basis.AdmissionVersion, Basis.Calendar, Basis.Revision, Basis.AdvancingSteps, gathering: Basis.Gathering,
        production: Basis.Production, progression: Basis.Progression, quests: Basis.Quests, construction: Basis.Construction,
        reservoirs: Reservoirs, excavation: new([SourceFixture.Site]), reservoirSeals: Seals, liquidCells: Cells);

    internal static LiquidCellKey Cell(long x) => new(Reservoir, new(x, 0, 0));
    internal static LiquidCellKey OtherCell(long x) => new(OtherReservoir, new(x, 0, 0));
}
