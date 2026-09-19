using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Spatial;

public sealed class WalkingRouteTests
{
    [Fact]
    public void Every_waypoint_replays_through_the_same_ordinary_movement_kernel()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        var space = StairWalkingTests.Scene(actor, StairWalkingTests.Tread(1, 256, 480, 320),
            StairWalkingTests.Tread(2, 480, 704, 384));
        var destination = new SubunitPosition(608, 128, 384);
        var route = space.QueryWalkingRoute(actor.ActorId, destination);
        Assert.Equal(GroundRouteStatus.Reachable, route.Status);
        Assert.Equal(15, route.Waypoints.Count);
        Assert.Equal(route.Waypoints, space.QueryWalkingRoute(actor.ActorId, destination).Waypoints);
        foreach (SubunitPosition waypoint in route.Waypoints)
        {
            var current = space.GetActor(actor.ActorId);
            var move = space.PrepareMove(actor.ActorId, current.Revision, waypoint.X - current.Feet.X,
                waypoint.Y - current.Feet.Y);
            Assert.Equal(SpatialMoveStatus.Prepared, move.Status);
            Assert.Equal(waypoint, move.Candidate!.Feet);
            space = space.WithActors([move.Candidate]);
        }
        Assert.Equal(destination, space.GetActor(actor.ActorId).Feet);
    }

    [Fact]
    public void Missing_cells_budget_and_grid_residue_have_distinct_truthful_results()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        var space = StairWalkingTests.Scene(actor);
        Assert.Equal(GroundRouteStatus.Unaligned, space.QueryWalkingRoute(actor.ActorId, new(129, 128, 256)).Status);
        Assert.Equal(GroundRouteStatus.ExpansionLimit, space.QueryWalkingRoute(actor.ActorId, new(384, 128, 256), 1).Status);
        Assert.Equal(GroundRouteStatus.Unavailable, space.QueryWalkingRoute(actor.ActorId, new(20000, 128, 256)).Status);
        Assert.Throws<ArgumentOutOfRangeException>(() => space.QueryWalkingRoute(actor.ActorId, actor.Feet, 4097));
    }

    [Fact]
    public void Removed_treads_invalidate_the_route_and_cannot_snap_a_later_sample_upstairs()
    {
        var actor = SpatialFixture.Actor(1, new(160, 128, 256));
        var space = StairWalkingTests.Scene(actor, StairWalkingTests.Tread(1, 256, 480, 320));
        var route = space.QueryWalkingRoute(actor.ActorId, new(384, 128, 320));
        Assert.Equal(GroundRouteStatus.Reachable, route.Status);
        Assert.Equal(320, route.NextWaypoint!.Value.Z);
        space = space.WithStructures([]);
        Assert.Equal(GroundRouteStatus.Blocked, space.QueryWalkingRoute(actor.ActorId, new(384, 128, 320)).Status);
        var move = space.PrepareMove(actor.ActorId, 0, 32, 0);
        Assert.Equal(SpatialMoveStatus.Prepared, move.Status);
        Assert.Equal(256, move.Candidate!.Feet.Z);
    }

    [Fact]
    public void Numeric_boundary_refuses_without_wrapping()
    {
        var actor = SpatialFixture.Actor(1, new(long.MaxValue - 16, 128, 256), SpatialFixture.Body(1, 32, 128));
        var terrain = SpatialFixture.Terrain(x: SubunitPosition.CellOf(long.MaxValue) - 4);
        var space = new LocalSpatialSnapshot(SpatialFixture.Scope, [terrain], [actor],
            groundSupport: GroundSupportMode.SteppedTerrainAndConstruction);
        Assert.Equal(SpatialMoveStatus.NumericLimit, space.PrepareMove(actor.ActorId, 0, 32, 0).Status);
    }

    [Fact]
    public void Placement_uses_the_matching_walk_budget_without_changing_legacy_limits()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        var regions = new[] { SpatialFixture.Terrain(), SpatialFixture.Terrain(x: 12), SpatialFixture.Terrain(x: 28) };
        var space = new LocalSpatialSnapshot(SpatialFixture.Scope, regions, [actor],
            groundSupport: GroundSupportMode.SteppedTerrainAndConstruction);
        var structure = new SpatialStructure(SpatialFixture.Id(5000),
            [new SubunitBox(new(512, 512, 256), new(576, 576, 320))]);
        GroundAccessRequirement[] access = [new(actor.ActorId, actor.Feet, new(36 * 256 + 128, 128, 256))];
        Assert.Equal(StructurePlacementStatus.AccessLimit, space.PrepareStructurePlacement(structure, access, 256).Status);
        Assert.Equal(StructurePlacementStatus.Prepared, space.PrepareStructurePlacement(structure, access).Status);
        Assert.Equal(StructurePlacementStatus.Prepared, space.PrepareStructurePlacement(structure, access, 2048).Status);
        var legacy = new LocalSpatialSnapshot(SpatialFixture.Scope, regions, [actor]);
        Assert.Throws<ArgumentOutOfRangeException>(() => legacy.PrepareStructurePlacement(structure, access, 2048));
        Assert.Equal(StructurePlacementStatus.AccessLimit, legacy.PrepareStructurePlacement(structure, access).Status);
        GroundAccessRequirement[] nearbyAccess = [new(actor.ActorId, actor.Feet, new(384, 128, 256))];
        Assert.Equal(StructurePlacementStatus.Prepared, legacy.PrepareStructurePlacement(structure, nearbyAccess).Status);
    }

    [Fact]
    public void Frontiers_can_join_at_an_unexpanded_node_without_inventing_a_movement_sample()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        var space = StairWalkingTests.Scene(actor);
        var destination = actor.Feet.Offset(32, 0, 0);
        GroundRouteResult route = space.QueryWalkingRoute(actor.ActorId, destination, 1);
        Assert.Equal(GroundRouteStatus.Reachable, route.Status);
        Assert.Equal(1, route.ExpandedNodes);
        Assert.Equal(destination, Assert.Single(route.Waypoints));
        Assert.InRange(route.EvaluatedTransitions, 1, 16);
        Assert.Equal(destination, space.PrepareMove(actor.ActorId, 0, 32, 0).Candidate!.Feet);
    }

    [Fact]
    public void Supported_endpoints_do_not_hide_an_unloaded_corridor()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        var space = new LocalSpatialSnapshot(SpatialFixture.Scope,
            [SpatialFixture.Terrain(x: 0, y: 0, width: 1, depth: 2),
             SpatialFixture.Terrain(x: 2, y: 0, width: 1, depth: 2)], [actor],
            groundSupport: GroundSupportMode.SteppedTerrainAndConstruction);
        var destination = new SubunitPosition(640, 128, 256);
        Assert.Equal(SpatialOccupancy.Clear, space.QueryOccupancy(actor.ActorId, destination));
        GroundRouteResult route = space.QueryWalkingRoute(actor.ActorId, destination);
        Assert.Equal(GroundRouteStatus.Unavailable, route.Status);
        Assert.Empty(route.Waypoints);
    }

    [Fact]
    public void Reverse_search_cannot_route_through_a_closed_wall()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        var enclosure = new SpatialStructure(SpatialFixture.Id(6000),
            [new SubunitBox(new(0, 0, 256), new(32, 512, 1280)),
             new SubunitBox(new(480, 0, 256), new(512, 512, 1280)),
             new SubunitBox(new(32, 0, 256), new(480, 32, 1280)),
             new SubunitBox(new(32, 480, 256), new(480, 512, 1280))]);
        var space = StairWalkingTests.Scene(actor, enclosure);
        var destination = new SubunitPosition(640, 128, 256);
        Assert.Equal(SpatialOccupancy.Clear, space.QueryOccupancy(actor.ActorId, destination));
        GroundRouteResult route = space.QueryWalkingRoute(actor.ActorId, destination);
        Assert.Equal(GroundRouteStatus.Blocked, route.Status);
        Assert.Empty(route.Waypoints);
        Assert.InRange(route.EvaluatedTransitions, 1, LocalSpatialSnapshot.DefaultWalkingRouteExpansions * 16);
    }

    [Fact]
    public void Blocked_flat_predecessor_does_not_hide_unloaded_lower_support()
    {
        var basis = SpatialFixture.Terrain(x: -4, y: -4, width: 8, depth: 8);
        var generator = new StratifiedTerrainGenerator();
        TerrainRegionSnapshot Region(TerrainRegionBounds bounds) =>
            generator.Generate(basis.Scope, basis.Seed, basis.Rules, bounds);
        var upper = Region(new(new(-4, -4, 1), 8, 8, 7));
        foreach (var batch in upper.Cells.Where(cell => cell.IsSolid).Select(cell => cell.Coordinate)
            .Chunk(TerrainRegionSnapshot.MaximumExcavationBatch))
        {
            upper = upper.Excavate(upper.Revision, batch);
        }
        var actor = SpatialFixture.Actor(1, new(-112, 128, 256), SpatialFixture.Body(32, 32, 256));
        var obstacle = new SpatialStructure(SpatialFixture.Id(6001),
            [new SubunitBox(new(256, 112, 256), new(288, 144, 320)),
             new SubunitBox(new(224, 112, 544), new(256, 144, 576)),
             new SubunitBox(new(288, 112, 256), new(320, 144, 640)),
             new SubunitBox(new(256, 80, 256), new(288, 112, 640)),
             new SubunitBox(new(256, 144, 256), new(288, 176, 640))],
            [new StructureBearingSurface(new(256000, 112000, 288000, 144000), 320000)]);
        var space = new LocalSpatialSnapshot(SpatialFixture.Scope,
            [upper, Region(new(new(-4, -4, 0), 4, 8, 1)), Region(new(new(1, -4, 0), 3, 8, 1))],
            [actor], [obstacle], GroundSupportMode.SteppedTerrainAndConstruction);
        var destination = new SubunitPosition(272, 128, 320);
        Assert.Equal(SpatialOccupancy.Clear, space.QueryOccupancy(actor.ActorId, destination));
        Assert.Equal(SpatialOccupancy.BlockedStructure, space.QueryOccupancy(actor.ActorId, new(240, 128, 320)));
        Assert.Equal(SpatialOccupancy.Unavailable, space.QueryOccupancy(actor.ActorId, new(240, 128, 256)));
        Assert.Equal(GroundRouteStatus.Unavailable, space.QueryWalkingRoute(actor.ActorId, destination).Status);
    }
}
