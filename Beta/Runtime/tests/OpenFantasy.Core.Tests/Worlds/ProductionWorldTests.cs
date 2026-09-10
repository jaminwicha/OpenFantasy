using OpenFantasy.Core.Content;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Tests.Gathering;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;
using static OpenFantasy.Core.Tests.Worlds.ProductionWorldFixture;

namespace OpenFantasy.Core.Tests.Worlds;

public sealed class ProductionWorldTests
{
    [Fact]
    public void UpgradePreservesProgressAndDoesNotAdvanceSimulation()
    {
        WorldSnapshot basis = GatheringFixture.Create(step: 40);
        WorldProductionMigration request = Migration(basis);
        WorldProductionMigrationDecision decision = new WorldProductionMigrationProcessor().Decide(basis, request);
        WorldSnapshot upgraded = decision.Candidate;
        Assert.Equal(3, upgraded.ProfileVersion);
        Assert.Equal(basis.Revision + 1, upgraded.Revision);
        Assert.Equal(basis.AdvancingSteps, upgraded.AdvancingSteps);
        Assert.Equal(basis.Calendar, upgraded.Calendar);
        Assert.Same(basis.Inventory, upgraded.Inventory);
        Assert.Same(basis.Space, upgraded.Space);
        Assert.Same(basis.Gathering, upgraded.Gathering);
        Assert.Equal(basis.Residents, upgraded.Residents);
        Assert.Equal(5, decision.AdmittedDefinitionIds.Count);
        Assert.DoesNotContain(Material.DefinitionId, decision.AdmittedDefinitionIds);
        Assert.Equal(WorldProductionMigrationCodecV1.Encode(request),
            WorldProductionMigrationCodecV1.Encode(WorldProductionMigrationCodecV1.Decode(WorldProductionMigrationCodecV1.Encode(request))));
        byte[] bytes = WorldSnapshotCodec.Encode(upgraded);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));
        Assert.Throws<ArgumentException>(() => WorldSnapshotCodecV2.Encode(upgraded));
        Assert.Throws<InvalidOperationException>(() => new WorldProductionMigrationProcessor().Decide(upgraded, request));
    }

    [Theory]
    [InlineData(1100UL)]
    [InlineData(1001UL)]
    [InlineData(1004UL)]
    [InlineData(1005UL)]
    public void UpgradeRejectsHistoricalIdentityCollisions(ulong occupied)
    {
        WorldSnapshot basis = GatheringFixture.Create();
        Assert.Throws<InvalidOperationException>(() => new WorldProductionMigrationProcessor().Decide(basis, Migration(basis), id => id == Id(occupied)));
    }

    [Fact]
    public void CraftRetiresRealInputsAndReservesEveryProducedIdentity()
    {
        WorldSnapshot world = ProductionWorldFixture.Create();
        WorldActionCommand action = Craft(world);
        WorldStepDecision decision = new WorldStepProcessor().Decide(world, Batch(world, 1300, action));
        Assert.Equal(WorldActionRejection.None, decision.Receipts.Single().Rejection);
        Assert.Equal(4UL, decision.Candidate.Inventory.Custody.GetLot(Id(70)).Quantity);
        Assert.Equal(1UL, decision.Candidate.Inventory.Custody.GetLot(Id(70)).Revision);
        Assert.Single(decision.Candidate.Production!.Batches);
        Assert.Equal(2UL, decision.Candidate.Production.Batches.Single().Ingredients.Single().Quantity);
        Assert.Equal(1UL, decision.Candidate.Production.Lots.Single().Quantity);
        Assert.Equal(2, decision.Identities.Count(identity => identity.Kind == WorldCommittedIdentityKind.SpawnedEntity));
        Assert.Equal(world.AdvancingSteps + 1, decision.Candidate.AdvancingSteps);
        Assert.Equal(world.Gathering!.GetActorWork(Id(10)).Effort, decision.Candidate.Gathering!.GetActorWork(Id(10)).Effort);
        byte[] snapshot = WorldSnapshotCodec.Encode(decision.Candidate);
        Assert.Equal(snapshot, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(snapshot)));
        byte[] command = WorldActionCodec.Encode(action);
        Assert.Equal(command, WorldActionCodec.Encode(WorldActionCodec.Decode(command)));
        byte[] batch = WorldStepCodec.Encode(Batch(world, 1300, action));
        Assert.Equal(batch, WorldStepCodec.Encode(WorldStepCodec.Decode(batch)));
    }

    [Fact]
    public void HistoricalOutputCollisionRejectsWholeCraftWithoutConsumingIngredients()
    {
        WorldSnapshot world = ProductionWorldFixture.Create();
        WorldActionCommand action = Craft(world);
        EntityId output = EntityId.DeriveSpawned(action.CommandId, 1);
        WorldStepDecision decision = new WorldStepProcessor().Decide(world, Batch(world, 1300, action), id => id == output);
        Assert.Equal(WorldActionRejection.Unavailable, decision.Receipts.Single().Rejection);
        Assert.Same(world.Inventory, decision.Candidate.Inventory);
        Assert.Same(world.Production, decision.Candidate.Production);
        Assert.DoesNotContain(decision.Identities, identity => identity.Kind == WorldCommittedIdentityKind.SpawnedEntity);
    }

    [Fact]
    public void ReadingHoldDoesNotCraftOrPurchaseWorkOrTime()
    {
        WorldSnapshot world = ProductionWorldFixture.Create();
        WorldActionCommand speak = StepFixture.Action(world, 1400, new SpeakToActorIntent(Id(11), 0));
        WorldSnapshot held = new WorldStepProcessor().Decide(world, Batch(world, 1401, speak)).Candidate;
        Assert.True(held.HasReadingHold);
        WorldSnapshot rejected = new WorldStepProcessor().Decide(held, Batch(held, 1402, Craft(held))).Candidate;
        Assert.Same(held.Production, rejected.Production);
        Assert.Same(held.Inventory, rejected.Inventory);
        Assert.Equal(held.Calendar, rejected.Calendar);
        Assert.Equal(held.AdvancingSteps, rejected.AdvancingSteps);
        Assert.Equal(held.Gathering!.ContentHash, rejected.Gathering!.ContentHash);
    }

    [Fact]
    public void OldProfilesCannotInterpretCraftingCommands()
    {
        WorldSnapshot world = ProductionWorldFixture.Create();
        Assert.Throws<ArgumentException>(() => new WorldStepBatch(Id(1300), world.WorldId, world.BranchId,
            world.Revision, world.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [Craft(world)], 2));
    }

    [Fact]
    public void GatheringCountsDerivedContentsAgainstTheSamePackCapacity()
    {
        WorldSnapshot initial = ProductionWorldFixture.Create(storageCapacity: 7);
        WorldSnapshot crafted = new WorldStepProcessor().Decide(initial, Batch(initial, 1500, Craft(initial))).Candidate;
        WorldActionCommand first = GatheringFixture.Command(crafted, 1501);
        WorldSnapshot full = new WorldStepProcessor().Decide(crafted, Batch(crafted, 1502, first)).Candidate;
        Assert.Equal(7UL, full.Production!.GetCombinedStoredQuantity(full.Inventory, Id(60)));
        WorldActionCommand second = GatheringFixture.Command(full, 1503);
        WorldStepDecision rejected = new WorldStepProcessor().Decide(full, Batch(full, 1504, second));
        Assert.Equal(WorldActionRejection.Unavailable, rejected.Receipts.Single().Rejection);
        Assert.Same(full.Inventory, rejected.Candidate.Inventory);
        Assert.Equal(full.Gathering!.GetNode(GatheringFixture.Node).NextUnissuedUnit,
            rejected.Candidate.Gathering!.GetNode(GatheringFixture.Node).NextUnissuedUnit);
    }

    [Fact]
    public void ConsumedGatheringRightsStillPreventCursorRewind()
    {
        WorldSnapshot initial = ProductionWorldFixture.Create();
        WorldActionCommand gather = GatheringFixture.Command(initial, 1600);
        WorldSnapshot harvested = new WorldStepProcessor().Decide(initial, Batch(initial, 1601, gather)).Candidate;
        WorldActionCommand craft = Craft(harvested, 1602,
            [new(IngredientLotKind.Source, Id(70), 0, 1),
             new(IngredientLotKind.Source, EntityId.DeriveSpawned(gather.CommandId, 0), 0, 1)]);
        WorldSnapshot crafted = new WorldStepProcessor().Decide(harvested, Batch(harvested, 1603, craft)).Candidate;
        Assert.Single(crafted.Production!.Batches);
        GatherNodeState node = crafted.Gathering!.GetNode(GatheringFixture.Node);
        var rewound = new GatherNodeState(node.NodeId, node.Definition, node.SourceId, node.UnitCapacity, node.Feet,
            node.Revision, 0, node.Definition.MaxSwings, null);
        var gathering = new GatheringSnapshot(crafted.WorldId, crafted.BranchId, crafted.AdvancingSteps,
            [rewound], crafted.Gathering.Tools, crafted.Gathering.Actors);
        Assert.Throws<ArgumentException>(() => new WorldSnapshot(crafted.Inventory, crafted.Space, crafted.Residents,
            crafted.AdmissionVersion, crafted.Calendar, crafted.Revision, crafted.AdvancingSteps,
            crafted.Dialogues, gathering, crafted.Production));
    }
}
