using OpenFantasy.Core;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class LocalNpcControllerTests
{
    [Fact]
    public void Versioned_controller_is_deterministic_and_uses_only_the_explicit_host_actor_binding()
    {
        WorldSnapshot state = WithAmblingNpc(Genesis());
        var controller = new LocalNpcController();
        WorldMovementInput input = Assert.Single(controller.SelectMovement(state));
        Assert.Equal("local.home-amble.v1", LocalNpcController.PolicyVersion);
        Assert.Equal(Id(11), input.ActorId);
        Assert.Equal(Id(101), input.Delivery.PrincipalId);
        Assert.Equal(Id(111), input.Delivery.SessionId);
        Assert.Equal(32, Math.Abs(input.DeltaX) + Math.Abs(input.DeltaY));
        WorldSnapshot restored = WorldSnapshotCodecV1.Decode(WorldSnapshotCodecV1.Encode(state));
        Assert.Equal(controller.SelectMovement(state).ToArray(), controller.SelectMovement(restored).ToArray());
        Assert.Equal(state.Calendar, restored.Calendar);
        Assert.Equal(0UL, state.AdvancingSteps);
        Assert.Empty(controller.SelectMovement(Genesis()));
    }

    [Fact]
    public void Idle_periods_and_reading_holds_produce_no_npc_samples()
    {
        WorldSnapshot initial = WithAmblingNpc(Genesis());
        var idle = new WorldSnapshot(initial.Inventory, initial.Space, initial.Residents, initial.AdmissionVersion,
            initial.Calendar, 16, 16);
        var controller = new LocalNpcController();
        Assert.Empty(controller.SelectMovement(idle));
        var dialogue = new DialogueSession(Id(2000), Id(10), Id(11), Id(92), Id(2001), initial.Calendar);
        var held = new WorldSnapshot(initial.Inventory, initial.Space, initial.Residents, initial.AdmissionVersion,
            initial.Calendar, 0, 0, [dialogue]);
        Assert.Empty(controller.SelectMovement(held));
    }

    [Fact]
    public void A_proposed_step_beyond_the_home_radius_is_omitted_before_authoritative_geometry()
    {
        WorldSnapshot initial = WithAmblingNpc(Genesis());
        var controller = new LocalNpcController();
        WorldMovementInput first = Assert.Single(controller.SelectMovement(initial));
        ActorPresence old = initial.Space.GetActor(Id(11));
        var feet = new SubunitPosition(old.Feet.X + Math.Sign(first.DeltaX) * 256,
            old.Feet.Y + Math.Sign(first.DeltaY) * 256, old.Feet.Z);
        var edge = new ActorPresence(old.Binding, old.Condition, old.Scope, feet, old.Revision, old.HomePlaceId);
        var space = new LocalSpatialSnapshot(initial.Space.Scope, initial.Space.Regions,
            initial.Space.Actors.Select(actor => actor.ActorId == edge.ActorId ? edge : actor));
        var state = new WorldSnapshot(initial.Inventory, space, initial.Residents, initial.AdmissionVersion,
            initial.Calendar, 0, 0);
        Assert.Empty(controller.SelectMovement(state));
        Assert.Equal(feet, state.Space.GetActor(Id(11)).Feet);
        Assert.Throws<ArgumentNullException>(() => controller.SelectMovement(null!));
    }
}
