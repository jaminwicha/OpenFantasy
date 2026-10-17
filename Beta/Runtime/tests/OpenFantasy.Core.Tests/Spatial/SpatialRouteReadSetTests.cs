using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Tests.Construction;

namespace OpenFantasy.Core.Tests.Spatial;

public sealed class SpatialRouteReadSetTests
{
    [Theory]
    [InlineData(128, 640)]
    [InlineData(-128, -640)]
    public void Prepared_flat_route_matches_complete_scene_and_replays_every_segment(long start, long finish)
    {
        var actor = SpatialFixture.Actor(1, new(start, 128, 256));
        var scene = SpatialFixture.Scene(SpatialFixture.Terrain(), actor);
        var prepared = Read(scene, scene.Regions.Single().Bounds).Read;
        var destination = new SubunitPosition(finish, 128, 256);
        var route = prepared.QueryGroundRoute(actor.ActorId, actor.Feet, destination);
        SameRoute(scene.QueryGroundRoute(actor.ActorId, destination), route);
        Assert.Equal(GroundRouteStatus.Reachable, route.Status);
        foreach (var waypoint in route.Waypoints)
        {
            var current = scene.GetActor(actor.ActorId);
            var move = scene.PrepareMove(actor.ActorId, current.Revision, waypoint.X - current.Feet.X, waypoint.Y - current.Feet.Y);
            Assert.Equal(SpatialMoveStatus.Prepared, move.Status);
            Assert.Equal(waypoint, move.Candidate!.Feet);
            scene = scene.WithActors([move.Candidate]);
        }
        Assert.Equal(destination, scene.GetActor(actor.ActorId).Feet);
    }

    [Fact]
    public void Loaded_archive_cells_outside_the_declared_query_are_not_available_to_the_route()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        var scene = SpatialFixture.Scene(SpatialFixture.Terrain(), actor);
        var bounds = new TerrainRegionBounds(new(0, 0, 0), 1, 1, 4);
        var read = Read(scene, bounds).Read;
        var destination = new SubunitPosition(384, 128, 256);
        Assert.True(read.Terrain.Regions.Single().Bounds.Contains(destination.Cell));
        Assert.Equal(GroundRouteStatus.Reachable, scene.QueryGroundRoute(actor.ActorId, destination).Status);
        Assert.Equal(GroundRouteStatus.Unavailable, read.QueryGroundRoute(actor.ActorId, actor.Feet, destination).Status);
        Assert.Equal(GroundRouteStatus.Unavailable, read.QueryGroundRoute(actor.ActorId, destination, actor.Feet).Status);
    }

    [Fact]
    public void Search_cannot_detour_through_unchecked_cells_even_with_both_endpoints_inside()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        var wall = new SpatialStructure(SpatialFixture.Id(6000), [new SubunitBox(new(376, 0, 256), new(392, 256, 768))]);
        var scene = new LocalSpatialSnapshot(SpatialFixture.Scope, [SpatialFixture.Terrain()], [actor], [wall]);
        var destination = new SubunitPosition(640, 128, 256);
        Assert.Equal(GroundRouteStatus.Reachable, scene.QueryGroundRoute(actor.ActorId, destination).Status);
        var read = Read(scene, new(new(0, 0, 0), 3, 1, 4)).Read;
        var route = read.QueryGroundRoute(actor.ActorId, actor.Feet, destination);
        Assert.Equal(GroundRouteStatus.Unavailable, route.Status);
        Assert.Empty(route.Waypoints);
    }

    [Fact]
    public void Prepared_stair_search_preserves_waypoints_order_budget_and_failure_statuses()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        var scene = StairWalkingTests.Scene(actor, StairWalkingTests.Tread(1, 256, 480, 320),
            StairWalkingTests.Tread(2, 480, 704, 384));
        var prepared = Read(scene, scene.Regions.Single().Bounds).Read;
        var destination = new SubunitPosition(608, 128, 384);
        SameRoute(scene.QueryWalkingRoute(actor.ActorId, destination), prepared.QueryWalkingRoute(actor.ActorId, actor.Feet, destination));
        SameRoute(scene.QueryGroundRoute(actor.ActorId, destination), prepared.QueryGroundRoute(actor.ActorId, actor.Feet, destination));
        Assert.Equal(GroundRouteStatus.ExpansionLimit, prepared.QueryWalkingRoute(actor.ActorId, actor.Feet, destination, 1).Status);
        Assert.Equal(GroundRouteStatus.Unaligned, prepared.QueryWalkingRoute(actor.ActorId, actor.Feet, new(129, 128, 256)).Status);
        Assert.Throws<ArgumentOutOfRangeException>(() => prepared.QueryWalkingRoute(actor.ActorId, actor.Feet, destination, 4097));
    }

    [Fact]
    public void Freshness_rejects_arrivals_changed_terrain_and_support_interpretation()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        var scene = SpatialFixture.Scene(SpatialFixture.Terrain(), actor);
        var (read, directory) = Read(scene, new(new(0, 0, 0), 4, 4, 4));
        var terrain = scene.Regions.Select(TerrainRegionReference.Capture).ToArray();
        read.ValidateCurrent(directory.ReadPage, terrain, scene.GroundSupport);
        var arrived = directory.Apply(0, [new(null, SpatialOccupancyReference.Capture(SpatialFixture.Actor(2, new(640, 128, 256))))]);
        Assert.Throws<InvalidOperationException>(() => read.ValidateCurrent(arrived.ReadPage, terrain, scene.GroundSupport));
        var changed = scene.Regions.Single().Excavate(scene.Regions.Single().Revision, [new(3, 3, 0)]);
        Assert.Throws<InvalidOperationException>(() => read.ValidateCurrent(directory.ReadPage,
            [TerrainRegionReference.Capture(changed)], scene.GroundSupport));
        Assert.Throws<InvalidOperationException>(() => read.ValidateCurrent(directory.ReadPage, terrain,
            GroundSupportMode.SteppedTerrainAndConstruction));
        var narrower = Read(scene, new(new(0, 0, 0), 3, 4, 4)).Read;
        Assert.Throws<ArgumentException>(() => new SpatialRouteReadSet(read.Terrain, narrower.Occupancy, scene.GroundSupport));
        Assert.Throws<ArgumentException>(() => new SpatialRouteReadSet(read.Terrain, read.Occupancy, (GroundSupportMode)999));
    }

    [Fact]
    public void Actual_funded_construction_access_agrees_before_and_after_paid_work_and_installation()
    {
        var world = ConstructionWorldFixture.Create();
        var originalStock = world.Inventory.Custody.Lots.Where(lot => lot.ReservedBy is not null).ToArray();
        CheckAccess(world.Space, world.Construction!.Projects.Single());
        for (ulong step = 0; step < 200; step++)
        {
            var decision = ConstructionWorldFixture.Step(world, 2000 + step);
            Assert.Equal(OpenFantasy.Core.Worlds.WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
            world = decision.Candidate;
            if (step == 0)
            {
                Assert.Equal(originalStock, world.Inventory.Custody.Lots.Where(lot => lot.ReservedBy is not null));
                CheckAccess(world.Space, world.Construction!.Projects.Single());
            }
        }
        Assert.True(world.Construction!.Projects.Single().IsCompleted);
        Assert.Single(world.Space.Structures);
        Assert.DoesNotContain(world.Inventory.Custody.Lots, lot => lot.ReservedBy == world.Construction.Projects.Single().ReservationId);
        CheckAccess(world.Space, world.Construction.Projects.Single());
    }

    private static void CheckAccess(LocalSpatialSnapshot scene, ConstructionProject project)
    {
        var prepared = Read(scene, scene.Regions.Single().Bounds).Read;
        foreach (var access in project.RequiredAccess)
        {
            var route = prepared.QueryGroundRoute(access.ActorId, access.From, access.To);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            SameRoute(scene.QueryGroundRoute(access.ActorId, access.From, access.To), route);
        }
    }

    private static (SpatialRouteReadSet Read, SpatialOccupancyDirectory Directory) Read(LocalSpatialSnapshot scene, TerrainRegionBounds bounds)
    {
        var directory = SpatialOccupancyDirectory.Create(scene.Scope,
            scene.Actors.Select(SpatialOccupancyReference.Capture).Concat(scene.Structures.Select(value => SpatialOccupancyReference.Capture(scene.Scope, value))),
            96, 128);
        var occupancy = new SpatialOccupancyReadPlan(scene.Scope, bounds, directory.ReadPage, 32, 64, 32);
        var detail = occupancy.Resolve(occupancy.Actors.Select(entry => scene.GetActor(entry.EntityId)),
            occupancy.Structures.Select(entry => scene.Structures.Single(value => value.StructureId == entry.EntityId)));
        var terrain = new TerrainReadPlan(scene.Scope, bounds, scene.Regions.Select(TerrainRegionReference.Capture), 16);
        return (new(terrain.Resolve(scene.Regions.Where(region => terrain.References.Any(reference => reference.Bounds == region.Bounds))),
            detail, scene.GroundSupport), directory);
    }

    private static void SameRoute(GroundRouteResult expected, GroundRouteResult actual)
    {
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.Waypoints, actual.Waypoints);
        Assert.Equal(expected.ExpandedNodes, actual.ExpandedNodes);
        Assert.Equal(expected.EvaluatedTransitions, actual.EvaluatedTransitions);
    }
}
