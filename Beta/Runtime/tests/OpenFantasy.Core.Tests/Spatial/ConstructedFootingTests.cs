using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Spatial;

public sealed class ConstructedFootingTests
{
    [Fact]
    public void Installed_top_supports_a_body_only_in_explicit_support_mode()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 512));
        var floor = Floor(1, 0, 256);
        var space = Scene([actor], [floor]);
        Assert.Equal(SpatialOccupancy.Clear, space.QueryOccupancy(actor.ActorId, actor.Feet));
        Assert.Equal(GroundSupportMode.TerrainAndConstruction, space.WithActors([actor]).GroundSupport);
        Assert.Throws<ArgumentException>(() => new LocalSpatialSnapshot(SpatialFixture.Scope,
            space.Regions, [actor], [floor]));
        Assert.Throws<ArgumentException>(() => space.WithStructures([]));
    }

    [Fact]
    public void Exact_positive_slit_refuses_support_even_when_rounded_colliders_touch()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 512));
        var box = new SubunitBox(new(0, 0, 256), new(256, 256, 512));
        var faces = new[] { new StructureBearingSurface(new(0, 0, 128000, 256000), 512000),
            new StructureBearingSurface(new(128001, 0, 256000, 256000), 512000) };
        var floor = new SpatialStructure(SpatialFixture.Id(900), [box], faces);
        Assert.Throws<ArgumentException>(() => Scene([actor], [floor]));
    }

    [Fact]
    public void Movement_checks_support_between_supported_endpoints()
    {
        var actor = SpatialFixture.Actor(1, new(64, 128, 512), SpatialFixture.Body(32, 32, 128));
        var space = Scene([actor], [Floor(1, 0, 96), Floor(2, 160, 256)]);
        Assert.Equal(SpatialOccupancy.Clear, space.QueryOccupancy(actor.ActorId, new(224, 128, 512)));
        Assert.Equal(SpatialMoveStatus.Unsupported, space.PrepareMove(actor.ActorId, 0, 160, 0).Status);
    }

    [Fact]
    public void Terrain_and_installed_member_jointly_cover_the_whole_footprint()
    {
        var actor = SpatialFixture.Actor(1, new(256, 128, 512));
        var terrain = SpatialFixture.Terrain(keptSolids: [new CellCoordinate(0, 0, 1)]);
        var space = new LocalSpatialSnapshot(SpatialFixture.Scope, [terrain], [actor],
            [Floor(1, 256, 512)], GroundSupportMode.TerrainAndConstruction);
        Assert.Equal(SpatialOccupancy.Clear, space.QueryOccupancy(actor.ActorId, actor.Feet));
    }

    [Fact]
    public void Missing_body_terrain_and_missing_structure_base_never_become_supported()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 512));
        Assert.Throws<ArgumentException>(() => new LocalSpatialSnapshot(SpatialFixture.Scope,
            [SpatialFixture.Terrain(height: 3)], [actor], [Floor(1, 0, 256)], GroundSupportMode.TerrainAndConstruction));
        var terrain = SpatialFixture.Terrain();
        terrain = terrain.Excavate(terrain.Revision, [new CellCoordinate(0, 0, 0)]);
        Assert.Throws<ArgumentException>(() => new LocalSpatialSnapshot(SpatialFixture.Scope,
            [terrain], [actor], [Floor(1, 0, 256)], GroundSupportMode.TerrainAndConstruction));
    }

    [Theory]
    [InlineData(512000, 512)]
    [InlineData(511001, 512)]
    [InlineData(-512001, -512)]
    [InlineData(-512000, -512)]
    public void Contact_rounds_up_with_less_than_one_subunit_tolerance(long physicalTop, long standingZ)
    {
        var surface = new StructureBearingSurface(new(0, 0, 1000, 1000), physicalTop);
        Assert.Equal(standingZ, surface.StandingZ);
        Assert.InRange((Int128)surface.StandingZ * 1000 - surface.TopTicks, 0, 999);
    }

    [Fact]
    public void Surface_must_fit_a_real_collider_top()
    {
        var box = new SubunitBox(new(0, 0, 256), new(256, 256, 512));
        Assert.Throws<ArgumentException>(() => new SpatialStructure(SpatialFixture.Id(900), [box],
            [new StructureBearingSurface(new(0, 0, 256001, 256000), 512000)]));
        Assert.Throws<ArgumentException>(() => new SpatialStructure(SpatialFixture.Id(900), [box],
            [new StructureBearingSurface(new(0, 0, 256000, 256000), 513000)]));
    }

    [Theory]
    [InlineData(256, SpatialOccupancy.Clear)]
    [InlineData(257, SpatialOccupancy.Unavailable)]
    public void Exact_union_budget_refuses_overflow_instead_of_dropping_patches(int count, SpatialOccupancy expected)
    {
        var actor = SpatialFixture.Actor(1, new(-512, 128, 256), SpatialFixture.Body(count, 32, 128));
        var structures = Enumerable.Range(0, count).Chunk(32).Select((indices, group) =>
            new SpatialStructure(SpatialFixture.Id((ulong)1000 + (ulong)group),
                [new SubunitBox(new(indices[0], 0, 256), new(indices[^1] + 1, 256, 512))],
                indices.Select(x => new StructureBearingSurface(new(x * 1000, 0, (x + 1) * 1000, 256000), 512000))));
        var space = Scene([actor], structures);
        Assert.Equal(expected, space.QueryOccupancy(actor.ActorId, new(count / 2, 128, 512)));
    }

    [Fact]
    public void Contact_tolerance_is_vertical_only_and_accepts_adjacent_physical_top_variation()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 512));
        var floor = new SpatialStructure(SpatialFixture.Id(900),
            [new SubunitBox(new(0, 0, 256), new(256, 256, 512))],
            [new StructureBearingSurface(new(0, 0, 128000, 256000), 511001),
             new StructureBearingSurface(new(128000, 0, 256000, 256000), 512000)]);
        Assert.Equal(SpatialOccupancy.Clear, Scene([actor], [floor]).QueryOccupancy(actor.ActorId, actor.Feet));
    }

    private static LocalSpatialSnapshot Scene(IEnumerable<ActorPresence> actors, IEnumerable<SpatialStructure> structures) =>
        new(SpatialFixture.Scope, [SpatialFixture.Terrain()], actors, structures, GroundSupportMode.TerrainAndConstruction);

    private static SpatialStructure Floor(ulong id, long minX, long maxX) => new(SpatialFixture.Id(1000 + id),
        [new SubunitBox(new(minX, 0, 256), new(maxX, 256, 512))],
        [new StructureBearingSurface(new((Int128)minX * 1000, 0, (Int128)maxX * 1000, 256000), 512000)]);
}
