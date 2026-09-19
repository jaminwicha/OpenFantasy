using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class ReservoirGeometryTests
{
    private static readonly TerrainScope Scope = new(new(0, 1), new(0, 2), new(0, 3));

    [Fact]
    public void TerracedReservoirDerivesCapacityAndExactConnectedFill()
    {
        var geometry = new ReservoirGeometry(Scope, new(50, 1), new(50, 2), 3, [new(0, 0, 0), new(1, 0, 1)]);
        Assert.Equal(5_000_000_000UL, geometry.Capacity.Value);
        var fill = geometry.Fill(new(1_500_000_000));
        Assert.Equal(1L, fill.SurfaceMetres);
        Assert.Equal(250_000_000U, fill.SurfaceBillionths);
        Assert.Equal(new ulong[] { 1_250_000_000, 250_000_000 }, fill.Columns.Select(c => c.Volume.Value));
        Assert.Throws<ArgumentOutOfRangeException>(() => geometry.Fill(new(5_000_000_001)));
    }

    [Fact]
    public void PerchedPocketsCannotPretendToShareOneEquilibriumLevel()
    {
        Assert.Throws<ArgumentException>(() => new ReservoirGeometry(Scope, new(50, 1), new(50, 2), 3,
            [new(0, 0, 0), new(1, 0, 2), new(2, 0, 1)]));
        Assert.Throws<ArgumentException>(() => new ReservoirGeometry(Scope, new(50, 1), new(50, 2), 3,
            [new(0, 0, 0), new(2, 0, 0)]));
    }
    [Fact]
    public void MicrolitreRemaindersAndBandBoundariesConserveVolumeWithoutOrderDependence()
    {
        ReservoirColumn[] columns = [new(0, 0, -2), new(1, 0, -2), new(2, 0, -1)];
        var geometry = new ReservoirGeometry(Scope, new(50, 1), new(50, 2), 1, columns);
        var reversed = new ReservoirGeometry(Scope, new(50, 1), new(50, 2), 1, columns.Reverse());
        foreach (ulong volume in new ulong[] { 0, 1, 2, 3, 1_999_999_999, 2_000_000_000, 2_000_000_001,
            2_000_000_002, 2_000_000_003, 7_999_999_999, 8_000_000_000 })
        {
            var fill = geometry.Fill(new(volume));
            Assert.Equal(volume, fill.Columns.Aggregate(0UL, (sum, c) => sum + c.Volume.Value));
            Assert.Equal(fill.Columns, reversed.Fill(new(volume)).Columns);
            Assert.All(fill.Columns, c => Assert.InRange(c.Volume.Value, 0UL,
                (ulong)(geometry.SpillZ - c.Column.BottomZ) * 1_000_000_000));
        }
        var partial = geometry.Fill(new(2_000_000_002));
        Assert.Equal(-1L, partial.SurfaceMetres);
        Assert.Equal(0U, partial.SurfaceBillionths);
        Assert.Equal(2UL, partial.RemainderMicrolitres);
        Assert.Equal(new ulong[] { 1_000_000_001, 1_000_000_001, 0 }, partial.Columns.Select(c => c.Volume.Value));
    }

    [Fact]
    public void CoordinateExtremesDoNotWrapAdjacencyOrAbsoluteHeight()
    {
        var geometry = new ReservoirGeometry(Scope, new(50, 1), new(50, 2), long.MinValue + 2,
            [new(long.MaxValue - 1, long.MinValue, long.MinValue), new(long.MaxValue, long.MinValue, long.MinValue + 1)]);
        var fill = geometry.Fill(new(1_500_000_000));
        Assert.Equal(long.MinValue + 1, fill.SurfaceMetres);
        Assert.Equal(250_000_000U, fill.SurfaceBillionths);
        Assert.Throws<ArgumentException>(() => new ReservoirGeometry(Scope, new(50, 1), new(50, 2), 1,
            [new(long.MaxValue, 0, 0), new(long.MinValue, 0, 0)]));
        Assert.Throws<ArgumentException>(() => new ReservoirGeometry(Scope, new(50, 1), new(50, 2), long.MaxValue,
            [new(0, 0, long.MinValue)]));
    }

    [Fact]
    public void EmptyDuplicateAndExcessiveGeometryRejectBeforeUse()
    {
        Assert.Throws<ArgumentException>(() => new ReservoirGeometry(Scope, new(50, 1), new(50, 2), 1, []));
        Assert.Throws<ArgumentException>(() => new ReservoirGeometry(Scope, new(50, 1), new(50, 2), 1,
            [new(0, 0, 0), new(0, 0, -1)]));
        Assert.Throws<ArgumentException>(() => new ReservoirGeometry(Scope, new(50, 1), new(50, 2), 1,
            Enumerable.Range(0, 4097).Select(i => new ReservoirColumn(i, 0, 0))));
        var maximum = new ReservoirGeometry(Scope, new(50, 1), new(50, 2), 0,
            Enumerable.Range(0, 4096).Select(i => new ReservoirColumn(i, 0, -1024)));
        Assert.Equal(4_194_304_000_000_000UL, maximum.Capacity.Value);
        Assert.Equal(maximum.Capacity.Value, maximum.Fill(maximum.Capacity).Columns.Aggregate(0UL, (sum, c) => sum + c.Volume.Value));
    }
}
