using OpenFantasy.Core.Spatial;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Server.Tests;

public sealed class NativeExpeditionTravelTests
{
    [Fact]
    public void Separated_lanes_keep_the_real_corridor_turn_clear_when_a_peer_stops()
    {
        var world = AdventureWorldAdmission.CreateEmbedded(AdventureWorldAdmission.SeedText, ["Rowan", "Mira"]);
        var lead = world.Excavation!.Sites.Single().OwnerActorId;
        var helper = world.Residents.Single(row => row.ControllerKind == OpenFantasy.Core.Worlds.ResidentControllerKind.Player
            && row.ActorId != lead).ActorId;
        var old = world.Space.WithActors(world.Space.Actors.Select(row => row.ActorId == lead || row.ActorId == helper
            ? new ActorPresence(row.Binding, row.Condition, row.Scope,
                row.ActorId == lead ? new(88512, 89440, 256) : new(88224, 89440, 256), row.Revision, row.HomePlaceId) : row));
        Assert.NotEqual(GroundRouteStatus.Reachable, old.QueryGroundRoute(helper, new(88448, 89472, 256)).Status);
        var separated = old.WithActors(old.Actors.Select(row => row.ActorId == helper
            ? new ActorPresence(row.Binding, row.Condition, row.Scope, new(88192, 88960, 256), row.Revision, row.HomePlaceId) : row));
        Assert.Equal(GroundRouteStatus.Reachable, separated.QueryGroundRoute(lead, new(88704, 89472, 256)).Status);
        Assert.Equal(GroundRouteStatus.Reachable, separated.QueryGroundRoute(helper, new(88192, 86912, 256)).Status);
    }

    [Fact]
    public void Rendezvous_uses_the_chiefs_real_entry_range_for_both_people()
    {
        var world = AdventureWorldAdmission.CreateEmbedded(AdventureWorldAdmission.SeedText, ["Rowan", "Mira"]);
        var lead = world.Excavation!.Sites.Single().OwnerActorId;
        var helper = world.Residents.Single(row => row.ControllerKind == OpenFantasy.Core.Worlds.ResidentControllerKind.Player
            && row.ActorId != lead).ActorId;
        var scenario = world.EncounterLifecycle!.Scenarios.Single(row => row.TargetActorId == lead
            && world.GetResident(row.InitiatingActorId).ControllerKind == OpenFantasy.Core.Worlds.ResidentControllerKind.Host);
        var chief = world.Space.GetActor(scenario.InitiatingActorId).Feet;
        Assert.Equal(400L, scenario.EntryRangeSubunits);
        var space = world.Space.WithActors(world.Space.Actors.Select(row => row.ActorId == lead || row.ActorId == helper
            ? new ActorPresence(row.Binding, row.Condition, row.Scope,
                row.ActorId == lead ? chief.Offset(-256, 0, 0) : chief.Offset(0, -384, 0), row.Revision, row.HomePlaceId) : row));
        Assert.Equal(SpatialInteraction.Reachable, space.QueryInteraction(scenario.InitiatingActorId, lead, scenario.EntryRangeSubunits));
        Assert.Equal(SpatialInteraction.Reachable, space.QueryInteraction(scenario.InitiatingActorId, helper, scenario.EntryRangeSubunits));
        var original = space.GetActor(helper);
        var wrongLane = space.WithActors(space.Actors.Select(row => row.ActorId == helper
            ? new ActorPresence(row.Binding, row.Condition, row.Scope, chief.Offset(-512, 0, 0), original.Revision, row.HomePlaceId) : row));
        Assert.NotEqual(SpatialInteraction.Reachable, wrongLane.QueryInteraction(scenario.InitiatingActorId, helper, scenario.EntryRangeSubunits));
    }

    [Theory]
    [InlineData(24832, 89504)]
    [InlineData(25088, 89504)]
    [InlineData(24992, 89344)]
    public void Advice_near_a_real_merchant_makes_progress_across_the_entire_release_envelope(long x, long y)
    {
        var world = AdventureWorldAdmission.CreateEmbedded(AdventureWorldAdmission.SeedText, ["Rowan", "Mira"]);
        var lead = world.Excavation!.Sites.Single().OwnerActorId;
        var original = world.Space.GetActor(lead);
        var from = new SubunitPosition(x, y, original.Feet.Z);
        var goal = new SubunitPosition(24960, 89472, original.Feet.Z);
        var space = world.Space.WithActors(world.Space.Actors.Select(row => row.ActorId == lead
            ? new ActorPresence(original.Binding, original.Condition, original.Scope, from, original.Revision, original.HomePlaceId) : row));
        var planned = NativeExpeditionTravel.NextLeg(space, lead, goal);
        long dx = Math.Sign(planned.X - from.X) * 32;
        long dy = Math.Sign(planned.Y - from.Y) * 32;
        Assert.True((dx == 0) != (dy == 0));
        long initialDistance = Distance(from, goal);
        int samples = checked((int)(Distance(from, planned) / 32));
        for (int sample = 1; sample <= samples + 4; sample++)
        {
            var move = space.PrepareMove(lead, space.GetActor(lead).Revision, dx, dy);
            Assert.Equal(SpatialMoveStatus.Prepared, move.Status);
            var actual = move.Candidate!;
            space = space.WithActors(space.Actors.Select(row => row.ActorId == lead ? actual : row));
            if (sample >= Math.Max(1, samples - 3)) Assert.True(Distance(actual.Feet, goal) < initialDistance,
                "Any admitted streamed endpoint must improve the actual destination distance.");
        }
    }

    [Fact]
    public void Road_advice_allows_early_release_without_losing_safe_progress()
    {
        var world = AdventureWorldAdmission.CreateEmbedded(AdventureWorldAdmission.SeedText, ["Rowan", "Mira"]);
        var lead = world.Excavation!.Sites.Single().OwnerActorId;
        var original = world.Space.GetActor(lead);
        var from = new SubunitPosition(61824, 89408, 256);
        var goal = new SubunitPosition(63808, 89408, 256);
        var space = world.Space.WithActors(world.Space.Actors.Select(row => row.ActorId == lead
            ? new ActorPresence(original.Binding, original.Condition, original.Scope, from, original.Revision, original.HomePlaceId) : row));
        var planned = NativeExpeditionTravel.NextLeg(space, lead, goal);
        long dx = Math.Sign(planned.X - from.X) * 32;
        long dy = Math.Sign(planned.Y - from.Y) * 32;
        int samples = checked((int)(Distance(from, planned) / 32));
        Assert.InRange(samples, 1, 64);
        for (int sample = 1; sample <= samples + 4; sample++)
        {
            var move = space.PrepareMove(lead, space.GetActor(lead).Revision, dx, dy);
            Assert.Equal(SpatialMoveStatus.Prepared, move.Status);
            var actual = move.Candidate!;
            space = space.WithActors(space.Actors.Select(row => row.ActorId == lead ? actual : row));
            if (sample >= Math.Max(1, samples - 3))
                Assert.True(Distance(actual.Feet, goal) < Distance(from, goal));
        }
    }

    private static long Distance(SubunitPosition a, SubunitPosition b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
}
