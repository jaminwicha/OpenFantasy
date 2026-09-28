using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed class CoordinatedBatchCapacityTests
{
    [Fact]
    public void Five_hundred_twenty_actual_recrafts_trigger_two_material_only_cuts_with_surviving_ancestry()
    {
        var journey = new CoordinatedRecraftJourney();
        var actor = journey.Actors[0];
        var bag = journey.Genesis.GetResident(actor).InventoryCustodianId;
        var originalRaw = journey.Genesis.Inventory.Custody.Lots.Single(value =>
            value.CustodianId == bag && value.Definition == journey.Potion);
        var closed = new List<WorldMaterialCareArchiveSegment>();
        WorldActionCommand? originalCommand = null;
        TransformedLot? previousOutput = null;

        for (ulong operation = 1; operation <= 520; operation++)
        {
            var before = journey.World;
            AssertNoResourceEffects(before);
            var command = journey.Craft(actor);
            originalCommand ??= command;
            var decision = journey.Commit(journey.Batch(command));
            AssertApplied(decision, command);
            Assert.Null(decision.SealedResourceSegment);
            var after = journey.World;
            Assert.Equal(operation, after.Production!.TotalBatchCount);
            Assert.Equal(operation, after.Production.Revision);
            Assert.Equal(operation, after.Inventory.Custody.Revision);
            Assert.Equal(operation, after.Revision);
            Assert.Equal(0UL, after.Production.TotalConsumedItemCount);
            AssertNoResourceEffects(after);
            var batch = Assert.Single(after.Production.Batches, value => value.CommandId == command.CommandId);
            var input = Assert.Single(batch.Ingredients);
            if (previousOutput is null)
            {
                Assert.Equal(IngredientLotKind.Source, input.Kind);
                Assert.Equal(originalRaw.Source, input.Source);
                Assert.Equal(originalRaw.LotId, input.LotId);
            }
            else
            {
                Assert.Equal(IngredientLotKind.Transformed, input.Kind);
                Assert.Equal(previousOutput.Output, input.Output);
                Assert.Equal(previousOutput.LotId, input.LotId);
            }
            var output = Assert.Single(after.Production.Lots);
            Assert.Equal(EntityId.DeriveSpawned(command.CommandId, 1), output.LotId);
            Assert.Equal(batch.BatchId, output.Output.BatchId);
            Assert.Equal(1UL, output.Quantity);
            Assert.DoesNotContain(after.Inventory.Custody.Lots, value => value.LotId == originalRaw.LotId);

            if (decision.SealedMaterialCareSegment is { } segment)
            {
                Assert.Equal(255, before.Production!.Batches.Count);
                Assert.Single(after.Production.Batches);
                Assert.Equal((ulong)closed.Count, segment.Reference.Ordinal);
                Assert.Equal(WorldSnapshotCodec.Encode(before), segment.Encode());
                Assert.Equal(before.Revision, segment.Reference.BoundarySequence);
                Assert.Equal(before.ContentHash, segment.Reference.BoundaryHash);
                var imported = Assert.Single(segment.Anchor.Production.Basis.TransformedLots);
                Assert.Equal(previousOutput!.Output, imported.Output);
                Assert.Equal(previousOutput.LotId, imported.LotId);
                Assert.Single(segment.Anchor.Production.Basis.OutputBudgets);
                var witness = Assert.Single(segment.Anchor.Production.Basis.BatchWitnesses);
                Assert.Equal(previousOutput.Output.BatchId, witness.BatchId);
                Assert.Equal(journey.Recipe.Version, witness.RecipeVersion);
                var rawRetirement = Assert.Single(segment.Anchor.Production.Basis.RawRetirements);
                Assert.Equal(originalRaw.Source, rawRetirement.Source);
                Assert.Equal(originalRaw.CustodianId, rawRetirement.CustodianId);
                Assert.Equal(originalRaw.Definition, rawRetirement.Definition);
                Assert.Equal(before.Inventory.ContentHash, segment.Continued.Inventory.ContentHash);
                Assert.Equal(before.Production.Revision, segment.Continued.Production!.Revision);
                Assert.Empty(segment.Continued.Production.Batches);
                AssertNoResourceEffects(segment.Continued);
                var retrieved = WorldMaterialCareArchiveSegment.Decode(segment.Reference, segment.Encode(), before.Coordination!.Anchor);
                Assert.Equal(segment.Anchor.ContentHash, retrieved.Anchor.ContentHash);
                journey.Reopen(retrieved.Anchor);
                closed.Add(retrieved);
            }
            previousOutput = output;
        }

        Assert.Equal(new ulong[] { 255, 510 }, closed.Select(value => value.Reference.BoundarySequence));
        Assert.Equal(10, journey.World.Production!.Batches.Count);
        Assert.Equal(520UL, journey.World.Production.TotalBatchCount);
        Assert.Single(journey.World.Production.Lots);
        Assert.Equal(closed[0].Anchor.Production.Basis.Encode().Length, closed[1].Anchor.Production.Basis.Encode().Length);
        Assert.Single(journey.World.Production.Continuation!.Basis.RawRetirements);
        Assert.Contains(originalCommand!.CommandId, journey.OccupiedIdentities);
        Assert.Contains(EntityId.DeriveSpawned(originalCommand.CommandId, 0), journey.OccupiedIdentities);
        Assert.Contains(EntityId.DeriveSpawned(originalCommand.CommandId, 1), journey.OccupiedIdentities);
        byte[] finalBytes = WorldSnapshotCodec.Encode(journey.World);
        Assert.Throws<InvalidOperationException>(() => journey.Commit(journey.Batch(originalCommand!)));
        Assert.Equal(finalBytes, WorldSnapshotCodec.Encode(journey.World));
        journey.Reopen(closed[^1].Anchor);
        Assert.Equal(finalBytes, WorldSnapshotCodec.Encode(journey.World));
    }

    [Fact]
    public void A_later_refused_writer_is_reserved_before_the_cut_and_preserves_the_first_accepted_craft()
    {
        var journey = new CoordinatedRecraftJourney();
        var firstActor = journey.Actors[0];
        var secondActor = journey.Actors[1];
        for (int operation = 0; operation < 254; operation++)
        {
            var command = journey.Craft(firstActor);
            var result = journey.Commit(journey.Batch(command));
            AssertApplied(result, command);
            Assert.Null(result.SealedMaterialCareSegment);
        }
        var before = journey.World;
        Assert.Equal(254, before.Production!.Batches.Count);
        AssertNoResourceEffects(before);
        var first = journey.Craft(firstActor);
        // This independently captured offer is legitimately stale after the first actor's craft.
        // It is still a potential writer and must be counted before either action is interpreted.
        var later = journey.Craft(secondActor);
        var selected = journey.Batch(later, first);
        Assert.Equal(firstActor, selected.Actions[0].ActorId);
        var secondBag = before.GetResident(secondActor).InventoryCustodianId;
        var secondLot = before.Inventory.Custody.Lots.Single(value => value.CustodianId == secondBag && value.Definition == journey.Potion);
        var decision = journey.Commit(selected);
        AssertApplied(decision, first);
        var refusal = Assert.Single(decision.Receipts, value => value.CommandId == later.CommandId);
        Assert.Equal(CommandReceiptStatus.Rejected, refusal.Status);
        Assert.Equal(WorldActionRejection.RevisionConflict, refusal.Rejection);
        Assert.Equal(later.ContentHash, refusal.CommandHash);
        var segment = Assert.IsType<WorldMaterialCareArchiveSegment>(decision.SealedMaterialCareSegment);
        Assert.Equal(0UL, segment.Reference.Ordinal);
        Assert.Equal(254UL, segment.Reference.BoundarySequence);
        Assert.Equal(WorldSnapshotCodec.Encode(before), segment.Encode());
        Assert.Null(decision.SealedResourceSegment);
        var after = journey.World;
        Assert.Equal(255UL, after.Production!.Revision);
        Assert.Equal(255UL, after.Production.TotalBatchCount);
        Assert.Equal(255UL, after.Revision);
        Assert.Equal(first.CommandId, Assert.Single(after.Production.Batches).CommandId);
        Assert.Equal(EntityId.DeriveSpawned(first.CommandId, 1), Assert.Single(after.Production.Lots).LotId);
        Assert.Equal(secondLot, after.Inventory.Custody.GetLot(secondLot.LotId));
        Assert.Equal(before.Inventory.Custody.GetStorage(secondBag), after.Inventory.Custody.GetStorage(secondBag));
        Assert.DoesNotContain(after.Production.Batches, value => value.CommandId == later.CommandId);
        Assert.DoesNotContain(decision.Identities, value => value.Kind == WorldCommittedIdentityKind.SpawnedEntity
            && (value.Identity == EntityId.DeriveSpawned(later.CommandId, 0)
                || value.Identity == EntityId.DeriveSpawned(later.CommandId, 1)));
        Assert.Contains(later.CommandId, journey.OccupiedIdentities);
        AssertNoResourceEffects(after);
        var retrieved = WorldMaterialCareArchiveSegment.Decode(segment.Reference, segment.Encode());
        journey.Reopen(retrieved.Anchor);
        Assert.Throws<InvalidOperationException>(() => journey.Commit(journey.Batch(later)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Two_valid_ordered_material_writers_are_admitted_in_one_world_publication(bool continued)
    {
        var journey = new CoordinatedRecraftJourney();
        if (continued)
        {
            var earned = journey.Craft(journey.Actors[0]);
            AssertApplied(journey.Commit(journey.Batch(earned)), earned);
            journey.ManualCut();
        }
        var before = journey.World;
        var first = journey.Craft(journey.Actors[0]);
        // Explicit ordered-batch precondition: the first accepted craft advances the shared
        // Production revision, while this actor's independently owned storage and lot stay exact.
        var second = journey.Craft(journey.Actors[1], before.Production!.Revision + 1);
        var used = journey.OccupiedIdentities.ToHashSet();
        bool Occupied(EntityId identity) => before.IdentityIndex.Contains(identity) || used.Contains(identity);
        var firstPlan = journey.Plan(before.Inventory, before.Production, first, Occupied);
        Assert.Equal(WorldActionRejection.None, firstPlan.Rejection);
        var secondPlan = journey.Plan(firstPlan.CandidateInventory, firstPlan.CandidateProduction, second,
            identity => Occupied(identity) || identity == first.CommandId || firstPlan.CreatedIds.Contains(identity));
        Assert.Equal(WorldActionRejection.None, secondPlan.Rejection);
        Assert.Equal(before.Production.Revision + 2, secondPlan.CandidateProduction.Revision);

        var decision = journey.Commit(journey.Batch(second, first));
        AssertApplied(decision, first);
        // Both material plans are valid above. A per-publication revision ceiling must not
        // reclassify the later valid writer as an actor failure at complete-world admission.
        AssertApplied(decision, second);
        Assert.Equal(before.Revision + 1, journey.World.Revision);
        Assert.Equal(before.Production.Revision + 2, journey.World.Production!.Revision);
        Assert.Equal(secondPlan.CandidateInventory.ContentHash, journey.World.Inventory.ContentHash);
        Assert.Equal(secondPlan.CandidateProduction.ContentHash, journey.World.Production.ContentHash);
        Assert.Equal(2, journey.World.Production.Lots.Count);
        AssertNoResourceEffects(journey.World);
        journey.Reopen(journey.World.Coordination!.Anchor);
    }

    private static void AssertApplied(WorldStepDecision decision, WorldActionCommand command)
    {
        var receipt = Assert.Single(decision.Receipts, value => value.CommandId == command.CommandId);
        Assert.True(receipt.Status == CommandReceiptStatus.Applied,
            $"Actual craft {command.CommandId} at publication {receipt.CommitSequence} refused: {receipt.Rejection}.");
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
        Assert.Equal(command.ContentHash, receipt.CommandHash);
    }

    private static void AssertNoResourceEffects(WorldSnapshot world)
    {
        Assert.Equal((ushort)27, world.ProfileVersion);
        Assert.Empty(world.ResourceHistory!.History.Batches);
        Assert.Equal(0, world.ResourceHistory.History.ChangeCount);
        Assert.Empty(world.ResourceHistory.Evidence);
        Assert.Null(world.ResourceHistory.ArchiveAnchor);
        Assert.Empty(world.Care!.Acknowledgments);
        Assert.Empty(world.Care.Treatments);
        Assert.Empty(world.Care.Ordinary!.Uses);
        Assert.Equal(0UL, world.Care.Revision);
        Assert.NotNull(world.Excavation);
        Assert.NotNull(world.Construction);
        Assert.NotNull(world.Gathering);
        Assert.NotNull(world.EncounterLifecycle);
        Assert.NotNull(world.ReservoirSeals);
        Assert.NotNull(world.LiquidCells!.Topology);
        Assert.Equal(world.Inventory.ContentHash, world.Reservoirs!.Capacity.Inventory.ContentHash);
        Assert.Equal(world.Production!.ContentHash, world.Reservoirs.Capacity.Production!.ContentHash);
    }
}
