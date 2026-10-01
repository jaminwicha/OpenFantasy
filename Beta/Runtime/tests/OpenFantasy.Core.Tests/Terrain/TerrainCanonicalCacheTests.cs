using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Terrain;

public sealed class TerrainCanonicalCacheTests
{
    [Fact]
    public void Repeated_and_concurrent_exports_keep_the_independent_canonical_bytes()
    {
        foreach (var example in TerrainFixture.Read().GetProperty("examples").EnumerateArray())
        {
            foreach (var state in example.GetProperty("states").EnumerateArray())
            {
                byte[] expected = Convert.FromHexString(state.GetProperty("canonicalHex").GetString()!);
                TerrainRegionSnapshot region = TerrainRegionCodecV1.Decode(expected);
                byte[][] exports = new byte[16][];
                Parallel.For(0, exports.Length, index => exports[index] = TerrainRegionCodecV1.Encode(region));
                Assert.All(exports, bytes => Assert.Equal(expected, bytes));
                Array.Fill(exports[0], (byte)0);
                Assert.All(exports.Skip(1), bytes => Assert.Equal(expected, bytes));
                Assert.Equal(expected, TerrainRegionCodecV1.Encode(region));
            }
        }
    }

    [Fact]
    public void Equal_bounds_and_revision_do_not_alias_different_excavations()
    {
        TerrainRegionSnapshot original = TerrainFixture.Generate(TerrainFixture.Example("flat"));
        byte[] before = TerrainRegionCodecV1.Encode(original);
        CellCoordinate[] cells = original.Cells.Where(cell => cell.IsSolid).Take(2).Select(cell => cell.Coordinate).ToArray();
        TerrainRegionSnapshot left = original.Excavate(0, [cells[0]]);
        TerrainRegionSnapshot right = original.Excavate(0, [cells[1]]);
        Assert.Equal(left.Bounds, right.Bounds);
        Assert.Equal(left.Revision, right.Revision);
        byte[] leftBytes = TerrainRegionCodecV1.Encode(left);
        byte[] rightBytes = TerrainRegionCodecV1.Encode(right);
        Assert.False(leftBytes.AsSpan().SequenceEqual(rightBytes));
        Assert.True(TerrainRegionCodecV1.Decode(leftBytes).GetCell(cells[0]).IsExcavated);
        Assert.False(TerrainRegionCodecV1.Decode(rightBytes).GetCell(cells[0]).IsExcavated);
        Assert.True(TerrainRegionCodecV1.Decode(rightBytes).GetCell(cells[1]).IsExcavated);
        Assert.Equal(before, TerrainRegionCodecV1.Encode(original));
    }

    [Fact]
    public void Cached_exports_do_not_change_null_or_malformed_input_refusals()
    {
        Assert.Throws<ArgumentNullException>(() => TerrainRegionCodecV1.Encode(null!));
        foreach (var item in TerrainFixture.Read().GetProperty("malformed").EnumerateArray())
        {
            byte[] malformed = Convert.FromHexString(item.GetProperty("canonicalHex").GetString()!);
            Assert.Throws<FormatException>(() => TerrainRegionCodecV1.Decode(malformed));
        }
    }
}
