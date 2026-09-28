using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed class CoordinatedLiveWriterTests
{
    [Fact]
    public void Partial_wet_excavation_continues_then_gathering_issues_real_stock_after_two_cuts()
    {
        var journey = new CoordinatedLiveWriterJourney();
        journey.WalkToMiningStance();
        var original = journey.World;
        var site = Assert.Single(original.Excavation!.Sites);
        Assert.Equal(BreachFarmLayout.RequiredExcavationWork, site.RequiredWork);
        ulong water = LiquidQuantity(original);
        int openCells = original.LiquidCells!.Topology!.OpenCells.Count;
        WorldActionCommand? firstSwing = null;

        for (ulong work = 1; work <= BreachFarmLayout.RequiredExcavationWork; work++)
        {
            journey.EarnEffort();
            var before = journey.World;
            var command = journey.Mine();
            firstSwing ??= command;
            journey.Step([command]);
            var after = journey.World;
            site = Assert.Single(after.Excavation!.Sites);
            Assert.Equal(work, site.CompletedWork);
            Assert.Equal(before.Gathering!.GetActorWork(journey.Player).Effort - GatherRulesV1.EffortPerSwing
                + GatherRulesV1.EffortPerAdvancingStep, after.Gathering!.GetActorWork(journey.Player).Effort);
            Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
            Assert.Equal(before.ProcessIntervals + 1, after.ProcessIntervals);
            Assert.Equal(before.Care!.ContentHash, after.Care!.ContentHash);
            Assert.Equal(before.Construction!.ContentHash, after.Construction!.ContentHash);
            Assert.Equal(before.Production!.Revision, after.Production!.Revision);
            Assert.Equal(water, LiquidQuantity(after));

            if (!site.IsComplete)
            {
                Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
                Assert.Equal(before.Production.ContentHash, after.Production.ContentHash);
                Assert.Equal(before.Space.Regions.Select(value => value.ComputeContentHash()),
                    after.Space.Regions.Select(value => value.ComputeContentHash()));
                Assert.False(after.Space.Regions.Single(value => value.Bounds.Contains(site.Cell)).GetCell(site.Cell).IsExcavated);
                Assert.DoesNotContain(after.Inventory.Custody.Lots, value => value.Source.SourceId == site.CountedSourceId);
            }
            else
            {
                var lot = Assert.Single(after.Inventory.Custody.Lots, value => value.Source.SourceId == site.CountedSourceId);
                Assert.Equal(EntityId.DeriveSpawned(command.CommandId, 0), lot.LotId);
                Assert.Equal(0UL, lot.Source.Start);
                Assert.Equal(site.Quantity, lot.Source.End);
                Assert.Equal(before.Inventory.Custody.Revision + 1, after.Inventory.Custody.Revision);
                Assert.True(after.Space.Regions.Single(value => value.Bounds.Contains(site.Cell)).GetCell(site.Cell).IsExcavated);
                Assert.Equal(openCells + 1, after.LiquidCells!.Topology!.OpenCells.Count);
            }

            if (work == 1)
            {
                var first = journey.CutAndReopen();
                Assert.Equal(0UL, first.Reference.Ordinal);
                Assert.Equal(1UL, journey.World.Excavation!.Sites.Single().CompletedWork);
                Assert.True(journey.World.Production!.IsContinued);
            }
        }

        for (int step = 0; step < 10; step++) journey.Step();
        Assert.Contains(journey.World.LiquidCells!.Quantities,
            value => value.Key.Coordinate == new CellCoordinate(118, 329, -1) && value.Value > 0);
        var extracted = Assert.Single(journey.World.Inventory.Custody.Lots, value => value.Source.SourceId == site.CountedSourceId);
        var second = journey.CutAndReopen();
        Assert.Equal(1UL, second.Reference.Ordinal);
        Assert.Equal(extracted, Assert.Single(journey.World.Inventory.Custody.Lots, value => value.LotId == extracted.LotId));
        Assert.Equal(water, LiquidQuantity(journey.World));

        // A stale captured site request still refuses after archival; it cannot issue the completed source again.
        var beforeRefusal = journey.World;
        var stale = journey.Command(journey.Player, firstSwing!.Intent);
        var refused = journey.Step([stale], accepted: false);
        Assert.NotEqual(WorldActionRejection.None, Assert.Single(refused.Receipts).Rejection);
        Assert.Equal(beforeRefusal.Inventory.ContentHash, journey.World.Inventory.ContentHash);
        Assert.Equal(beforeRefusal.Production!.ContentHash, journey.World.Production!.ContentHash);
        Assert.Equal(beforeRefusal.Excavation!.ContentHash, journey.World.Excavation!.ContentHash);

        journey.WalkToBerries();
        journey.EarnEffort();
        var (node, gather) = journey.Berries();
        var beforeGather = journey.World;
        journey.Step([gather]);
        var gathered = journey.World;
        var advancedNode = gathered.Gathering!.Nodes.Single(value => value.NodeId == node.NodeId);
        var issued = Assert.Single(gathered.Inventory.Custody.Lots, value => value.LotId == EntityId.DeriveSpawned(gather.CommandId, 0));
        Assert.Equal(node.SourceId, issued.Source.SourceId);
        Assert.Equal(node.NextUnissuedUnit, issued.Source.Start);
        Assert.Equal(advancedNode.NextUnissuedUnit, issued.Source.End);
        Assert.True(issued.Quantity > 0);
        Assert.Equal(beforeGather.Inventory.Custody.Revision + 1, gathered.Inventory.Custody.Revision);
        Assert.Equal(beforeGather.Gathering!.GetActorWork(journey.Player).Effort - GatherRulesV1.EffortPerSwing
            + GatherRulesV1.EffortPerAdvancingStep, gathered.Gathering.GetActorWork(journey.Player).Effort);
        Assert.True(gathered.Excavation!.Sites.Single().IsComplete);
        Assert.Equal(water, LiquidQuantity(gathered));
        var bytes = WorldSnapshotCodec.Encode(gathered);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.DecodeCoordinated(bytes, second.Anchor)));
    }

    [Fact]
    public void Quest_funding_and_two_hundred_builder_actions_continue_with_exact_installed_backing_across_cuts()
    {
        var journey = new CoordinatedLiveWriterJourney();
        journey.Step();
        Assert.Equal(0UL, journey.CutAndReopen().Reference.Ordinal);
        var delivery = journey.PrepareQuestDelivery();
        var beforeDelivery = journey.World;
        journey.Step([delivery]);
        var funded = journey.World;
        var project = funded.Construction!.Projects.Single();
        Assert.Equal(ActorQuestStatus.Completed, funded.Quests!.Participations.Single().Status);
        Assert.Equal(6UL, funded.Inventory.Custody.Lots.Where(value => value.ReservedBy == project.ReservationId)
            .Aggregate(0UL, (sum, value) => sum + value.Quantity));
        Assert.True(funded.Progression!.GetActor(journey.Player).Revision > beforeDelivery.Progression!.GetActor(journey.Player).Revision);
        Assert.Equal(200UL, funded.Progression.Currency.GetActorAccount(journey.Player).Balance);
        journey.Step([journey.Command(journey.Player, new CloseDialogueIntent(journey.World.Dialogues.Single().DialogueId))]);

        int acceptedWork = 0;
        for (int step = 0; !journey.World.Construction!.Projects.Single().IsCompleted; step++)
        {
            Assert.True(step < 512, "The original routed construction must finish within the retained journey bound.");
            var before = journey.World;
            var decision = journey.BuilderStep();
            project = journey.World.Construction!.Projects.Single();
            if (decision.Receipts.Count == 0)
            {
                Assert.Equal(before.Construction!.ContentHash, journey.World.Construction.ContentHash);
                continue;
            }
            Assert.Single(decision.Receipts);
            acceptedWork++;
            Assert.Equal(before.Construction!.Projects.Single().Progress.CompletedUnits + 1, project.Progress.CompletedUnits);
            Assert.Equal((ulong)acceptedWork, project.Progress.CompletedUnits);
            if (!project.IsCompleted)
            {
                Assert.Equal(before.Inventory.ContentHash, journey.World.Inventory.ContentHash);
                Assert.Equal(before.Production!.ContentHash, journey.World.Production!.ContentHash);
            }
            if (acceptedWork == 100)
            {
                var partial = journey.CutAndReopen();
                Assert.Equal(1UL, partial.Reference.Ordinal);
                Assert.Equal(100UL, journey.World.Construction!.Projects.Single().Progress.CompletedUnits);
                Assert.False(journey.World.Construction.Projects.Single().IsCompleted);
            }
        }

        Assert.Equal(200, acceptedWork);
        project = journey.World.Construction!.Projects.Single();
        var installed = project.Installed!;
        var batch = Assert.Single(journey.World.Production!.Batches);
        var guard = Assert.Single(journey.World.Production.Lots, value => value.ReservedBy == project.ReservationId);
        Assert.Equal(installed.ProductionBatchId, batch.BatchId);
        Assert.Equal(installed.CompletionCommandId, batch.CommandId);
        Assert.Equal(installed.GuardLotId, guard.LotId);
        Assert.Equal(installed.GuardLotRevision, guard.Revision);
        Assert.Equal(project.ProjectCustodianId, guard.CustodianId);
        Assert.Equal(1UL, guard.Quantity);
        Assert.Equal(installed.StructureId, Assert.Single(journey.World.Space.Structures).StructureId);
        Assert.DoesNotContain(journey.World.Inventory.Custody.Lots, value => value.ReservedBy == project.ReservationId);
        var structure = journey.World.Space.Structures.Single();

        for (ulong ordinal = 2; ordinal <= 3; ordinal++)
        {
            var segment = journey.CutAndReopen();
            Assert.Equal(ordinal, segment.Reference.Ordinal);
            Assert.Empty(journey.World.Production!.Batches);
            Assert.Equal(1UL, journey.World.Production.TotalBatchCount);
            var imported = Assert.Single(journey.World.Production.Continuation!.ConstructionBatches);
            AssertBatchEqual(batch, imported);
            var retainedGuard = Assert.Single(journey.World.Production.Lots, value => value.LotId == installed.GuardLotId);
            Assert.Equal((guard.LotId, guard.CustodianId, guard.Definition, guard.Output, guard.Revision, guard.Protection, guard.ReservedBy),
                (retainedGuard.LotId, retainedGuard.CustodianId, retainedGuard.Definition, retainedGuard.Output,
                    retainedGuard.Revision, retainedGuard.Protection, retainedGuard.ReservedBy));
            Assert.Equal(installed, journey.World.Construction!.Projects.Single().Installed);
            Assert.Equal(200UL, journey.World.Construction.Projects.Single().Progress.CompletedUnits);
            var retained = Assert.Single(journey.World.Space.Structures);
            Assert.Equal(structure.StructureId, retained.StructureId);
            Assert.Equal(structure.Boxes, retained.Boxes);
            Assert.Equal(structure.BearingSurfaces, retained.BearingSurfaces);
            Assert.True(journey.World.IdentityIndex.ContainsEntity(installed.ProductionBatchId));
            journey.Step();
        }
    }

    private static ulong LiquidQuantity(WorldSnapshot world) => world.LiquidCells!.Portions.Aggregate(0UL,
        (sum, value) => sum + value.Quantity);

    private static void AssertBatchEqual(ProductionBatch expected, ProductionBatch actual)
    {
        Assert.Equal((expected.BatchId, expected.CommandId, expected.ActorId, expected.CustodianId, expected.RecipeId, expected.RecipeVersion),
            (actual.BatchId, actual.CommandId, actual.ActorId, actual.CustodianId, actual.RecipeId, actual.RecipeVersion));
        Assert.Equal(expected.Ingredients.Select(value => (value.LotId, value.Revision, value.Definition, value.Kind, value.Source, value.Output)),
            actual.Ingredients.Select(value => (value.LotId, value.Revision, value.Definition, value.Kind, value.Source, value.Output)));
    }
}
