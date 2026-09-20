using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Spatial;

public sealed class ConstructionSupportModeTests
{
    [Fact]
    public void Foreign_assembly_support_is_explicit_and_preserved_by_spatial_successors()
    {
        var lower = Slab(1, 0, 256, 256, 512);
        var upper = Slab(2, 0, 256, 512, 768);
        var rider = SpatialFixture.Actor(1, new(128, 128, 768));
        var terrain = SpatialFixture.Terrain();
        Assert.Throws<ArgumentException>(() => new LocalSpatialSnapshot(SpatialFixture.Scope, [terrain],
            [rider], [lower, upper], GroundSupportMode.SteppedTerrainAndConstruction));

        var space = Coupled(terrain, [lower, upper], [rider]);
        Assert.Equal(SpatialOccupancy.Clear, space.QueryOccupancy(rider.ActorId, rider.Feet));
        Assert.Equal(StructureSupportMode.TerrainAndConstruction, space.WithActors([rider]).StructureSupport);
        Assert.Equal(StructureSupportMode.TerrainAndConstruction, space.WithStructures([upper, lower]).StructureSupport);
        Assert.Throws<ArgumentException>(() => space.WithStructures([upper]));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LocalSpatialSnapshot(SpatialFixture.Scope,
            [terrain], [], structureSupport: (StructureSupportMode)2));
    }

    [Fact]
    public void Rounded_top_fringes_and_missing_lower_support_cannot_complete_a_foreign_base()
    {
        var box = new SubunitBox(new(0, 0, 256), new(256, 256, 512));
        var lower = new SpatialStructure(SpatialFixture.Id(1101), [box],
            [new StructureBearingSurface(new(0, 0, 128000, 256000), 512000),
             new StructureBearingSurface(new(128001, 0, 256000, 256000), 512000)]);
        var upper = Slab(2, 0, 256, 512, 768);
        Assert.Throws<ArgumentException>(() => Coupled(SpatialFixture.Terrain(), [lower, upper], []));

        var terrain = SpatialFixture.Terrain();
        terrain = terrain.Excavate(terrain.Revision, [new CellCoordinate(0, 0, 0)]);
        Assert.Throws<ArgumentException>(() => Coupled(terrain, [Slab(1, 0, 256, 256, 512), upper], []));
    }

    [Fact]
    public void Terrain_and_foreign_material_jointly_cover_a_complete_structure_base()
    {
        var terrain = SpatialFixture.Terrain(keptSolids: [new CellCoordinate(0, 0, 1)]);
        var lower = Slab(1, 256, 512, 256, 512);
        var upper = Slab(2, 0, 512, 512, 768);
        var space = Coupled(terrain, [upper, lower], []);
        Assert.Equal(2, space.Structures.Count);
        Assert.Throws<ArgumentException>(() => space.WithStructures([upper]));
        Assert.Throws<ArgumentException>(() => Coupled(terrain, [upper, lower],
            [SpatialFixture.Actor(1, new(384, 128, 512))]));
    }

    [Theory]
    [InlineData(1, 192)]
    [InlineData(2, 256)]
    [InlineData(32, 2176)]
    public void Coupled_policy_has_an_exact_finite_work_bound_and_canonical_archive(int assemblies, int work)
    {
        var policy = new CoupledSettlementPolicy(assemblies);
        Assert.Equal(work, policy.MaximumWorkUnits);
        byte[] bytes = CoupledSettlementPolicyCodecV1.Encode(policy);
        var restored = CoupledSettlementPolicyCodecV1.Decode(bytes);
        Assert.Equal(assemblies, restored.MaximumAssemblies);
        Assert.Equal(policy.ContentHash, restored.ContentHash);
        Assert.Equal(bytes, CoupledSettlementPolicyCodecV1.Encode(restored));
        Assert.Throws<FormatException>(() => CoupledSettlementPolicyCodecV1.Decode(bytes.Concat(new byte[] { 0 }).ToArray()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CoupledSettlementPolicy(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CoupledSettlementPolicy(33));
    }

    private static LocalSpatialSnapshot Coupled(TerrainRegionSnapshot terrain,
        IEnumerable<SpatialStructure> structures, IEnumerable<ActorPresence> actors) =>
        new(SpatialFixture.Scope, [terrain], actors, structures,
            GroundSupportMode.SteppedTerrainAndConstruction, StructureSupportMode.TerrainAndConstruction);

    private static SpatialStructure Slab(ulong number, long minimumX, long maximumX, long bottom, long top) =>
        new(SpatialFixture.Id(1100 + number), [new SubunitBox(new(minimumX, 0, bottom), new(maximumX, 256, top))],
            [new StructureBearingSurface(new((Int128)minimumX * 1000, 0, (Int128)maximumX * 1000, 256000),
                (Int128)top * 1000)]);
}
