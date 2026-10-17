using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Spatial;

public sealed class MovementTerrainFootprintTests
{
    [Theory]
    [InlineData(256, 0)]
    [InlineData(-256, 0)]
    [InlineData(0, 256)]
    [InlineData(0, -256)]
    public void Odd_body_extents_and_all_cardinal_sweeps_retain_real_clearance(long dx, long dy)
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, 256), SpatialFixture.Body(181, 141, 465));
        Compare(actor, GroundSupportMode.TerrainOnly, dx, dy, SpatialFixture.Terrain(), [], SpatialMoveStatus.Prepared);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stair_footprints_include_ascending_clearance_and_descending_endpoint_support(bool descending)
    {
        var actor = SpatialFixture.Actor(1, descending ? new(320, 128, 320) : new(160, 128, 256));
        var result = Compare(actor, GroundSupportMode.SteppedTerrainAndConstruction, descending ? -256 : 32, 0,
            SpatialFixture.Terrain(), [StairWalkingTests.Tread(1, 256, 480, 320)], SpatialMoveStatus.Prepared);
        Assert.Equal(descending ? 256 : 320, result.Candidate!.Feet.Z);
    }

    [Fact]
    public void Legacy_constructed_support_retains_its_refusal_instead_of_acquiring_stair_walking()
    {
        var actor = SpatialFixture.Actor(1, new(160, 128, 256));
        Compare(actor, GroundSupportMode.TerrainAndConstruction, 32, 0, SpatialFixture.Terrain(),
            [StairWalkingTests.Tread(1, 256, 480, 320)], SpatialMoveStatus.BlockedStructure);
    }

    [Fact]
    public void Negative_elevation_uses_floor_and_ceiling_cells_without_truncating_toward_zero()
    {
        var actor = SpatialFixture.Actor(1, new(128, 128, -256));
        var terrain = SpatialFixture.Terrain(floorZ: -2);
        Compare(actor, GroundSupportMode.SteppedTerrainAndConstruction, -256, 0, terrain, [], SpatialMoveStatus.Prepared);
        var footprint = MovementTerrainFootprint.Create(actor, GroundSupportMode.SteppedTerrainAndConstruction, -256, 0);
        Assert.Equal(-2, footprint.Bounds.Minimum.Z);
    }

    [Fact]
    public void Maximum_admitted_body_and_displacement_fit_the_existing_terrain_read_envelope()
    {
        var actor = SpatialFixture.Actor(1, new(0, 0, 256), SpatialFixture.Body(2048, 2048, 2048));
        Compare(actor, GroundSupportMode.SteppedTerrainAndConstruction, 256, 0,
            SpatialFixture.Terrain(x: -16, y: -16, width: 32, depth: 32, height: 12), [], SpatialMoveStatus.Prepared);
        var footprint = MovementTerrainFootprint.Create(actor, GroundSupportMode.SteppedTerrainAndConstruction, 256, 0);
        Assert.Equal((9, 8, 10), (footprint.Bounds.Width, footprint.Bounds.Depth, footprint.Bounds.Height));
    }

    [Fact]
    public void Invalid_motion_unadmitted_body_and_numeric_overflow_cannot_prepare_terrain()
    {
        var actor = SpatialFixture.Actor(1, new(0, 0, 256));
        foreach (var (x, y) in new (long, long)[] { (0, 0), (32, 32), (257, 0), (0, -257) })
            Assert.Throws<ArgumentOutOfRangeException>(() => MovementTerrainFootprint.Create(actor, GroundSupportMode.TerrainOnly, x, y));
        Assert.Throws<ArgumentException>(() => MovementTerrainFootprint.Create(
            SpatialFixture.Actor(1, actor.Feet, SpatialFixture.Body(2049, 128, 384)), GroundSupportMode.TerrainOnly, 32, 0));
        Assert.Throws<OverflowException>(() => MovementTerrainFootprint.Create(
            SpatialFixture.Actor(1, new(long.MaxValue - 100, 0, 256)), GroundSupportMode.TerrainOnly, 256, 0));
    }

    private static SpatialMoveResult Compare(ActorPresence actor, GroundSupportMode mode, long dx, long dy,
        TerrainRegionSnapshot complete, SpatialStructure[] structures, SpatialMoveStatus status)
    {
        var full = new LocalSpatialSnapshot(actor.Scope, [complete], [actor], structures, mode);
        var footprint = MovementTerrainFootprint.Create(actor, mode, dx, dy);
        Assert.Same(actor, footprint.Actor);
        var selected = Crop(complete, footprint.Bounds);
        var plan = new TerrainReadPlan(actor.Scope, footprint.Bounds, [TerrainRegionReference.Capture(selected)], 1);
        var decoded = TerrainRegionCodecV1.Decode(TerrainRegionCodecV1.Encode(selected), plan.References.Single());
        var local = new LocalSpatialSnapshot(actor.Scope, plan.Resolve([decoded]).Regions, [actor], structures, mode);
        var expected = full.PrepareMove(actor.ActorId, actor.Revision, dx, dy);
        var actual = local.PrepareMove(actor.ActorId, actor.Revision, dx, dy);
        Assert.Equal(status, actual.Status);
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.Candidate?.Feet, actual.Candidate?.Feet);
        Assert.Equal(expected.Candidate?.Revision, actual.Candidate?.Revision);
        return actual;
    }

    private static TerrainRegionSnapshot Crop(TerrainRegionSnapshot source, TerrainRegionBounds bounds)
    {
        var region = new StratifiedTerrainGenerator().Generate(source.Scope, source.Seed, source.Rules, bounds);
        var edits = region.Cells.Where(cell => source.GetCell(cell.Coordinate).IsExcavated)
            .Select(cell => cell.Coordinate).ToArray();
        foreach (var batch in edits.Chunk(TerrainRegionSnapshot.MaximumExcavationBatch))
            region = region.Excavate(region.Revision, batch);
        foreach (var cell in region.Cells)
        {
            var original = source.GetCell(cell.Coordinate);
            Assert.Equal(original.SourceId, cell.SourceId);
            Assert.Equal(original.IsSolid, cell.IsSolid);
            Assert.Equal(original.IsExcavated, cell.IsExcavated);
        }
        return region;
    }
}
