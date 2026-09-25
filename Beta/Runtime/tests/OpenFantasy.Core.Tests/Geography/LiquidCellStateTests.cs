using System.Buffers.Binary;
using OpenFantasy.Core.Geography;
using static OpenFantasy.Core.Tests.Geography.LiquidCellPlannerFixture;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class LiquidCellStateTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("overlap")]
    [InlineData("source")]
    [InlineData("origin")]
    [InlineData("vessel")]
    [InlineData("outside")]
    [InlineData("capacity")]
    public void Exact_spatial_partition_refuses_unbacked_missing_overlapping_or_overfull_claims(string fault)
    {
        var fixture = new LiquidCellPlannerFixture([new(0, 0, 0), new(1, 0, 0)], 1, 12);
        LiquidCellPortion[] portions = fault switch
        {
            "missing" => [Raw(Cell(0), 0, 6), Raw(Cell(1), 7, 12)],
            "extra" => [Raw(Cell(0), 0, 6), Raw(Cell(1), 6, 13)],
            "overlap" => [Raw(Cell(0), 0, 7), Raw(Cell(1), 6, 12)],
            "source" => [new(Cell(0), LiquidStockOrigin.RawSource, Id(900), 0, 6), Raw(Cell(1), 6, 12)],
            "origin" => [new(Cell(0), LiquidStockOrigin.ProducedLot, Source, 0, 6), Raw(Cell(1), 6, 12)],
            "vessel" => [Raw(new(Id(900), new(0, 0, 0)), 0, 6), Raw(Cell(1), 6, 12)],
            "outside" => [Raw(Cell(2), 0, 6), Raw(Cell(1), 6, 12)],
            _ => [Raw(Cell(0), 0, 12)]
        };
        var before = fixture.Reservoirs.ContentHash;
        Assert.Throws<ArgumentException>(() => fixture.Place(portions));
        Assert.Equal(before, fixture.Reservoirs.ContentHash);
    }

    [Theory]
    [InlineData(3UL)]
    [InlineData(1_000_000_001UL)]
    public void Nonintegral_or_oversized_unit_quantum_cannot_silently_round(ulong microlitres)
    {
        var fixture = new LiquidCellPlannerFixture([new(0, 0, 0)], 2, 1, microlitres);
        Assert.Throws<ArgumentException>(() => fixture.PlaceAll(Cell(0)));
    }

    [Fact]
    public void Detailed_domain_has_an_enforced_finite_cell_limit()
    {
        var columns = Enumerable.Range(0, 4).Select(x => new ReservoirColumn(x, 0, 0)).ToArray();
        var maximum = new LiquidCellPlannerFixture(columns, 1024, 1);
        var state = maximum.PlaceAll(Cell(0));
        Assert.Equal(LiquidCellState.MaximumCells, state.DomainCells.Count);
        Assert.Equal(state.ContentHash, LiquidCellStateCodecV1.Decode(
            LiquidCellStateCodecV1.Encode(state), maximum.Reservoirs).ContentHash);
        var excessive = new LiquidCellPlannerFixture(columns.Append(new(4, 0, 1023)), 1024, 1);
        Assert.Throws<ArgumentException>(() => excessive.PlaceAll(Cell(0)));
    }

    [Fact]
    public void Codec_requires_canonical_order_exact_owner_and_complete_fields()
    {
        var fixture = new LiquidCellPlannerFixture([new(0, 0, 0), new(1, 0, 0)], 1, 12);
        var state = fixture.Place([Raw(Cell(1), 6, 12), Raw(Cell(0), 3, 6), Raw(Cell(0), 0, 3)]);
        Assert.Equal(2, state.Portions.Count);
        byte[] canonical = LiquidCellStateCodecV1.Encode(state);
        Assert.Equal(canonical, LiquidCellStateCodecV1.Encode(LiquidCellStateCodecV1.Decode(canonical, fixture.Reservoirs)));
        byte[] reordered = canonical.ToArray();
        int offset = 12;
        for (int field = 0; field < 6; field++)
        {
            offset += 4 + checked((int)BinaryPrimitives.ReadUInt32BigEndian(reordered.AsSpan(offset)));
        }
        offset += 4;
        canonical.AsSpan(offset + 80, 80).CopyTo(reordered.AsSpan(offset));
        canonical.AsSpan(offset, 80).CopyTo(reordered.AsSpan(offset + 80));
        Assert.Throws<FormatException>(() => LiquidCellStateCodecV1.Decode(reordered, fixture.Reservoirs));
        Assert.Throws<FormatException>(() => LiquidCellStateCodecV1.Decode(canonical[..^1], fixture.Reservoirs));
        Assert.Throws<FormatException>(() => LiquidCellStateCodecV1.Decode([.. canonical, 0], fixture.Reservoirs));
        var other = new LiquidCellPlannerFixture([new(0, 0, 0), new(1, 0, 0)], 1, 13);
        Assert.Throws<FormatException>(() => LiquidCellStateCodecV1.Decode(canonical, other.Reservoirs));
    }
}
