using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Geography;

/// <summary>Finite authored water, followed only by actual craft/consume plans and explicit material cuts.</summary>
internal sealed class LiquidMaterialContinuationFixture
{
    internal static EntityId Id(ulong value) => new(0x6c6971636f6e7469, value);
    internal static readonly EntityId Owner = Id(10), Store = Id(11), SourceStore = Id(12),
        Source = Id(13), RawLot = Id(14), HeldLot = Id(15), Budget = Id(16),
        Reservoir = Id(17), SourceReservoir = Id(18), Client = Id(19), Session = Id(20), FreeLot = Id(23);
    private readonly HashSet<EntityId> _occupied = [];

    internal LiquidMaterialContinuationFixture(bool pending = true, bool consumption = true,
        ulong receiverUnitCapacity = 1000, long receiverSpill = 1)
    {
        var scope = new TerrainScope(Id(1), Id(2), Id(3));
        var basin = new WatershedPlanner().Prepare(new(scope, Id(4), new(0, 0, 1, 1), new(0, 0), 0, Id(5)),
            TestContext.Current.CancellationToken);
        var network = new WatershedNetworkPlanner().Prepare(Id(6), [basin],
            [WatershedOutletClosure.ToTerminal(basin.BasinId, 1)], TestContext.Current.CancellationToken);
        var water = new CountedItemDefinition(Id(21), ContentHash.Compute("continued-finite-water"u8));
        Material = new(water, new(new PackageId("openfantasy.liquidcontinuation"), Id(22), "water",
            new(1), new(pending ? 1_000_000UL : 100_000_000UL), new(0)));
        var geometries = pending
            ? new ReservoirGeometry[] { new(scope, Reservoir, Store, receiverSpill, [new(0, 0, 0)]),
                new(scope, SourceReservoir, SourceStore, 2, [new(2, 0, 0)]) }
            : [new ReservoirGeometry(scope, Reservoir, Store, 3, [new(0, 0, 0), new(1, 0, 0)])];
        var storages = pending
            ? new CustodianStorage[] { new(Store, receiverUnitCapacity, 0), new(SourceStore, 2000, 0) }
            : [new CustodianStorage(Store, 10, 0)];
        var lots = pending
            ? new CountedSourceLot[] { new(RawLot, Store, water, new(Source, 900, 1000), 0),
                new(HeldLot, SourceStore, water, new(Source, 0, 900), 0, LotProtection.None, Budget),
                new(FreeLot, SourceStore, water, new(Source, 1000, 1010), 0) }
            : [new CountedSourceLot(RawLot, Store, water, new(Source, 0, 10), 0)];
        var inventory = new CountedAuthorityState(new(scope.WorldId, scope.BranchId, 0, storages, lots),
            pending ? 1010UL : 10UL, [Owner], [new(Client, Session, Owner)],
            storages.Select(value => new CustodianAccessPolicy(value.CustodianId, Owner, CustodyDepositRule.OwnerOnly)),
            [new(Source, water, pending ? 1010UL : 10UL)], [Budget]);
        var production = new ProductionSnapshot(scope.WorldId, scope.BranchId, 0,
            [Recipe(30, 4), Recipe(40, 2)], [], [], consumption ? [] : null);
        var request = new WatershedRunoffRequest(network, new(pending ? 30 : 1, 1, 0), inventory, production,
            Budget, Owner, new(-10, 0), new(-9, 0), Material,
            pending ? [new(basin.BasinId, 1000, new(Source, 0, 900), SourceStore)] : [WatershedRunoffInput.Dry(basin.BasinId)],
            [new(basin.BasinId, Store, geometries[0].Capacity)]);
        var capacity = WatershedRunoffCapacityState.Create(new WatershedRunoffPlanner().Prepare(request,
            TestContext.Current.CancellationToken));
        if (!pending) capacity = capacity.PlanCancel(Budget).ApplyTo(capacity);
        Reservoirs = LiquidReservoirState.Create(capacity, geometries);
    }

    internal LiquidReservoirState Reservoirs { get; private set; }
    internal WatershedRunoffCapacityState Capacity => Reservoirs.Capacity;
    internal CountedAuthorityState Inventory => Capacity.Inventory;
    internal ProductionSnapshot Production => Capacity.Production!;
    internal ItemPhysicalBinding Material { get; }
    internal bool IsOccupied(EntityId identity) => _occupied.Contains(identity);
    internal GeographyProductionDependencies Dependencies => new(Reservoirs,
        Production.MaterialHistory?.GatherEvidence.ToArray());

    internal WatershedRunoffCapacityPlan CutPlan(bool material = false) => material
        ? Capacity.PlanContinueMaterial(cancellationToken: TestContext.Current.CancellationToken)
        : Capacity.PlanContinue(TestContext.Current.CancellationToken);

    internal void Cut(bool material = false)
    {
        var before = Reservoirs;
        Apply(CutPlan(material));
        Assert.Equal(before.Capacity.Revision, Capacity.Revision);
        Assert.Equal(before.Capacity.Inventory.ContentHash, Inventory.ContentHash);
        Assert.Equal(before.Capacity.Production!.Revision, Production.Revision);
        Assert.Equal(before.Capacity.Production.TotalBatchCount, Production.TotalBatchCount);
        Assert.Equal(before.Capacity.Production.TotalConsumedItemCount, Production.TotalConsumedItemCount);
        Assert.Equal(before.Capacity.Production.TotalMaterialOperationCount, Production.TotalMaterialOperationCount);
        Assert.Empty(Production.Batches);
        Assert.Empty(Production.ConsumedItems);
        Assert.Equal(before.Capacity.Production.ContentHash, Production.Continuation!.Basis.ProductionHash);
        Assert.Equal(before.Capacity.Inventory.ContentHash, Production.Continuation.Basis.InventoryHash);
    }

    internal void Apply(WatershedRunoffCapacityPlan plan) => Reservoirs = Reservoirs.PlanApply(plan).ApplyTo(Reservoirs);
    internal void Reconcile(CountedAuthorityState inventory, ProductionSnapshot production) =>
        Apply(Capacity.PlanReconcile(inventory, production, TestContext.Current.CancellationToken));

    internal CraftPlan PlanCraft(IngredientLotKind kind, EntityId lotId, ulong quantity, EntityId command,
        Func<EntityId, bool>? occupied = null)
    {
        var raw = kind == IngredientLotKind.Source ? Inventory.Custody.GetLot(lotId) : null;
        var made = kind == IngredientLotKind.Transformed ? Production.Lots.Single(lot => lot.LotId == lotId) : null;
        EntityId custodian = raw?.CustodianId ?? made!.CustodianId;
        var recipe = Production.Recipes.Single(value => value.Inputs.Single().Count == quantity);
        var request = new CraftRequest(recipe.DefinitionId, recipe.Version, Production.Revision, custodian,
            Inventory.Custody.GetStorage(custodian).Revision,
            [new(kind, lotId, raw?.Revision ?? made!.Revision, quantity)]);
        return new InstantCraftPlanner().Plan(Inventory, Production, new(Client, Session), Owner, command,
            request, occupied ?? IsOccupied);
    }

    internal EntityId Craft(IngredientLotKind kind, EntityId lotId, ulong quantity, EntityId command)
    {
        var plan = PlanCraft(kind, lotId, quantity, command);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Reconcile(plan.CandidateInventory, plan.CandidateProduction);
        var batch = Production.Batches.Single(value => value.CommandId == command);
        var lot = Assert.Single(Production.Lots, value => value.Output.BatchId == batch.BatchId);
        Assert.True(_occupied.Add(command));
        Assert.True(_occupied.Add(batch.BatchId));
        Assert.True(_occupied.Add(lot.LotId));
        return lot.LotId;
    }

    internal ItemConsumptionPlan PlanConsume(IngredientLotKind kind, EntityId lotId, EntityId command,
        Func<EntityId, bool>? occupied = null)
    {
        var raw = kind == IngredientLotKind.Source ? Inventory.Custody.GetLot(lotId) : null;
        var made = kind == IngredientLotKind.Transformed ? Production.Lots.Single(lot => lot.LotId == lotId) : null;
        EntityId custodian = raw?.CustodianId ?? made!.CustodianId;
        return new ItemConsumptionPlanner().Plan(Inventory, Production, new(Client, Session), Owner, command,
            new(custodian, Inventory.Custody.GetStorage(custodian).Revision, Production.Revision, Owner,
                raw?.Definition ?? made!.Definition, new(kind, lotId, raw?.Revision ?? made!.Revision, 1)), occupied ?? IsOccupied);
    }

    internal void Consume(IngredientLotKind kind, EntityId lotId, EntityId command)
    {
        var plan = PlanConsume(kind, lotId, command);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Reconcile(plan.CandidateInventory, plan.CandidateProduction);
        Assert.True(_occupied.Add(command));
        Assert.True(_occupied.Add(plan.Claim!.ConsumptionId));
    }

    internal LiquidCellState PlaceAll(LiquidCellKey cell) => LiquidCellState.Create(Reservoirs, new(4, 2), new(0, 0),
        Inventory.Custody.Lots.Select(lot => new LiquidCellPortion(cell, LiquidStockOrigin.RawSource,
            lot.Source.SourceId, lot.Source.Start, lot.Source.End))
            .Concat(Production.Lots.Select(lot => new LiquidCellPortion(cell, LiquidStockOrigin.ProducedLot,
                lot.LotId, 0, lot.Quantity))));

    private CraftRecipeDefinition Recipe(ulong number, ulong quantity)
    {
        var physical = new MaterialRecipeDefinition(Material.Physical.Package, Id(number), "repack_water_" + quantity,
            new(1), null, new MaterialCatalog([Material.Physical]),
            [new(RecipeMaterialRole.Input, Material.Physical.Pin(), quantity),
                new(RecipeMaterialRole.Output, Material.Physical.Pin(), quantity)]);
        return new(Id(number + 1), physical, [Material], [new(Material.Item, 0, Material.Physical.MassPerUnit)]);
    }
}
