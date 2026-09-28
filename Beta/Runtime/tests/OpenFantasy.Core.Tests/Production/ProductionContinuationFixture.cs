using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Production.InstantCraftFixture;

namespace OpenFantasy.Core.Tests.Production;

/// <summary>Actual standalone material operations; the external identity set models retained journal ownership.</summary>
internal sealed class ProductionContinuationFixture
{
    private readonly HashSet<EntityId> _usedIdentities = [];

    internal ProductionContinuationFixture(CountedAuthorityState inventory, ProductionSnapshot production)
    {
        Inventory = inventory;
        Production = production;
    }

    internal CountedAuthorityState Inventory { get; private set; }
    internal ProductionSnapshot Production { get; private set; }
    internal List<ClosedProductionSegment> Closed { get; } = [];
    internal bool IsOccupied(EntityId identity) => _usedIdentities.Contains(identity);

    internal static ProductionContinuationFixture RawStock()
    {
        var initial = Create(chained: true);
        var owner = initial.Inventory;
        // An explicit finite fresh admission, before any operation: no earned rights are inserted.
        var inventory = new CountedAuthorityState(new(owner.WorldId, owner.BranchId, 0,
            [new(Id(60), 1024, 0), new(Id(61), 64, 0)],
            [new(Id(70), Id(60), Berries, new(Id(71), 0, 600), 0), owner.Custody.GetLot(Id(72))]),
            604, owner.Actors, owner.Controls, owner.Policies,
            [new(Id(71), Berries, 600), new(Id(73), Wood, 4)]);
        return new(inventory, new(owner.WorldId, owner.BranchId, 0, initial.Production.Recipes, [], [], []));
    }

    internal static ProductionContinuationFixture Crafted(bool consumptionEnabled = true)
    {
        var initial = Create(chained: true);
        var production = consumptionEnabled
            ? new ProductionSnapshot(initial.Production.WorldId, initial.Production.BranchId, 0,
                initial.Production.Recipes, [], [], [])
            : initial.Production;
        var result = new ProductionContinuationFixture(initial.Inventory, production);
        var request = Request(result.Inventory, production, recipe: production.GetRecipe(Id(5202)));
        result.AcceptCraft(new InstantCraftPlanner().Plan(result.Inventory, production, Delivery, Id(10), Id(20000),
            request, result.IsOccupied), Id(20000));
        if (consumptionEnabled)
        {
            var ration = Assert.Single(result.Production.Lots, lot => lot.Definition == Rations);
            result.Consume(IngredientLotKind.Transformed, ration.LotId, Id(20001));
        }
        return result;
    }

    internal ConsumeItemRequest ConsumptionRequest(IngredientLotKind kind, EntityId lotId)
    {
        var raw = kind == IngredientLotKind.Source ? Inventory.Custody.GetLot(lotId) : null;
        var transformed = kind == IngredientLotKind.Transformed
            ? Production.Lots.Single(lot => lot.LotId == lotId) : null;
        EntityId custodian = raw?.CustodianId ?? transformed!.CustodianId;
        return new(custodian, Inventory.Custody.GetStorage(custodian).Revision, Production.Revision, Id(11),
            raw?.Definition ?? transformed!.Definition, new(kind, lotId, raw?.Revision ?? transformed!.Revision, 1));
    }

    internal ItemConsumptionPlan PlanConsumption(IngredientLotKind kind, EntityId lotId, EntityId command,
        Func<EntityId, bool>? occupied = null) => new ItemConsumptionPlanner().Plan(Inventory, Production,
            Delivery, Id(10), command, ConsumptionRequest(kind, lotId), occupied ?? IsOccupied);

    internal void Consume(IngredientLotKind kind, EntityId lotId, EntityId command)
    {
        var plan = PlanConsumption(kind, lotId, command);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Assert.NotNull(plan.Claim);
        Inventory = plan.CandidateInventory;
        Production = plan.CandidateProduction;
        Production.ValidateForAuthority(Inventory);
        Assert.True(_usedIdentities.Add(command));
        Assert.True(_usedIdentities.Add(plan.Claim.ConsumptionId));
    }

    internal CraftRequest RepackRequest()
    {
        var ration = Assert.Single(Production.Lots, lot => lot.Definition == Rations);
        return Request(Inventory, Production,
            [new(IngredientLotKind.Transformed, ration.LotId, ration.Revision, 1)], Production.GetRecipe(Id(5204)));
    }

    internal CraftPlan PlanRepack(EntityId command, Func<EntityId, bool>? occupied = null) =>
        new InstantCraftPlanner().Plan(Inventory, Production, Delivery, Id(10), command,
            RepackRequest(), occupied ?? IsOccupied);

    internal void Repack(EntityId command) => AcceptCraft(PlanRepack(command), command);

    internal void Cut()
    {
        var prior = Production;
        byte[] bytes = Encode(prior);
        var next = prior.Continue(Inventory);
        Assert.True(next.IsContinued);
        Assert.NotNull(next.Continuation);
        Assert.Equal(prior.Revision, next.Revision);
        Assert.Equal(prior.TotalBatchCount, next.TotalBatchCount);
        Assert.Equal(prior.TotalConsumedItemCount, next.TotalConsumedItemCount);
        Assert.Empty(next.Batches);
        Assert.Empty(next.ConsumedItems);
        Assert.Equal(prior.ContentHash, next.Continuation.Basis.ProductionHash);
        Assert.Equal(Inventory.ContentHash, next.Continuation.Basis.InventoryHash);
        Closed.Add(new(Inventory, prior, bytes, next.Continuation));
        Production = next;
        Reopen();
    }

    internal void Reopen()
    {
        Inventory = AuthorityEncodingV1.Decode(AuthorityEncodingV1.Encode(Inventory));
        byte[] bytes = Encode(Production);
        Production = Decode(bytes, Production.Continuation, Production.SupportsConsumption);
        Assert.Equal(bytes, Encode(Production));
        Production.ValidateForAuthority(Inventory);
    }

    internal static byte[] Encode(ProductionSnapshot production) => production.IsContinued
        ? ProductionCodecV3.Encode(production)
        : production.SupportsConsumption ? ProductionCodecV2.Encode(production) : ProductionCodecV1.Encode(production);

    internal static ProductionSnapshot Decode(byte[] bytes, ProductionContinuationContext? context, bool consumption) =>
        context is not null ? ProductionCodecV3.Decode(bytes, context)
            : consumption ? ProductionCodecV2.Decode(bytes) : ProductionCodecV1.Decode(bytes);

    private void AcceptCraft(CraftPlan plan, EntityId command)
    {
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Inventory = plan.CandidateInventory;
        Production = plan.CandidateProduction;
        Production.ValidateForAuthority(Inventory);
        var batch = Assert.Single(Production.Batches, value => value.CommandId == command);
        Assert.True(_usedIdentities.Add(command));
        Assert.True(_usedIdentities.Add(batch.BatchId));
        foreach (var lot in Production.Lots.Where(value => value.Output.BatchId == batch.BatchId))
            Assert.True(_usedIdentities.Add(lot.LotId));
    }

    internal sealed record ClosedProductionSegment(CountedAuthorityState Inventory, ProductionSnapshot Production,
        byte[] Bytes, ProductionContinuationContext SuccessorContext);
}
