using System.Buffers.Binary;
using System.Text.Json;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class WatershedNetworkTests
{
    private static readonly EntityId Province = new(0, 900);
    private readonly WatershedNetworkPlanner _planner = new();

    [Fact]
    public void Every_network_byte_matches_the_independent_archive_and_origin_path_reference()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "watershed-network-v1.json")));
        foreach (JsonElement example in document.RootElement.GetProperty("examples").EnumerateArray())
        {
            byte[] bytes = Convert.FromHexString(example.GetProperty("preparedHex").GetString()!);
            PreparedWatershedNetwork network = DecodeNetwork(bytes);
            Assert.Equal(bytes, WatershedNetworkCodecV1.Encode(network));
            Assert.Equal(example.GetProperty("preparedHash").GetString(), network.ComputeContentHash().Value);
            Assert.Equal(example.GetProperty("columnCount").GetInt32(), network.ColumnCount);
            Assert.Equal(example.GetProperty("faceIds").EnumerateArray().Select(v => v.GetString()),
                network.SharedFaces.Select(face => face.BoundaryId.ToString()));
            AssertPathCounts(network);
        }
    }

    [Fact]
    public void Fan_in_propagates_through_the_exact_receiving_columns_and_preserves_native_facts()
    {
        var (basins, closures) = FanIn();
        byte[][] before = basins.Select(WatershedCodecV1.Encode).ToArray();
        PreparedWatershedNetwork network = Prepare(basins, closures);
        Assert.Equal(20, network.ColumnCount);
        Assert.Equal(20, Assert.Single(network.Terminals).ContributingColumns);
        PreparedWatershed receiver = basins[1];
        Assert.Equal(receiver.Columns[0].ContributingColumns + 10, network.ContributingColumns(receiver.BasinId, 0));
        Assert.Equal(receiver.Columns[5].ContributingColumns, network.ContributingColumns(receiver.BasinId, 5));
        AssertPathCounts(network);
        for (int i = 0; i < basins.Length; i++) Assert.Equal(before[i], WatershedCodecV1.Encode(basins[i]));
    }

    [Fact]
    public void Every_touching_face_has_one_identity_from_both_sides_and_only_declared_links_drain()
    {
        var (basins, closures) = FanIn();
        PreparedWatershedNetwork network = Prepare(basins, closures);
        Assert.Equal(7, network.SharedFaces.Count);
        Assert.Equal(3, network.SharedFaces.Count(face => face.DrainsFromBasinId is not null));
        foreach (WatershedNetworkFace face in network.SharedFaces)
        {
            Assert.Same(face, network.SharedFace(face.First, face.Second));
            Assert.Same(face, network.SharedFace(face.Second, face.First));
            Assert.NotEqual(basins.Single(b => b.BasinId == face.First.BasinId).Request.ExternalBoundaryId, face.BoundaryId);
        }
        Assert.Throws<ArgumentException>(() => network.SharedFace(new(basins[0].BasinId, 0), new(basins[2].BasinId, 0)));
    }

    [Fact]
    public void Member_and_closure_order_and_concurrent_queries_cannot_change_the_archive()
    {
        var (basins, closures) = FanIn();
        byte[] expected = WatershedNetworkCodecV1.Encode(Prepare(basins, closures));
        foreach (int workers in new[] { 1, 2, 4 })
        {
            Parallel.For(0, 12, new ParallelOptions { MaxDegreeOfParallelism = workers, CancellationToken = TestContext.Current.CancellationToken }, i =>
            {
                PreparedWatershed[] order = basins.Skip(i % 4).Concat(basins.Take(i % 4)).ToArray();
                if (i % 2 == 0) Array.Reverse(order);
                PreparedWatershedNetwork network = Prepare(order, closures.Reverse());
                Assert.Equal(expected, WatershedNetworkCodecV1.Encode(network));
                AssertPathCounts(network);
            });
        }
    }

    [Fact]
    public void Shared_ancestor_faces_ignore_branch_but_scope_and_province_remain_explicit()
    {
        var first = FanIn();
        var fork = FanIn(new EntityId(0, 888));
        PreparedWatershedNetwork a = Prepare(first.Basins, first.Closures);
        PreparedWatershedNetwork b = Prepare(fork.Basins, fork.Closures);
        Assert.Equal(a.SharedFaces, b.SharedFaces);
        Assert.NotEqual(a.ComputeContentHash(), b.ComputeContentHash());
        PreparedWatershedNetwork otherProvince = _planner.Prepare(new EntityId(0, 901), first.Basins, first.Closures, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(a.SharedFaces[0].BoundaryId, otherProvince.SharedFaces.Select(face => face.BoundaryId));
    }

    [Fact]
    public void Disconnected_components_must_each_close_and_terminal_totals_cover_all_columns_once()
    {
        PreparedWatershed[] basins = [Basin(-10, 0, 2, 2, 0, 10, 100), Basin(10, 0, 3, 2, 0, 10, 101)];
        var closures = basins.Select(b => WatershedOutletClosure.ToTerminal(b.BasinId, b.Columns.Count));
        PreparedWatershedNetwork network = Prepare(basins, closures);
        Assert.Empty(network.SharedFaces);
        Assert.Equal(2, network.Terminals.Count);
        Assert.Equal(10, network.Terminals.Sum(t => t.ContributingColumns));
        AssertPathCounts(network);
    }

    [Fact]
    public void A_terminal_must_cover_incoming_contributors_not_just_its_native_basin()
    {
        var (basins, closures) = FanIn();
        Assert.Throws<ArgumentException>(() => Prepare(basins, closures.Select(c => c.Kind == WatershedOutletKind.Terminal
            ? WatershedOutletClosure.ToTerminal(c.BasinId, 19) : c)));
        Assert.Equal(20, Assert.Single(Prepare(basins, closures).Terminals).ContributingColumns);
        Assert.Throws<ArgumentOutOfRangeException>(() => WatershedOutletClosure.ToTerminal(basins[0].BasinId, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => WatershedOutletClosure.ToTerminal(basins[0].BasinId, 65_537));
    }

    [Fact]
    public void Equal_height_cycles_and_self_links_are_refused_without_mutating_members()
    {
        PreparedWatershed a = Basin(0, 0, 1, 1, 0, 0, 100);
        PreparedWatershed b = Basin(1, 0, 1, 1, 0, 0, 101);
        byte[] before = WatershedCodecV1.Encode(a);
        Assert.Throws<ArgumentException>(() => Prepare([a, b],
            [WatershedOutletClosure.ToBasin(a.BasinId, b.BasinId, 0), WatershedOutletClosure.ToBasin(b.BasinId, a.BasinId, 0)]));
        Assert.Throws<ArgumentException>(() => WatershedOutletClosure.ToBasin(a.BasinId, a.BasinId, 0));
        Assert.Equal(before, WatershedCodecV1.Encode(a));
    }

    [Fact]
    public void Outlet_links_reject_nonadjacent_interior_out_of_range_unknown_or_uphill_receivers()
    {
        PreparedWatershed a = Basin(0, 0, 2, 3, 1, 5, 100);
        PreparedWatershed b = Basin(2, 0, 3, 3, 0, 0, 101);
        foreach (int badIndex in new[] { 2, 4, 100 })
        {
            Assert.ThrowsAny<ArgumentException>(() => Prepare([a, b],
                [WatershedOutletClosure.ToBasin(a.BasinId, b.BasinId, badIndex), WatershedOutletClosure.ToTerminal(b.BasinId, 15)]));
        }
        Assert.Throws<ArgumentException>(() => Prepare([a, b],
            [WatershedOutletClosure.ToBasin(a.BasinId, new EntityId(0, 999), 0), WatershedOutletClosure.ToTerminal(b.BasinId, 15)]));
        PreparedWatershed uphill = Basin(2, 0, 3, 3, 0, 6, 102);
        Assert.Throws<ArgumentException>(() => Prepare([a, uphill],
            [WatershedOutletClosure.ToBasin(a.BasinId, uphill.BasinId, 0), WatershedOutletClosure.ToTerminal(uphill.BasinId, 15)]));
    }

    [Fact]
    public void Missing_duplicate_or_unknown_outlet_closures_are_refused()
    {
        var (basins, closures) = FanIn();
        Assert.Throws<ArgumentException>(() => Prepare(basins, closures.Take(3)));
        Assert.Throws<ArgumentException>(() => Prepare(basins, [closures[0], closures[0], closures[2], closures[3]]));
        Assert.Throws<ArgumentException>(() => Prepare(basins,
            [.. closures.Take(3), WatershedOutletClosure.ToTerminal(new EntityId(0, 999), 20)]));
    }

    [Fact]
    public void Overlap_scope_seed_and_cross_role_identity_conflicts_are_refused()
    {
        PreparedWatershed a = Basin(0, 0, 2, 2, 0, 0, 100);
        PreparedWatershed[] invalid =
        [
            Basin(1, 1, 2, 2, 0, 0, 101),
            Basin(3, 0, 2, 2, 0, 0, 101, new EntityId(0, 777)),
            Basin(3, 0, 2, 2, 0, 0, 101, seed: new EntityId(0, 778)),
            Basin(3, 0, 2, 2, 0, 0, 100),
            Basin(3, 0, 2, 2, 0, 0, 101, external: a.Columns[0].SubstrateParcelId)
        ];
        foreach (PreparedWatershed b in invalid)
        {
            Assert.Throws<ArgumentException>(() => Prepare([a, b],
                [WatershedOutletClosure.ToTerminal(a.BasinId, 4), WatershedOutletClosure.ToTerminal(b.BasinId, 4)]));
        }
        foreach (EntityId occupied in new[] { a.BasinId, a.FormationId, a.Columns[0].SubstrateParcelId,
            a.Boundaries[0].BoundaryId, a.Request.ExternalBoundaryId, a.Request.Scope.WorldId })
        {
            Assert.Throws<ArgumentException>(() => _planner.Prepare(occupied, [a], [WatershedOutletClosure.ToTerminal(a.BasinId, 4)], TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public void Signed_address_extrema_do_not_wrap_or_create_false_neighbors()
    {
        foreach (long x in new[] { long.MinValue, long.MaxValue - 1 })
        {
            PreparedWatershed a = Basin(x, long.MaxValue, 1, 1, 0, 1, 100);
            PreparedWatershed b = Basin(x + 1, long.MaxValue, 1, 1, 0, 0, 101);
            PreparedWatershedNetwork network = Prepare([a, b],
                [WatershedOutletClosure.ToBasin(a.BasinId, b.BasinId, 0), WatershedOutletClosure.ToTerminal(b.BasinId, 2)]);
            Assert.Single(network.SharedFaces);
            Assert.Equal(2, network.ContributingColumns(b.BasinId, 0));
        }
        Assert.False(new WatershedColumnAddress(long.MinValue, 0).IsAdjacentTo(new(long.MaxValue, 0)));
    }

    [Theory]
    [InlineData(11, 11, 13)]
    [InlineData(11, 12, 11)]
    [InlineData(11, 12, 12)]
    public void Scope_roots_cannot_share_an_identity(int world, int branch, int realm)
    {
        PreparedWatershed basin = new WatershedPlanner().Prepare(new WatershedRequest(
            new TerrainScope(new EntityId(0, (ulong)world), new EntityId(0, (ulong)branch), new EntityId(0, (ulong)realm)),
            new EntityId(0, 14), new WatershedBounds(0, 0, 1, 1), new WatershedRules(0, 0), 0, new EntityId(0, 100)),
            TestContext.Current.CancellationToken);
        Assert.Throws<ArgumentException>(() => Prepare([basin], [WatershedOutletClosure.ToTerminal(basin.BasinId, 1)]));
    }

    [Fact]
    public void Decode_refuses_aliased_scope_even_when_the_nested_basin_is_valid_and_derived_bytes_match()
    {
        PreparedWatershed basin = Basin(0, 0, 1, 1, 0, 0, 100);
        byte[] bytes = WatershedNetworkCodecV1.Encode(Prepare([basin], [WatershedOutletClosure.ToTerminal(basin.BasinId, 1)]));
        basin.Request.Scope.WorldId.WriteBytes(bytes.AsSpan(FieldOffset(bytes, 2), 16));
        int memberOffset = FieldOffset(bytes, 6) + 8;
        byte[] member = bytes.AsSpan(memberOffset, WatershedCodecV1.Encode(basin).Length).ToArray();
        int requestOffset = FieldOffset(member, 1);
        byte[] request = member.AsSpan(requestOffset).ToArray();
        basin.Request.Scope.WorldId.WriteBytes(member.AsSpan(requestOffset + FieldOffset(request, 2), 16));
        Assert.Equal(basin.BasinId, WatershedCodecV1.Decode(member).BasinId);
        member.CopyTo(bytes, memberOffset);
        Assert.Throws<FormatException>(() => DecodeNetwork(bytes));
    }

    [Fact]
    public void Maximum_network_has_finite_exact_conservation_and_a_bounded_roundtrip()
    {
        PreparedWatershed[] basins = Enumerable.Range(0, 16).Select(i => Basin(i * 64, 0, 64, 64, 63, 160 - i * 10, (ulong)(100 + i))).ToArray();
        WatershedOutletClosure[] closures = basins.Select((b, i) => i == 15
            ? WatershedOutletClosure.ToTerminal(b.BasinId, 65_536) : WatershedOutletClosure.ToBasin(b.BasinId, basins[i + 1].BasinId, 0)).ToArray();
        PreparedWatershedNetwork network = Prepare(basins, closures);
        Assert.Equal(65_536, network.ColumnCount);
        Assert.Equal(960, network.SharedFaces.Count);
        Assert.Equal(65_536, Assert.Single(network.Terminals).ContributingColumns);
        byte[] bytes = WatershedNetworkCodecV1.Encode(network);
        Assert.True(bytes.Length < WatershedNetworkCodecV1.MaximumEncodedByteLength);
        Assert.Equal(bytes, WatershedNetworkCodecV1.Encode(DecodeNetwork(bytes)));
        if (Environment.GetEnvironmentVariable("OPENFANTASY_WATERSHED_NETWORK_EVIDENCE") is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, "maximum.bin"), bytes);
            File.WriteAllText(Path.Combine(directory, "expected.json"), JsonSerializer.Serialize(new
            {
                ContentHash = network.ComputeContentHash().Value, BasinCount = 16, ColumnCount = 65_536,
                SharedFaceCount = 960, DrainageLinkCount = 15
            }));
        }
        Assert.Throws<ArgumentException>(() => Prepare(basins.Append(basins[0]), closures));
        Assert.Throws<ArgumentException>(() => Prepare([], []));
    }

    [Fact]
    public void Archive_rederivation_rejects_tampering_truncation_unused_fields_and_noncanonical_order()
    {
        var (basins, closures) = FanIn();
        byte[] bytes = WatershedNetworkCodecV1.Encode(Prepare(basins, closures));
        Assert.Equal(bytes, WatershedNetworkCodecV1.Encode(DecodeNetwork(bytes)));
        foreach (int field in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 })
        {
            byte[] corrupt = (byte[])bytes.Clone();
            int offset = FieldOffset(corrupt, field);
            corrupt[offset + (field == 6 ? 8 : 0)] ^= 0x80;
            Assert.Throws<FormatException>(() => DecodeNetwork(corrupt));
        }
        byte[] order = (byte[])bytes.Clone();
        int start = FieldOffset(order, 7);
        byte[] first = order.AsSpan(start, 44).ToArray();
        order.AsSpan(start + 44, 44).CopyTo(order.AsSpan(start, 44));
        first.CopyTo(order, start + 44);
        Assert.Throws<FormatException>(() => DecodeNetwork(order));
        foreach (int length in new[] { 0, 11, 15, bytes.Length - 1 })
            Assert.Throws<FormatException>(() => DecodeNetwork(bytes.AsSpan(0, length)));
        Assert.Throws<FormatException>(() => DecodeNetwork([.. bytes, 0]));
        Assert.Throws<FormatException>(() => DecodeNetwork(new byte[WatershedNetworkCodecV1.MaximumEncodedByteLength + 1]));
    }

    [Fact]
    public void Cancellation_and_caller_collection_changes_never_publish_partial_or_mutable_state()
    {
        var (basins, closures) = FanIn();
        using var stop = new CancellationTokenSource(); stop.Cancel();
        Assert.Throws<OperationCanceledException>(() => _planner.Prepare(Province, basins, closures, stop.Token));
        PreparedWatershedNetwork network = Prepare(basins, closures);
        byte[] bytes = WatershedNetworkCodecV1.Encode(network);
        basins[0] = basins[1]; closures[0] = closures[1];
        Assert.Equal(bytes, WatershedNetworkCodecV1.Encode(network));
        Assert.Throws<OperationCanceledException>(() => WatershedNetworkCodecV1.Decode(bytes, stop.Token));
        Assert.Throws<ArgumentOutOfRangeException>(() => network.ContributingColumns(network.Basins[0].BasinId, -1));
    }

    private PreparedWatershedNetwork Prepare(IEnumerable<PreparedWatershed> basins, IEnumerable<WatershedOutletClosure> closures) =>
        _planner.Prepare(Province, basins, closures, TestContext.Current.CancellationToken);

    [Fact]
    public void A_derived_cross_basin_face_cannot_alias_an_existing_outlet_identity()
    {
        var (basins, closures) = FanIn();
        EntityId collision = Prepare(basins, closures).SharedFaces[0].BoundaryId;
        basins[0] = Basin(0, 0, 2, 2, 1, 10, 100, external: collision);
        closures[0] = WatershedOutletClosure.ToBasin(basins[0].BasinId, basins[1].BasinId, 0);
        Assert.Throws<ArgumentException>(() => Prepare(basins, closures));
    }

    [Fact]
    public void A_low_receiving_depression_does_not_bypass_its_higher_spill_route()
    {
        PreparedWatershed? receiver = null;
        for (ulong seed = 0; seed < 32 && receiver is null; seed++)
        {
            var candidate = new WatershedPlanner().Prepare(new WatershedRequest(
                new TerrainScope(new EntityId(0, 11), new EntityId(0, 12), new EntityId(0, 13)), new EntityId(0, seed),
                new WatershedBounds(0, 0, 3, 3), new WatershedRules(10, 8), 8, new EntityId(0, 101)),
                TestContext.Current.CancellationToken);
            if (candidate.Columns[0].NaturalElevation < candidate.Columns[0].SpillElevation) receiver = candidate;
        }
        Assert.NotNull(receiver);
        PreparedWatershed sender = Basin(-1, 0, 1, 1, 0, receiver.Columns[0].NaturalElevation, 100, seed: receiver.Request.Seed);
        Assert.Throws<ArgumentException>(() => Prepare([sender, receiver],
            [WatershedOutletClosure.ToBasin(sender.BasinId, receiver.BasinId, 0), WatershedOutletClosure.ToTerminal(receiver.BasinId, 10)]));
    }

    private static PreparedWatershed Basin(long x, long y, int width, int depth, int outlet, long elevation,
        ulong boundary, EntityId? branch = null, EntityId? seed = null, EntityId? external = null) => new WatershedPlanner().Prepare(
            new WatershedRequest(new TerrainScope(new EntityId(0, 11), branch ?? new EntityId(0, 12), new EntityId(0, 13)),
                seed ?? new EntityId(0, 14), new WatershedBounds(x, y, width, depth), new WatershedRules(elevation, 0),
                outlet, external ?? new EntityId(0, boundary)), TestContext.Current.CancellationToken);

    private static (PreparedWatershed[] Basins, WatershedOutletClosure[] Closures) FanIn(EntityId? branch = null)
    {
        PreparedWatershed[] basins = [Basin(0, 0, 2, 2, 1, 10, 100, branch), Basin(2, 0, 3, 2, 2, 5, 101, branch),
            Basin(5, 0, 2, 2, 0, 0, 102, branch), Basin(2, -2, 3, 2, 3, 10, 103, branch)];
        return (basins, [WatershedOutletClosure.ToBasin(basins[0].BasinId, basins[1].BasinId, 0),
            WatershedOutletClosure.ToBasin(basins[1].BasinId, basins[2].BasinId, 0),
            WatershedOutletClosure.ToTerminal(basins[2].BasinId, 20),
            WatershedOutletClosure.ToBasin(basins[3].BasinId, basins[1].BasinId, 0)]);
    }

    // Independent path enumeration, rather than the planner's topological accumulation algorithm.
    private static void AssertPathCounts(PreparedWatershedNetwork network)
    {
        var basins = network.Basins.ToDictionary(b => b.BasinId);
        var closures = network.Closures.ToDictionary(c => c.BasinId);
        var expected = network.Basins.SelectMany(b => b.Columns.Select(c => new WatershedColumnReference(b.BasinId, c.Index)))
            .ToDictionary(reference => reference, _ => 0);
        foreach (WatershedColumnReference start in expected.Keys.ToArray())
        {
            WatershedColumnReference at = start;
            var visited = new HashSet<WatershedColumnReference>();
            while (true)
            {
                Assert.True(visited.Add(at));
                expected[at]++;
                WatershedColumn column = basins[at.BasinId].Columns[at.ColumnIndex];
                if (column.DownstreamIndex is { } next) at = new(at.BasinId, next);
                else if (closures[at.BasinId].ReceivingBasinId is { } receiver)
                    at = new(receiver, closures[at.BasinId].ReceivingColumnIndex!.Value);
                else break;
            }
        }
        foreach (var (reference, count) in expected)
            Assert.Equal(count, network.ContributingColumns(reference.BasinId, reference.ColumnIndex));
    }

    private static PreparedWatershedNetwork DecodeNetwork(ReadOnlySpan<byte> bytes) =>
        WatershedNetworkCodecV1.Decode(bytes, TestContext.Current.CancellationToken);

    private static int FieldOffset(byte[] bytes, int field)
    {
        int offset = 12;
        for (int i = 0; i < field; i++) offset += checked(4 + (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
        return offset + 4;
    }
}
