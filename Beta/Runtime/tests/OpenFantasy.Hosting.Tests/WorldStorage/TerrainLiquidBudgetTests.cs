using OpenFantasy.Core;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class TerrainLiquidBudgetTests
{
    [Theory]
    [InlineData(16_384UL, true)]
    [InlineData(16_385UL, false)]
    public void Fresh_admission_bounds_live_units_even_when_one_compact_portion_fits_the_vessel(ulong units, bool admitted)
    {
        var fixture = new TerrainLiquidBudgetFixture(units);
        Assert.Equal((ushort)5, fixture.Basis.ProfileVersion);
        Assert.Equal(units, Assert.Single(fixture.Cells.Portions).Quantity);
        Assert.Equal(units, Assert.Single(fixture.Reservoirs.Projections).ExistingUnits);
        Assert.True(units < fixture.Cells.UnitsPerCell);
        Assert.Equal(3, fixture.Cells.DomainCells.Count);
        Assert.Empty(fixture.Reservoirs.Capacity.Pending);
        Assert.Equal(1UL, fixture.Cells.VolumePerUnit.Value);
        // Both fixtures already have valid calibrated stock, topology and compact placements.
        // Only the full world's promise that *any* partition remains representable differs.
        if (!admitted)
        {
            var error = Assert.Throws<ArgumentException>(() => fixture.CreateFreshGenesis());
            Assert.Contains("provenance certificate", error.Message, StringComparison.Ordinal);
            return;
        }

        WorldSnapshot world = fixture.CreateFreshGenesis();
        Assert.Equal((ushort)21, world.ProfileVersion);
        Assert.Equal(fixture.Cells.ContentHash, world.LiquidCells!.ContentHash);
        Assert.Equal(fixture.Basis.Inventory.ContentHash, world.Inventory.ContentHash);
        Assert.Equal(0UL, world.Revision);
        Assert.Equal(0UL, world.AdvancingSteps);
        byte[] bytes = WorldSnapshotCodec.Encode(world);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));
        using var files = new StorageFiles();
        using (SqliteWorldAuthority.CreateNew(files.Path, world)) { }
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
    }

    [Theory]
    [InlineData(8_192UL, true)]
    [InlineData(8_193UL, false)]
    public void Complete_root_budget_sums_raw_and_earned_outputs_across_vessels_without_recounting_retired_inputs(
        ulong secondVesselUnits, bool admitted)
    {
        // Actual crafting makes revision one. This deliberately tests complete supplied-root
        // admission; it does not claim a fresh-world migration or a live liquid repacking action.
        var fixture = new TerrainLiquidBudgetFixture(8_192, secondVesselUnits, producedUnits: 4_096);
        Assert.Equal(1UL, fixture.Basis.Revision);
        Assert.Equal(1UL, fixture.Basis.Production!.Revision);
        Assert.Equal(4_096UL, Assert.Single(fixture.Basis.Production.Batches).Ingredients
            .Aggregate(0UL, (sum, value) => checked(sum + value.Quantity)));
        Assert.Equal(4_096UL, Assert.Single(fixture.Basis.Production.Lots).Quantity);
        Assert.Equal(fixture.ProducedLot, Assert.Single(fixture.Cells.Portions,
            portion => portion.Origin == LiquidStockOrigin.ProducedLot).OriginId);
        Assert.Equal(4_096UL + secondVesselUnits, fixture.Cells.Portions.Where(portion => portion.Origin == LiquidStockOrigin.RawSource)
            .Aggregate(0UL, (sum, portion) => checked(sum + portion.Quantity)));
        Assert.Equal(8_192UL + secondVesselUnits, Total(fixture.Cells));
        Assert.Equal(2, fixture.Reservoirs.Geometries.Count);
        Assert.All(fixture.Reservoirs.Projections, projection =>
            Assert.True(projection.ExistingUnits < LiquidCellState.MaximumPortions));
        Assert.Equal(3, fixture.Cells.Portions.Count);
        Assert.Throws<ArgumentException>(() => fixture.CreateFreshGenesis());

        if (!admitted)
        {
            var error = Assert.Throws<ArgumentException>(() => fixture.CreateSuppliedRoot());
            Assert.Contains("provenance certificate", error.Message, StringComparison.Ordinal);
            return;
        }

        WorldSnapshot world = fixture.CreateSuppliedRoot();
        Assert.Equal((ushort)21, world.ProfileVersion);
        Assert.Equal(16_384UL, Total(world.LiquidCells!));
        Assert.Equal(fixture.Basis.Inventory.ContentHash, world.Inventory.ContentHash);
        Assert.Equal(fixture.Basis.Production.ContentHash, world.Production!.ContentHash);
        byte[] bytes = WorldSnapshotCodec.Encode(world);
        WorldSnapshot restored = WorldSnapshotCodec.Decode(bytes);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(restored));
        Assert.Equal(fixture.Basis.Production.ContentHash, restored.Production!.ContentHash);
        Assert.Equal(16_384UL, Total(restored.LiquidCells!));
    }

    [Fact]
    public void Maximum_one_unit_fragments_flow_through_real_commits_and_exact_recovery()
    {
        var fixture = new TerrainLiquidBudgetFixture(16_384, fragmented: true);
        WorldSnapshot genesis = fixture.CreateFreshGenesis();
        LiquidCellState initial = genesis.LiquidCells!;
        Assert.Equal(16_384, initial.Portions.Count);
        Assert.All(initial.Portions, portion => Assert.Equal(1UL, portion.Quantity));
        Assert.Equal(8_192UL, initial.Quantities[TerrainLiquidBudgetFixture.Cell(10)]);
        Assert.Equal(8_192UL, initial.Quantities[TerrainLiquidBudgetFixture.Cell(12)]);
        Assert.False(initial.Quantities.ContainsKey(TerrainLiquidBudgetFixture.Cell(11)));
        byte[] genesisBytes = WorldSnapshotCodec.Encode(genesis);
        var history = new List<(ulong Revision, byte[] Bytes)> { (0, genesisBytes) };
        using var files = new StorageFiles();
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            WorldSnapshot world = authority.InspectForHost().State;
            for (int step = 1; step <= 3; step++)
            {
                world = authority.Commit(fixture.Actions.Batch(world, [], [])).Candidate;
                Assert.Equal((ulong)step, world.Revision);
                Assert.Equal((ulong)step, world.AdvancingSteps);
                Assert.Equal(genesis.Calendar.AddMilliseconds(step * 1_800L), world.Calendar);
                Assert.Equal(world.Calendar, world.LiquidCells!.Boundary);
                Assert.Equal((ulong)step, world.LiquidCells.Revision);
                Assert.Equal((ulong)step * 2, world.LiquidCells.Quantities[TerrainLiquidBudgetFixture.Cell(11)]);
                Assert.Equal(8_192UL - (ulong)step, world.LiquidCells.Quantities[TerrainLiquidBudgetFixture.Cell(10)]);
                Assert.Equal(8_192UL - (ulong)step, world.LiquidCells.Quantities[TerrainLiquidBudgetFixture.Cell(12)]);
                AssertConserved(world.LiquidCells);
                Assert.Equal(genesis.Inventory.ContentHash, world.Inventory.ContentHash);
                Assert.Equal(genesis.Production!.ContentHash, world.Production!.ContentHash);
                Assert.Equal(genesis.Excavation!.ContentHash, world.Excavation!.ContentHash);
                Assert.Equal(initial.Topology!.ContentHash, world.LiquidCells.Topology!.ContentHash);
                history.Add((world.Revision, WorldSnapshotCodec.Encode(world)));
            }
            Assert.Equal(3UL, authority.InspectForHost().State.Revision);
        }

        byte[] databaseBeforeRecovery = File.ReadAllBytes(files.Path);
        using (SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            WorldSnapshot recovered = reopened.InspectForHost().State;
            Assert.Equal(history[^1].Bytes, WorldSnapshotCodec.Encode(recovered));
            AssertConserved(recovered.LiquidCells!);
            foreach (var recorded in history)
            {
                Assert.Equal(recorded.Bytes, WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost(recorded.Revision).State));
            }
        }
        Assert.Equal(databaseBeforeRecovery, File.ReadAllBytes(files.Path));
    }

    private static ulong Total(LiquidCellState cells) => cells.Portions.Aggregate(0UL,
        (sum, portion) => checked(sum + portion.Quantity));

    private static void AssertConserved(LiquidCellState cells)
    {
        Assert.Equal(16_384UL, Total(cells));
        Assert.InRange(cells.Portions.Count, 1, 16_384);
        Assert.All(cells.Portions, portion =>
        {
            Assert.Equal(LiquidStockOrigin.RawSource, portion.Origin);
            Assert.Equal(TerrainLiquidBudgetFixture.Source, portion.OriginId);
            Assert.True(portion.Quantity > 0);
        });
        Assert.Equal(Enumerable.Range(0, 16_384).Select(value => (ulong)value),
            cells.Portions.OrderBy(portion => portion.Start).SelectMany(portion =>
                Enumerable.Range(checked((int)portion.Start), checked((int)portion.Quantity)).Select(value => (ulong)value)));
    }
}
