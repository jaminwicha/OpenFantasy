using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed class LiveMaterialWorkTests
{
    [Fact]
    public void Actual_partial_then_complete_wet_excavation_pins_work_before_once_only_flow_and_reopens()
    {
        using var journey = new MaterialEvidenceJourney();
        var start = journey.World.Space.GetActor(journey.Player).Feet;
        journey.Walk(journey.Player, new(start.X, BreachFarmLayout.MiningStance.Y, start.Z));
        journey.Walk(journey.Player, BreachFarmLayout.MiningStance);
        var original = journey.World;
        var originalVolume = original.LiquidCells!.Portions.Aggregate(0UL, (sum, value) => sum + value.Quantity);
        for (ulong work = 1; work <= 3; work++)
        {
            journey.EarnEffort(journey.Player);
            var before = journey.World;
            var command = journey.Mine();
            var decision = journey.Step([command]);
            var evidence = Assert.Single(decision.MaterialEvidence);
            Assert.Equal(command.CommandId, evidence.Command.CommandId);
            Assert.Equal(before.Revision + 1, evidence.PublicationSequence);
            Assert.Equal(0, evidence.ActionOrdinal);
            Assert.Equal(before.ContentHash, evidence.SourceWorldHash);
            Assert.Equal(before.Revision, evidence.SourceWorldRevision);
            Assert.Equal(before.ContentHash, evidence.InputWorldHash);
            Assert.Equal(WorldMaterialOwnerPins.Observe(before).ContentHash, evidence.Before.ContentHash);
            var spent = new GatheringSnapshot(before.WorldId, before.BranchId, before.Gathering!.AdvancingStep,
                before.Gathering.Nodes, before.Gathering.Tools, before.Gathering.Actors.Select(value => value.ActorId == journey.Player
                    ? new GatherActorWork(value.ActorId, value.Effort - GatherRulesV1.EffortPerSwing) : value));
            Assert.Equal(spent.ContentHash, evidence.After.Gathering);
            Assert.Equal(journey.World.Inventory.ContentHash, evidence.After.Inventory);
            Assert.NotEqual(journey.World.Gathering!.ContentHash, evidence.After.Gathering);
            Assert.Equal(work, journey.World.Excavation!.Sites.Single().CompletedWork);
            Assert.Equal(before.Gathering!.GetActorWork(journey.Player).Effort - GatherRulesV1.EffortPerSwing
                + GatherRulesV1.EffortPerAdvancingStep, journey.World.Gathering!.GetActorWork(journey.Player).Effort);
            Assert.Equal(before.AdvancingSteps + 1, journey.World.AdvancingSteps);
            Assert.Equal(before.ProcessIntervals + 1, journey.World.ProcessIntervals);
            Assert.Equal(before.Care!.ContentHash, journey.World.Care!.ContentHash);
            if (work < 3)
            {
                Assert.Equal(before.Inventory.ContentHash, journey.World.Inventory.ContentHash);
                Assert.Equal(before.Production!.ContentHash, journey.World.Production!.ContentHash);
                Assert.Empty(evidence.CreatedIds);
                Assert.Equal(before.Space.Regions.Select(value => value.ComputeContentHash()),
                    journey.World.Space.Regions.Select(value => value.ComputeContentHash()));
            }
            else
            {
                var site = journey.World.Excavation.Sites.Single();
                var lot = Assert.Single(journey.World.Inventory.Custody.Lots, value => value.Source.SourceId == site.CountedSourceId);
                Assert.Equal(site.Quantity, lot.Quantity);
                Assert.Contains(lot.LotId, evidence.CreatedIds);
                Assert.Equal(original.LiquidCells.Topology!.OpenCells.Count + 1, journey.World.LiquidCells!.Topology!.OpenCells.Count);
            }
        }
        for (int step = 0; step < 10; step++) Assert.Empty(journey.Step().MaterialEvidence);
        Assert.Contains(journey.World.LiquidCells!.Quantities, value => value.Key.Coordinate == new CellCoordinate(118, 329, -1) && value.Value > 0);
        Assert.Equal(originalVolume, journey.World.LiquidCells.Portions.Aggregate(0UL, (sum, value) => sum + value.Quantity));
        journey.ReopenExactly();
    }

    [Fact]
    public void Quest_funding_and_all_builder_work_are_observed_through_actual_installed_backing()
    {
        using var journey = new MaterialEvidenceJourney();
        var deliver = journey.PrepareQuestDelivery();
        var before = journey.World;
        var delivery = journey.Step([deliver]);
        Assert.Single(delivery.MaterialEvidence);
        Assert.Equal(ActorQuestStatus.Completed, journey.World.Quests!.Participations.Single().Status);
        var project = journey.World.Construction!.Projects.Single();
        Assert.Equal(6UL, journey.World.Inventory.Custody.Lots.Where(value => value.ReservedBy == project.ReservationId)
            .Aggregate(0UL, (sum, value) => sum + value.Quantity));
        Assert.True(journey.World.Progression!.GetActor(journey.Player).Revision > before.Progression!.GetActor(journey.Player).Revision);
        Assert.Empty(journey.Step([journey.Command(journey.Player, new CloseDialogueIntent(journey.World.Dialogues.Single().DialogueId))]).MaterialEvidence);
        int workRecords = 0;
        for (int step = 0; !journey.World.Construction!.Projects.Single().IsCompleted; step++)
        {
            Assert.True(step < 512);
            before = journey.World;
            var decision = journey.BuilderStep();
            if (decision.MaterialEvidence.Count == 0) continue;
            var evidence = Assert.Single(decision.MaterialEvidence);
            Assert.IsType<OpenFantasy.Core.Construction.WorkConstructionIntent>(evidence.Command.Intent);
            workRecords++;
            project = journey.World.Construction.Projects.Single();
            Assert.Equal(before.Construction!.Projects.Single().Progress.CompletedUnits + 1, project.Progress.CompletedUnits);
            if (!project.IsCompleted)
            {
                Assert.Empty(evidence.CreatedIds);
                Assert.Equal(before.Inventory.ContentHash, journey.World.Inventory.ContentHash);
                Assert.Equal(before.Production!.ContentHash, journey.World.Production!.ContentHash);
            }
            else
            {
                Assert.Contains(project.Installed!.StructureId, evidence.CreatedIds);
                Assert.Contains(project.Installed.GuardLotId, evidence.CreatedIds);
            }
        }
        Assert.Equal(200, workRecords);
        project = journey.World.Construction!.Projects.Single();
        Assert.Single(journey.World.Space.Structures);
        Assert.Single(journey.World.Production!.Batches);
        Assert.Equal(project.Installed!.GuardLotId, Assert.Single(journey.World.Production.Lots, value => value.ReservedBy == project.ReservationId).LotId);
        Assert.NotNull(journey.World.Excavation);
        Assert.NotNull(journey.World.Care);
        Assert.NotNull(journey.World.LiquidCells);
        journey.ReopenExactly();
    }

    [Fact]
    public void Gathering_and_crafting_by_two_actors_share_exact_intermediate_material_pins()
    {
        using var journey = new MaterialEvidenceJourney();
        var (node, _) = journey.GatherBerries();
        journey.WalkToBerryConnector(node.Feet);
        journey.EarnEffort(journey.Player);
        var before = journey.World;
        var commands = new[] { journey.GatherBerries().Command, journey.CraftHelperDose() };
        var decision = journey.Step(commands);
        Assert.Equal(2, decision.MaterialEvidence.Count);
        var first = decision.MaterialEvidence[0];
        var second = decision.MaterialEvidence[1];
        Assert.Equal(0, first.ActionOrdinal);
        Assert.Equal(1, second.ActionOrdinal);
        Assert.Equal(first.After.ContentHash, second.Before.ContentHash);
        Assert.Equal(before.Revision + 1, first.PublicationSequence);
        Assert.Equal(first.PublicationSequence, second.PublicationSequence);
        Assert.Equal(commands.OrderBy(value => value.ActorId).Select(value => value.CommandId),
            decision.MaterialEvidence.Select(value => value.Command.CommandId));
        Assert.Equal(node.NextUnissuedUnit + 1, journey.World.Gathering!.GetNode(node.NodeId).NextUnissuedUnit);
        Assert.Single(journey.World.Production!.Batches);
        Assert.Equal(before.AdvancingSteps + 1, journey.World.AdvancingSteps);
        journey.ReopenExactly();
    }

    [Fact]
    public void Actual_split_transfer_preserves_source_rights_and_records_the_created_child()
    {
        using var journey = new MaterialEvidenceJourney();
        journey.Walk(journey.Player, journey.World.Space.GetActor(journey.Helper).Feet, interaction: true);
        var before = journey.World;
        var source = before.GetResident(journey.Player).InventoryCustodianId;
        var destination = before.GetResident(journey.Helper).InventoryCustodianId;
        var definition = before.Quests!.Definitions.Single().Item;
        var lot = before.Inventory.Custody.Lots.Single(value => value.CustodianId == source && value.Definition == definition);
        Assert.True(lot.Quantity > 1);
        var command = journey.Command(journey.Player, new GiveItemIntent(journey.Helper,
            before.Space.GetActor(journey.Helper).Revision, lot.LotId, lot.Revision, source,
            before.Inventory.Custody.GetStorage(source).Revision, 1));
        var decision = journey.Step([command]);
        var evidence = Assert.Single(decision.MaterialEvidence);
        Assert.Equal(WorldMaterialEvidenceKind.Transfer, evidence.Kind);
        var childId = Assert.Single(evidence.CreatedIds);
        var child = journey.World.Inventory.Custody.GetLot(childId);
        Assert.Equal(destination, child.CustodianId);
        Assert.Equal(lot.Source.SourceId, child.Source.SourceId);
        Assert.Equal(lot.Source.Start, child.Source.Start);
        Assert.Equal(1UL, child.Quantity);
        Assert.Equal(lot.Quantity - 1, journey.World.Inventory.Custody.GetLot(lot.LotId).Quantity);
        Assert.Equal(lot.Source.Start + 1, journey.World.Inventory.Custody.GetLot(lot.LotId).Source.Start);
        Assert.Equal(before.Production!.ContentHash, evidence.Before.Production);
        Assert.Equal(evidence.Before.Production, evidence.After.Production);
        Assert.Equal(journey.World.Inventory.ContentHash, evidence.After.Inventory);
        journey.ReopenExactly();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Refused_actor_leaves_a_real_action_ordinal_gap_without_a_phantom_material_effect(int refusedIndex)
    {
        using var journey = new MaterialEvidenceJourney();
        var (node, _) = journey.GatherBerries();
        journey.WalkToBerryConnector(node.Feet);
        journey.EarnEffort(journey.Player);
        var commands = new[] { journey.GatherBerries().Command, journey.CraftHelperDose() }.OrderBy(value => value.ActorId).ToArray();
        var rejected = commands[refusedIndex];
        commands[refusedIndex] = new(rejected.CommandId, rejected.PrincipalId, rejected.OriginSessionId,
            rejected.ActorId, rejected.WorldId, rejected.BranchId, rejected.ExpectedActorRevision + 1, rejected.Intent);
        var decision = journey.Step(commands, accepted: false);
        Assert.Equal(WorldActionRejection.RevisionConflict, decision.Receipts.Single(value => value.CommandId == rejected.CommandId).Rejection);
        var evidence = Assert.Single(decision.MaterialEvidence);
        Assert.Equal(1 - refusedIndex, evidence.ActionOrdinal);
        Assert.Equal(commands[1 - refusedIndex].CommandId, evidence.Command.CommandId);
        journey.ReopenExactly();
    }

    [Fact]
    public void Late_complete_world_reward_refusal_emits_no_evidence_or_partial_delivery()
    {
        var basis = new CareWorldFixture(rewardExperience: 1000, invalidNormalizedSpeedOnReward: true).Genesis;
        var remedy = basis.Care!.Remedies.Single();
        var player = basis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        var form = basis.Space.GetActor(player).Binding;
        var definition = new RestorativeUseDefinition(new(0x11223344, 990001), remedy.DefinitionId, remedy.Version,
            RestorativeDeliveryMode.SelfUse, [new(form.DefinitionId, form.DefinitionVersion)]);
        using var journey = new MaterialEvidenceJourney(WorldRestorativeAdmission.CreateFresh(basis, new([definition], [], [])));
        var command = journey.PrepareQuestDelivery();
        var before = journey.World;
        var standalone = new DeliveryQuestPlanner().Plan(before, before.Inventory, before.Production!, before.Progression!,
            before.Quests!, command, before.Dialogues);
        Assert.True(standalone.Succeeded);
        var decision = journey.Step([command], accepted: false);
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(decision.Receipts).Rejection);
        Assert.Empty(decision.MaterialEvidence);
        Assert.Equal(before.Inventory.ContentHash, journey.World.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, journey.World.Production!.ContentHash);
        Assert.Equal(before.Progression!.ContentHash, journey.World.Progression!.ContentHash);
        Assert.Equal(before.Quests!.ContentHash, journey.World.Quests!.ContentHash);
        Assert.Equal(before.Reservoirs!.ContentHash, journey.World.Reservoirs!.ContentHash);
        Assert.Equal(before.LiquidCells!.ContentHash, journey.World.LiquidCells!.ContentHash);
        journey.ReopenExactly();
    }
}
