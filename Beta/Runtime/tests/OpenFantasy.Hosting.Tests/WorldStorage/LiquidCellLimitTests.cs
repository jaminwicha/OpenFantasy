using OpenFantasy.Core;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class LiquidCellLimitTests
{
    [Fact]
    public void Whole_step_fragment_overflow_leaves_exact_durable_state_and_all_ledgers_unchanged()
    {
        var fixture = new LiquidCellLimitFixture();
        WorldSnapshot genesis = fixture.Genesis;
        LiquidCellState cells = genesis.LiquidCells!;
        Assert.Equal((ushort)20, genesis.ProfileVersion);
        Assert.Equal(16_384, cells.Portions.Count);
        Assert.Equal(1UL, cells.VolumePerUnit.Value);
        Assert.Equal(16_384UL, cells.Quantities[LiquidCellLimitFixture.Cell(0)]);
        Assert.Equal(16_384UL, cells.Quantities[LiquidCellLimitFixture.Cell(2)]);
        Assert.False(cells.Quantities.ContainsKey(LiquidCellLimitFixture.Cell(1)));
        Assert.Equal(LiquidCellLimitFixture.Units, cells.Portions.Aggregate(0UL, (sum, portion) => checked(sum + portion.Quantity)));
        Assert.Equal(Enumerable.Range(0, LiquidCellState.MaximumPortions).Select(index => ((ulong)index * 2, (ulong)index * 2 + 2)),
            cells.Portions.OrderBy(portion => portion.Start).Select(portion => (portion.Start, portion.End)));

        // Both donors send one unit into the empty middle cell. Their first two-unit intervals
        // split into [0,1)/[1,2) and [2,3)/[3,4); neither incoming interval can merge with the other.
        // Thus all original fragments remain represented and two additional fragments are required.
        LiquidCellPortion[] required = cells.Portions.Select(portion => portion.Start is 0 or 2
            ? new LiquidCellPortion(portion.Cell, portion.Origin, portion.OriginId, portion.Start + 1, portion.End)
            : portion).Concat([
                new(LiquidCellLimitFixture.Cell(1), LiquidStockOrigin.RawSource, LiquidCellLimitFixture.Source, 0, 1),
                new(LiquidCellLimitFixture.Cell(1), LiquidStockOrigin.RawSource, LiquidCellLimitFixture.Source, 2, 3)
            ]).ToArray();
        Assert.Equal(16_386, required.Length);
        Assert.Equal(LiquidCellLimitFixture.Units, required.Aggregate(0UL, (sum, portion) => checked(sum + portion.Quantity)));
        Assert.All(required.GroupBy(portion => portion.Cell), group =>
        {
            LiquidCellPortion[] ordered = group.OrderBy(portion => portion.Start).ToArray();
            Assert.All(ordered.Zip(ordered.Skip(1)), pair => Assert.True(pair.First.End < pair.Second.Start));
        });
        Assert.Throws<ArgumentException>(() => new LiquidCellPlanner().PlanAdvance(cells, genesis.Calendar.AddMilliseconds(1800)));
        byte[] canonical = WorldSnapshotCodec.Encode(genesis);
        Assert.Equal(canonical, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(canonical)));

        EntityId player = genesis.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player).ActorId;
        var delivery = ConstructionUpgradeFixture.Delivery(genesis, player);
        // This ordinary unavailable command would have a refusal receipt in a successful step.
        // The stage failure must discard that prepared receipt and its identity reservations too.
        var command = new WorldActionCommand(fixture.Actions.NextIdentity(), delivery.PrincipalId, delivery.SessionId,
            player, genesis.WorldId, genesis.BranchId, genesis.Space.GetActor(player).Revision,
            new CloseDialogueIntent(LiquidCellLimitFixture.Id(90)));
        WorldStepBatch batch = fixture.Actions.Batch(genesis, [command], []);
        using var files = new StorageFiles();
        using (SqliteWorldAuthority.CreateNew(files.Path, genesis)) { }
        byte[] databaseBefore = File.ReadAllBytes(files.Path);

        using (SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            WorldInspection before = authority.InspectForHost();
            Assert.Null(authority.FindReceipt(command, delivery));
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(batch));
            WorldInspection after = authority.InspectForHost();
            Assert.Equal(canonical, WorldSnapshotCodec.Encode(after.State));
            Assert.Equal(before.Journal, after.Journal);
            Assert.Equal(genesis.ContentHash, after.State.ContentHash);
            Assert.Equal(genesis.Calendar, after.State.Calendar);
            Assert.Equal(genesis.AdvancingSteps, after.State.AdvancingSteps);
            Assert.Equal(genesis.Inventory.ContentHash, after.State.Inventory.ContentHash);
            Assert.Equal(genesis.Production!.ContentHash, after.State.Production!.ContentHash);
            Assert.Equal(genesis.Gathering!.ContentHash, after.State.Gathering!.ContentHash);
            Assert.Equal(cells.ContentHash, after.State.LiquidCells!.ContentHash);
            Assert.Null(authority.FindReceipt(command, delivery));
            Assert.False(authority.IsIdentityOccupiedForHost(batch.CauseId));
            Assert.False(authority.IsIdentityOccupiedForHost(command.CommandId));
        }
        Assert.Equal(databaseBefore, File.ReadAllBytes(files.Path));
        using (WorldDatabase database = WorldDatabase.Open(files.Path, create: false))
        {
            Assert.Equal(3, database.SchemaVersion);
            Assert.Equal(canonical, database.ReadGenesis());
            var head = database.ReadHead();
            Assert.Equal(0UL, head.Sequence);
            Assert.Equal(0UL, head.AdvancingSteps);
            Assert.Equal(canonical, head.State);
            Assert.Empty(database.ReadJournal());
            Assert.Empty(database.ReadCommandOutcomes());
            Assert.Empty(database.ReadIdentities());
            Assert.Equal(canonical, Assert.Single(database.ReadCheckpoints()).State);
        }
        using (SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(canonical, WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
            RecordedWorldState recorded = reopened.ReadRecordedStateForHost(0);
            Assert.Equal(0UL, recorded.SourceHeadRevision);
            Assert.Equal(genesis.ContentHash, recorded.SourceHeadHash);
            Assert.Equal(canonical, WorldSnapshotCodec.Encode(recorded.State));
            Assert.Null(reopened.FindReceipt(command, delivery));
        }
        Assert.Equal(databaseBefore, File.ReadAllBytes(files.Path));
    }
}
