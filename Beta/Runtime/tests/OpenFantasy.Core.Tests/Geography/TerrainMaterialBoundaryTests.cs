using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Geography;

public sealed partial class TerrainMaterialTests
{
    [Fact]
    public void FullDemotionCoalescesAdjacentStockButDoesNotReuseRetiredLotIds()
    {
        TerrainMaterialState state = Create();
        CellCoordinate[] cells = [new(-2, -2, -1), new(-1, -2, -1), new(-2, -1, -1)];
        state = Refine(state, cells, 1).ApplyTo(state);
        EntityId[] retired = state.Inventory.Custody.Lots.Select(l => l.LotId).ToArray();
        state = _planner.Demote(state, cells.Reverse(), Id(2), state.Revision).ApplyTo(state);
        CountedSourceLot merged = Assert.Single(state.Inventory.Custody.Lots);
        Assert.Equal(new SourceInterval(Source, 10, 90), merged.Source);
        Assert.Equal(Coarse, merged.CustodianId);
        Assert.DoesNotContain(merged.LotId, retired);
        AssertConserved(state);
    }

    [Fact]
    public void SourceUnitsOutsideTheAllocatedSlabArePreservedThroughInteriorSplitting()
    {
        TerrainMaterialState basis = Create();
        var lot = new CountedSourceLot(Id(106), Coarse, basis.Deposit.Material.Item, new(Source, 0, 100), 0);
        var inventory = CopyInventory(basis.Inventory, new(basis.Inventory.WorldId, basis.Inventory.BranchId, 0,
            basis.Inventory.Custody.Storages, [lot]));
        TerrainMaterialState state = TerrainMaterialState.Create(basis.Deposit, inventory);
        state = Refine(state, [new(-2, -2, -1)], 1).ApplyTo(state);
        Assert.Equal(new[] { (0UL, 10UL), (20UL, 100UL) }, Intervals(state, Coarse));
        state = _planner.Demote(state, state.ActiveCells, Id(2), state.Revision).ApplyTo(state);
        Assert.Equal(80UL, state.CoarseQuantity);
        Assert.Equal(100UL, state.Inventory.Custody.GetStoredQuantity(Coarse));
    }

    [Fact]
    public void DetailProtectionAndCoarseReceivingCapacityRejectAtomically()
    {
        TerrainMaterialState state = Create();
        CellCoordinate cell = new(-2, -2, -1);
        state = Refine(state, [cell], 1).ApplyTo(state);
        CountedSourceLot fine = Assert.Single(state.Inventory.Custody.Lots, l => l.CustodianId == Fine);
        var protectedLot = new CountedSourceLot(fine.LotId, Fine, fine.Definition, fine.Source, 2, LotProtection.Heirloom);
        var protectedInventory = CopyInventory(state.Inventory, new(state.Inventory.WorldId, state.Inventory.BranchId, 2,
            state.Inventory.Custody.Storages, state.Inventory.Custody.Lots.Select(l => l.LotId == fine.LotId ? protectedLot : l)));
        TerrainMaterialState protectedState = state.ReconcileInventory(protectedInventory);
        byte[] before = TerrainMaterialCodecV1.Encode(protectedState);
        Assert.Throws<InvalidOperationException>(() => _planner.Demote(protectedState, [cell], Id(2), protectedState.Revision));
        Assert.Equal(before, TerrainMaterialCodecV1.Encode(protectedState));

        CountedSourceLot[] lots = [.. state.Inventory.Custody.Lots,
            new(Id(250), Coarse, fine.Definition, new(Source, 0, 10), 2),
            new(Id(251), Coarse, fine.Definition, new(Source, 90, 100), 2)];
        var full = CopyInventory(state.Inventory, new(state.Inventory.WorldId, state.Inventory.BranchId, 2,
            state.Inventory.Custody.Storages.Select(s => s.CustodianId == Coarse ? s with { UnitCapacity = 90 } : s), lots));
        TerrainMaterialState filled = state.ReconcileInventory(full);
        Assert.Throws<ArgumentException>(() => _planner.Demote(filled, [cell], Id(2), filled.Revision));
        Assert.Equal(10UL, filled.DetailedQuantity);
    }

    [Fact]
    public void ReservationBindingsCannotBeDiscardedDuringRefinement()
    {
        TerrainMaterialState basis = Create();
        CountedSourceLot lot = Assert.Single(basis.Inventory.Custody.Lots);
        var reserved = new CountedSourceLot(lot.LotId, lot.CustodianId, lot.Definition, lot.Source, 0, reservedBy: Id(250));
        var custody = new CustodySnapshot(basis.Inventory.WorldId, basis.Inventory.BranchId, 0, basis.Inventory.Custody.Storages, [reserved]);
        var inventory = new CountedAuthorityState(custody, basis.Inventory.AdmittedUnitCapacity, basis.Inventory.Actors,
            basis.Inventory.Controls, basis.Inventory.Policies, basis.Inventory.Sources, [Id(250)]);
        TerrainMaterialState state = TerrainMaterialState.Create(basis.Deposit, inventory);
        Assert.Throws<InvalidOperationException>(() => Refine(state, [new(-2, -2, -1)], 1));
    }

    [Fact]
    public void ReconciliationRejectsSourceHolesScopeAdmissionAndOwnershipChanges()
    {
        TerrainMaterialState state = Create();
        Assert.Same(state, state.ReconcileInventory(state.Inventory));
        var empty = CopyInventory(state.Inventory, new(state.Inventory.WorldId, state.Inventory.BranchId, 1, state.Inventory.Custody.Storages, []));
        Assert.Throws<ArgumentException>(() => state.ReconcileInventory(empty));
        Assert.Throws<ArgumentException>(() => state.ReconcileInventory(Create(branch: Id(255)).Inventory));
        var changedAdmission = new CountedAuthorityState(new(state.Inventory.WorldId, state.Inventory.BranchId, 1,
                state.Inventory.Custody.Storages, state.Inventory.Custody.Lots), state.Inventory.AdmittedUnitCapacity + 1,
            state.Inventory.Actors, state.Inventory.Controls, state.Inventory.Policies, state.Inventory.Sources);
        Assert.Throws<ArgumentException>(() => state.ReconcileInventory(changedAdmission));
        var foreignOwner = new CountedAuthorityState(state.Inventory.Custody, state.Inventory.AdmittedUnitCapacity,
            [Actor, Id(256)], state.Inventory.Controls, state.Inventory.Policies.Select(p => p.CustodianId == Fine
                ? new CustodianAccessPolicy(Fine, Id(256), CustodyDepositRule.OwnerOnly) : p), state.Inventory.Sources);
        Assert.Throws<ArgumentException>(() => TerrainMaterialState.Create(state.Deposit, foreignOwner));
        Assert.Throws<InvalidOperationException>(() => Refine(state, [new(-2, -2, -1)], 1).ApplyTo(Create(fineCapacity: 101)));
    }

    [Fact]
    public void MacroAndSpawnedIdentityCollisionsAndRevisionOverflowReject()
    {
        TerrainMaterialState state = Create();
        CellCoordinate[] cell = [new(-2, -2, -1)];
        foreach (EntityId cause in new[] { state.Deposit.Terrain.Scope.RealmId, state.Deposit.Terrain.Network.ProvinceId,
            state.Deposit.Material.Physical.DefinitionId })
            Assert.Throws<InvalidOperationException>(() => _planner.Refine(state, state.Deposit.Terrain, cell, cause, state.Revision));
        EntityId collision = EntityId.DeriveSpawned(Id(1), 1);
        Assert.Throws<InvalidOperationException>(() => _planner.Refine(state, state.Deposit.Terrain, cell, Id(1), state.Revision, id => id == collision));
        var inventory = CopyInventory(state.Inventory, new(state.Inventory.WorldId, state.Inventory.BranchId, ulong.MaxValue,
            state.Inventory.Custody.Storages, state.Inventory.Custody.Lots));
        TerrainMaterialState exhausted = TerrainMaterialState.Create(state.Deposit, inventory);
        Assert.Throws<OverflowException>(() => Refine(exhausted, cell, 1));
    }

    [Fact]
    public void BatchAndExistingLotCapsApplyWithoutTruncationOrIncreasingBudgets()
    {
        TerrainMaterialState state = Create(side: 32, depth: 8);
        CellCoordinate[] cells = Enumerable.Range(0, 4096).Select(i => state.Deposit.CellForUnit((ulong)i * 10 + 10)).ToArray();
        Assert.Throws<ArgumentException>(() => Refine(state, cells.Take(257), 1));
        for (int i = 0; i < 4095; i += 256)
            state = Refine(state, cells.Skip(i).Take(Math.Min(256, 4095 - i)), (ulong)(1000 + i)).ApplyTo(state);
        Assert.Equal(CustodySnapshot.MaximumLots, state.Inventory.Custody.Lots.Count);
        byte[] before = TerrainMaterialCodecV1.Encode(state);
        Assert.Throws<ArgumentException>(() => Refine(state, [cells[4095]], 9000));
        Assert.Equal(before, TerrainMaterialCodecV1.Encode(state));
        Assert.Equal(before, TerrainMaterialCodecV1.Encode(TerrainMaterialCodecV1.Decode(before, TestContext.Current.CancellationToken)));
        RetainNamed(state, "lot-cap");
    }

    [Fact]
    public void CompleteSlabCanHave4096ActiveCellsAndReturnToOneCoarseLot()
    {
        TerrainMaterialState state = Create(side: 16, depth: 16);
        CellCoordinate[] cells = Enumerable.Range(0, 4096).Select(i => state.Deposit.CellForUnit((ulong)i * 10 + 10)).ToArray();
        for (int i = 0; i < cells.Length; i += 256)
            state = Refine(state, cells.Skip(i).Take(256), (ulong)(1000 + i)).ApplyTo(state);
        Assert.Equal(4096, state.ActiveCells.Count);
        Assert.Equal(0UL, state.CoarseQuantity);
        RetainNamed(state, "active-cap");
        for (int i = cells.Length - 256; i >= 0; i -= 256)
            state = _planner.Demote(state, cells.Skip(i).Take(256), Id((ulong)(10000 + i)), state.Revision).ApplyTo(state);
        Assert.Empty(state.ActiveCells);
        Assert.Equal(state.Deposit.Source, Assert.Single(state.Inventory.Custody.Lots).Source);
    }

    [Fact]
    public void DecodeCancellationAndImpossiblePhysicalTotalsReject()
    {
        TerrainMaterialState state = Create();
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.Throws<OperationCanceledException>(() => TerrainMaterialCodecV1.Decode(TerrainMaterialCodecV1.Encode(state), cancel.Token));
        var physical = state.Deposit.Material.Physical;
        var excessive = new OpenFantasy.Core.Production.MaterialDefinition(physical.Package, physical.DefinitionId, physical.Key,
            new(ulong.MaxValue), physical.VolumePerUnit, physical.Hardness);
        Assert.Throws<OverflowException>(() => new TerrainMaterialDeposit(state.Deposit.Terrain, state.Deposit.MacroColumn, 2,
            new(state.Deposit.Material.Item, excessive), 10, state.Deposit.Source, Coarse, Fine));
    }

    [Fact]
    public void FragmentedFundingKeepsAnInteriorTransferHoleAndUnrelatedProtectedReservation()
    {
        TerrainMaterialState basis = Create();
        var item = basis.Deposit.Material.Item;
        var unrelated = new CountedSourceLot(Id(252), Coarse, item, new(Source, 0, 10), 0, LotProtection.Favorite, Id(253));
        var custody = new CustodySnapshot(basis.Inventory.WorldId, basis.Inventory.BranchId, 0,
            basis.Inventory.Custody.Storages, [unrelated, new(Id(250), Coarse, item, new(Source, 10, 15), 0),
                new(Id(251), Coarse, item, new(Source, 15, 90), 0)]);
        var inventory = new CountedAuthorityState(custody, basis.Inventory.AdmittedUnitCapacity,
            basis.Inventory.Actors, basis.Inventory.Controls, basis.Inventory.Policies, basis.Inventory.Sources, [Id(253)]);
        TerrainMaterialState state = TerrainMaterialState.Create(basis.Deposit, inventory);
        CellCoordinate cell = new(-2, -2, -1);
        state = Refine(state, [cell], 1).ApplyTo(state);
        CountedSourceLot second = Assert.Single(state.Inventory.Custody.Lots, l => l.CustodianId == Fine && l.Source.Start == 15);
        state = state.ReconcileInventory(Transfer(state.Inventory, second, Away, 2));
        state = _planner.Demote(state, [cell], Id(2), state.Revision).ApplyTo(state);
        Assert.Equal(new[] { (0UL, 10UL), (10UL, 15UL), (17UL, 90UL) }, Intervals(state, Coarse));
        state = Refine(state, [cell], 3).ApplyTo(state);
        Assert.Equal(new[] { (10UL, 15UL), (17UL, 20UL) }, Intervals(state, Fine));
        Assert.Equal(unrelated, state.Inventory.Custody.GetLot(unrelated.LotId));
        Assert.Equal(2UL, state.OtherQuantity);
        Assert.Equal(80UL, state.CoarseQuantity + state.DetailedQuantity + state.OtherQuantity);
    }

    [Fact]
    public void AGeneratedLotCannotAliasAnAlreadyAdmittedActor()
    {
        TerrainMaterialState basis = Create();
        EntityId actorCollision = EntityId.DeriveSpawned(Id(1), 0);
        var inventory = new CountedAuthorityState(basis.Inventory.Custody, basis.Inventory.AdmittedUnitCapacity,
            basis.Inventory.Actors.Append(actorCollision), basis.Inventory.Controls, basis.Inventory.Policies, basis.Inventory.Sources);
        TerrainMaterialState state = TerrainMaterialState.Create(basis.Deposit, inventory);
        Assert.Throws<InvalidOperationException>(() => Refine(state, [new(-2, -2, -1)], 1));
    }

    [Fact]
    public void ExtremeSingleCellCoordinatesAreValidButExtendingBelowInt64IsNot()
    {
        TerrainMaterialState basis = Create();
        PreparedWatershed basin = new WatershedPlanner().Prepare(new(basis.Deposit.Terrain.Scope, new(0, 4),
            new(long.MaxValue, long.MinValue, 1, 1), new(long.MinValue, 0), 0, new(0, 100)), TestContext.Current.CancellationToken);
        PreparedWatershedNetwork network = new WatershedNetworkPlanner().Prepare(new(0, 900), [basin],
            [WatershedOutletClosure.ToTerminal(basin.BasinId, 1)], TestContext.Current.CancellationToken);
        var coordinate = new CellCoordinate(long.MaxValue, long.MinValue, long.MinValue);
        PreparedWatershedTerrain terrain = new WatershedTerrainMapper().Prepare(network, new(1, 1, 0),
            new(coordinate, 1, 1, 1), TestContext.Current.CancellationToken);
        var deposit = new TerrainMaterialDeposit(terrain, new(basin.BasinId, 0), 1,
            basis.Deposit.Material, 10, new(Source, 10, 20), Coarse, Fine);
        Assert.Equal(coordinate, deposit.Minimum);
        Assert.Equal(coordinate, deposit.Maximum);
        Assert.Equal(new SourceInterval(Source, 10, 20), deposit.IntervalFor(coordinate));
        Assert.Throws<OverflowException>(() => new TerrainMaterialDeposit(terrain, new(basin.BasinId, 0), 2,
            basis.Deposit.Material, 10, new(Source, 10, 30), Coarse, Fine));
    }

    [Fact]
    public void EntityRevisionsCannotRunAheadOfTheCompleteMaterialRoot()
    {
        TerrainMaterialState basis = Create();
        CountedSourceLot lot = Assert.Single(basis.Inventory.Custody.Lots);
        var aheadLot = new CountedSourceLot(lot.LotId, lot.CustodianId, lot.Definition, lot.Source, 1);
        var custody = new CustodySnapshot(basis.Inventory.WorldId, basis.Inventory.BranchId, 0, basis.Inventory.Custody.Storages, [aheadLot]);
        Assert.Throws<ArgumentException>(() => TerrainMaterialState.Create(basis.Deposit, CopyInventory(basis.Inventory, custody)));
        custody = new(basis.Inventory.WorldId, basis.Inventory.BranchId, 0,
            basis.Inventory.Custody.Storages.Select(s => s.CustodianId == Coarse ? s with { Revision = 1 } : s), [lot]);
        Assert.Throws<ArgumentException>(() => TerrainMaterialState.Create(basis.Deposit, CopyInventory(basis.Inventory, custody)));
    }

    private static void RetainNamed(TerrainMaterialState state, string name)
    {
        string? root = Environment.GetEnvironmentVariable("OPENFANTASY_TERRAIN_MATERIAL_EVIDENCE");
        if (string.IsNullOrWhiteSpace(root)) return;
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, name + ".bin"), TerrainMaterialCodecV1.Encode(state));
    }
}
