using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Spatial;

public sealed class SpatialPlacementReadSetTests
{
    [Theory]
    [InlineData(StructurePlacementStatus.Prepared)]
    [InlineData(StructurePlacementStatus.BlockedTerrain)]
    [InlineData(StructurePlacementStatus.BlockedActor)]
    [InlineData(StructurePlacementStatus.BlockedStructure)]
    [InlineData(StructurePlacementStatus.Unsupported)]
    [InlineData(StructurePlacementStatus.Unavailable)]
    public void Prepared_geometry_preserves_ordinary_clearance_and_support_results(StructurePlacementStatus expected)
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        var normal = Box(512, 512, 256);
        var proposed = new SpatialStructure(SpatialFixture.Id(6000), [expected switch
        {
            StructurePlacementStatus.BlockedActor => Box(128, 128, 256),
            StructurePlacementStatus.Unsupported => Box(512, 512, 512),
            StructurePlacementStatus.Unavailable => Box(20_000, 512, 256),
            _ => normal
        }]);
        SpatialStructure[] existing = expected == StructurePlacementStatus.BlockedStructure
            ? [new(SpatialFixture.Id(6001), [normal])] : [];
        var terrain = SpatialFixture.Terrain(keptSolids: expected == StructurePlacementStatus.BlockedTerrain
            ? [new CellCoordinate(2, 2, 1)] : []);
        var scene = new LocalSpatialSnapshot(SpatialFixture.Scope, [terrain], [actor], existing);
        GroundAccessRequirement[] access = [new(actor.ActorId, actor.Feet, new(640, 128, 256))];
        var read = Read(scene).Read;
        Assert.Equal(expected, scene.PrepareStructurePlacement(proposed, access).Status);
        Assert.Equal(expected, read.Assess(proposed, access));
        Assert.Equal(existing.Length, scene.Structures.Count);
        Assert.Equal(existing.Length, read.Occupancy.Structures.Count);
    }

    [Fact]
    public void Proposed_wall_cannot_claim_access_by_detouring_outside_the_declared_reads()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        var scene = SpatialFixture.Scene(SpatialFixture.Terrain(), actor);
        var proposed = new SpatialStructure(SpatialFixture.Id(6000), [new SubunitBox(new(376, 0, 256), new(392, 256, 768))]);
        GroundAccessRequirement[] access = [new(actor.ActorId, actor.Feet, new(640, 128, 256))];
        Assert.Equal(StructurePlacementStatus.Prepared, scene.PrepareStructurePlacement(proposed, access).Status);
        var route = SpatialRouteReadSetTests.Read(scene, new(new(0, 0, 0), 3, 1, 4)).Read;
        var read = new SpatialPlacementReadSet(route.Terrain, route.Occupancy, scene.GroundSupport, scene.StructureSupport);
        Assert.Equal(StructurePlacementStatus.Unavailable, read.Assess(proposed, access));
        Assert.Empty(scene.Structures);
    }

    [Fact]
    public void A_clear_supported_structure_that_seals_an_exit_is_still_refused()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        var scene = SpatialFixture.Scene(SpatialFixture.Terrain(), actor);
        var enclosure = new SpatialStructure(SpatialFixture.Id(6000),
            [new SubunitBox(new(0, 0, 256), new(32, 512, 1280)),
             new SubunitBox(new(480, 0, 256), new(512, 512, 1280)),
             new SubunitBox(new(32, 0, 256), new(480, 32, 1280)),
             new SubunitBox(new(32, 480, 256), new(480, 512, 1280))]);
        GroundAccessRequirement[] access = [new(actor.ActorId, actor.Feet, new(640, 128, 256))];
        Assert.Equal(StructurePlacementStatus.AccessBlocked, scene.PrepareStructurePlacement(enclosure, access).Status);
        Assert.Equal(StructurePlacementStatus.AccessBlocked, Read(scene).Read.Assess(enclosure, access));
        Assert.Empty(scene.Structures);
    }

    [Fact]
    public void Stair_access_preserves_the_finite_budget_and_ordinary_default()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        var scene = StairWalkingTests.Scene(actor, StairWalkingTests.Tread(1, 256, 480, 320),
            StairWalkingTests.Tread(2, 480, 704, 384));
        var proposed = new SpatialStructure(SpatialFixture.Id(6000), [Box(512, 512, 256)]);
        GroundAccessRequirement[] access = [new(actor.ActorId, actor.Feet, new(608, 128, 384))];
        var prepared = Read(scene).Read;
        Assert.Equal(StructurePlacementStatus.Prepared, prepared.Assess(proposed, access));
        Assert.Equal(scene.PrepareStructurePlacement(proposed, access).Status, prepared.Assess(proposed, access));
        Assert.Equal(StructurePlacementStatus.AccessLimit, prepared.Assess(proposed, access, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => prepared.Assess(proposed, access, 4097));
    }

    [Fact]
    public void Arrival_at_the_site_invalidates_old_reads_and_new_assessment_refuses_collision()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        var scene = SpatialFixture.Scene(SpatialFixture.Terrain(), actor);
        var (prepared, directory) = Read(scene);
        var proposed = new SpatialStructure(SpatialFixture.Id(6000), [Box(512, 512, 256)]);
        GroundAccessRequirement[] access = [new(actor.ActorId, actor.Feet, new(640, 128, 256))];
        Assert.Equal(StructurePlacementStatus.Prepared, prepared.Assess(proposed, access));
        var arrival = SpatialFixture.Actor(2, new(544, 544, 256));
        var changed = directory.Apply(0, [new(null, SpatialOccupancyReference.Capture(arrival))]);
        var terrain = scene.Regions.Select(TerrainRegionReference.Capture).ToArray();
        Assert.Throws<InvalidOperationException>(() => prepared.ValidateCurrent(changed.ReadPage, terrain,
            scene.GroundSupport, scene.StructureSupport));
        prepared.ValidateCurrent(directory.ReadPage, terrain, scene.GroundSupport, scene.StructureSupport);
        Assert.Throws<InvalidOperationException>(() => prepared.ValidateCurrent(directory.ReadPage, terrain,
            scene.GroundSupport, StructureSupportMode.TerrainAndConstruction));
        var updated = scene.WithActors([actor, arrival]);
        Assert.Equal(StructurePlacementStatus.BlockedActor, Read(updated).Read.Assess(proposed, access));
    }

    [Fact]
    public void Access_identity_and_support_contracts_cannot_be_bypassed()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256));
        var scene = SpatialFixture.Scene(SpatialFixture.Terrain(), actor);
        var read = Read(scene).Read;
        var proposed = new SpatialStructure(SpatialFixture.Id(6000), [Box(512, 512, 256)]);
        var access = new GroundAccessRequirement(actor.ActorId, actor.Feet, new(640, 128, 256));
        Assert.Throws<ArgumentException>(() => read.Assess(proposed, []));
        Assert.Throws<ArgumentException>(() => read.Assess(proposed, Enumerable.Repeat(access, 33)));
        Assert.Throws<ArgumentException>(() => read.Assess(proposed, [access with { ActorId = SpatialFixture.Id(9999) }]));
        Assert.Throws<ArgumentException>(() => read.Assess(new(actor.ActorId, proposed.Boxes), [access]));
        Assert.Throws<ArgumentException>(() => new SpatialPlacementReadSet(read.Terrain, read.Occupancy,
            scene.GroundSupport, (StructureSupportMode)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => read.Assess(proposed, [access], 257));
    }

    private static (SpatialPlacementReadSet Read, SpatialOccupancyDirectory Directory) Read(LocalSpatialSnapshot scene)
    {
        var (route, directory) = SpatialRouteReadSetTests.Read(scene, scene.Regions.Single().Bounds);
        return (new(route.Terrain, route.Occupancy, scene.GroundSupport, scene.StructureSupport), directory);
    }

    private static SubunitBox Box(long x, long y, long z) => new(new(x, y, z), new(x + 64, y + 64, z + 64));
}
