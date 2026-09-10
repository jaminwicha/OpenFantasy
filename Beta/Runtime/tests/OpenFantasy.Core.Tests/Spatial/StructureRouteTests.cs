using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using static OpenFantasy.Core.Tests.Spatial.StructureGeometryTests;

namespace OpenFantasy.Core.Tests.Spatial;

public sealed class StructureRouteTests
{
    [Fact]
    public void ActualBodyRoutesAroundGuardAndEveryWaypointHasClearSweptTraversal()
    {
        var actor = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256));
        var space = new LocalSpatialSnapshot(SpatialFixture.Scope, [SpatialFixture.Terrain()], [actor],
            [Structure(700, 500, -128, 256, 520, 400, 340)]);
        var destination = new SubunitPosition(896, 128, 256);
        GroundRouteResult route = space.QueryGroundRoute(actor.ActorId, destination);
        Assert.Equal(GroundRouteStatus.Reachable, route.Status);
        Assert.Equal(destination, route.Waypoints[^1]);
        Assert.Contains(route.Waypoints, point => point.Y != 128);
        foreach (SubunitPosition waypoint in route.Waypoints)
        {
            var current = space.GetActor(actor.ActorId);
            var move = space.PrepareMove(actor.ActorId, current.Revision, waypoint.X - current.Feet.X, waypoint.Y - current.Feet.Y);
            Assert.Equal(SpatialMoveStatus.Prepared, move.Status);
            space = space.WithActors([move.Candidate!]);
        }
    }

    [Fact]
    public void PlacementRejectsClosingTheOnlyGroundRoute()
    {
        var actor = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256));
        var space = SpatialFixture.Scene(SpatialFixture.Terrain(0, 0, 4, 1, 4), actor);
        var destination = new SubunitPosition(896, 128, 256);
        Assert.Equal(GroundRouteStatus.Reachable, space.QueryGroundRoute(actor.ActorId, destination).Status);
        var result = space.PrepareStructurePlacement(Structure(700, 500, 0, 256, 520, 256, 300),
            [new GroundAccessRequirement(actor.ActorId, actor.Feet, destination)]);
        Assert.NotEqual(StructurePlacementStatus.Prepared, result.Status);
        Assert.Null(result.Candidate);
        Assert.Empty(space.Structures);
    }

    [Fact]
    public void SearchBoundIsExplicitAndCannotAuthorizeAnUnprovenRoute()
    {
        var actor = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256));
        var space = SpatialFixture.Scene(SpatialFixture.Terrain(), actor);
        Assert.Equal(GroundRouteStatus.ExpansionLimit,
            space.QueryGroundRoute(actor.ActorId, new SubunitPosition(1664, 128, 256), 1).Status);
        Assert.Throws<ArgumentOutOfRangeException>(() => space.QueryGroundRoute(actor.ActorId, actor.Feet, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => space.QueryGroundRoute(actor.ActorId, actor.Feet, 257));
    }

    [Fact]
    public void OffGridNegativeFeetUseCheckedCardinalConnectors()
    {
        var actor = SpatialFixture.Actor(1, new SubunitPosition(-87, -43, 256));
        var space = SpatialFixture.Scene(SpatialFixture.Terrain(), actor);
        var target = new SubunitPosition(913, -13, 256);
        var route = space.QueryGroundRoute(actor.ActorId, target);
        Assert.Equal(GroundRouteStatus.Reachable, route.Status);
        foreach (var point in route.Waypoints)
        {
            var current = space.GetActor(actor.ActorId);
            var move = space.PrepareMove(actor.ActorId, current.Revision, point.X - current.Feet.X, point.Y - current.Feet.Y);
            Assert.Equal(SpatialMoveStatus.Prepared, move.Status);
            space = space.WithActors([move.Candidate!]);
        }
        Assert.Equal(target, space.GetActor(actor.ActorId).Feet);
    }

    [Fact]
    public void DeterministicRouteIsIndependentOfStructureEnumerationOrder()
    {
        var actor = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256));
        SpatialStructure[] structures = [Structure(701, 500, -100, 256, 520, 400, 300),
            Structure(700, 700, 500, 256, 800, 520, 300)];
        var terrain = SpatialFixture.Terrain();
        var one = new LocalSpatialSnapshot(SpatialFixture.Scope, [terrain], [actor], structures);
        var two = new LocalSpatialSnapshot(SpatialFixture.Scope, [terrain], [actor], structures.Reverse());
        var target = new SubunitPosition(1152, 128, 256);
        Assert.Equal(one.QueryGroundRoute(actor.ActorId, target).Waypoints, two.QueryGroundRoute(actor.ActorId, target).Waypoints);
    }

    [Fact]
    public void PlacementNeedsExplicitBoundedAccessAndRejectsUnsupportedDestination()
    {
        var actor = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256));
        var terrain = SpatialFixture.Terrain();
        terrain = terrain.Excavate(terrain.Revision, [new CellCoordinate(3, 0, 0)]);
        var space = SpatialFixture.Scene(terrain, actor);
        var guard = Structure(700, 500, 500, 256, 510, 510, 300);
        Assert.Throws<ArgumentException>(() => space.PrepareStructurePlacement(guard, []));
        Assert.Equal(StructurePlacementStatus.AccessBlocked, space.PrepareStructurePlacement(guard,
            [new GroundAccessRequirement(actor.ActorId, actor.Feet, new SubunitPosition(896, 128, 256))]).Status);
        Assert.Equal(GroundRouteStatus.DifferentFloor,
            space.QueryGroundRoute(actor.ActorId, new SubunitPosition(128, 128, 512)).Status);
    }
    [Fact]
    public void ReplanningAfterEveryControllerSizedSampleActuallyArrivesWithoutOscillating()
    {
        var actor = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256));
        var space = new LocalSpatialSnapshot(SpatialFixture.Scope, [SpatialFixture.Terrain()], [actor],
            [Structure(700, 500, -128, 256, 520, 400, 340)]);
        var target = new SubunitPosition(896, 128, 256);
        int samples = 0;
        while (space.GetActor(actor.ActorId).Feet != target && samples < 100)
        {
            var current = space.GetActor(actor.ActorId);
            var route = space.QueryGroundRoute(actor.ActorId, target);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            var next = Assert.IsType<SubunitPosition>(route.NextWaypoint);
            long dx = Math.Clamp(next.X - current.Feet.X, -32, 32);
            long dy = Math.Clamp(next.Y - current.Feet.Y, -32, 32);
            var move = space.PrepareMove(actor.ActorId, current.Revision, dx, dy);
            Assert.Equal(SpatialMoveStatus.Prepared, move.Status);
            space = space.WithActors([move.Candidate!]);
            samples++;
        }
        Assert.Equal(target, space.GetActor(actor.ActorId).Feet);
        Assert.InRange(samples, 1, 99);
    }

    [Fact]
    public void AdmissionOfAFreeStandingGuardRetainsUnchangedActorsAndRealAccess()
    {
        var actor = SpatialFixture.Actor(1, new SubunitPosition(128, 128, 256));
        var space = SpatialFixture.Scene(SpatialFixture.Terrain(), actor);
        var guard = Structure(700, 500, 500, 256, 604, 534, 320);
        var result = space.PrepareStructurePlacement(guard,
            [new GroundAccessRequirement(actor.ActorId, actor.Feet, new SubunitPosition(1152, 128, 256))]);
        Assert.Equal(StructurePlacementStatus.Prepared, result.Status);
        Assert.Single(result.Candidate!.Structures);
        Assert.Same(actor, result.Candidate.GetActor(actor.ActorId));
        Assert.Empty(space.Structures);
    }

}
