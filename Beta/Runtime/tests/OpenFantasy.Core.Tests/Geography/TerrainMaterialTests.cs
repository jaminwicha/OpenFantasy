using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Geography;

public sealed partial class TerrainMaterialTests
{
    private readonly TerrainMaterialPlanner _planner = new();

    [Fact]
    public void RefinementMovesExactSpatialIntervalsWithoutMintingOrChangingTheInput()
    {
        TerrainMaterialState state = Create();
        byte[] before = TerrainMaterialCodecV1.Encode(state);
        CellCoordinate[] cells = [new(-1, -1, 0), new(-2, -2, -1)];
        TerrainMaterialPlan plan = Refine(state, cells, 1);
        TerrainMaterialState changed = plan.ApplyTo(state);
        Assert.Equal(60UL, changed.CoarseQuantity);
        Assert.Equal(20UL, changed.DetailedQuantity);
        Assert.Equal(0UL, changed.OtherQuantity);
        Assert.Equal(new[] { (10UL, 20UL), (80UL, 90UL) }, changed.Inventory.Custody.Lots
            .Where(l => l.CustodianId == Fine).OrderBy(l => l.Source.Start).Select(l => (l.Source.Start, l.Source.End)));
        Assert.Equal(before, TerrainMaterialCodecV1.Encode(state));
        Assert.Equal(state.Inventory.Sources, changed.Inventory.Sources);
        Assert.Equal(state.Inventory.Controls, changed.Inventory.Controls);
        Assert.Equal(state.Inventory.Policies, changed.Inventory.Policies);
        Assert.Throws<InvalidOperationException>(() => plan.ApplyTo(changed));
        Assert.Throws<InvalidOperationException>(() => Refine(changed, [cells[0]], 2));
        AssertConserved(changed);
    }

    [Fact]
    public void PartialActualTransferThenDemotionAndRefinementNeverRestoresMovedUnits()
    {
        TerrainMaterialState state = Create();
        CellCoordinate cell = new(-2, -2, -1);
        state = Refine(state, [cell], 1).ApplyTo(state);
        CountedSourceLot lot = Assert.Single(state.Inventory.Custody.Lots, l => l.CustodianId == Fine);
        CountedAuthorityState inventory = Transfer(state.Inventory, lot, Away, 4);
        state = state.ReconcileInventory(inventory);
        Assert.Equal(6UL, state.DetailedQuantity);
        Assert.Equal(4UL, state.OtherQuantity);
        CountedSourceLot moved = Assert.Single(inventory.Custody.Lots, l => l.CustodianId == Away);
        state = _planner.Demote(state, [cell], Id(2), state.Revision).ApplyTo(state);
        Assert.Empty(state.ActiveCells);
        Assert.Equal(76UL, state.CoarseQuantity);
        Assert.Equal(0UL, state.DetailedQuantity);
        Assert.Equal(moved, state.Inventory.Custody.GetLot(moved.LotId));
        state = Refine(state, [cell], 3).ApplyTo(state);
        Assert.Equal(6UL, state.DetailedQuantity);
        Assert.Equal(4UL, state.OtherQuantity);
        Assert.Equal(new SourceInterval(Source, 14, 20), Assert.Single(state.Inventory.Custody.Lots, l => l.CustodianId == Fine).Source);
        AssertConserved(state);
        Retain(state);
    }

    [Fact]
    public void FullyMovedCellCanDemoteWithoutRecreatingAClaim()
    {
        TerrainMaterialState state = Create();
        CellCoordinate cell = new(-2, -2, -1);
        state = Refine(state, [cell], 1).ApplyTo(state);
        CountedSourceLot lot = Assert.Single(state.Inventory.Custody.Lots, l => l.CustodianId == Fine);
        state = state.ReconcileInventory(Transfer(state.Inventory, lot, Away, 10));
        state = _planner.Demote(state, [cell], Id(2), state.Revision).ApplyTo(state);
        Assert.Equal(70UL, state.CoarseQuantity);
        Assert.Equal(10UL, state.OtherQuantity);
        Assert.Throws<InvalidOperationException>(() => Refine(state, [cell], 3));
        AssertConserved(state);
    }

    [Fact]
    public void CellAndWindowOrderPreserveSourcePartitionsIncludingAcrossBranchCopies()
    {
        TerrainMaterialState original = Create();
        CellCoordinate[] cells = [new(-2, -2, -1), new(-1, -1, 0), new(-1, -2, -1)];
        var reverse = Refine(original, cells.Reverse(), 1).ApplyTo(original);
        var forward = Refine(original, cells, 1).ApplyTo(original);
        Assert.Equal(TerrainMaterialCodecV1.Encode(forward), TerrainMaterialCodecV1.Encode(reverse));
        TerrainMaterialState split = original;
        for (int i = 0; i < cells.Length; i++)
        {
            PreparedWatershedTerrain window = new WatershedTerrainMapper().Prepare(original.Deposit.Terrain.Network,
                original.Deposit.Terrain.Mapping, new(cells[i], 1, 1, 1), TestContext.Current.CancellationToken);
            split = _planner.Refine(split, window, [cells[i]], Id((ulong)(i + 2)), split.Revision).ApplyTo(split);
        }
        Assert.Equal(Intervals(forward, Fine), Intervals(split, Fine));
        TerrainMaterialState fork = Create(branch: new(0, 999));
        fork = Refine(fork, cells, 1).ApplyTo(fork);
        Assert.Equal(Intervals(forward, Fine), Intervals(fork, Fine));
        Assert.NotEqual(forward.ContentHash, fork.ContentHash);
        Assert.Throws<ArgumentException>(() => _planner.Refine(original, fork.Deposit.Terrain, cells, Id(8), original.Revision));
    }

    [Fact]
    public void DefinitionHasFinitePhysicalCalibrationAndCanonicalWholeSlabIntervals()
    {
        TerrainMaterialDeposit deposit = Create().Deposit;
        Assert.Equal(1_000_000_000UL, deposit.Material.Physical.VolumePerUnit.Multiply(deposit.UnitsPerCell).Value);
        Assert.Equal(20_000_000_000UL, deposit.TotalMass.Value);
        ulong cursor = deposit.Source.Start;
        for (long z = -1; z <= 0; z++) for (long y = -2; y <= -1; y++) for (long x = -2; x <= -1; x++)
        {
            SourceInterval interval = deposit.IntervalFor(new(x, y, z));
            Assert.Equal(cursor, interval.Start); Assert.Equal(cursor + 10, interval.End);
            Assert.Equal(new CellCoordinate(x, y, z), deposit.CellForUnit(interval.Start)); cursor += 10;
        }
        Assert.Equal(deposit.Source.End, cursor);
        Assert.Throws<ArgumentOutOfRangeException>(() => deposit.IntervalFor(new(-2, -2, -2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => deposit.IntervalFor(new(0, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => deposit.CellForUnit(deposit.Source.End));
        Assert.Throws<ArgumentException>(() => new TerrainMaterialDeposit(deposit.Terrain, deposit.MacroColumn, 2,
            deposit.Material, 9, deposit.Source, Coarse, Fine));
        Assert.Throws<ArgumentException>(() => new TerrainMaterialDeposit(deposit.Terrain, deposit.MacroColumn, 3,
            deposit.Material, 10, deposit.Source, Coarse, Fine));
    }

    [Fact]
    public void MissingFundingBadCapacityProtectedStockAndForeignPinsReject()
    {
        TerrainMaterialState state = Create();
        Assert.Throws<ArgumentException>(() => TerrainMaterialState.Create(state.Deposit,
            CopyInventory(state.Inventory, new(state.Inventory.WorldId, state.Inventory.BranchId, 0, state.Inventory.Custody.Storages, []))));
        TerrainMaterialState small = Create(fineCapacity: 9);
        Assert.Throws<ArgumentException>(() => Refine(small, [new(-2, -2, -1)], 1));
        TerrainMaterialState protectedState = Create(protection: LotProtection.Favorite);
        Assert.Throws<InvalidOperationException>(() => Refine(protectedState, [new(-2, -2, -1)], 1));
        Assert.Throws<ArgumentException>(() => _planner.Refine(state,
            new WatershedTerrainMapper().Prepare(state.Deposit.Terrain.Network, new(2, 1, 1), state.Deposit.Terrain.Bounds,
                TestContext.Current.CancellationToken), [new(-2, -2, -1)], Id(1), state.Revision));
        Assert.Throws<ArgumentException>(() => _planner.Refine(state, state.Deposit.Terrain, [new(-2, -2, 1)], Id(1), state.Revision));
        Assert.Throws<ArgumentException>(() => Refine(state, [new(-2, -2, -1), new(-2, -2, -1)], 1));
    }

    [Fact]
    public void PlansPinCompleteStateAndRejectKnownOrHistoricallyOccupiedIdentities()
    {
        TerrainMaterialState state = Create();
        var cell = new CellCoordinate(-2, -2, -1);
        Assert.Throws<InvalidOperationException>(() => _planner.Refine(state, state.Deposit.Terrain, [cell], Source, state.Revision));
        Assert.Throws<InvalidOperationException>(() => _planner.Refine(state, state.Deposit.Terrain, [cell], Id(1), state.Revision, _ => true));
        Assert.Throws<InvalidOperationException>(() => _planner.Refine(state, state.Deposit.Terrain, [cell], Id(1), 99));
        var plan = Refine(state, [cell], 1);
        TerrainMaterialState moved = plan.ApplyTo(state);
        var firstIds = moved.Inventory.Custody.Lots.Select(l => l.LotId).ToHashSet();
        moved = _planner.Demote(moved, [cell], Id(2), moved.Revision).ApplyTo(moved);
        var next = _planner.Refine(moved, moved.Deposit.Terrain, [cell], Id(3), moved.Revision, firstIds.Contains).ApplyTo(moved);
        Assert.DoesNotContain(next.Inventory.Custody.Lots.Where(l => l.CustodianId == Fine), l => firstIds.Contains(l.LotId));
    }

    [Fact]
    public void CanonicalStateRoundTripsBindingsAndRejectsCorruptOrTrailingData()
    {
        TerrainMaterialState state = Create(); state = Refine(state, [new(-2, -2, -1)], 1).ApplyTo(state);
        byte[] bytes = TerrainMaterialCodecV1.Encode(state);
        Assert.Equal(bytes, TerrainMaterialCodecV1.Encode(TerrainMaterialCodecV1.Decode(bytes, TestContext.Current.CancellationToken)));
        byte[] changed = (byte[])bytes.Clone(); changed[^1] ^= 0x80;
        foreach (byte[] invalid in new[] { changed, bytes[..^1], bytes.Concat(new byte[] { 0 }).ToArray() })
            Assert.Throws<FormatException>(() => TerrainMaterialCodecV1.Decode(invalid, TestContext.Current.CancellationToken));
    }

    private TerrainMaterialPlan Refine(TerrainMaterialState state, IEnumerable<CellCoordinate> cells, ulong cause) =>
        _planner.Refine(state, state.Deposit.Terrain, cells, Id(cause), state.Revision);
    private static (ulong, ulong)[] Intervals(TerrainMaterialState state, EntityId owner) => state.Inventory.Custody.Lots
        .Where(l => l.CustodianId == owner).OrderBy(l => l.Source.Start).Select(l => (l.Source.Start, l.Source.End)).ToArray();
    private static void AssertConserved(TerrainMaterialState state)
    {
        Assert.Equal(80UL, state.CoarseQuantity + state.DetailedQuantity + state.OtherQuantity);
        Assert.Equal(80UL, state.Inventory.Custody.Lots.Aggregate(0UL, (n,l) => checked(n+l.Quantity)));
    }

    private static TerrainMaterialState Create(EntityId? branch = null, ulong? fineCapacity = null,
        LotProtection protection = LotProtection.None, int side = 2, int depth = 2)
    {
        TerrainScope scope = new(new(0, 1), branch ?? new(0, 2), new(0, 3));
        PreparedWatershed basin = new WatershedPlanner().Prepare(new(scope, new(0, 4), new(-1, -1, 1, 1), new(0, 0), 0, new(0, 100)),
            TestContext.Current.CancellationToken);
        PreparedWatershedNetwork network = new WatershedNetworkPlanner().Prepare(new(0, 900), [basin],
            [WatershedOutletClosure.ToTerminal(basin.BasinId, 1)], TestContext.Current.CancellationToken);
        PreparedWatershedTerrain terrain = new WatershedTerrainMapper().Prepare(network, new(side, 1, 0),
            new(new(-side, -side, 1 - depth), Math.Min(side, 32), Math.Min(side, 32), Math.Min(depth + 1, 16)),
            TestContext.Current.CancellationToken);
        var item = new CountedItemDefinition(Id(100), ContentHash.Compute("terrain-rock"u8));
        var physical = new MaterialDefinition(new PackageId("openfantasy.terrain"), Id(101), "rock", new(250_000_000), new(100_000_000), new(10));
        ulong units = checked((ulong)side * (ulong)side * (ulong)depth * 10);
        ulong capacity = checked(units + 20);
        var deposit = new TerrainMaterialDeposit(terrain, new(basin.BasinId, 0), depth, new(item, physical), 10,
            new(Source, 10, 10 + units), Coarse, Fine);
        var custody = new CustodySnapshot(scope.WorldId, scope.BranchId, 0,
            [new(Coarse, capacity, 0), new(Fine, fineCapacity ?? capacity, 0), new(Away, capacity, 0)],
            [new(Id(106), Coarse, item, new(Source, 10, 10 + units), 0, protection)]);
        var inventory = new CountedAuthorityState(custody, capacity, [Actor], [new(Id(107), Id(108), Actor)],
            [new(Coarse, Actor, CustodyDepositRule.OwnerOnly), new(Fine, Actor, CustodyDepositRule.OwnerOnly), new(Away, Actor, CustodyDepositRule.OwnerOnly)],
            [new(Source, item, capacity)]);
        return TerrainMaterialState.Create(deposit, inventory);
    }

    private static CountedAuthorityState Transfer(CountedAuthorityState inventory, CountedSourceLot lot, EntityId destination, ulong quantity)
    {
        var command = new CommandEnvelope(Id(900), Id(107), Id(108), Actor, inventory.WorldId, inventory.BranchId,
            [new(RevisionTargetKind.Branch, inventory.BranchId, inventory.Custody.Revision),
             new(RevisionTargetKind.Entity, lot.LotId, lot.Revision),
             new(RevisionTargetKind.Entity, lot.CustodianId, inventory.Custody.GetStorage(lot.CustodianId).Revision),
             new(RevisionTargetKind.Entity, destination, inventory.Custody.GetStorage(destination).Revision)],
            new(lot.LotId, lot.CustodianId, destination, quantity));
        Assert.True(inventory.CanControl(new(Id(107), Id(108)), Actor));
        return CopyInventory(inventory, new CustodyTransferPlanner().Plan(inventory.Custody, command).ApplyTo(inventory.Custody));
    }
    private static CountedAuthorityState CopyInventory(CountedAuthorityState basis, CustodySnapshot custody) =>
        new(custody, basis.AdmittedUnitCapacity, basis.Actors, basis.Controls, basis.Policies, basis.Sources, basis.ReservationIds);
    private static EntityId Id(ulong value) => new(0x7465727261696e, value);
    private static readonly EntityId Actor = Id(102), Coarse = Id(103), Fine = Id(104), Source = Id(105), Away = Id(109);
    private static void Retain(TerrainMaterialState state)
    {
        string? root = Environment.GetEnvironmentVariable("OPENFANTASY_TERRAIN_MATERIAL_EVIDENCE");
        if (string.IsNullOrWhiteSpace(root)) return;
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "partial-transfer.bin"), TerrainMaterialCodecV1.Encode(state));
        File.WriteAllText(Path.Combine(root, "expected.json"), System.Text.Json.JsonSerializer.Serialize(new
        {
            ContentHash = state.ContentHash.Value, state.CoarseQuantity, state.DetailedQuantity, state.OtherQuantity
        }));
    }
}
