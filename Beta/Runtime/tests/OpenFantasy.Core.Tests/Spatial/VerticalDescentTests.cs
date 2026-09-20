using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Spatial;

public sealed class VerticalDescentTests
{
    [Theory]
    [InlineData(256, 0)]
    [InlineData(768, 512)]
    public void Grounded_and_clear_unsupported_actor_poses_reach_the_known_floor(long feetZ, long expectedDrop)
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, feetZ), SpatialFixture.Body(64, 64, 128));
        TerrainRegionSnapshot terrain = Region();
        if (expectedDrop != 0)
        {
            Assert.Throws<ArgumentException>(() => new LocalSpatialSnapshot(SpatialFixture.Scope, [terrain], [actor]));
        }
        var query = Query([terrain], actors: [actor]);
        AssertContact(query.QueryActor(actor.ActorId, 1024), VerticalDescentStatus.Landed, expectedDrop,
            new(new(96, 96, 256), new(160, 160, 384)));
        Assert.Equal(feetZ, actor.Feet.Z);
        Assert.Equal(0UL, actor.Revision);
    }

    [Fact]
    public void A_partial_high_terrain_ledge_stops_before_the_fully_supported_lower_floor()
    {
        var terrain = SpatialFixture.Terrain(keptSolids: [new(0, 0, 1), new(0, 0, 2)]);
        var falling = Box(192, 96, 1024, 320, 160, 1152);
        AssertContact(Query([terrain]).QueryBox(falling, 1024), VerticalDescentStatus.IncompleteSupport, 256,
            Box(192, 96, 768, 320, 160, 896));
    }

    [Fact]
    public void One_subunit_obstacle_without_bearing_cannot_be_tunneled_through_to_a_lower_platform()
    {
        var thin = new SpatialStructure(SpatialFixture.Id(1001), [Box(96, 96, 700, 128, 160, 701)]);
        var lower = Platform(2, 0, 256, 512);
        var falling = Box(96, 96, 1024, 160, 160, 1152);
        AssertContact(Query(structures: [lower, thin]).QueryBox(falling, 1024),
            VerticalDescentStatus.IncompleteSupport, 323, Box(96, 96, 701, 160, 160, 829));
    }

    [Fact]
    public void Contact_with_a_small_interior_obstacle_is_detected_without_corner_sampling()
    {
        var interior = new SpatialStructure(SpatialFixture.Id(1001), [Box(127, 127, 899, 128, 128, 900)]);
        var falling = Box(32, 32, 1024, 224, 224, 1152);
        AssertContact(Query(structures: [interior]).QueryBox(falling, 1024),
            VerticalDescentStatus.IncompleteSupport, 124, Box(32, 32, 900, 224, 224, 1028));
    }

    [Theory]
    [InlineData(0, VerticalDescentStatus.Landed)]
    [InlineData(1, VerticalDescentStatus.IncompleteSupport)]
    public void Landing_requires_exact_union_coverage_even_when_colliders_cover_a_one_tick_slit(long slit,
        VerticalDescentStatus expected)
    {
        var floor = new SpatialStructure(SpatialFixture.Id(1001), [Box(0, 0, 256, 256, 256, 512)],
            [new(new(0, 0, 128000, 256000), 512000),
             new(new(128000 + slit, 0, 256000, 256000), 512000)]);
        AssertContact(Query(structures: [floor]).QueryBox(Box(0, 0, 768, 256, 256, 896), 768),
            expected, 256, Box(0, 0, 512, 256, 256, 640));
    }

    [Fact]
    public void Adjacent_tops_at_different_standing_planes_do_not_borrow_stair_contact_tolerance()
    {
        var left = Platform(1, 0, 128, 512);
        var right = Platform(2, 128, 256, 511);
        AssertContact(Query(structures: [right, left]).QueryBox(Box(32, 32, 768, 224, 224, 896), 768),
            VerticalDescentStatus.IncompleteSupport, 256, Box(32, 32, 512, 224, 224, 640));
    }

    [Fact]
    public void Exact_tops_with_the_same_outward_standing_plane_keep_the_existing_vertical_tolerance()
    {
        var floor = new SpatialStructure(SpatialFixture.Id(1001), [Box(0, 0, 256, 256, 256, 512)],
            [new(new(0, 0, 128000, 256000), 511001), new(new(128000, 0, 256000, 256000), 512000)]);
        AssertContact(Query(structures: [floor]).QueryBox(Box(32, 32, 768, 224, 224, 896), 768),
            VerticalDescentStatus.Landed, 256, Box(32, 32, 512, 224, 224, 640));
    }

    [Fact]
    public void Terrain_and_structure_faces_can_jointly_cover_the_landing_footprint()
    {
        var terrain = SpatialFixture.Terrain(keptSolids: [new(0, 0, 1)]);
        var floor = Platform(1, 256, 512, 512);
        AssertContact(Query([terrain], [floor]).QueryBox(Box(192, 96, 768, 320, 160, 896), 768),
            VerticalDescentStatus.Landed, 256, Box(192, 96, 512, 320, 160, 640));
    }

    [Fact]
    public void A_stationary_actor_stops_descent_without_becoming_a_supporting_floor()
    {
        var obstacle = SpatialFixture.Actor(2, new(128, 128, 256), SpatialFixture.Body(64, 64, 128));
        AssertContact(Query(actors: [obstacle]).QueryBox(Box(96, 96, 768, 160, 160, 896), 768),
            VerticalDescentStatus.ActorContact, 384, Box(96, 96, 384, 160, 160, 512));
    }

    [Fact]
    public void Only_actor_queries_exclude_their_own_body_and_boxes_exclude_no_obstacles()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 768), SpatialFixture.Body(64, 64, 128));
        var query = Query(actors: [actor]);
        Assert.Equal(VerticalDescentStatus.Landed, query.QueryActor(actor.ActorId, 1024).Status);
        AssertError(query.QueryBox(Box(96, 96, 768, 160, 160, 896), 1024), VerticalDescentStatus.InitialActorOverlap);
        var floor = Platform(1, 0, 256, 512);
        AssertError(Query(structures: [floor]).QueryBox(floor.Boxes.Single(), 1024),
            VerticalDescentStatus.InitialStructureOverlap);
    }

    [Fact]
    public void Initial_terrain_structure_and_other_actor_overlap_are_distinct_failures()
    {
        AssertError(Query().QueryBox(Box(96, 96, 128, 160, 160, 384), 1024), VerticalDescentStatus.InitialTerrainOverlap);
        var floor = Platform(1, 0, 256, 512);
        AssertError(Query(structures: [floor]).QueryBox(Box(96, 96, 500, 160, 160, 628), 1024),
            VerticalDescentStatus.InitialStructureOverlap);
        var first = SpatialFixture.Actor(1, new(128, 128, 768), SpatialFixture.Body(64, 64, 128));
        var second = SpatialFixture.Actor(2, new(144, 128, 768), first.Condition.Definition);
        AssertError(Query(actors: [first, second]).QueryActor(first.ActorId, 1024), VerticalDescentStatus.InitialActorOverlap);
    }

    [Fact]
    public void A_missing_intermediate_layer_cannot_be_skipped_to_the_known_floor_below()
    {
        var below = Region(new(new(0, 0, 0), 1, 1, 2));
        var above = Region(new(new(0, 0, 3), 1, 1, 3));
        AssertError(Query([below, above]).QueryBox(Box(32, 32, 1024, 224, 224, 1152), 1024),
            VerticalDescentStatus.Unavailable);
    }

    [Theory]
    [InlineData(3, VerticalDescentStatus.Landed)]
    [InlineData(4, VerticalDescentStatus.Unavailable)]
    public void Missing_below_a_known_early_landing_is_irrelevant_but_its_support_layer_must_be_loaded(int minimumZ,
        VerticalDescentStatus expected)
    {
        var known = Region(new(new(0, 0, minimumZ), 1, 1, 4));
        var floor = new SpatialStructure(SpatialFixture.Id(1001), [Box(0, 0, 896, 256, 256, 1024)],
            [new(new(0, 0, 256000, 256000), 1024000)]);
        VerticalDescentResult result = Query([known], [floor]).QueryBox(Box(32, 32, 1280, 224, 224, 1408), 16384);
        if (expected == VerticalDescentStatus.Landed)
        {
            AssertContact(result, expected, 256, Box(32, 32, 1024, 224, 224, 1152));
        }
        else { AssertError(result, expected); }
    }

    [Fact]
    public void Split_regions_and_input_order_preserve_the_same_first_contact()
    {
        var whole = Region(new(new(0, 0, 0), 2, 1, 8));
        var left = Region(new(new(0, 0, 0), 1, 1, 8));
        var right = Region(new(new(1, 0, 0), 1, 1, 8));
        var first = Platform(1, 0, 256, 512);
        var second = Platform(2, 256, 512, 512);
        var initial = Box(128, 32, 1024, 384, 224, 1152);
        var expected = Box(128, 32, 512, 384, 224, 640);
        foreach (var query in new[] { Query([whole], [first, second]), Query([left, right], [first, second]),
                     Query([right, left], [second, first]) })
        {
            AssertContact(query.QueryBox(initial, 1024), VerticalDescentStatus.Landed, 512, expected);
        }
    }

    [Fact]
    public void A_finite_clear_prefix_does_not_invent_a_landing_beyond_its_requested_limit()
    {
        AssertContact(Query().QueryBox(Box(32, 32, 1024, 224, 224, 1152), 512),
            VerticalDescentStatus.NoContactWithinLimit, 512, Box(32, 32, 512, 224, 224, 640));
    }

    [Fact]
    public void Exact_maximum_drop_lands_while_one_subunit_short_stays_in_flight()
    {
        Assert.Equal(16384L, VerticalDescentQuery.MaximumDropSubunits);
        TerrainRegionSnapshot[] regions = Enumerable.Range(0, 5)
            .Select(index => Region(new(new(0, 0, index * 16), 1, 1, 16))).ToArray();
        var query = Query(regions);
        var initial = Box(32, 32, 16640, 96, 96, 16704);
        AssertContact(query.QueryBox(initial, 16384), VerticalDescentStatus.Landed, 16384,
            Box(32, 32, 256, 96, 96, 320));
        AssertContact(query.QueryBox(initial, 16383), VerticalDescentStatus.NoContactWithinLimit, 16383,
            Box(32, 32, 257, 96, 96, 321));
    }

    [Fact]
    public void Signed_coordinates_use_floor_cells_and_do_not_truncate_toward_zero()
    {
        var terrain = Region(new(new(-2, -2, -3), 4, 4, 8), floorZ: -3);
        AssertContact(Query([terrain]).QueryBox(Box(-200, -200, -128, -40, -40, 0), 1024),
            VerticalDescentStatus.Landed, 384, Box(-200, -200, -512, -40, -40, -384));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(16385)]
    public void Invalid_drop_ranges_are_explicit_api_errors(long maximumDrop) =>
        Assert.ThrowsAny<ArgumentException>(() => Query().QueryBox(Box(32, 32, 768, 96, 96, 896), maximumDrop));

    [Fact]
    public void Overwide_box_and_actor_bodies_cannot_exceed_the_bounded_scene_profile()
    {
        Assert.ThrowsAny<ArgumentException>(() => Query().QueryBox(Box(0, 0, 768, 2049, 64, 896), 1024));
        var actor = SpatialFixture.Actor(1, new(128, 128, 768), SpatialFixture.Body(width: 2049));
        Assert.ThrowsAny<ArgumentException>(() => Query(actors: [actor]).QueryActor(actor.ActorId, 1024));
    }

    [Fact]
    public void Signed_overflow_returns_numeric_limit_without_publishing_a_wrapped_pose()
    {
        AssertError(Query().QueryBox(Box(0, 0, long.MinValue + 10, 64, 64, long.MinValue + 74), 20),
            VerticalDescentStatus.NumericLimit);
    }

    [Fact]
    public void An_unrepresentable_starting_actor_body_is_rejected_when_the_query_scene_is_created()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, long.MaxValue - 10), SpatialFixture.Body(64, 64, 128));
        Assert.Throws<OverflowException>(() => Query(actors: [actor]));
    }

    [Fact]
    public void Candidate_scenes_reject_ambiguous_terrain_and_duplicate_or_aliased_obstacle_roles()
    {
        TerrainRegionSnapshot terrain = Region();
        Assert.Throws<ArgumentException>(() => Query(regions: []));
        Assert.Throws<ArgumentException>(() => Query(regions: [terrain, terrain]));
        Assert.Throws<ArgumentException>(() => Query(regions: [terrain, Region(floorZ: 1)]));
        var alias = new TerrainScope(SpatialFixture.Scope.WorldId, SpatialFixture.Scope.WorldId, SpatialFixture.Scope.RealmId);
        Assert.Throws<ArgumentException>(() => new VerticalDescentQuery(alias, [terrain], [], []));
        ActorPresence actor = SpatialFixture.Actor(1, new(128, 128, 768));
        Assert.Throws<ArgumentException>(() => Query(actors: [actor, actor]));
        var obstacle = new SpatialStructure(actor.ActorId, [Box(0, 0, 256, 256, 256, 512)]);
        Assert.Throws<ArgumentException>(() => Query(structures: [obstacle], actors: [actor]));
        Assert.Throws<ArgumentException>(() => Query(structures: [obstacle, obstacle]));
    }

    private static VerticalDescentQuery Query(IEnumerable<TerrainRegionSnapshot>? regions = null,
        IEnumerable<SpatialStructure>? structures = null, IEnumerable<ActorPresence>? actors = null) =>
        new(SpatialFixture.Scope, regions ?? [Region()], structures ?? [], actors ?? []);

    private static TerrainRegionSnapshot Region(TerrainRegionBounds? bounds = null, long floorZ = 0)
    {
        var material = new CountedItemDefinition(SpatialFixture.Id(8000), ContentHash.Compute("descent-material"u8));
        var rules = new StratifiedTerrainRules(floorZ, 0, 1, new(material, material, material));
        return new StratifiedTerrainGenerator().Generate(SpatialFixture.Scope, SpatialFixture.Id(7000), rules,
            bounds ?? new(new(-2, -2, 0), 6, 6, 8));
    }

    private static SpatialStructure Platform(ulong id, long minimumX, long maximumX, long top) =>
        new(SpatialFixture.Id(1000 + id), [Box(minimumX, 0, 256, maximumX, 256, top)],
            [new(new((Int128)minimumX * 1000, 0, (Int128)maximumX * 1000, 256000), (Int128)top * 1000)]);

    private static SubunitBox Box(long x0, long y0, long z0, long x1, long y1, long z1) =>
        new(new(x0, y0, z0), new(x1, y1, z1));

    private static void AssertContact(VerticalDescentResult result, VerticalDescentStatus status, long drop, SubunitBox reached)
    {
        Assert.Equal(status, result.Status);
        Assert.Equal(drop, result.DropSubunits);
        Assert.Equal(reached, result.ReachedVolume);
    }

    private static void AssertError(VerticalDescentResult result, VerticalDescentStatus status)
    {
        Assert.Equal(status, result.Status);
        Assert.Equal(0L, result.DropSubunits);
        Assert.Null(result.ReachedVolume);
    }
}
