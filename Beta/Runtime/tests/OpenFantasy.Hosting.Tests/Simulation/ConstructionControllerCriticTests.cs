using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class ConstructionControllerCriticTests
{
    [Fact]
    public void OffGridRetainedBuilderDefersInsteadOfConstructingAnIllegalShortMovement()
    {
        WorldSnapshot world = Create();
        ConstructionProject project = world.Construction!.Projects.Single();
        ActorPresence actor = world.Space.GetActor(project.BuilderActorId);
        world = WithFeet(world, actor.ActorId, actor.Feet.Offset(1, 0, 0));
        byte[] before = WorldSnapshotCodec.Encode(world);

        WorldMovementInput? movement = new LocalConstructionController().SelectMovement(world, project);

        Assert.Null(movement);
        Assert.Equal(before, WorldSnapshotCodec.Encode(world));
        Assert.NotEqual(0L, (project.WorkFeet.X - world.Space.GetActor(actor.ActorId).Feet.X) % 32);
    }

    [Fact]
    public void FinalAlignedControllerSampleKeepsTheFrozenThirtyTwoSubunitRule()
    {
        WorldSnapshot world = Create();
        ConstructionProject project = world.Construction!.Projects.Single();
        world = WithFeet(world, project.BuilderActorId, project.WorkFeet.Offset(-32, 0, 0));

        WorldMovementInput movement = Assert.IsType<WorldMovementInput>(
            new LocalConstructionController().SelectMovement(world, project));

        Assert.Equal(32L, movement.DeltaX);
        Assert.Equal(0L, movement.DeltaY);
        Assert.Equal(project.BuilderActorId, movement.ActorId);
        Assert.True(world.Inventory.CanControl(movement.Delivery, movement.ActorId));
    }

    [Fact]
    public void ActualAddressedHoldSuppressesNpcMovementAndWorkWithoutAllocatingCommands()
    {
        WorldSnapshot world = Create();
        ConstructionProject project = world.Construction!.Projects.Single();
        EntityId player = world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        world = WithFeet(world, project.BuilderActorId, project.WorkFeet);
        world = WithFeet(world, player, project.WorkFeet.Offset(-256, 0, 0));
        ActorControlBinding binding = world.Inventory.Controls.Single(value => value.ActorId == player);
        var speak = new WorldActionCommand(new EntityId(0, 8_801_001), binding.PrincipalId, binding.SessionId,
            player, world.WorldId, world.BranchId, world.Space.GetActor(player).Revision,
            new SpeakToActorIntent(project.BuilderActorId, world.Space.GetActor(project.BuilderActorId).Revision));
        var batch = new WorldStepBatch(new EntityId(0, 8_801_002), world.WorldId, world.BranchId,
            world.Revision, world.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [speak], world.ProfileVersion);
        WorldStepDecision decision = new WorldStepProcessor().Decide(world, batch);
        Assert.Equal(WorldActionRejection.None, decision.Receipts.Single().Rejection);
        WorldSnapshot held = decision.Candidate;
        Assert.True(held.HasReadingHold);
        int allocated = 0;
        EntityId Allocate() { allocated++; return new EntityId(0, 8_801_003); }

        Assert.Empty(new LocalNpcController().SelectMovement(held));
        Assert.Empty(new LocalNpcController().SelectActions(held, Allocate));
        Assert.Null(new LocalConstructionController().SelectWork(held, held.Construction!.Projects.Single(), Allocate));
        Assert.Equal(0, allocated);
        Assert.Equal(0UL, held.Construction.Projects.Single().Progress.CompletedUnits);
    }

    private static WorldSnapshot Create()
    {
        WorldSnapshot gathered = GatheringFarmAdmission.CreateEmbedded("construction-controller-critic", "Traveler");
        WorldSnapshot production = FarmSaveOpener.ProductionGenesis(gathered);
        WorldSnapshot quests = FarmQuestSaveOpener.CreateGenesis(production);
        return ConstructionFarmAdmission.CreateGenesis(quests);
    }

    private static WorldSnapshot WithFeet(WorldSnapshot world, EntityId actorId, SubunitPosition feet)
    {
        ActorPresence original = world.Space.GetActor(actorId);
        var relocated = new ActorPresence(original.Binding, original.Condition, original.Scope,
            feet, original.Revision, original.HomePlaceId);
        LocalSpatialSnapshot space = world.Space.WithActors(world.Space.Actors.Select(actor =>
            actor.ActorId == actorId ? relocated : actor));
        return new WorldSnapshot(world.Inventory, space, world.Residents, world.AdmissionVersion,
            world.Calendar, world.Revision, world.AdvancingSteps, world.Dialogues, world.Gathering,
            world.Production, world.Progression, world.Quests, world.Construction);
    }
}
