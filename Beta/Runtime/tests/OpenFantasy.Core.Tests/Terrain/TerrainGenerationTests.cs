using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Terrain;

public sealed class TerrainGenerationTests
{
    [Fact]
    public void EveryGeneratedCellAndIdentityMatchesIndependentIntegerOracle()
    {
        foreach (var example in TerrainFixture.Read().GetProperty("examples").EnumerateArray())
        {
            TerrainRegionSnapshot snapshot = TerrainFixture.Generate(example);
            Assert.Equal(example.GetProperty("rulesCanonicalHex").GetString(),
                Convert.ToHexString(TerrainRegionCodecV1.EncodeRules(snapshot.Rules)).ToLowerInvariant());
            Assert.Equal(example.GetProperty("rulesSha256").GetString(), snapshot.Rules.Version.Value);
            Assert.Equal(example.GetProperty("cells").GetArrayLength(), snapshot.Cells.Count);
            int index = 0;
            foreach (var expected in example.GetProperty("cells").EnumerateArray())
            {
                TerrainCell cell = snapshot.Cells[index++];
                Assert.Equal(TerrainFixture.Coordinate(expected.GetProperty("coordinate")), cell.Coordinate);
                Assert.Equal((TerrainStratum)expected.GetProperty("stratum").GetInt32(), cell.Stratum);
                Assert.Equal(expected.GetProperty("formationId").GetString(), cell.FormationId?.ToString());
                Assert.Equal(expected.GetProperty("sourceId").GetString(), cell.SourceId?.ToString());
                Assert.Equal(cell.Stratum != TerrainStratum.Void, cell.IsSolid);
                Assert.False(cell.IsExcavated);
                if (cell.IsSolid)
                {
                    Assert.Equal(TerrainFixture.Pin(expected.GetProperty("material")), cell.Material);
                    Assert.NotEqual(cell.FormationId, cell.SourceId);
                }
                else
                {
                    Assert.Null(cell.Material);
                }
            }
        }
    }

    [Fact]
    public void OverlappingAndVerticallySplitRegionsResolveIdenticalSubstrateRegardlessOfRequestOrder()
    {
        TerrainRegionSnapshot template = TerrainFixture.Generate(TerrainFixture.Example("negative-lattice"));
        var generator = new StratifiedTerrainGenerator();
        var whole = generator.Generate(template.Scope, template.Seed, template.Rules,
            new TerrainRegionBounds(new CellCoordinate(-10, -10, -5), 12, 12, 12));
        var slices = new[]
        {
            new TerrainRegionBounds(new CellCoordinate(-2, -10, -5), 4, 12, 12),
            new TerrainRegionBounds(new CellCoordinate(-10, -10, -5), 9, 12, 6),
            new TerrainRegionBounds(new CellCoordinate(-10, -10, 0), 9, 12, 7)
        };
        foreach (TerrainRegionBounds bounds in slices)
        {
            var slice = generator.Generate(template.Scope, template.Seed, template.Rules, bounds);
            foreach (TerrainCell actual in slice.Cells)
            {
                AssertSameCell(whole.GetCell(actual.Coordinate), actual);
            }
        }
        var repeated = generator.Generate(whole.Scope, whole.Seed, whole.Rules, whole.Bounds);
        Assert.Equal(whole.ComputeContentHash(), repeated.ComputeContentHash());
    }

    [Fact]
    public void BranchesShareInitialSourcesButScopeHashAndWorldRealmSeedRuleInputsRemainDistinct()
    {
        var initial = TerrainFixture.Generate(TerrainFixture.Example("flat"));
        var generator = new StratifiedTerrainGenerator();
        var branch = generator.Generate(initial.Scope with { BranchId = new EntityId(99, 2) },
            initial.Seed, initial.Rules, initial.Bounds);
        Assert.NotEqual(initial.ComputeContentHash(), branch.ComputeContentHash());
        for (int index = 0; index < initial.Cells.Count; index++)
        {
            AssertSameCell(initial.Cells[index], branch.Cells[index]);
        }
        TerrainRegionSnapshot[] changed =
        [
            generator.Generate(initial.Scope with { WorldId = new EntityId(99, 1) }, initial.Seed, initial.Rules, initial.Bounds),
            generator.Generate(initial.Scope with { RealmId = new EntityId(99, 3) }, initial.Seed, initial.Rules, initial.Bounds),
            generator.Generate(initial.Scope, new EntityId(99, 4), initial.Rules, initial.Bounds),
            generator.Generate(initial.Scope, initial.Seed,
                new StratifiedTerrainRules(0, 1, 2, initial.Rules.Materials), initial.Bounds)
        ];
        foreach (var other in changed)
        {
            Assert.NotEqual(initial.Cells[0].SourceId, other.Cells[0].SourceId);
            Assert.NotEqual(initial.ComputeContentHash(), other.ComputeContentHash());
        }
    }

    [Fact]
    public void IndexingUsesActualSignedThreeDimensionalExtentsAndCanonicalZyxOrder()
    {
        var bounds = new TerrainRegionBounds(new CellCoordinate(-3, -5, -7), 4, 3, 2);
        for (int index = 0; index < bounds.CellCount; index++)
        {
            Assert.Equal(index, bounds.IndexOf(bounds.CoordinateAt(index)));
            if (index > 0)
            {
                Assert.True(bounds.CoordinateAt(index - 1).CompareTo(bounds.CoordinateAt(index)) < 0);
            }
        }
        Assert.Equal(new CellCoordinate(0, -3, -6), bounds.Maximum);
        Assert.Equal(12, bounds.IndexOf(new CellCoordinate(-3, -5, -6)));
        Assert.False(bounds.Contains(bounds.Maximum.Offset(1, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => bounds.IndexOf(new CellCoordinate(-4, -5, -7)));
        Assert.Throws<ArgumentOutOfRangeException>(() => bounds.CoordinateAt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => bounds.CoordinateAt(bounds.CellCount));
    }

    [Fact]
    public void DimensionsRulesAndExtentArithmeticRejectUnsupportedOrOverflowingValues()
    {
        var palette = TerrainFixture.Generate(TerrainFixture.Example("flat")).Rules.Materials;
        foreach (int invalid in new[] { int.MinValue, -1, 0, 33, int.MaxValue })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TerrainRegionBounds(default, invalid, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TerrainRegionBounds(default, 1, invalid, 1));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new TerrainRegionBounds(default, 1, 1, 17));
        Assert.Throws<OverflowException>(() => new TerrainRegionBounds(new CellCoordinate(long.MaxValue, 0, 0), 2, 1, 1));
        Assert.Throws<OverflowException>(() => new TerrainRegionBounds(new CellCoordinate(0, long.MaxValue, 0), 1, 2, 1));
        Assert.Throws<OverflowException>(() => new TerrainRegionBounds(new CellCoordinate(0, 0, long.MaxValue), 1, 1, 2));
        Assert.Throws<OverflowException>(() => new CellCoordinate(long.MinValue, 0, 0).Offset(-1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StratifiedTerrainRules(0, -1, 1, palette));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StratifiedTerrainRules(0, 9, 1, palette));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StratifiedTerrainRules(0, 1, 0, palette));
        Assert.Throws<ArgumentOutOfRangeException>(() => new StratifiedTerrainRules(0, 1, 17, palette));
        Assert.Throws<OverflowException>(() => new StratifiedTerrainRules(long.MaxValue, 1, 1, palette));
        Assert.Throws<OverflowException>(() => new StratifiedTerrainRules(long.MinValue, 0, 1, palette));
        var minimum = new StratifiedTerrainRules(long.MinValue + 24, 8, 16, palette);
        var maximum = new StratifiedTerrainRules(long.MaxValue - 8, 8, 16, palette);
        Assert.Equal(long.MinValue + 24, minimum.BaseSurfaceZ);
        Assert.Equal(long.MaxValue - 8, maximum.BaseSurfaceZ);
    }

    [Fact]
    public void MaterialRolesMayShareOneExactPinButConflictingVersionsAndNullInputsReject()
    {
        var initial = TerrainFixture.Generate(TerrainFixture.Example("flat"));
        CountedItemDefinition pin = initial.Rules.Materials.Surface;
        var shared = new TerrainMaterialPalette(pin, pin, pin);
        Assert.Same(pin, shared.Stone);
        var conflict = new CountedItemDefinition(pin.DefinitionId, ContentHash.Compute("another-version"u8));
        Assert.Throws<ArgumentException>(() => new TerrainMaterialPalette(pin, conflict, pin));
        Assert.Throws<ArgumentNullException>(() => new TerrainMaterialPalette(null!, pin, pin));
        Assert.Throws<ArgumentNullException>(() => new TerrainMaterialPalette(pin, null!, pin));
        Assert.Throws<ArgumentNullException>(() => new TerrainMaterialPalette(pin, pin, null!));
        Assert.Throws<ArgumentNullException>(() => new StratifiedTerrainRules(0, 0, 1, null!));
        var generator = new StratifiedTerrainGenerator();
        Assert.Throws<ArgumentNullException>(() => generator.Generate(null!, initial.Seed, initial.Rules, initial.Bounds));
        Assert.Throws<ArgumentNullException>(() => generator.Generate(initial.Scope, initial.Seed, null!, initial.Bounds));
        Assert.Throws<ArgumentNullException>(() => generator.Generate(initial.Scope, initial.Seed, initial.Rules, null!));
    }

    internal static void AssertSameCell(TerrainCell expected, TerrainCell actual)
    {
        Assert.Equal(expected.Coordinate, actual.Coordinate);
        Assert.Equal(expected.Stratum, actual.Stratum);
        Assert.Equal(expected.Material, actual.Material);
        Assert.Equal(expected.FormationId, actual.FormationId);
        Assert.Equal(expected.SourceId, actual.SourceId);
        Assert.Equal(expected.IsExcavated, actual.IsExcavated);
    }
}
