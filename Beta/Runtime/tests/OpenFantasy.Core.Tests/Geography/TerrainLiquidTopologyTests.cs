using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Terrain;
using static OpenFantasy.Core.Tests.Actors.BodyDecodingTestFrames;
using static OpenFantasy.Core.Tests.Geography.LiquidCellPlannerFixture;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class TerrainLiquidTopologyTests
{
    [Fact]
    public void Actual_divider_blocks_flow_until_proved_removal_opens_a_conserved_causal_front()
    {
        var fixture = new TopologyFixture();
        TerrainLiquidTopology topology = fixture.Topology(fixture.Region);
        Assert.Equal(6, topology.NominalCells.Count);
        Assert.Equal(5, topology.OpenCells.Count);
        Assert.Equal(5_000_000_000UL, topology.OpenCapacity.Value);
        Assert.DoesNotContain(Cell(1), topology.OpenCells);
        LiquidCellState initial = fixture.State(topology);
        var planner = new LiquidCellPlanner();
        LiquidCellState closed = planner.PlanAdvance(initial, new(0, 1800)).ApplyTo(initial);
        Assert.Equal(initial.Portions, closed.Portions);
        Assert.Same(topology, closed.Topology);
        Assert.Equal(0UL, Quantity(closed, Cell(2, z: -1)));

        // This is a terrain proposal, not a claim that a diagnostic authorizes live excavation.
        TerrainRegionSnapshot removed = fixture.Region.Excavate(fixture.Region.Revision, [new(1, 0, 0)]);
        TerrainLiquidTopology opened = fixture.Topology(removed);
        Assert.Equal(6, opened.OpenCells.Count);
        Assert.NotEqual(topology.ContentHash, opened.ContentHash);
        LiquidCellState rebound = closed.RebindTerrain(fixture.Stock.Reservoirs, opened);
        Assert.Equal(closed.ReservoirHash, rebound.ReservoirHash);
        Assert.Equal(closed.Boundary, rebound.Boundary);
        Assert.Equal(closed.Revision, rebound.Revision);
        Assert.Equal(closed.Portions, rebound.Portions);
        Assert.NotEqual(closed.ContentHash, rebound.ContentHash);
        LiquidCellState first = planner.PlanAdvance(rebound, new(0, 3600)).ApplyTo(rebound);
        LiquidCellState second = planner.PlanAdvance(first, new(0, 5400)).ApplyTo(first);
        LiquidCellState third = planner.PlanAdvance(second, new(0, 7200)).ApplyTo(second);
        Assert.Equal(2UL, Quantity(first, Cell(1)));
        Assert.Equal(0UL, Quantity(first, Cell(2)));
        Assert.Equal(1UL, Quantity(second, Cell(2)));
        Assert.Equal(0UL, Quantity(second, Cell(2, z: -1)));
        Assert.Equal(1UL, Quantity(third, Cell(2, z: -1)));
        foreach (LiquidCellState state in new[] { initial, closed, rebound, first, second, third })
        {
            AssertExactStock(state);
            byte[] bytes = LiquidCellStateCodecV2.Encode(state);
            Assert.Equal(bytes, LiquidCellStateCodecV2.Encode(LiquidCellStateCodecV2.Decode(bytes,
                fixture.Stock.Reservoirs, state.Topology!)));
        }
        Assert.Throws<InvalidOperationException>(() => planner.PlanAdvance(closed, new(0, 3600)).ApplyTo(rebound));
        Assert.Equal(initial.Portions, planner.PlanAdvance(rebound, new(0, 3600), held: true).ApplyTo(rebound).Portions);
        Assert.True(fixture.Region.GetCell(new(1, 0, 0)).IsSolid);
    }

    [Fact]
    public void Exact_region_policy_and_geometry_pins_are_canonical_and_cannot_be_substituted()
    {
        var fixture = new TopologyFixture();
        TerrainRegionSnapshot unrelated = fixture.Generate(new(new(8, -1, -2), 2, 3, 5), []);
        var first = TerrainLiquidTopology.Create(fixture.Stock.Reservoirs, [fixture.Region, unrelated], fixture.Seals);
        var reversed = TerrainLiquidTopology.Create(fixture.Stock.Reservoirs, [unrelated, fixture.Region], fixture.Seals);
        Assert.Equal(first.ContentHash, reversed.ContentHash);
        Assert.Equal(first.OpenCells, reversed.OpenCells);
        LiquidCellState state = fixture.State(first);
        byte[] bytes = LiquidCellStateCodecV2.Encode(state);
        Assert.Equal(bytes, LiquidCellStateCodecV2.Encode(LiquidCellStateCodecV2.Decode(bytes, fixture.Stock.Reservoirs, reversed)));

        TerrainRegionSnapshot changed = unrelated.Excavate(0, [new(8, 0, 0)]);
        var changedContext = TerrainLiquidTopology.Create(fixture.Stock.Reservoirs, [fixture.Region, changed], fixture.Seals);
        Assert.Equal(first.OpenCells, changedContext.OpenCells);
        Assert.NotEqual(first.ContentHash, changedContext.ContentHash);
        Assert.Throws<FormatException>(() => LiquidCellStateCodecV2.Decode(bytes, fixture.Stock.Reservoirs, changedContext));
        var alteredSeals = new ReservoirSealPolicy(fixture.Seals.Liquid,
            fixture.Seals.Rules.Select(rule => new ReservoirSealRule(rule.Material, 999)));
        var changedPolicy = TerrainLiquidTopology.Create(fixture.Stock.Reservoirs, [fixture.Region, unrelated], alteredSeals);
        Assert.NotEqual(first.ContentHash, changedPolicy.ContentHash);
        Assert.Throws<FormatException>(() => LiquidCellStateCodecV2.Decode(bytes, fixture.Stock.Reservoirs, changedPolicy));
        var otherGeometry = new LiquidCellPlannerFixture([new(0, 0, 0), new(1, 0, 0), new(2, 0, -1)], 1, 10);
        Assert.Throws<ArgumentException>(() => state.Rebind(otherGeometry.Reservoirs));
        Assert.Throws<ArgumentException>(() => LiquidCellState.Create(otherGeometry.Reservoirs, state.Profile,
            state.Boundary, state.Portions, first));
        var otherUnits = new LiquidCellPlannerFixture(TopologyFixture.Columns, 1, 11);
        Assert.Throws<ArgumentException>(() => state.Rebind(otherUnits.Reservoirs));
        Assert.Throws<FormatException>(() => LiquidCellStateCodecV2.Decode(bytes, otherUnits.Reservoirs, first));
    }

    [Fact]
    public void Proved_new_solid_cannot_displace_existing_water_and_nominal_capacity_is_not_available_void()
    {
        var fixture = new TopologyFixture();
        LiquidCellState state = fixture.State(fixture.Topology(fixture.Region));
        TerrainRegionSnapshot filled = fixture.Generate(fixture.Region.Bounds,
            TopologyFixture.Holes.Where(cell => cell != new CellCoordinate(0, 0, 0)));
        TerrainLiquidTopology filledTopology = fixture.Topology(filled);
        Assert.Equal(4_000_000_000UL, filledTopology.OpenCapacity.Value);
        Assert.Throws<ArgumentException>(() => state.RebindTerrain(fixture.Stock.Reservoirs, filledTopology));
        Assert.Throws<ArgumentException>(() => LiquidCellState.Create(fixture.Stock.Reservoirs, state.Profile,
            state.Boundary, [Raw(Cell(1), 0, 10)], state.Topology));
        TerrainRegionSnapshot allSolid = fixture.Generate(fixture.Region.Bounds, []);
        TerrainLiquidTopology noVoid = fixture.Topology(allSolid);
        Assert.Empty(noVoid.OpenCells);
        Assert.Equal(0UL, noVoid.OpenCapacity.Value);
        Assert.Throws<ArgumentException>(() => fixture.State(noVoid));
        Assert.Same(state, state.Rebind(fixture.Stock.Reservoirs));
        Assert.Same(state, state.RebindTerrain(fixture.Stock.Reservoirs, state.Topology!));
    }

    [Theory]
    [InlineData("floor")]
    [InlineData("wall")]
    [InlineData("missing-wall")]
    [InlineData("missing-vent")]
    [InlineData("spill-roof")]
    [InlineData("internal-roof")]
    [InlineData("unlisted-material")]
    [InlineData("wrong-liquid")]
    public void Unknown_unsealed_or_unvented_terrain_cannot_supply_a_topology(string fault)
    {
        var fixture = new TopologyFixture();
        TerrainRegionSnapshot region = fixture.Region;
        ReservoirSealPolicy policy = fixture.Seals;
        switch (fault)
        {
            case "floor": region = region.Excavate(region.Revision, [new(2, 0, -2)]); break;
            case "wall": region = region.Excavate(region.Revision, [new(0, -1, 0)]); break;
            case "missing-wall": region = fixture.Generate(new(new(0, -1, -2), 5, 3, 5), TopologyFixture.Holes); break;
            case "missing-vent": region = fixture.Generate(new(new(-1, -1, -2), 6, 3, 3), TopologyFixture.Holes); break;
            case "spill-roof":
                region = fixture.Generate(fixture.Region.Bounds, TopologyFixture.Holes,
                    new StratifiedTerrainRules(1, 0, 1, fixture.Region.Rules.Materials));
                break;
            case "internal-roof":
                region = fixture.Generate(fixture.Region.Bounds, TopologyFixture.Holes.Where(cell => cell != new CellCoordinate(2, 0, 0)));
                break;
            case "unlisted-material":
                Assert.True(region.GetCell(new(1, 0, 0)).IsSolid);
                policy = new(policy.Liquid, policy.Rules.Where(rule => rule.Material != region.GetCell(new(1, 0, 0)).Material));
                break;
            case "wrong-liquid": policy = new(Item(999), policy.Rules); break;
        }
        Assert.Throws<ArgumentException>(() => TerrainLiquidTopology.Create(fixture.Stock.Reservoirs, [region], policy));
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("duplicate")]
    [InlineData("overlap")]
    [InlineData("scope")]
    [InlineData("seed")]
    [InlineData("rules")]
    [InlineData("count")]
    public void Terrain_catalogs_keep_the_existing_finite_scope_partition_seed_and_rules_contract(string fault)
    {
        var fixture = new TopologyFixture();
        TerrainRegionSnapshot other = fixture.Generate(new(new(8, -1, -2), 2, 3, 5), []);
        TerrainRegionSnapshot[] regions = fault switch
        {
            "empty" => [],
            "duplicate" => [fixture.Region, fixture.Region],
            "overlap" => [fixture.Region, fixture.Generate(new(new(0, -1, -2), 2, 3, 5), [])],
            "scope" => [new StratifiedTerrainGenerator().Generate(new(Id(90), Id(91), Id(92)), other.Seed, other.Rules, other.Bounds)],
            "seed" => [fixture.Region, new StratifiedTerrainGenerator().Generate(other.Scope, Id(900), other.Rules, other.Bounds)],
            "rules" => [fixture.Region, fixture.Generate(other.Bounds, [], new(0, 0, 2, other.Rules.Materials))],
            _ => Enumerable.Repeat(fixture.Region, 17).ToArray()
        };
        Assert.Throws<ArgumentException>(() => TerrainLiquidTopology.Create(fixture.Stock.Reservoirs, regions, fixture.Seals));
    }

    [Fact]
    public void V2_codec_rejects_tampered_context_rows_truncation_and_noncanonical_placement()
    {
        var fixture = new TopologyFixture();
        TerrainLiquidTopology topology = fixture.Topology(fixture.Region);
        LiquidCellState state = LiquidCellState.Create(fixture.Stock.Reservoirs, new(2, 2), new(0, 0),
            [Raw(Cell(0), 0, 6), Raw(Cell(2, z: -1), 6, 10)], topology);
        byte[] bytes = LiquidCellStateCodecV2.Encode(state);
        Assert.Equal(8, Fields(bytes).Count);
        List<byte[]> fields = Fields(bytes);
        byte[] blockedRow = fields[7].ToArray();
        BinaryPrimitives.WriteInt64BigEndian(blockedRow.AsSpan(16), 1);
        byte[] unknownOrigin = fields[7].ToArray();
        BinaryPrimitives.WriteUInt64BigEndian(unknownOrigin.AsSpan(40), 3);
        byte[] wrongPin = fields[6].ToArray();
        wrongPin[0] ^= 1;
        byte[][] bad = [bytes[..^1], [.. bytes, 0], Replace(bytes, 6, wrongPin), Replace(bytes, 7, blockedRow),
            Replace(bytes, 7, unknownOrigin), Replace(bytes, 7, fields[7][..^1]),
            Replace(bytes, 7, fields[7].Chunk(80).Reverse().SelectMany(row => row).ToArray()),
            Replace(bytes, 7, fields[7].Concat(fields[7][..80]).ToArray()),
            new byte[LiquidCellStateCodecV2.MaximumEncodedByteLength + 1]];
        foreach (byte[] input in bad)
        {
            Assert.Throws<FormatException>(() => LiquidCellStateCodecV2.Decode(input, fixture.Stock.Reservoirs, topology));
        }
        Assert.Equal(bytes, LiquidCellStateCodecV2.Encode(state));
        Assert.Throws<ArgumentException>(() => LiquidCellStateCodecV1.Encode(state));
        LiquidCellState legacy = fixture.Stock.PlaceAll(Cell(0), downward: 2, lateral: 2);
        byte[] original = LiquidCellStateCodecV1.Encode(legacy);
        Assert.Equal(7, Fields(original).Count);
        Assert.Equal(original, LiquidCellStateCodecV1.Encode(LiquidCellStateCodecV1.Decode(original, fixture.Stock.Reservoirs)));
        Assert.Throws<ArgumentException>(() => LiquidCellStateCodecV2.Encode(legacy));
        Assert.Throws<FormatException>(() => LiquidCellStateCodecV1.Decode(bytes, fixture.Stock.Reservoirs));
        Assert.Throws<FormatException>(() => LiquidCellStateCodecV2.Decode(original, fixture.Stock.Reservoirs, topology));
    }

    private static void AssertExactStock(LiquidCellState state)
    {
        ulong cursor = 0;
        foreach (LiquidCellPortion portion in state.Portions.OrderBy(portion => portion.Start))
        {
            Assert.Equal(LiquidStockOrigin.RawSource, portion.Origin);
            Assert.Equal(Source, portion.OriginId);
            Assert.Equal(cursor, portion.Start);
            cursor = portion.End;
        }
        Assert.Equal(10UL, cursor);
        Assert.Equal(10UL, state.Quantities.Values.Aggregate(0UL, (sum, units) => checked(sum + units)));
    }

    private static ulong Quantity(LiquidCellState state, LiquidCellKey cell) => state.Quantities.GetValueOrDefault(cell);
    private static CountedItemDefinition Item(ulong ordinal) => new(Id(ordinal), ContentHash.Compute(BitConverter.GetBytes(ordinal)));

    private sealed class TopologyFixture
    {
        internal static ReservoirColumn[] Columns => [new(0, 0, 0), new(1, 0, 0), new(2, 0, -1), new(3, 0, -1)];
        internal static CellCoordinate[] Holes => [new(0, 0, 0), new(2, 0, 0), new(2, 0, -1), new(3, 0, 0), new(3, 0, -1)];
        internal LiquidCellPlannerFixture Stock { get; } = new(Columns, 1, 10);
        internal TerrainRegionSnapshot Region { get; }
        internal ReservoirSealPolicy Seals { get; }

        internal TopologyFixture()
        {
            var palette = new TerrainMaterialPalette(Item(201), Item(202), Item(203));
            Region = Generate(new(new(-1, -1, -2), 6, 3, 5), Holes, new(0, 0, 1, palette));
            Seals = new(Stock.Reservoirs.Capacity.Domain.Material.Item,
                new[] { palette.Surface, palette.Soil, palette.Stone }.Select(material => new ReservoirSealRule(material, 1000)));
        }

        internal TerrainRegionSnapshot Generate(TerrainRegionBounds bounds, IEnumerable<CellCoordinate> holes,
            StratifiedTerrainRules? rules = null)
        {
            var region = new StratifiedTerrainGenerator().Generate(Stock.Reservoirs.Capacity.Domain.Network.Scope,
                Id(200), rules ?? Region.Rules, bounds);
            CellCoordinate[] edits = holes.ToArray();
            return edits.Length == 0 ? region : region.Excavate(0, edits);
        }

        internal TerrainLiquidTopology Topology(TerrainRegionSnapshot region) => TerrainLiquidTopology.Create(Stock.Reservoirs, [region], Seals);
        internal LiquidCellState State(TerrainLiquidTopology topology) => LiquidCellState.Create(Stock.Reservoirs,
            new(2, 2), new(0, 0), [Raw(Cell(0), 0, 10)], topology);
    }
}
