using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using static OpenFantasy.Core.Tests.Geography.LiquidCellPlannerFixture;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class LiquidCellPlannerTests
{
    private static readonly LiquidCellPlanner Planner = new();

    [Fact]
    public void FallingFrontTakesSeveralBoundariesAndNeverRelaysIncomingUnits()
    {
        var fixture = new LiquidCellPlannerFixture([new(0, 0, 0)], 3, 10);
        var initial = fixture.PlaceAll(Cell(0, z: 2), downward: 4);
        byte[] custody = AuthorityEncodingV1.Encode(fixture.Reservoirs.Capacity.Inventory);
        var first = Advance(initial);
        AssertQuantities(first, (Cell(0, z: 2), 6), (Cell(0, z: 1), 4));
        Assert.Equal([Raw(Cell(0, z: 1), 0, 4), Raw(Cell(0, z: 2), 4, 10)], first.Portions);
        var second = Advance(first);
        AssertQuantities(second, (Cell(0), 4), (Cell(0, z: 1), 4), (Cell(0, z: 2), 2));
        var third = Advance(second);
        AssertQuantities(third, (Cell(0), 8), (Cell(0, z: 1), 2));
        var fourth = Advance(third);
        AssertQuantities(fourth, (Cell(0), 10));
        Assert.Equal(4UL, fourth.Revision);
        Assert.Equal(new WorldInstant(0, 7200), fourth.Boundary);
        AssertConserved(initial, fourth);
        Assert.Equal(custody, AuthorityEncodingV1.Encode(fixture.Reservoirs.Capacity.Inventory));
    }

    [Fact]
    public void LateralSpreadingUsesOneFaceAndStartingHalfDifferencesIncludingOddRemainder()
    {
        var fixture = new LiquidCellPlannerFixture([new(0, 0, 0), new(1, 0, 0), new(2, 0, 0)], 1, 9);
        var initial = fixture.PlaceAll(Cell(0));
        var first = Advance(initial);
        AssertQuantities(first, (Cell(0), 5), (Cell(1), 4));
        var second = Advance(first);
        AssertQuantities(second, (Cell(0), 5), (Cell(1), 2), (Cell(2), 2));
        AssertConserved(initial, second);
    }

    [Fact]
    public void DownwardAndLateralFacesShareTheDonorsStartingBudgetInThatOrder()
    {
        var fixture = new LiquidCellPlannerFixture([new(0, 0, 0), new(1, 0, 0)], 2, 9);
        var initial = fixture.PlaceAll(Cell(0, z: 1), downward: 7, lateral: 10);
        var next = Advance(initial);
        AssertQuantities(next, (Cell(0), 7), (Cell(1, z: 1), 2));
        Assert.Equal([Raw(Cell(0), 0, 7), Raw(Cell(1, z: 1), 7, 9)], next.Portions);
        AssertConserved(initial, next);
    }

    [Fact]
    public void OutgoingStockCannotCreateRoomAndCompetingFacesShareStartingIncomingCapacity()
    {
        var fixture = new LiquidCellPlannerFixture([new(-1, 0, 0), new(0, 0, -1)], 2, 28);
        var initial = fixture.Place([Raw(Cell(0), 0, 8), Raw(Cell(0, z: 1), 8, 18), Raw(Cell(-1), 18, 28)], lateral: 1);
        var next = Advance(initial);
        // The center sends eight units down, but may still receive only its original two units of room.
        // The above cell uses both; the west cell cannot also send its otherwise legal one-unit proposal.
        AssertQuantities(next, (Cell(0, z: -1), 8), (Cell(-1), 10), (Cell(0), 2),
            (Cell(-1, z: 1), 1), (Cell(0, z: 1), 7));
        AssertConserved(initial, next);
    }

    [Fact]
    public void FullColumnAndMissingNeighborsCannotLoseMaterialOrGainThroughput()
    {
        var fixture = new LiquidCellPlannerFixture([new(0, 0, 0)], 2, 20);
        var initial = fixture.Place([Raw(Cell(0), 0, 10), Raw(Cell(0, z: 1), 10, 20)]);
        var next = Advance(initial);
        Assert.Equal(initial.Portions, next.Portions);
        AssertConserved(initial, next);
        Assert.Equal(1UL, next.Revision);
        var empty = new LiquidCellPlannerFixture([new(0, 0, 0)], 1, 0).Place([]);
        Assert.Empty(Advance(empty).Portions);
    }

    [Fact]
    public void AdjacentDifferentVesselsDoNotBecomeAnImplicitGate()
    {
        var fixture = new ReservoirTransportFixture(receiverUnits: 0);
        var original = fixture.State.Reservoirs;
        var capacity = original.Capacity.PlanCancel(ReservoirTransportFixture.Budget).ApplyTo(original.Capacity);
        var reservoirs = LiquidReservoirState.Create(capacity, original.Geometries);
        var source = new LiquidCellKey(ReservoirTransportFixture.SourceGeometry, new(0, 0, 0));
        var portions = capacity.Inventory.Custody.Lots.Select(l => new LiquidCellPortion(source,
            LiquidStockOrigin.RawSource, l.Source.SourceId, l.Source.Start, l.Source.End));
        var initial = LiquidCellState.Create(reservoirs, new(ulong.MaxValue, ulong.MaxValue), new(0, 0), portions);
        var next = Advance(initial);
        AssertQuantities(next, (source, 2000));
        Assert.Equal(initial.Portions, next.Portions);
        AssertConserved(initial, next);
    }

    [Fact]
    public void CompetingLateralFacesUseCanonicalOrderAndNeverRelay()
    {
        var columns = new ReservoirColumn[] { new(0, -1, 0), new(-1, 0, 0), new(0, 0, 0), new(1, 0, 0), new(0, 1, 0) };
        var fixture = new LiquidCellPlannerFixture(columns, 1, 9);
        var initial = fixture.PlaceAll(Cell(0));
        var next = Advance(initial);
        AssertQuantities(next, (Cell(0, -1), 4), (Cell(-1), 4), (Cell(1), 1));
        AssertConserved(initial, next);
        var reversed = new LiquidCellPlannerFixture(columns.Reverse(), 1, 9).Place(initial.Portions.Reverse());
        Assert.Equal(initial.ContentHash, reversed.ContentHash);
        Assert.Equal(next.ContentHash, Advance(reversed).ContentHash);
    }

    [Fact]
    public void RawSourceThenActualProducedLotOffsetsAreAllocatedWithoutChangingBacking()
    {
        var fixture = new LiquidCellPlannerFixture([new(0, 0, 0)], 2, 10, producedUnits: 4);
        var initial = fixture.PlaceAll(Cell(0, z: 1), downward: 8);
        byte[] production = ProductionCodecV1.Encode(fixture.Reservoirs.Capacity.Production!);
        var next = Advance(initial);
        AssertQuantities(next, (Cell(0), 8), (Cell(0, z: 1), 2));
        Assert.Equal([Raw(Cell(0), 4, 10),
            new LiquidCellPortion(Cell(0), LiquidStockOrigin.ProducedLot, fixture.ProducedLot!.Value, 0, 2),
            new LiquidCellPortion(Cell(0, z: 1), LiquidStockOrigin.ProducedLot, fixture.ProducedLot.Value, 2, 4)], next.Portions);
        AssertConserved(initial, next);
        Assert.Equal(production, ProductionCodecV1.Encode(fixture.Reservoirs.Capacity.Production!));
        var reordered = fixture.Place(initial.Portions.Reverse(), downward: 8);
        Assert.Equal(next.ContentHash, Advance(reordered).ContentHash);
    }

    [Fact]
    public void HeldBoundaryHasNoFlowAndDoesNotAccumulateAllowance()
    {
        var fixture = new LiquidCellPlannerFixture([new(0, 0, 0)], 2, 10);
        var initial = fixture.PlaceAll(Cell(0, z: 1), downward: 3);
        var held = Planner.PlanAdvance(initial, new(0, 1800), held: true).ApplyTo(initial);
        Assert.Equal(initial.Portions, held.Portions);
        Assert.Equal(1UL, held.Revision);
        Assert.Equal(new WorldInstant(0, 1800), held.Boundary);
        var next = Advance(held);
        AssertQuantities(next, (Cell(0), 3), (Cell(0, z: 1), 7));
        Assert.Same(next, Planner.PlanAdvance(next, next.Boundary).ApplyTo(next));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(1799)]
    [InlineData(1801)]
    [InlineData(3600)]
    public void PastPartialAndSkippedBoundariesRefuseWithoutMutatingInput(long milliseconds)
    {
        var initial = new LiquidCellPlannerFixture([new(0, 0, 0)], 2, 10).PlaceAll(Cell(0, z: 1));
        var hash = initial.ContentHash;
        Assert.Throws<ArgumentException>(() => Planner.PlanAdvance(initial, initial.Boundary.AddMilliseconds(milliseconds)));
        Assert.Equal(hash, initial.ContentHash);
        Assert.Equal(0UL, initial.Revision);
    }

    [Fact]
    public void PlanRequiresTheWholeExactPlacementProfileAndBoundary()
    {
        var fixture = new LiquidCellPlannerFixture([new(0, 0, 0)], 2, 10);
        var initial = fixture.PlaceAll(Cell(0, z: 1));
        var plan = Planner.PlanAdvance(initial, new(0, 1800));
        var changedPlacement = fixture.PlaceAll(Cell(0));
        var changedProfile = fixture.PlaceAll(Cell(0, z: 1), downward: 1);
        Assert.Throws<InvalidOperationException>(() => plan.ApplyTo(changedPlacement));
        Assert.Throws<InvalidOperationException>(() => plan.ApplyTo(changedProfile));
        Assert.Throws<InvalidOperationException>(() => plan.ApplyTo(plan.Candidate));
        Assert.Same(plan.Candidate, plan.ApplyTo(initial));
        Assert.Same(initial, Planner.PlanAdvance(initial, initial.Boundary, held: true).ApplyTo(initial));
    }

    [Fact]
    public void ExceedingNormalizedFragmentBoundRefusesTheEntireStage()
    {
        const int fragments = LiquidCellState.MaximumPortions;
        var fixture = new LiquidCellPlannerFixture([new(0, 0, 0), new(1, 0, 0), new(2, 0, 0)], 1,
            (ulong)fragments * 2, microlitresPerUnit: 1);
        var portions = Enumerable.Range(0, fragments).Select(i => Raw(Cell(i % 2 == 0 ? 0 : 2), (ulong)i * 2, (ulong)i * 2 + 2));
        var initial = fixture.Place(portions, downward: 1, lateral: 1);
        byte[] before = LiquidCellStateCodecV1.Encode(initial);
        Assert.Equal(fragments, initial.Portions.Count);
        Assert.Throws<ArgumentException>(() => Planner.PlanAdvance(initial, new(0, 1800)));
        Assert.Equal(before, LiquidCellStateCodecV1.Encode(initial));
        Assert.Equal(0UL, initial.Revision);
    }

    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void HorizontalCoordinateEdgesAreClosedWithoutOverflow(long x)
    {
        var fixture = new LiquidCellPlannerFixture([new(x, x, 0)], 1, 10);
        var initial = fixture.PlaceAll(Cell(x, x));
        Assert.Equal(initial.Portions, Advance(initial).Portions);
    }

    private static LiquidCellState Advance(LiquidCellState state) =>
        Planner.PlanAdvance(state, state.Boundary.AddMilliseconds(1800)).ApplyTo(state);

    private static void AssertQuantities(LiquidCellState state, params (LiquidCellKey Cell, ulong Quantity)[] expected) =>
        Assert.Equal(expected.OrderBy(p => p.Cell), state.Quantities.OrderBy(p => p.Key).Select(p => (p.Key, p.Value)));

    private static void AssertConserved(LiquidCellState before, LiquidCellState after)
    {
        Assert.Equal(before.ReservoirHash, after.ReservoirHash);
        Assert.Equal(Units(before), Units(after));
        Assert.All(after.Quantities, pair => Assert.InRange(pair.Value, 1UL, after.UnitsPerCell));
    }

    private static IEnumerable<(EntityId Reservoir, LiquidStockOrigin Origin, EntityId Id, ulong Unit)> Units(LiquidCellState state) =>
        state.Portions.SelectMany(p => Enumerable.Range(0, checked((int)p.Quantity))
                .Select(i => (p.Cell.ReservoirId, p.Origin, p.OriginId, p.Start + (ulong)i)))
            .OrderBy(p => p.ReservoirId).ThenBy(p => p.Origin).ThenBy(p => p.OriginId).ThenBy(p => p.Item4);
}
