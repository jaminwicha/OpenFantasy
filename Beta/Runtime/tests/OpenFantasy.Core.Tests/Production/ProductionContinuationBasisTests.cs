using System.Reflection;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Tests.Gathering;
using OpenFantasy.Core.Tests.Worlds;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Production.InstantCraftFixture;

namespace OpenFantasy.Core.Tests.Production;

public sealed class ProductionContinuationBasisTests
{
    [Fact]
    public void Actual_raw_retirement_preserves_live_lot_and_owner_pins_without_rotating_the_owner()
    {
        var original = ProductionContinuationBasisFixture.Fresh();
        var state = original.Consume(IngredientLotKind.Source, Id(70), 2, 8000);
        byte[] inventoryBytes = AuthorityEncodingV1.Encode(state.Inventory);
        byte[] productionBytes = ProductionCodecV2.Encode(state.Production);
        var basis = state.Capture();

        Assert.Equal(state.Inventory.WorldId, basis.WorldId);
        Assert.Equal(state.Inventory.BranchId, basis.BranchId);
        Assert.Equal(state.Inventory.ContentHash, basis.InventoryHash);
        Assert.Equal(state.Production.ContentHash, basis.ProductionHash);
        Assert.Equal(1UL, basis.ProductionRevision);
        Assert.Equal(1UL, basis.CustodyRevision);
        Assert.Equal(state.Inventory.Custody.Lots, basis.RawLots);
        AssertTransformedLots(state.Production.Lots, basis.TransformedLots);
        var retained = Assert.Single(basis.RawRetirements);
        Assert.Equal(Berries, retained.Definition);
        Assert.Equal(Id(60), retained.CustodianId);
        Assert.Equal(new SourceInterval(Id(71), 0, 2), retained.Source);
        var remainder = Assert.Single(basis.RawLots, value => value.LotId == Id(70));
        Assert.Equal((Id(60), 1UL, 2UL, 6UL),
            (remainder.CustodianId, remainder.Revision, remainder.Source.Start, remainder.Source.End));
        Assert.Empty(basis.OutputBudgets);
        Assert.Empty(basis.BatchWitnesses);
        Assert.Equal(inventoryBytes, AuthorityEncodingV1.Encode(state.Inventory));
        Assert.Equal(productionBytes, ProductionCodecV2.Encode(state.Production));
        Assert.Single(state.Production.ConsumedItems);
    }

    [Fact]
    public void Imported_multioutput_budgets_exclude_an_actually_consumed_prefix()
    {
        var crafted = ProductionContinuationBasisFixture.Fresh().CraftRations();
        var ration = Assert.Single(crafted.Production.Lots, value => value.Definition == Rations);
        var state = crafted.Consume(IngredientLotKind.Transformed, ration.LotId, 1, 8000);
        var basis = state.Capture();
        var budget = basis.GetOutputBudget(ration.Output.BatchId, ration.Output.OutputIndex);

        Assert.Equal(Rations, budget.Definition);
        Assert.Equal(new ProductionOutputInterval(ration.Output.BatchId, 0, 1, 2), Assert.Single(budget.Intervals));
        Assert.True(budget.Covers(new(ration.Output.BatchId, 0, 1, 2), Rations));
        Assert.False(budget.Covers(new(ration.Output.BatchId, 0, 0, 1), Rations));
        Assert.False(budget.Covers(new(ration.Output.BatchId, 0, 0, 2), Rations));
        Assert.False(budget.Covers(new(ration.Output.BatchId, 0, 1, 3), Rations));
        Assert.False(budget.Covers(new(ration.Output.BatchId, 1, 1, 2), Rations));
        Assert.False(budget.Covers(new(Id(9900), 0, 1, 2), Rations));
        Assert.False(budget.Covers(new(ration.Output.BatchId, 0, 1, 2),
            new(Rations.DefinitionId, ContentHash.Compute("another item version"u8))));
        Assert.False(budget.Covers(new(ration.Output.BatchId, 0, 1, 2), Offcuts));
        var waste = Assert.Single(state.Production.Lots, value => value.Definition == Offcuts);
        var wasteBudget = basis.GetOutputBudget(waste.Output.BatchId, waste.Output.OutputIndex);
        Assert.Equal(Offcuts, wasteBudget.Definition);
        Assert.Equal(waste.Output, Assert.Single(wasteBudget.Intervals));
        Assert.Equal(2, basis.OutputBudgets.Count);
        AssertTransformedLots(state.Production.Lots, basis.TransformedLots);
        Assert.Equal(1UL, basis.TransformedLots.Single(value => value.LotId == ration.LotId).Revision);
        AssertProducerWitness(state, basis, ration.Output.BatchId);
        Assert.Single(basis.BatchWitnesses);
        Assert.Equal(3UL, basis.RawRetirements.Aggregate(0UL, (sum, value) => sum + value.Source.Quantity));
    }

    [Fact]
    public void Actual_recraft_keeps_only_live_output_budgets_without_retiring_raw_ancestry_twice()
    {
        var crafted = ProductionContinuationBasisFixture.Fresh().CraftRations();
        var ration = Assert.Single(crafted.Production.Lots, value => value.Definition == Rations);
        var consumed = crafted.Consume(IngredientLotKind.Transformed, ration.LotId, 1, 8000);
        var state = consumed.RepackRation(6001);
        var basis = state.Capture();
        Assert.Equal(2, state.Production.Batches.Count);
        Assert.Throws<ArgumentException>(() => basis.GetOutputBudget(ration.Output.BatchId, 0));
        var output = Assert.Single(state.Production.Lots, value => value.Definition == Rations);
        Assert.NotEqual(ration.Output.BatchId, output.Output.BatchId);
        Assert.Equal(output.Output, Assert.Single(basis.GetOutputBudget(output.Output.BatchId, 0).Intervals));
        Assert.DoesNotContain(basis.TransformedLots, value => value.LotId == ration.LotId);
        Assert.Equal(2, basis.OutputBudgets.Count);
        Assert.Equal(2, basis.BatchWitnesses.Count);
        AssertProducerWitness(state, basis, ration.Output.BatchId); // The original waste is still live.
        AssertProducerWitness(state, basis, output.Output.BatchId);
        Assert.Equal(consumed.Capture().RawRetirements.Select(value => (value.Definition, value.Source, value.CustodianId)),
            basis.RawRetirements.Select(value => (value.Definition, value.Source, value.CustodianId)));
        Assert.Equal(3UL, basis.RawRetirements.Aggregate(0UL, (sum, value) => sum + value.Source.Quantity));
    }

    [Fact]
    public void Normalized_retirements_merge_only_adjacent_rights_with_the_same_custodian_and_pin()
    {
        var state = ProductionContinuationBasisFixture.Fresh(splitSources: true)
            .Consume(IngredientLotKind.Source, Id(70), 1, 8000)
            .Consume(IngredientLotKind.Source, Id(74), 1, 8001)
            .Consume(IngredientLotKind.Source, Id(75), 1, 8002, Id(11))
            .Consume(IngredientLotKind.Source, Id(74), 1, 8003);
        var basis = state.Capture();
        var ranges = basis.RawRetirements.OrderBy(value => value.Source.Start).ToArray();
        Assert.Equal(new[] { (0UL, 1UL, Id(60)), (6UL, 8UL, Id(60)), (8UL, 9UL, Id(61)) },
            ranges.Select(value => (value.Source.Start, value.Source.End, value.CustodianId)));
        Assert.All(ranges, value => Assert.Equal(Berries, value.Definition));
        Assert.All(ranges, value => Assert.Equal(Id(71), value.Source.SourceId));
        Assert.Equal(4UL, ranges.Aggregate(0UL, (sum, value) => sum + value.Source.Quantity));
        Assert.DoesNotContain(ranges, value => value.Source.Start < 6 && value.Source.End > 4);
        Assert.Equal(12UL, state.Inventory.Sources.Single(value => value.SourceId == Id(71)).UnitCapacity);
        Assert.Equal(6UL, basis.RawLots.Where(value => value.Source.SourceId == Id(71)).Aggregate(0UL, (sum, value) => sum + value.Quantity));
        Assert.Equal(state.Inventory.Custody.Lots, basis.RawLots);
    }

    [Fact]
    public void Real_gathering_issuance_never_becomes_the_sources_entire_admitted_capacity()
    {
        var initial = ProductionWorldFixture.Create();
        var gather = GatheringFixture.Command(initial, 11001);
        var processor = new WorldStepProcessor();
        var harvestedDecision = processor.Decide(initial, ProductionWorldFixture.Batch(initial, 11002, gather));
        Assert.Equal(WorldActionRejection.None, Assert.Single(harvestedDecision.Receipts).Rejection);
        var harvested = harvestedDecision.Candidate;
        var craft = ProductionWorldFixture.Craft(harvested, 11003,
            [new(IngredientLotKind.Source, AdmissionFixture.Id(70), 0, 1),
                new(IngredientLotKind.Source, EntityId.DeriveSpawned(gather.CommandId, 0), 0, 1)]);
        var craftedDecision = processor.Decide(harvested, ProductionWorldFixture.Batch(harvested, 11004, craft));
        Assert.Equal(WorldActionRejection.None, Assert.Single(craftedDecision.Receipts).Rejection);
        var world = craftedDecision.Candidate;
        var basis = ProductionContinuationBasis.Capture(world.Inventory, world.Production!);
        Assert.Equal(20UL, world.Gathering!.GetNode(GatheringFixture.Node).UnitCapacity);
        Assert.Equal(1UL, world.Gathering.GetNode(GatheringFixture.Node).NextUnissuedUnit);
        Assert.Equal(new SourceInterval(GatheringFixture.Source, 0, 1),
            Assert.Single(basis.RawRetirements, value => value.Source.SourceId == GatheringFixture.Source).Source);
        Assert.DoesNotContain(basis.RawLots, value => value.Source.SourceId == GatheringFixture.Source);
        Assert.Equal(world.Inventory.ContentHash, basis.InventoryHash);
        Assert.Equal(world.Production!.ContentHash, basis.ProductionHash);
        // Capture is a structural observation of these actual owners; it is not a new world
        // admission, an issuance cursor, or permission to issue the other nineteen source units.
        Assert.Equal(WorldSnapshotCodec.Encode(world), WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(world))));
    }

    [Fact]
    public void Structurally_valid_unissued_claim_is_observable_but_complete_world_admission_rejects_it()
    {
        var world = ProductionWorldFixture.Create();
        var owner = world.Inventory;
        // This supplied diagnostic proposal is deliberately not an earned world boundary.
        // Its interval fits the source capacity, but the actual gathering cursor is still zero.
        var proposed = new CountedSourceLot(Id(19000), AdmissionFixture.Id(60), AdmissionFixture.Material,
            new(GatheringFixture.Source, 0, 1), 0);
        var inventory = new CountedAuthorityState(new(owner.WorldId, owner.BranchId, owner.Custody.Revision,
            owner.Custody.Storages, owner.Custody.Lots.Append(proposed)), owner.AdmittedUnitCapacity,
            owner.Actors, owner.Controls, owner.Policies, owner.Sources, owner.ReservationIds);
        var basis = ProductionContinuationBasis.Capture(inventory, world.Production!);
        Assert.Equal(proposed, Assert.Single(basis.RawLots, value => value.LotId == proposed.LotId));
        Assert.Equal(0UL, world.Gathering!.GetNode(GatheringFixture.Node).NextUnissuedUnit);
        var rejection = Assert.Throws<ArgumentException>(() => new WorldSnapshot(inventory, world.Space, world.Residents,
            world.AdmissionVersion, world.Calendar, world.Revision, world.AdvancingSteps,
            world.Dialogues, world.Gathering, world.Production));
        Assert.Equal("Issued gathering claims cannot extend beyond the retained source cursor.", rejection.Message);
        Assert.DoesNotContain(world.Inventory.Custody.Lots, value => value.LotId == proposed.LotId);
    }

    [Fact]
    public void Diagnostic_output_partition_keeps_internal_holes_and_individual_custody_restrictions()
    {
        var state = ProductionContinuationBasisFixture.DiagnosticOutputPartition();
        var basis = state.Capture();
        var budget = Assert.Single(basis.OutputBudgets);
        Assert.Equal(new[] { (0UL, 1UL), (2UL, 4UL) },
            budget.Intervals.Select(value => (value.Start, value.End)));
        Assert.True(budget.Covers(new(budget.BatchId, budget.OutputIndex, 2, 4), Rations));
        Assert.False(budget.Covers(new(budget.BatchId, budget.OutputIndex, 1, 2), Rations));
        Assert.False(budget.Covers(new(budget.BatchId, budget.OutputIndex, 0, 3), Rations));
        Assert.False(budget.Covers(new(budget.BatchId, budget.OutputIndex, 0, 4), Rations));
        AssertTransformedLots(state.Production.Lots, basis.TransformedLots);
        var protectedLot = Assert.Single(basis.TransformedLots, value => value.LotId == Id(9101));
        Assert.Equal((Id(60), 5UL, LotProtection.Favorite | LotProtection.UserReserved, (EntityId?)Id(90)),
            (protectedLot.CustodianId, protectedLot.Revision, protectedLot.Protection, protectedLot.ReservedBy));
        var otherOwner = Assert.Single(basis.TransformedLots, value => value.LotId == Id(9102));
        Assert.Equal((Id(61), 8UL, LotProtection.Heirloom),
            (otherOwner.CustodianId, otherOwner.Revision, otherOwner.Protection));
        Assert.Equal(4UL, Assert.Single(basis.RawRetirements).Source.Quantity);
        AssertProducerWitness(state, basis, budget.BatchId);
        // A global material remainder budget grants neither actor control nor withdrawal
        // permission. These supplied partitions do not claim an executed split/transfer path.
    }

    [Fact]
    public void Entire_admitted_live_lot_envelope_has_bounded_observation_bytes()
    {
        var initial = Create();
        var owner = initial.Inventory;
        const int count = ProductionContinuationBasis.MaximumLiveLots;
        var lots = Enumerable.Range(0, count).Select(index => new CountedSourceLot(Id(20000UL + (ulong)index),
            Id(60), Berries, new(Id(71), (ulong)index, (ulong)index + 1), 0)).ToArray();
        var inventory = new CountedAuthorityState(new(owner.WorldId, owner.BranchId, 0,
            [new(Id(60), count, 0), new(Id(61), 64, 0)], lots), (ulong)count + 4,
            owner.Actors, owner.Controls, owner.Policies, [new(Id(71), Berries, count), new(Id(73), Wood, 4)]);
        var basis = ProductionContinuationBasis.Capture(inventory, initial.Production);
        Assert.Equal(4096, basis.RawLots.Count);
        Assert.Equal(lots, basis.RawLots);
        Assert.Empty(basis.RawRetirements);
        Assert.Empty(basis.OutputBudgets);
        byte[] bytes = basis.Encode();
        Assert.True(bytes.Length < ProductionContinuationBasis.MaximumEncodedByteLength);
        Assert.Equal(bytes, basis.Encode());
        Assert.Equal(ContentHash.Compute(bytes), basis.ContentHash);
        // This supplied fresh envelope verifies bounded observation, not world throughput.
    }

    [Fact]
    public void Snapshot_pins_protected_live_lots_and_canonical_order_are_retained_immutably()
    {
        var protectedState = ProductionContinuationBasisFixture.Fresh(protectedStock: true);
        var protectedBasis = protectedState.Capture();
        var protectedLot = Assert.Single(protectedBasis.RawLots, value => value.LotId == Id(70));
        Assert.Equal(Id(90), protectedLot.ReservedBy);
        Assert.Equal(LotProtection.Favorite | LotProtection.UserReserved, protectedLot.Protection);
        Assert.Equal(protectedState.Inventory.Custody.GetLot(Id(70)), protectedLot);

        var crafted = ProductionContinuationBasisFixture.Fresh().CraftRations();
        var ration = Assert.Single(crafted.Production.Lots, value => value.Definition == Rations);
        var state = crafted.Consume(IngredientLotKind.Transformed, ration.LotId, 1, 8000).RepackRation(6001);
        var basis = state.Capture();
        var inventory = state.Inventory;
        var reversedInventory = new CountedAuthorityState(new(inventory.WorldId, inventory.BranchId,
            inventory.Custody.Revision, inventory.Custody.Storages.Reverse(), inventory.Custody.Lots.Reverse()),
            inventory.AdmittedUnitCapacity, inventory.Actors.Reverse(), inventory.Controls.Reverse(), inventory.Policies.Reverse(),
            inventory.Sources.Reverse(), inventory.ReservationIds.Reverse());
        var production = state.Production;
        var reversedProduction = new ProductionSnapshot(production.WorldId, production.BranchId, production.Revision,
            production.Recipes.Reverse(), production.Batches.Reverse(), production.Lots.Reverse(), production.ConsumedItems.Reverse());
        var reverse = ProductionContinuationBasis.Capture(reversedInventory, reversedProduction);
        Assert.Equal(basis.ContentHash, reverse.ContentHash);
        Assert.Equal(basis.Encode(), reverse.Encode());
        byte[] alteredCopy = basis.Encode();
        alteredCopy[^1] ^= 0x80;
        Assert.NotEqual(alteredCopy, basis.Encode());
        Assert.Equal(ContentHash.Compute(basis.Encode()), basis.ContentHash);
        Assert.Throws<NotSupportedException>(() => ((IList<CountedSourceLot>)basis.RawLots).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<TransformedLot>)basis.TransformedLots).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<ProductionOutputInterval>)basis.OutputBudgets[0].Intervals).Clear());
    }

    [Fact]
    public void Capture_requires_matching_valid_owners_and_does_not_upgrade_legacy_bytes()
    {
        var initial = Create(chained: true);
        var crafted = InstantCraftFixture.Craft(initial.Inventory, initial.Production);
        byte[] legacy = ProductionCodecV1.Encode(crafted.CandidateProduction);
        var basis = ProductionContinuationBasis.Capture(crafted.CandidateInventory, crafted.CandidateProduction);
        Assert.False(crafted.CandidateProduction.SupportsConsumption);
        Assert.Equal(legacy, ProductionCodecV1.Encode(crafted.CandidateProduction));
        Assert.Equal(crafted.CandidateProduction.ContentHash, basis.ProductionHash);
        Assert.Throws<ArgumentException>(() => ProductionContinuationBasis.Capture(initial.Inventory, crafted.CandidateProduction));
        var foreign = new ProductionSnapshot(Id(9100), Id(9101), 0, initial.Production.Recipes, [], []);
        Assert.Throws<ArgumentException>(() => ProductionContinuationBasis.Capture(initial.Inventory, foreign));
        Assert.Equal(legacy, ProductionCodecV1.Encode(crafted.CandidateProduction));
    }

    [Fact]
    public void Long_actual_recraft_chain_retains_only_live_producers_without_owner_or_parent_references()
    {
        var crafted = ProductionContinuationBasisFixture.Fresh().CraftRations();
        var ration = Assert.Single(crafted.Production.Lots, value => value.Definition == Rations);
        var state = crafted.Consume(IngredientLotKind.Transformed, ration.LotId, 1, 8000).RepackRation(6001);
        var shortBasis = state.Capture();
        for (ulong step = 2; step <= 64; step++) state = state.RepackRation(6000 + step);
        var basis = state.Capture();
        Assert.Equal(65, state.Production.Batches.Count);
        Assert.Equal(66UL, state.Production.Revision);
        Assert.Equal(2, basis.TransformedLots.Count);
        Assert.Equal(2, basis.OutputBudgets.Count);
        Assert.Equal(2, basis.BatchWitnesses.Count);
        Assert.Equal(2, basis.RawRetirements.Count);
        Assert.Equal(3UL, basis.RawRetirements.Aggregate(0UL, (sum, value) => sum + value.Source.Quantity));
        Assert.Equal(shortBasis.Encode().Length, basis.Encode().Length);
        Assert.True(basis.Encode().Length < ProductionContinuationBasis.MaximumEncodedByteLength);
        var liveProducers = state.Production.Lots.Select(value => value.Output.BatchId).Distinct().Order().ToArray();
        Assert.Equal(liveProducers, basis.BatchWitnesses.Select(value => value.BatchId).Order());
        foreach (var producer in liveProducers) AssertProducerWitness(state, basis, producer);
        var ownerTypes = new[] { typeof(WorldSnapshot), typeof(CountedAuthorityState), typeof(ProductionSnapshot),
            typeof(ProductionContinuationBasis), typeof(ProductionBatch) };
        Assert.DoesNotContain(typeof(ProductionContinuationBasis).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            field => ownerTypes.Contains(field.FieldType));
        Assert.DoesNotContain(typeof(ProductionBatchWitness).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            field => ownerTypes.Contains(field.FieldType));
        // This bounded, real chain proves compact live output observation, not world history
        // rotation, a raised256-batch limit, or permission to discard archived production.
    }

    private static void AssertProducerWitness(ProductionContinuationBasisFixture state, ProductionContinuationBasis basis, EntityId batchId)
    {
        var batch = state.Production.Batches.Single(value => value.BatchId == batchId);
        var witness = Assert.Single(basis.BatchWitnesses, value => value.BatchId == batchId);
        Assert.Equal(batch.CommandId, witness.CommandId);
        Assert.Equal(batch.ActorId, witness.ActorId);
        Assert.Equal(batch.CustodianId, witness.CustodianId);
        Assert.Equal(batch.RecipeId, witness.RecipeId);
        Assert.Equal(batch.RecipeVersion, witness.RecipeVersion);
        Assert.Equal(state.BatchHash(batchId), witness.CanonicalBatchHash);
    }

    private static void AssertTransformedLots(IEnumerable<TransformedLot> expected, IEnumerable<TransformedLot> actual) =>
        Assert.Equal(expected.Select(value => (value.LotId, value.CustodianId, value.Definition, value.Output,
            value.Revision, value.Protection, value.ReservedBy)),
            actual.Select(value => (value.LotId, value.CustodianId, value.Definition, value.Output,
                value.Revision, value.Protection, value.ReservedBy)));
}
