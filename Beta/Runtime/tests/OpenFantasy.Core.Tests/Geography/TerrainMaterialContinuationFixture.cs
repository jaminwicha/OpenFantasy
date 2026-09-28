using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Geography;

/// <summary>Finite authored slabs; every subsequent extraction, transformation and representation change uses its actual planner.</summary>
internal sealed class TerrainMaterialContinuationFixture
{
    internal static EntityId Id(ulong value) => new(0x746572636f6e7469, value);
    internal static readonly EntityId Actor = Id(10), Coarse = Id(11), Fine = Id(12), Away = Id(13),
        OtherCoarse = Id(14), OtherFine = Id(15), Source = Id(16), AllocationA = Id(17), AllocationB = Id(18),
        Client = Id(19), Session = Id(20), InitialLot = Id(21);
    internal static readonly CellCoordinate CellA = new(-2, 0, -1), CellA2 = new(-1, 0, -1), CellB = new(0, 0, -1);
    private readonly HashSet<EntityId> _occupied = [];
    private readonly TerrainMaterialRegistryPlanner _planner = new();

    internal TerrainMaterialContinuationFixture(ulong awayCapacity = 200, ulong fineCapacity = 200,
        bool distinctRepresentationStores = false, bool consumption = true, int extraLots = 0, bool splitCoarse = false)
    {
        var scope = new TerrainScope(Id(1), Id(2), Id(3));
        var basin = new WatershedPlanner().Prepare(new(scope, Id(4), new(-1, 0, 2, 1), new(0, 0), 0, Id(5)), TestContext.Current.CancellationToken);
        var network = new WatershedNetworkPlanner().Prepare(Id(6), [basin], [WatershedOutletClosure.ToTerminal(basin.BasinId, 2)], TestContext.Current.CancellationToken);
        var terrain = new WatershedTerrainMapper().Prepare(network, new(2, 1, 0), new(new(-2, 0, -1), 4, 2, 3), TestContext.Current.CancellationToken);
        var item = new CountedItemDefinition(Id(30), ContentHash.Compute("continued-terrain-rock"u8));
        Material = new(item, new(new PackageId("openfantasy.terraincontinuation"), Id(31), "rock", new(250_000_000), new(100_000_000), new(10)));
        var allocations = new TerrainMaterialAllocation[] {
            new(AllocationA, new(terrain, new(basin.BasinId, 0), 2, Material, 10, new(Source, 10, 90), Coarse, Fine)),
            new(AllocationB, new(terrain, new(basin.BasinId, 1), 2, Material, 10, new(Source, 90, 170),
                distinctRepresentationStores ? OtherCoarse : Coarse, distinctRepresentationStores ? OtherFine : Fine)) };
        var stores = new List<CustodianStorage> { new(Coarse, 200, 0), new(Fine, fineCapacity, 0), new(Away, awayCapacity, 0) };
        if (distinctRepresentationStores) stores.AddRange([new(OtherCoarse, 200, 0), new(OtherFine, 200, 0)]);
        var lots = distinctRepresentationStores
            ? new List<CountedSourceLot> { new(InitialLot, Coarse, item, new(Source, 10, 90), 0), new(Id(22), OtherCoarse, item, new(Source, 90, 170), 0) }
            : [new CountedSourceLot(InitialLot, Coarse, item, new(Source, 10, 170), 0)];
        if (splitCoarse)
        {
            lots = [new(InitialLot, Coarse, item, new(Source, 10, 20), 0), new(Id(23), Coarse, item, new(Source, 20, 40), 0),
                new(Id(24), Coarse, item, new(Source, 40, 90), 0),
                new(Id(22), distinctRepresentationStores ? OtherCoarse : Coarse, item, new(Source, 90, 170), 0)];
        }
        lots.AddRange(Enumerable.Range(0, extraLots).Select(index => new CountedSourceLot(Id(100_000 + (ulong)index), Away,
            item, new(Source, 200 + (ulong)index, 201 + (ulong)index), 0)));
        ulong sourceCapacity = 200 + (ulong)extraLots;
        var inventory = new CountedAuthorityState(new(scope.WorldId, scope.BranchId, 0, stores, lots), sourceCapacity,
            [Actor], [new(Client, Session, Actor)], stores.Select(value => new CustodianAccessPolicy(value.CustodianId, Actor, CustodyDepositRule.OwnerOnly)),
            [new(Source, item, sourceCapacity)]);
        var registry = TerrainMaterialRegistry.Create(terrain, inventory, allocations);
        Registry = registry.AttachProduction(new(scope.WorldId, scope.BranchId, 0, [Recipe(40, 4), Recipe(50, 2)], [], [], consumption ? [] : null));
        foreach (var lot in lots) _occupied.Add(lot.LotId);
    }

    internal TerrainMaterialRegistry Registry { get; private set; }
    internal CountedAuthorityState Inventory => Registry.Inventory;
    internal ProductionSnapshot Production => Registry.Production!;
    internal ItemPhysicalBinding Material { get; }
    internal bool Occupied(EntityId identity) => _occupied.Contains(identity);
    internal TerrainProductionDependencies Dependencies => new(Registry, terrainEvidence: Production.MaterialHistory!.TerrainEvidence);

    internal static TerrainMaterialContinuationFixture Earned()
    {
        var journey = new TerrainMaterialContinuationFixture();
        journey.Refine(AllocationA, CellA, Id(100));
        var fine = journey.RawAt(Fine, 10);
        EntityId extracted = journey.Transfer(fine.LotId, Away, 7, Id(101));
        journey.Craft(IngredientLotKind.Source, extracted, 4, Id(102));
        journey.Consume(IngredientLotKind.Source, extracted, Id(103));
        return journey;
    }

    internal CountedSourceLot RawAt(EntityId store, ulong start) => Assert.Single(Inventory.Custody.Lots,
        value => value.CustodianId == store && value.Source.Start == start);

    internal TerrainMaterialRegistryPlan PlanRefine(EntityId allocation, CellCoordinate cell, EntityId cause,
        Func<EntityId, bool>? occupied = null) => _planner.Refine(Registry, allocation, Registry.Terrain, [cell], cause,
            Registry.Revision, occupied ?? Occupied);
    internal TerrainMaterialRegistryPlan PlanDemote(EntityId allocation, CellCoordinate cell, EntityId cause,
        Func<EntityId, bool>? occupied = null) => _planner.Demote(Registry, allocation, [cell], cause, Registry.Revision, occupied ?? Occupied);
    internal void Refine(EntityId allocation, CellCoordinate cell, EntityId cause) => Adopt(PlanRefine(allocation, cell, cause).ApplyTo(Registry), cause);
    internal void Demote(EntityId allocation, CellCoordinate cell, EntityId cause) => Adopt(PlanDemote(allocation, cell, cause).ApplyTo(Registry), cause);

    internal void Cut(bool first = false)
    {
        var before = Registry;
        var plan = first ? Registry.PlanContinueTerrain(cancellationToken: TestContext.Current.CancellationToken)
            : Registry.PlanContinue(TestContext.Current.CancellationToken);
        Registry = plan.ApplyTo(Registry);
        Assert.Equal(before.Revision, Registry.Revision);
        Assert.Equal(before.Inventory.ContentHash, Inventory.ContentHash);
        Assert.Equal(before.Production!.Revision, Production.Revision);
        Assert.Equal(before.Production.TotalBatchCount, Production.TotalBatchCount);
        Assert.Equal(before.Production.TotalConsumedItemCount, Production.TotalConsumedItemCount);
        Assert.Equal(before.Production.TotalMaterialOperationCount, Production.TotalMaterialOperationCount);
        foreach (var allocation in before.Allocations)
            Assert.Equal(before.GetState(allocation.AllocationId).ActiveCells, Registry.GetState(allocation.AllocationId).ActiveCells);
        Assert.Empty(Production.MaterialHistory!.Operations);
        Assert.Empty(Production.Batches);
        Assert.Empty(Production.ConsumedItems);
    }

    internal CommandEnvelope TransferCommand(EntityId lotId, EntityId destination, ulong quantity, EntityId command)
    {
        var lot = Inventory.Custody.GetLot(lotId);
        return new(command, Client, Session, Actor, Inventory.WorldId, Inventory.BranchId,
            [new(RevisionTargetKind.Branch, Inventory.BranchId, Inventory.Custody.Revision),
                new(RevisionTargetKind.Entity, lot.LotId, lot.Revision),
                new(RevisionTargetKind.Entity, lot.CustodianId, Inventory.Custody.GetStorage(lot.CustodianId).Revision),
                new(RevisionTargetKind.Entity, destination, Inventory.Custody.GetStorage(destination).Revision)],
            new(lot.LotId, lot.CustodianId, destination, quantity));
    }

    internal EntityId Transfer(EntityId lotId, EntityId destination, ulong quantity, EntityId command)
    {
        bool split = quantity < Inventory.Custody.GetLot(lotId).Quantity;
        var request = TransferCommand(lotId, destination, quantity, command);
        if (Production.IsContinued)
        {
            var result = new MaterialTransferPlanner().Plan(Inventory, Production, request, new(Client, Session), Occupied);
            Assert.Equal(WorldActionRejection.None, result.Rejection);
            Adopt(Registry.ReconcileProduction(result.CandidateInventory, result.CandidateProduction), command);
        }
        else
        {
            var result = new CountedTransferAuthority().Decide(Inventory, request, new(Client, Session), new(Inventory.WorldId, Registry.Revision + 1));
            Assert.Equal(CommandReceiptStatus.Applied, result.Receipt.Status);
            Adopt(Registry.ReconcileInventory(result.Candidate), command);
        }
        return split ? EntityId.DeriveSpawned(command, 0) : lotId;
    }

    internal CraftRequest CraftRequest(IngredientLotKind kind, EntityId lotId, ulong quantity)
    {
        var raw = kind == IngredientLotKind.Source ? Inventory.Custody.GetLot(lotId) : null;
        var made = kind == IngredientLotKind.Transformed ? Production.Lots.Single(value => value.LotId == lotId) : null;
        EntityId store = raw?.CustodianId ?? made!.CustodianId;
        var recipe = Production.Recipes.Single(value => value.Inputs.Single().Count == quantity);
        return new(recipe.DefinitionId, recipe.Version, Production.Revision, store, Inventory.Custody.GetStorage(store).Revision,
            [new(kind, lotId, raw?.Revision ?? made!.Revision, quantity)]);
    }

    internal EntityId Craft(IngredientLotKind kind, EntityId lotId, ulong quantity, EntityId command)
    {
        var plan = new TerrainMaterialCraftPlanner().Plan(Registry, new(Client, Session), Actor, command,
            CraftRequest(kind, lotId, quantity), Occupied);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Adopt(plan.ApplyTo(Registry), command);
        return EntityId.DeriveSpawned(command, 1);
    }

    internal ItemConsumptionPlan PlanConsume(IngredientLotKind kind, EntityId lotId, EntityId command)
    {
        var raw = kind == IngredientLotKind.Source ? Inventory.Custody.GetLot(lotId) : null;
        var made = kind == IngredientLotKind.Transformed ? Production.Lots.Single(value => value.LotId == lotId) : null;
        EntityId store = raw?.CustodianId ?? made!.CustodianId;
        return new ItemConsumptionPlanner().Plan(Inventory, Production, new(Client, Session), Actor, command,
            new(store, Inventory.Custody.GetStorage(store).Revision, Production.Revision, Actor, raw?.Definition ?? made!.Definition,
                new(kind, lotId, raw?.Revision ?? made!.Revision, 1)), Occupied);
    }
    internal void Consume(IngredientLotKind kind, EntityId lotId, EntityId command)
    {
        var plan = PlanConsume(kind, lotId, command);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Adopt(Registry.ReconcileProduction(plan.CandidateInventory, plan.CandidateProduction), command);
    }

    private void Adopt(TerrainMaterialRegistry candidate, EntityId command)
    {
        var previous = Inventory.Custody.Lots.Select(value => value.LotId).Concat(Production.Lots.Select(value => value.LotId))
            .Concat(Production.Batches.Select(value => value.BatchId)).Concat(Production.ConsumedItems.Select(value => value.ConsumptionId)).ToHashSet();
        foreach (EntityId identity in candidate.Inventory.Custody.Lots.Select(value => value.LotId)
            .Concat(candidate.Production!.Lots.Select(value => value.LotId)).Concat(candidate.Production.Batches.Select(value => value.BatchId))
            .Concat(candidate.Production.ConsumedItems.Select(value => value.ConsumptionId)).Where(identity => !previous.Contains(identity)))
            Assert.True(_occupied.Add(identity));
        Assert.True(_occupied.Add(command));
        Registry = candidate;
        Production.ValidateForAuthority(Inventory);
    }

    private CraftRecipeDefinition Recipe(ulong number, ulong quantity)
    {
        var physical = new MaterialRecipeDefinition(Material.Physical.Package, Id(number), "repack_rock_" + quantity, new(1), null,
            new MaterialCatalog([Material.Physical]), [new(RecipeMaterialRole.Input, Material.Physical.Pin(), quantity), new(RecipeMaterialRole.Output, Material.Physical.Pin(), quantity)]);
        return new(Id(number + 1), physical, [Material], [new(Material.Item, 0, Material.Physical.MassPerUnit)]);
    }
}
