using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Terrain;
using System.Text.Json;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class WatershedTests
{
    [Fact]
    public void Every_prepared_byte_and_shared_face_matches_the_independent_reference()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "watershed-v1.json")));
        foreach (JsonElement example in document.RootElement.GetProperty("examples").EnumerateArray())
        {
            var request = new WatershedRequest(Request().Scope, EntityId.Parse(example.GetProperty("seed").GetString()),
                new WatershedBounds(example.GetProperty("minimumX").GetInt64(), example.GetProperty("minimumY").GetInt64(),
                    example.GetProperty("width").GetInt32(), example.GetProperty("depth").GetInt32()),
                new WatershedRules(example.GetProperty("baseElevation").GetInt64(), example.GetProperty("relief").GetInt32()),
                example.GetProperty("outletIndex").GetInt32(), new EntityId(0, 5));
            PreparedWatershed plan = new WatershedPlanner().Prepare(request, TestContext.Current.CancellationToken);
            Assert.Equal(example.GetProperty("rulesHash").GetString(), request.Rules.Version.Value);
            Assert.Equal(example.GetProperty("preparedHash").GetString(), plan.ComputeContentHash().Value);
            Assert.Equal(Convert.FromHexString(example.GetProperty("preparedHex").GetString()!), WatershedCodecV1.Encode(plan));
            Assert.Equal(plan.ComputeContentHash(), WatershedCodecV1.Decode(WatershedCodecV1.Encode(plan)).ComputeContentHash());
            Assert.Equal(plan.Boundaries.Count, example.GetProperty("boundaries").GetArrayLength());
            foreach (JsonElement boundary in example.GetProperty("boundaries").EnumerateArray())
            {
                Assert.Equal(boundary.GetProperty("id").GetString(), plan.SharedBoundary(
                    boundary.GetProperty("first").GetInt32(), boundary.GetProperty("second").GetInt32()).BoundaryId.ToString());
            }
        }
    }

    [Fact]
    public void Maximum_preparation_has_unique_parcels_and_a_bounded_archive()
    {
        WatershedRequest input = Request();
        var request = new WatershedRequest(input.Scope, input.Seed, new WatershedBounds(-32, -32, 64, 64),
            input.Rules, 0, input.ExternalBoundaryId);
        PreparedWatershed plan = new WatershedPlanner().Prepare(request, TestContext.Current.CancellationToken);
        Assert.Equal(4096, plan.Columns.Count);
        Assert.Equal(8064, plan.Boundaries.Count);
        Assert.Equal(4096, plan.Columns[0].ContributingColumns);
        Assert.Equal(4096, plan.Columns.Select(column => column.SubstrateParcelId).Distinct().Count());
        Assert.True(WatershedCodecV1.Encode(plan).Length < WatershedCodecV1.MaximumEncodedByteLength);
        Assert.Equal(plan.ComputeContentHash(), WatershedCodecV1.Decode(WatershedCodecV1.Encode(plan)).ComputeContentHash());
    }

    internal static WatershedRequest Request(EntityId? seed = null, EntityId? branch = null) => new(
        new TerrainScope(new EntityId(0, 1), branch ?? new EntityId(0, 2), new EntityId(0, 3)),
        seed ?? new EntityId(0, 4), new WatershedBounds(-2, -1, 5, 4),
        new WatershedRules(10, 8), 2, new EntityId(0, 5));

    [Fact]
    public void Every_column_has_a_finite_nonascending_route_to_the_declared_outlet()
    {
        PreparedWatershed plan = new WatershedPlanner().Prepare(Request(), TestContext.Current.CancellationToken);
        Assert.Equal(plan.Columns.Count, plan.Columns[plan.Request.OutletIndex].ContributingColumns);
        foreach (WatershedColumn column in plan.Columns)
        {
            var seen = new HashSet<int>();
            WatershedColumn current = column;
            while (current.DownstreamIndex is { } next)
            {
                Assert.True(seen.Add(current.Index));
                WatershedColumn downstream = plan.Columns[next];
                Assert.True(current.DrainageRank > downstream.DrainageRank);
                Assert.True(current.SpillElevation >= downstream.SpillElevation);
                Assert.Contains(next, plan.Request.Bounds.Neighbors(current.Index));
                current = downstream;
            }
            Assert.Equal(plan.Request.OutletIndex, current.Index);
        }
    }

    [Fact]
    public void Independent_relaxation_confirms_minimum_spill_heights_including_depressions()
    {
        for (ulong seed = 0; seed < 24; seed++)
        {
            PreparedWatershed plan = new WatershedPlanner().Prepare(Request(new EntityId(0, seed)), TestContext.Current.CancellationToken);
            long[] best = Enumerable.Repeat(long.MaxValue, plan.Columns.Count).ToArray();
            int outlet = plan.Request.OutletIndex;
            best[outlet] = plan.Columns[outlet].NaturalElevation;
            for (int pass = 0; pass < best.Length; pass++)
            {
                foreach (WatershedColumn column in plan.Columns.Reverse())
                {
                    foreach (int neighbor in plan.Request.Bounds.Neighbors(column.Index))
                    {
                        best[column.Index] = Math.Min(best[column.Index],
                            Math.Max(column.NaturalElevation, best[neighbor]));
                    }
                }
            }
            Assert.Equal(best, plan.Columns.Select(column => column.SpillElevation));
        }
    }

    [Fact]
    public void Window_and_shared_boundary_queries_do_not_depend_on_order_or_workers()
    {
        var planner = new WatershedPlanner();
        PreparedWatershed plan = planner.Prepare(Request(), TestContext.Current.CancellationToken);
        byte[] before = WatershedCodecV1.Encode(plan);
        var windows = new[] { new WatershedBounds(-2, -1, 2, 4), new WatershedBounds(0, -1, 3, 4) };
        WatershedColumn[][] results = new WatershedColumn[2][];
        Parallel.For(0, 2, index => results[index] = plan.ReadWindow(windows[index]).ToArray());
        Assert.Equal(plan.Columns, results.SelectMany(value => value).OrderBy(column => column.Index));
        Assert.Equal(plan.SharedBoundary(1, 2), plan.SharedBoundary(2, 1));
        foreach (int workers in new[] { 1, 2, 4 })
        {
            var options = new ParallelOptions { MaxDegreeOfParallelism = workers, CancellationToken = TestContext.Current.CancellationToken };
            Parallel.For(0, 8, options, _ => Assert.Equal(before,
                WatershedCodecV1.Encode(planner.Prepare(Request(), TestContext.Current.CancellationToken))));
        }
        Assert.Equal(before, WatershedCodecV1.Encode(plan));
    }

    [Fact]
    public void Branches_share_genesis_geography_but_the_prepared_scope_stays_explicit()
    {
        var planner = new WatershedPlanner();
        PreparedWatershed first = planner.Prepare(Request(), TestContext.Current.CancellationToken);
        PreparedWatershed other = planner.Prepare(Request(branch: new EntityId(0, 19)), TestContext.Current.CancellationToken);
        Assert.Equal(first.Columns, other.Columns);
        Assert.Equal(first.BasinId, other.BasinId);
        Assert.NotEqual(first.ComputeContentHash(), other.ComputeContentHash());
        PreparedWatershed changedSeed = planner.Prepare(Request(seed: new EntityId(0, 20)), TestContext.Current.CancellationToken);
        Assert.NotEqual(first.BasinId, changedSeed.BasinId);
        Assert.NotEqual(first.Columns.Select(c => c.NaturalElevation), changedSeed.Columns.Select(c => c.NaturalElevation));
    }

    [Fact]
    public void Canonical_preparation_roundtrips_and_rejects_corruption_or_trailing_data()
    {
        PreparedWatershed plan = new WatershedPlanner().Prepare(Request(), TestContext.Current.CancellationToken);
        byte[] bytes = WatershedCodecV1.Encode(plan);
        Assert.Equal(bytes, WatershedCodecV1.Encode(WatershedCodecV1.Decode(bytes)));
        byte[] corrupt = (byte[])bytes.Clone();
        corrupt[^1] ^= 1;
        Assert.Throws<FormatException>(() => WatershedCodecV1.Decode(corrupt));
        Assert.Throws<FormatException>(() => WatershedCodecV1.Decode([.. bytes, 0]));
        Assert.Throws<FormatException>(() => WatershedCodecV1.Decode(bytes[..^1]));
        bytes[7] = 2;
        Assert.Throws<FormatException>(() => WatershedCodecV1.Decode(bytes));
    }

    [Fact]
    public void Invalid_or_unavailable_boundaries_are_refused_without_clamping()
    {
        WatershedRequest request = Request();
        Assert.Throws<ArgumentOutOfRangeException>(() => new WatershedBounds(0, 0, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WatershedBounds(0, 0, 65, 1));
        Assert.Throws<OverflowException>(() => new WatershedBounds(long.MaxValue, 0, 2, 1));
        Assert.Throws<OverflowException>(() => new WatershedRules(long.MaxValue, 1));
        Assert.Throws<ArgumentException>(() => new WatershedRequest(request.Scope, request.Seed,
            request.Bounds, request.Rules, 6, request.ExternalBoundaryId));
        PreparedWatershed plan = new WatershedPlanner().Prepare(request, TestContext.Current.CancellationToken);
        Assert.Throws<ArgumentOutOfRangeException>(() => plan.ReadWindow(new WatershedBounds(-3, -1, 2, 2)));
        Assert.Throws<ArgumentException>(() => plan.SharedBoundary(0, 6));
    }

    [Fact]
    public void Cancellation_never_publishes_partial_preparation()
    {
        var planner = new WatershedPlanner();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => planner.Prepare(Request(), cancellation.Token));
        Assert.Equal(20, planner.Prepare(Request(), TestContext.Current.CancellationToken).Columns.Count);
    }
}
