using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Geography.ReservoirTransportFixture;

namespace OpenFantasy.Core.Tests.Geography;

/// <summary>Authored finite water and reservations, followed by actual material and transport planners.</summary>
internal sealed class ReservoirTransportContinuationFixture
{
    private readonly HashSet<EntityId> _historical = [];
    internal ReservoirTransportFixture Original { get; }
    internal ReservoirTransportState State { get; private set; }
    internal WatershedRunoffCapacityState Capacity => State.Reservoirs.Capacity;
    internal CountedAuthorityState Inventory => Capacity.Inventory;
    internal ProductionSnapshot Production => Capacity.Production!;
    internal ItemPhysicalBinding Material { get; }
    internal bool Occupied(EntityId identity) => _historical.Contains(identity);
    internal GeographyProductionDependencies Dependencies => new(State,
        transportEvidence: Production.MaterialHistory!.TransportEvidence);

    internal ReservoirTransportContinuationFixture(bool split = false, bool secondBudget = false,
        ulong freeSource = 10000, ulong receiverUnits = 1000, ulong maximumUnits = 1000, bool consumption = true)
    {
        Original = new(freeSource, receiverUnits, split, secondBudget, maximumUnits);
        var original = Original.State;
        Material = original.Reservoirs.Capacity.Domain.Material;
        var inventory = original.Reservoirs.Capacity.Inventory;
        var production = new ProductionSnapshot(inventory.WorldId, inventory.BranchId, 0,
            [Recipe(40, 4), Recipe(50, 2)], [], [], consumption ? [] : null);
        var requests = original.Reservoirs.Capacity.Pending.Select(p => p.Request).ToArray();
        var planner = new WatershedRunoffPlanner();
        var prepared = requests.Select(request => planner.Prepare(new(request.Network, request.Mapping, inventory,
            production, request.ReservationId, request.OwnerActorId, request.Start, request.End, request.Material,
            request.Inputs, request.Terminals), TestContext.Current.CancellationToken)).ToArray();
        var capacity = WatershedRunoffCapacityState.Create(prepared[0]);
        foreach (var additional in prepared.Skip(1)) capacity = capacity.PlanAdmit(additional).ApplyTo(capacity);
        State = ReservoirTransportState.Create(LiquidReservoirState.Create(capacity, original.Reservoirs.Geometries),
            original.Connections, original.Boundary);
        Remember([]);
    }

    internal void Schedule(EntityId stage, EntityId reservation, WorldInstant? due = null)
    {
        var before = State;
        var basinIds = Capacity.Pending.Single(p => p.Request.ReservationId == reservation).Request.Inputs
            .Where(i => i.Source is not null).Select(i => i.BasinId).ToHashSet();
        var routes = Original.Routes.Where(r => basinIds.Contains(r.BasinId));
        var plan = State.PlanSchedule(stage, reservation, due ?? Original.Due, routes, Occupied);
        Assert.Equal(new[] { stage }, plan.CreatedIds);
        State = plan.ApplyTo(State);
        Assert.Equal(before.Boundary, State.Boundary);
        Remember(plan.CreatedIds);
    }

    internal void Cut(bool first = false)
    {
        var before = State;
        var plan = first ? State.PlanContinueTransport(cancellationToken: TestContext.Current.CancellationToken)
            : State.PlanContinue(TestContext.Current.CancellationToken);
        State = plan.ApplyTo(State);
        Assert.Equal(before.Revision, State.Revision);
        Assert.Equal(before.Boundary, State.Boundary);
        Assert.Equal(before.Reservoirs.Capacity.Revision, Capacity.Revision);
        Assert.Equal(before.Reservoirs.Capacity.Inventory.ContentHash, Inventory.ContentHash);
        Assert.Equal(before.Reservoirs.Capacity.Production!.Revision, Production.Revision);
        Assert.Equal(before.Reservoirs.Capacity.Production.TotalMaterialOperationCount, Production.TotalMaterialOperationCount);
        Assert.Equal(before.Reservoirs.Capacity.Production.TotalBatchCount, Production.TotalBatchCount);
        Assert.Equal(before.Reservoirs.Capacity.Production.TotalConsumedItemCount, Production.TotalConsumedItemCount);
        Assert.Equal(before.Reservoirs.Geometries.Select(g => g.ContentHash), State.Reservoirs.Geometries.Select(g => g.ContentHash));
        Assert.Equal(before.Connections, State.Connections);
        Assert.Equal(StageFacts(before), StageFacts(State));
        Assert.Equal(before.Reservoirs.Capacity.Closed, Capacity.Closed);
        Assert.Equal(before.Reservoirs.Capacity.Receivers, Capacity.Receivers);
        Assert.Equal(before.Reservoirs.Capacity.Pending.Select(p => p.Request.Inputs.ToArray()),
            Capacity.Pending.Select(p => p.Request.Inputs.ToArray()));
        Assert.Equal(before.ContentHash, Production.Continuation!.ReservedTransport!.ClosingTransportHash);
        Assert.Empty(Production.MaterialHistory!.Operations);
    }

    internal ReservoirTransportPlan Advance(WorldInstant? boundary = null, bool hold = false)
    {
        var plan = State.PlanAdvance(boundary ?? State.Boundary.AddMilliseconds(1800), hold, Occupied);
        State = plan.ApplyTo(State);
        Remember(plan.CreatedIds);
        Production.ValidateForAuthority(Inventory);
        return plan;
    }

    internal void Cancel(EntityId reservation)
    {
        State = State.PlanCancel(reservation).ApplyTo(State);
        Remember([]);
    }

    internal void Reconcile(CountedAuthorityState inventory, ProductionSnapshot production)
    {
        State = State.PlanApply(State.Reservoirs.PlanApply(Capacity.PlanReconcile(inventory, production,
            TestContext.Current.CancellationToken))).ApplyTo(State);
        Remember([]);
    }

    internal EntityId Craft(IngredientLotKind kind, EntityId lotId, ulong quantity, EntityId cause)
    {
        var raw = kind == IngredientLotKind.Source ? Inventory.Custody.GetLot(lotId) : null;
        var made = kind == IngredientLotKind.Transformed ? Production.Lots.Single(l => l.LotId == lotId) : null;
        EntityId store = raw?.CustodianId ?? made!.CustodianId;
        var recipe = Production.Recipes.Single(r => r.Inputs.Single().Count == quantity);
        var plan = new InstantCraftPlanner().Plan(Inventory, Production, new(Id(7), Id(8)), Owner, cause,
            new(recipe.DefinitionId, recipe.Version, Production.Revision, store, Inventory.Custody.GetStorage(store).Revision,
                [new(kind, lotId, raw?.Revision ?? made!.Revision, quantity)]), Occupied);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Reconcile(plan.CandidateInventory, plan.CandidateProduction);
        Remember([cause]);
        return EntityId.DeriveSpawned(cause, 1);
    }

    internal void Consume(IngredientLotKind kind, EntityId lotId, EntityId cause, ulong quantity = 1)
    {
        var raw = kind == IngredientLotKind.Source ? Inventory.Custody.GetLot(lotId) : null;
        var made = kind == IngredientLotKind.Transformed ? Production.Lots.Single(l => l.LotId == lotId) : null;
        EntityId store = raw?.CustodianId ?? made!.CustodianId;
        var plan = new ItemConsumptionPlanner().Plan(Inventory, Production, new(Id(7), Id(8)), Owner, cause,
            new(store, Inventory.Custody.GetStorage(store).Revision, Production.Revision, Owner,
                raw?.Definition ?? made!.Definition, new(kind, lotId, raw?.Revision ?? made!.Revision, quantity)), Occupied);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Reconcile(plan.CandidateInventory, plan.CandidateProduction);
        Remember([cause]);
    }

    internal EntityId Transfer(EntityId lotId, EntityId destination, ulong quantity, EntityId cause)
    {
        var lot = Inventory.Custody.GetLot(lotId);
        var request = new CommandEnvelope(cause, Id(7), Id(8), Owner, Inventory.WorldId, Inventory.BranchId,
            [new(RevisionTargetKind.Branch, Inventory.BranchId, Inventory.Custody.Revision),
                new(RevisionTargetKind.Entity, lotId, lot.Revision),
                new(RevisionTargetKind.Entity, lot.CustodianId, Inventory.Custody.GetStorage(lot.CustodianId).Revision),
                new(RevisionTargetKind.Entity, destination, Inventory.Custody.GetStorage(destination).Revision)],
            new(lotId, lot.CustodianId, destination, quantity));
        var plan = new MaterialTransferPlanner().Plan(Inventory, Production, request, new(Id(7), Id(8)), Occupied);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Reconcile(plan.CandidateInventory, plan.CandidateProduction);
        Remember([cause]);
        return quantity == lot.Quantity ? lotId : EntityId.DeriveSpawned(cause, 0);
    }

    internal static string[] StageFacts(ReservoirTransportState state) => state.Stages.Select(s =>
        $"{s.StageId}:{s.ReservationId}:{s.DueAt}:{s.FundingHash}:{s.Units}:{s.Status}:{s.ResolvedAt}:" +
        string.Join(',', s.Routes.Select(r => $"{r.BasinId}/{r.ConnectionId}"))).ToArray();

    private void Remember(IEnumerable<EntityId> accepted)
    {
        _historical.UnionWith(accepted);
        _historical.UnionWith(Inventory.Custody.Lots.Select(l => l.LotId));
        _historical.UnionWith(Production.Lots.Select(l => l.LotId));
        _historical.UnionWith(Production.Batches.Select(b => b.BatchId));
        _historical.UnionWith(Production.Batches.Select(b => b.CommandId));
        _historical.UnionWith(Production.ConsumedItems.Select(c => c.ConsumptionId));
        _historical.UnionWith(Production.ConsumedItems.Select(c => c.CommandId));
        _historical.UnionWith(Production.MaterialHistory?.Operations.Select(o => o.CommandId) ?? []);
        _historical.UnionWith(State.Stages.Select(s => s.StageId));
    }

    private CraftRecipeDefinition Recipe(ulong number, ulong quantity)
    {
        var physical = new MaterialRecipeDefinition(Material.Physical.Package, Id(number), "reserved_water_" + quantity,
            new(1), null, new MaterialCatalog([Material.Physical]),
            [new(RecipeMaterialRole.Input, Material.Physical.Pin(), quantity), new(RecipeMaterialRole.Output, Material.Physical.Pin(), quantity)]);
        return new(Id(number + 1), physical, [Material], [new(Material.Item, 0, Material.Physical.MassPerUnit)]);
    }
}
