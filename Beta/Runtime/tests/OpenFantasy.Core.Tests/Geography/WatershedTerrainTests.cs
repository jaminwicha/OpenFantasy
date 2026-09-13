using System.Text.Json;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class WatershedTerrainTests
{
    private readonly WatershedTerrainMapper _mapper = new();

    [Fact]
    public void EveryArchiveAndSubstrateCountMatchesTheIndependentCoordinateOracle()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "watershed-terrain-v1.json")));
        foreach (JsonElement example in document.RootElement.GetProperty("examples").EnumerateArray())
        {
            byte[] bytes = Convert.FromHexString(example.GetProperty("preparedHex").GetString()!);
            PreparedWatershedTerrain window = WatershedTerrainCodecV1.Decode(bytes, TestContext.Current.CancellationToken);
            Assert.Equal(bytes, WatershedTerrainCodecV1.Encode(window));
            Assert.Equal(example.GetProperty("preparedHash").GetString(), window.ComputeContentHash().Value);
            Assert.Equal(example.GetProperty("mappingHash").GetString(), window.Mapping.Version.Value);
            Assert.Equal(example.GetProperty("substrateCells").GetInt32(), window.SubstrateCellCount);
        }
    }

    [Theory]
    [InlineData(-7, -3, 2)]
    [InlineData(-6, -2, 0)]
    [InlineData(-1, -1, 2)]
    [InlineData(0, 0, 0)]
    [InlineData(5, 1, 2)]
    public void NegativeAndPositiveCoordinatesUseOneFloorPartition(long local, long macro, int offset)
    {
        var mapping = new WatershedTerrainMapping(3, 2, -1);
        Assert.Equal(new WatershedColumnAddress(macro, macro), mapping.MacroAddressOf(local, local));
        Assert.Equal((offset, offset), mapping.OffsetWithinColumn(local, local));
        Assert.Equal(5, mapping.SurfaceZ(3));
    }

    [Fact]
    public void WindowOrderParallelismAndAllThreePartitionAxesPreserveExactCellsAndAncestry()
    {
        PreparedWatershedNetwork network = Network();
        var mapping = new WatershedTerrainMapping(3, 2, -1);
        var bounds = new TerrainRegionBounds(new(-6, -6, -4), 12, 12, 12);
        PreparedWatershedTerrain whole = Prepare(network, mapping, bounds);
        var parts = (from z in new[] { -4, 2 } from y in new[] { -6, 0 } from x in new[] { -6, 0 }
                     select new TerrainRegionBounds(new(x, y, z), 6, 6, 6)).ToArray();
        foreach (int workers in new[] { 1, 2, 4 })
        {
            Parallel.ForEach(parts.Reverse(), new ParallelOptions { MaxDegreeOfParallelism = workers,
                CancellationToken = TestContext.Current.CancellationToken }, part =>
            {
                PreparedWatershedTerrain smaller = Prepare(network, mapping, part);
                for (int i = 0; i < part.CellCount; i++)
                {
                    CellCoordinate at = part.CoordinateAt(i);
                    Assert.Equal(whole.GetCell(at), smaller.GetCell(at));
                }
            });
        }
        Assert.Null(whole.GetCell(new(0, 0, 0)).Ancestry);
        Assert.NotNull(whole.GetCell(new(-1, 0, 0)).Ancestry);
        Assert.Throws<ArgumentOutOfRangeException>(() => whole.GetCell(new(0, 0, 8)));
    }

    [Fact]
    public void SharedSeamsUseExactMacroReferencesAndNaturalHeightsWithoutFillingSpillDepressions()
    {
        PreparedWatershedNetwork network = Network();
        PreparedWatershedTerrain window = Prepare(network, new(3, 2, -1), new(new(-1, -6, -2), 2, 1, 10));
        WatershedTerrainColumn left = window.GetColumn(-1, -6), right = window.GetColumn(0, -6);
        Assert.Equal(5, left.SurfaceZ);
        Assert.Equal(-1, right.SurfaceZ);
        WatershedNetworkFace face = network.SharedFace(left.MacroColumn, right.MacroColumn);
        Assert.Same(face, network.SharedFace(right.MacroColumn, left.MacroColumn));
        using JsonDocument fixtures = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "watershed-network-v1.json")));
        byte[] archive = Convert.FromHexString(fixtures.RootElement.GetProperty("examples").EnumerateArray()
            .Single(e => e.GetProperty("name").GetString() == "relief").GetProperty("preparedHex").GetString()!);
        network = WatershedNetworkCodecV1.Decode(archive, TestContext.Current.CancellationToken);
        var depressions = network.Basins.SelectMany(b => b.Columns.Where(c => c.SpillElevation > c.NaturalElevation)
            .Select(c => (Basin: b, Column: c))).ToArray();
        Assert.NotEmpty(depressions);
        foreach (var value in depressions)
        {
            var at = value.Basin.Request.Bounds.CoordinateAt(value.Column.Index);
            window = Prepare(network, new(1, 1, 0), new(new(at.X, at.Y, value.Column.NaturalElevation), 1, 1, 2));
            Assert.True(window.GetCell(window.Bounds.Minimum).IsSubstrate);
            Assert.False(window.GetCell(window.Bounds.Maximum).IsSubstrate);
        }
    }

    [Fact]
    public void BranchScopeIsPinnedWhileAncestralAddressesIgnoreBranchAndWindow()
    {
        var mapping = new WatershedTerrainMapping(3, 2, -1);
        var bounds = new TerrainRegionBounds(new(-6, -6, -4), 12, 12, 12);
        PreparedWatershedTerrain first = Prepare(Network(), mapping, bounds);
        PreparedWatershedTerrain fork = Prepare(Network(branch: new(0, 999)), mapping, bounds);
        Assert.Equal(first.Columns, fork.Columns);
        for (int i = 0; i < bounds.CellCount; i++)
            Assert.Equal(first.GetCell(bounds.CoordinateAt(i)), fork.GetCell(bounds.CoordinateAt(i)));
        Assert.NotEqual(first.ComputeContentHash(), fork.ComputeContentHash());
        PreparedWatershedTerrain changed = Prepare(Network(), new(3, 2, 0), bounds);
        Assert.NotEqual(first.GetCell(bounds.Minimum).Ancestry, changed.GetCell(bounds.Minimum).Ancestry);
    }

    [Fact]
    public void MissingFootprintsAndNumericOverflowRejectTheWholeCandidate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Prepare(Network(), new(3, 1, 0), new(new(-7, -6, 0), 2, 1, 1)));
        PreparedWatershedNetwork gaps = Network(separated: true);
        Assert.Throws<ArgumentOutOfRangeException>(() => Prepare(gaps, new(1, 1, 0), new(new(-1, -2, 0), 4, 1, 1)));
        Assert.Throws<OverflowException>(() => Prepare(Network(), new(1, 1, long.MaxValue), new(new(-2, -2, 0), 1, 1, 1)));
        PreparedWatershedNetwork extreme = Single(long.MaxValue, long.MinValue, 0);
        Assert.Throws<OverflowException>(() => Prepare(extreme, new(2, 1, 0), new(new(0, 0, 0), 1, 1, 1)));
        var edge = Prepare(extreme, new(1, 1, 0), new(new(long.MaxValue, long.MinValue, 0), 1, 1, 1));
        Assert.Equal(1, edge.SubstrateCellCount);
        var bottom = Prepare(Single(0, 0, long.MinValue), new(1, 1, 0), new(new(0, 0, long.MinValue), 1, 1, 2));
        Assert.Equal(1, bottom.SubstrateCellCount);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1025, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 1025)]
    public void UnsupportedScaleIsRejected(int horizontal, int vertical) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new WatershedTerrainMapping(horizontal, vertical, 0));

    [Fact]
    public void CancellationCorruptRowsAndMalformedFramesCannotPublishPartialCandidates()
    {
        PreparedWatershedNetwork network = Network();
        var mapping = new WatershedTerrainMapping(3, 2, -1);
        var bounds = new TerrainRegionBounds(new(-6, -6, -4), 12, 12, 12);
        byte[] original = WatershedNetworkCodecV1.Encode(network);
        byte[] bytes = WatershedTerrainCodecV1.Encode(Prepare(network, mapping, bounds));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => _mapper.Prepare(network, mapping, bounds, canceled.Token));
        Assert.Throws<OperationCanceledException>(() => WatershedTerrainCodecV1.Decode(bytes, canceled.Token));
        byte[] changed = (byte[])bytes.Clone(); changed[^1] ^= 1;
        foreach (byte[] invalid in new[] { changed, bytes[..^1], bytes.Concat(new byte[] { 0 }).ToArray(), original })
            Assert.Throws<FormatException>(() => WatershedTerrainCodecV1.Decode(invalid, TestContext.Current.CancellationToken));
        Assert.Equal(original, WatershedNetworkCodecV1.Encode(network));
    }

    [Fact]
    public void MaximumWindowHasFiniteExactOccupancyAndRetainableArchive()
    {
        PreparedWatershedNetwork network = Single(0, 0, 0);
        PreparedWatershedTerrain window = Prepare(network, new(1024, 1024, 0), new(new(0, 0, -8), 32, 32, 16));
        Assert.Equal(1024, window.Columns.Count);
        Assert.Equal(9216, window.SubstrateCellCount);
        byte[] bytes = WatershedTerrainCodecV1.Encode(window);
        Assert.True(bytes.Length < WatershedTerrainCodecV1.MaximumEncodedByteLength);
        Assert.Equal(bytes, WatershedTerrainCodecV1.Encode(WatershedTerrainCodecV1.Decode(bytes, TestContext.Current.CancellationToken)));
    }

    private PreparedWatershedTerrain Prepare(PreparedWatershedNetwork network, WatershedTerrainMapping mapping,
        TerrainRegionBounds bounds) => _mapper.Prepare(network, mapping, bounds, TestContext.Current.CancellationToken);

    [Fact]
    public void MaximumNetworkAndWindowRemainWithinTheSameFiniteArchiveEnvelope()
    {
        var basins = new List<PreparedWatershed>();
        for (int i = 0; i < 16; i++)
        {
            basins.Add(new WatershedPlanner().Prepare(new(new(new(0, 1), new(0, 2), new(0, 3)),
                new(0, 4), new((i % 4) * 64, (i / 4) * 64, 64, 64), new(0, 0), 0, new(0, (ulong)(100 + i))), TestContext.Current.CancellationToken));
        }
        PreparedWatershedNetwork network = new WatershedNetworkPlanner().Prepare(new(0, 900), basins,
            basins.Select(b => WatershedOutletClosure.ToTerminal(b.BasinId, 4096)), TestContext.Current.CancellationToken);
        PreparedWatershedTerrain window = Prepare(network, new(1, 1, 0), new(new(48, 48, -8), 32, 32, 16));
        byte[] bytes = WatershedTerrainCodecV1.Encode(window);
        Assert.Equal(65_536, window.Network.ColumnCount);
        Assert.Equal(1024, window.Columns.Count);
        Assert.Equal(9216, window.SubstrateCellCount);
        Assert.True(bytes.Length <= WatershedTerrainCodecV1.MaximumEncodedByteLength);
        Assert.Equal(bytes, WatershedTerrainCodecV1.Encode(WatershedTerrainCodecV1.Decode(bytes, TestContext.Current.CancellationToken)));
        string? root = Environment.GetEnvironmentVariable("OPENFANTASY_WATERSHED_TERRAIN_EVIDENCE");
        if (!string.IsNullOrWhiteSpace(root))
        {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(Path.Combine(root, "maximum.bin"), bytes);
            File.WriteAllText(Path.Combine(root, "expected.json"), JsonSerializer.Serialize(new
            {
                ContentHash = window.ComputeContentHash().Value, window.SubstrateCellCount,
                NetworkColumnCount = network.ColumnCount, ColumnCount = window.Columns.Count
            }));
        }
    }

    private static PreparedWatershedNetwork Network(EntityId? branch = null, bool separated = false)
    {
        TerrainScope scope = new(new(0, 1), branch ?? new(0, 2), new(0, 3));
        PreparedWatershed Basin(long x, long elevation, int outlet, ulong id) => new WatershedPlanner().Prepare(
            new(scope, new(0, 4), new(x, -2, 2, 4), new(elevation, 0), outlet, new(0, id)));
        PreparedWatershed a = Basin(-2, 3, 1, 100), b = Basin(separated ? 2 : 0, 0, 0, 101);
        return new WatershedNetworkPlanner().Prepare(new(0, 900), [a, b],
            [separated ? WatershedOutletClosure.ToTerminal(a.BasinId, 8) : WatershedOutletClosure.ToBasin(a.BasinId, b.BasinId, 0),
             WatershedOutletClosure.ToTerminal(b.BasinId, separated ? 8 : 16)]);
    }

    private static PreparedWatershedNetwork Single(long x, long y, long elevation)
    {
        PreparedWatershed basin = new WatershedPlanner().Prepare(new(new(new(0, 1), new(0, 2), new(0, 3)),
            new(0, 4), new(x, y, 1, 1), new(elevation, 0), 0, new(0, 100)));
        return new WatershedNetworkPlanner().Prepare(new(0, 900), [basin], [WatershedOutletClosure.ToTerminal(basin.BasinId, 1)]);
    }
}
