using System.Reflection;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Production.InstantCraftFixture;

namespace OpenFantasy.Core.Tests.Production;

public sealed class ProductionContinuationTests
{
    [Fact]
    public void Actual_530_raw_consumptions_survive_two_cuts_with_exact_cumulative_rights_and_identities()
    {
        var journey = ProductionContinuationFixture.RawStock();
        for (ulong operation = 0; operation < 530; operation++)
        {
            EntityId command = Id(30000 + operation);
            if (journey.Production.ConsumedItems.Count == ProductionSnapshot.MaximumConsumedItems)
            {
                Assert.Equal(WorldActionRejection.Unavailable,
                    journey.PlanConsumption(IngredientLotKind.Source, Id(70), command).Rejection);
                journey.Cut();
            }
            journey.Consume(IngredientLotKind.Source, Id(70), command);
        }
        Assert.Equal(2, journey.Closed.Count);
        Assert.Equal(530UL, journey.Production.Revision);
        Assert.Equal(530UL, journey.Inventory.Custody.Revision);
        Assert.Equal(530UL, journey.Production.TotalConsumedItemCount);
        Assert.Equal(0UL, journey.Production.TotalBatchCount);
        Assert.Equal(18, journey.Production.ConsumedItems.Count);
        var remaining = journey.Inventory.Custody.GetLot(Id(70));
        Assert.Equal((530UL, 600UL, 530UL), (remaining.Source.Start, remaining.Source.End, remaining.Revision));
        var basis = ProductionContinuationBasis.Capture(journey.Inventory, journey.Production);
        var retired = Assert.Single(basis.RawRetirements);
        Assert.Equal((0UL, 530UL, Id(60)), (retired.Source.Start, retired.Source.End, retired.CustodianId));
        Assert.Equal(600UL, retired.Source.Quantity + remaining.Quantity);
        Assert.Equal(2UL, journey.Production.Continuation!.Ordinal);
        Assert.Equal(512UL, journey.Production.Continuation.BaseConsumedItemCount);
        AssertRetainedSegments(journey);
        byte[] before = ProductionContinuationFixture.Encode(journey.Production);
        var repeated = journey.PlanConsumption(IngredientLotKind.Source, Id(70), Id(30000));
        Assert.Equal(WorldActionRejection.Unavailable, repeated.Rejection);
        Assert.Same(journey.Production, repeated.CandidateProduction);
        Assert.Same(journey.Inventory, repeated.CandidateInventory);
        Assert.Equal(before, ProductionContinuationFixture.Encode(journey.Production));
        journey.Reopen();
    }

    [Fact]
    public void Actual_530_recrafts_keep_old_live_ancestry_across_two_cuts_without_recounting_raw_material()
    {
        var journey = ProductionContinuationFixture.Crafted();
        var originalWaste = Assert.Single(journey.Production.Lots, value => value.Definition == Offcuts);
        var originalBasis = ProductionContinuationBasis.Capture(journey.Inventory, journey.Production);
        var originalWitness = Assert.Single(originalBasis.BatchWitnesses);
        for (ulong operation = 0; operation < 530; operation++)
        {
            EntityId command = Id(40000 + operation);
            if (journey.Production.Batches.Count == ProductionSnapshot.MaximumBatches)
            {
                Assert.Equal(WorldActionRejection.Unavailable, journey.PlanRepack(command).Rejection);
                journey.Cut();
            }
            journey.Repack(command);
        }
        Assert.Equal(2, journey.Closed.Count);
        Assert.Equal(531UL, journey.Production.TotalBatchCount);
        Assert.Equal(1UL, journey.Production.TotalConsumedItemCount);
        Assert.Equal(532UL, journey.Production.Revision);
        Assert.Equal(532UL, journey.Inventory.Custody.Revision);
        Assert.Equal(19, journey.Production.Batches.Count);
        Assert.Empty(journey.Production.ConsumedItems);
        Assert.Equal(2, journey.Production.Lots.Count);
        var waste = Assert.Single(journey.Production.Lots, value => value.Definition == Offcuts);
        Assert.Equal((originalWaste.LotId, originalWaste.Output, originalWaste.Revision, originalWaste.CustodianId),
            (waste.LotId, waste.Output, waste.Revision, waste.CustodianId));
        var basis = ProductionContinuationBasis.Capture(journey.Inventory, journey.Production);
        Assert.Equal(2, basis.OutputBudgets.Count);
        Assert.Equal(2, basis.BatchWitnesses.Count);
        Assert.Equal(originalWitness.CanonicalBatchHash,
            Assert.Single(basis.BatchWitnesses, value => value.BatchId == originalWaste.Output.BatchId).CanonicalBatchHash);
        Assert.Equal(3UL, basis.RawRetirements.Aggregate(0UL, (sum, value) => sum + value.Source.Quantity));
        Assert.Equal(originalBasis.RawRetirements.Select(value => (value.CustodianId, value.Definition, value.Source)),
            basis.RawRetirements.Select(value => (value.CustodianId, value.Definition, value.Source)));
        Assert.Equal(journey.Closed[0].SuccessorContext.Basis.Encode().Length,
            journey.Closed[1].SuccessorContext.Basis.Encode().Length);
        AssertRetainedSegments(journey);
        var refused = journey.PlanRepack(Id(40000));
        Assert.Equal(WorldActionRejection.Unavailable, refused.Rejection);
        Assert.Same(journey.Production, refused.CandidateProduction);
        journey.Reopen();
    }

    [Fact]
    public void Continued_planners_require_external_history_and_reject_reserved_spawned_identities()
    {
        var journey = ProductionContinuationFixture.Crafted();
        journey.Cut();
        EntityId command = Id(50000);
        var raw = journey.ConsumptionRequest(IngredientLotKind.Source, Id(70));
        Assert.Throws<ArgumentException>(() => new ItemConsumptionPlanner().Plan(journey.Inventory, journey.Production,
            Delivery, Id(10), command, raw));
        Assert.Throws<ArgumentException>(() => new InstantCraftPlanner().Plan(journey.Inventory, journey.Production,
            Delivery, Id(10), command, journey.RepackRequest()));
        var consumption = journey.PlanConsumption(IngredientLotKind.Source, Id(70), command,
            identity => identity == EntityId.DeriveSpawned(command, 0));
        Assert.Equal(WorldActionRejection.Unavailable, consumption.Rejection);
        for (ulong output = 0; output < 2; output++)
        {
            EntityId occupied = EntityId.DeriveSpawned(command, output);
            var craft = journey.PlanRepack(command, identity => identity == occupied);
            Assert.Equal(WorldActionRejection.Unavailable, craft.Rejection);
            Assert.Same(journey.Inventory, craft.CandidateInventory);
            Assert.Same(journey.Production, craft.CandidateProduction);
        }
    }

    [Fact]
    public void Continuation_context_is_compact_and_does_not_retain_a_parent_owner_or_context()
    {
        var journey = ProductionContinuationFixture.Crafted();
        journey.Cut();
        journey.Repack(Id(50001));
        journey.Cut();
        var context = journey.Production.Continuation!;
        Assert.Equal(2UL, context.Ordinal);
        Assert.Equal(3UL, context.BaseProductionRevision);
        Assert.Equal(2UL, context.BaseBatchCount);
        Assert.Equal(1UL, context.BaseConsumedItemCount);
        Type[] forbidden = [typeof(ProductionSnapshot), typeof(ProductionContinuationContext), typeof(CountedAuthorityState)];
        foreach (Type type in new[] { typeof(ProductionContinuationContext), typeof(ProductionContinuationBasis), typeof(ProductionBatchWitness) })
            Assert.DoesNotContain(type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                field => forbidden.Contains(field.FieldType));
        Assert.DoesNotContain(typeof(ProductionContinuationContext).GetConstructors(), value => value.IsPublic);
        // These standalone cycles do not establish complete-world, care or Host archive admission.
    }

    private static void AssertRetainedSegments(ProductionContinuationFixture journey)
    {
        foreach (var closed in journey.Closed)
        {
            var decoded = ProductionContinuationFixture.Decode(closed.Bytes, closed.Production.Continuation,
                closed.Production.SupportsConsumption);
            decoded.ValidateForAuthority(closed.Inventory);
            Assert.Equal(closed.Bytes, ProductionContinuationFixture.Encode(decoded));
            Assert.Equal(closed.Production.ContentHash, decoded.ContentHash);
            var recoveredSuccessor = decoded.Continue(closed.Inventory);
            Assert.Equal(closed.SuccessorContext.ContentHash, recoveredSuccessor.Continuation!.ContentHash);
            Assert.Equal(closed.SuccessorContext.Basis.Encode(), recoveredSuccessor.Continuation.Basis.Encode());
        }
    }
}
