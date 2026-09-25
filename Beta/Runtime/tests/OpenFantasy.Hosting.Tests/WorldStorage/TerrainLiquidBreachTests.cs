using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class TerrainLiquidBreachTests
{
    [Theory]
    [InlineData("aldermere-breach-v1")]
    [InlineData("breach-second-seed")]
    [InlineData("breach-03")]
    public void Fresh_content_retains_a_real_divider_and_canonical_separate_world(string seed)
    {
        var world = BreachFarmAdmission.CreateEmbedded(seed, "Traveler");
        Assert.Equal((ushort)21, world.ProfileVersion);
        var site = Assert.Single(world.Excavation!.Sites);
        Assert.Equal(BreachFarmLayout.DividerCell, site.Cell);
        Assert.Equal(ExcavationReachMode.RaisedBankTop, site.ReachMode);
        Assert.True(Cell(world, site.Cell).IsSolid);
        Assert.DoesNotContain(world.LiquidCells!.DomainCells, cell => cell.Coordinate == site.Cell);
        Assert.Contains(world.LiquidCells.Topology!.NominalCells, cell => cell.Coordinate == site.Cell);
        Assert.Equal(world.LiquidCells.DomainCells.Count + 1, world.LiquidCells.Topology.NominalCells.Count);
        Assert.Equal((ulong)world.LiquidCells.DomainCells.Count * 1_000_000_000, world.LiquidCells.Topology.OpenCapacity.Value);
        Assert.Equal(0UL, site.CompletedWork);
        Assert.DoesNotContain(world.Inventory.Custody.Lots, lot => lot.Source.SourceId == site.CountedSourceId);
        AssertConserved(world);
        byte[] bytes = WorldSnapshotCodec.Encode(world);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(BreachFarmAdmission.CreateEmbedded(seed, "Traveler")));
        Assert.Throws<ArgumentException>(() => WorldSnapshotCodecV20.Encode(world));
        Assert.Throws<ArgumentException>(() => LiquidCellStateCodecV1.Encode(world.LiquidCells));
        Assert.Throws<ArgumentException>(() => ExcavationCodecV1.Encode(world.Excavation));
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(bytes[..^1]));
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode([.. bytes, 0]));
        Assert.Empty(world.Quests!.Participations);
        Assert.Empty(world.Production!.Batches);
        Assert.False(Assert.Single(world.Construction!.Projects).IsCompleted);
    }

    [Fact]
    public void Earned_partial_work_restarts_then_breaches_with_one_ore_claim_and_neighboring_water_arrivals()
    {
        var actions = new ConstructionUpgradeFixture();
        WorldSnapshot genesis = BreachFarmAdmission.CreateEmbedded("aldermere-breach-v1", "Traveler");
        WorldSnapshot world = genesis;
        var states = new List<WorldSnapshot> { genesis };
        var batches = new List<WorldStepBatch>();
        var receipts = new List<(WorldActionCommand Command, WorldActionReceipt Receipt)>();
        using var files = new StorageFiles();
        SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        string? output = Environment.GetEnvironmentVariable("OPENFANTASY_TERRAIN_BREACH_EVIDENCE");
        if (!string.IsNullOrWhiteSpace(output)) Directory.CreateDirectory(output);
        WorldSnapshot partial = genesis, beforeBreach = genesis, breached = genesis;
        WorldSnapshot Commit(WorldStepBatch batch)
        {
            byte[] bytes = WorldStepCodec.Encode(batch);
            Assert.Equal(bytes, WorldStepCodec.Encode(WorldStepCodec.Decode(bytes)));
            WorldStepDecision result = authority.Commit(WorldStepCodec.Decode(bytes));
            Assert.All(result.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
            foreach (var receipt in result.Receipts)
                receipts.Add((batch.Actions.Single(command => command.CommandId == receipt.CommandId), receipt));
            world = result.Candidate;
            AssertConserved(world);
            states.Add(world);
            batches.Add(batch);
            return world;
        }
        try
        {
            EntityId player = Player(world);
            // Ordinary selected movement follows the dry north bank; no live teleport arranges reach.
            SubunitPosition start = world.Space.GetActor(player).Feet;
            foreach (var target in new[] { new SubunitPosition(start.X, BreachFarmLayout.MiningStance.Y, start.Z),
                BreachFarmLayout.MiningStance })
            {
                for (int step = 0; world.Space.GetActor(player).Feet != target; step++)
                {
                    Assert.True(step < 256, "The original farm start must reach the mining bank by ordinary movement.");
                    var feet = world.Space.GetActor(player).Feet;
                    long dx = Math.Clamp(target.X - feet.X, -32, 32);
                    long dy = dx == 0 ? Math.Clamp(target.Y - feet.Y, -32, 32) : 0;
                    Commit(actions.Batch(world, [], [new(ConstructionUpgradeFixture.Delivery(world, player), player, dx, dy)]));
                    Assert.Equal(feet.Offset(dx, dy, 0), world.Space.GetActor(player).Feet);
                }
            }
            Assert.Equal(genesis.LiquidCells!.Portions, world.LiquidCells!.Portions);
            for (ulong work = 1; work <= 3; work++)
            {
                for (int step = 0; world.Gathering!.GetActorWork(player).Effort < GatherRulesV1.EffortPerSwing; step++)
                {
                    Assert.True(step < 40);
                    Commit(actions.Batch(world, [], []));
                }
                WorldActionCommand command = Mine(actions, world);
                WorldStepBatch workBatch = actions.Batch(world, [command], []);
                if (work == 3)
                {
                    beforeBreach = world;
                    authority.Dispose();
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        File.Copy(files.Path, Path.Combine(output, "before-breach.sqlite"), true);
                        File.WriteAllBytes(Path.Combine(output, "breach-step.bin"), WorldStepCodec.Encode(workBatch));
                    }
                    authority = SqliteWorldAuthority.OpenExisting(files.Path);
                }
                Commit(workBatch);
                var site = Assert.Single(world.Excavation!.Sites);
                Assert.Equal(work, site.CompletedWork);
                Assert.Equal(work < 3, Cell(world, site.Cell).IsSolid);
                Assert.Equal(work == 3 ? site.Quantity : 0UL, world.Inventory.Custody.Lots
                    .Where(lot => lot.Source.SourceId == site.CountedSourceId).Aggregate(0UL, (sum, lot) => sum + lot.Quantity));
                if (work == 1)
                {
                    partial = world;
                    authority.Dispose();
                    byte[] closed = File.ReadAllBytes(files.Path);
                    authority = SqliteWorldAuthority.OpenExisting(files.Path);
                    Assert.Equal(WorldSnapshotCodec.Encode(partial), WorldSnapshotCodec.Encode(authority.InspectForHost().State));
                    authority.Dispose();
                    Assert.Equal(closed, File.ReadAllBytes(files.Path));
                    authority = SqliteWorldAuthority.OpenExisting(files.Path);
                }
            }
            breached = world;
            Assert.NotEqual(beforeBreach.LiquidCells!.Topology!.ContentHash, breached.LiquidCells!.Topology!.ContentHash);
            Assert.Equal(beforeBreach.LiquidCells.Topology.OpenCells.Count + 1, breached.LiquidCells.Topology.OpenCells.Count);
            Assert.Equal(4UL, Quantity(breached, new(116, 329, 0)));
            Assert.Equal(0UL, Quantity(breached, new(117, 329, 0)));
            Assert.Equal(0UL, Quantity(breached, new(117, 329, -1)));
            Commit(actions.Batch(world, [], []));
            Assert.Equal(2UL, Quantity(world, new(117, 329, 0)));
            Assert.Equal(0UL, Quantity(world, new(117, 329, -1)));
            Commit(actions.Batch(world, [], []));
            Assert.Equal(2UL, Quantity(world, new(117, 329, -1)));
            for (int step = 0; step < 8; step++) Commit(actions.Batch(world, [], []));
            Assert.True(Quantity(world, new(118, 329, -1)) > 0);
            Assert.Equal(beforeBreach.Space.GetActor(player).Feet, world.Space.GetActor(player).Feet);
            foreach (var pair in receipts)
            {
                Assert.Equal(WorldReceiptCodecV1.Encode(pair.Receipt), WorldReceiptCodecV1.Encode(
                    authority.FindReceipt(pair.Command, ConstructionUpgradeFixture.Delivery(world, player))!));
            }
        }
        finally { authority.Dispose(); }
        byte[] database = File.ReadAllBytes(files.Path);
        using (var reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(world), WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
            foreach (var expected in new[] { breached, genesis, partial, beforeBreach, world })
            {
                var recorded = reopened.ReadRecordedStateForHost(expected.Revision);
                Assert.Equal(world.ContentHash, recorded.SourceHeadHash);
                Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(recorded.State));
            }
            var original = receipts[0].Command;
            Assert.Throws<AuthorityDispatchException>(() => reopened.Commit(actions.Batch(world, [original], [])));
            Assert.Equal(world.ContentHash, reopened.InspectForHost().State.ContentHash);
        }
        Assert.Equal(database, File.ReadAllBytes(files.Path));
        if (!string.IsNullOrWhiteSpace(output))
        {
            File.Copy(files.Path, Path.Combine(output, "world.sqlite"), true);
            foreach (var expected in new[] { genesis, partial, beforeBreach, breached, world })
                File.WriteAllBytes(Path.Combine(output, $"state-{expected.Revision}.bin"), WorldSnapshotCodec.Encode(expected));
            File.WriteAllText(Path.Combine(output, "expected.json"), JsonSerializer.Serialize(new
            {
                ProfileVersion = 21, PartialRevision = partial.Revision, BeforeBreachRevision = beforeBreach.Revision,
                BreachRevision = breached.Revision, FinalRevision = world.Revision,
                States = states.Select(value => new { value.Revision, Hash = value.ContentHash.Value,
                    Topology = value.LiquidCells!.Topology!.ContentHash.Value,
                    Work = value.Excavation!.Sites.Single().CompletedWork,
                    Cells = value.LiquidCells.DomainCells.Select(cell => new { cell.Coordinate.X, cell.Coordinate.Y, cell.Coordinate.Z,
                        Units = value.LiquidCells.Quantities.GetValueOrDefault(cell) }) })
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    [Fact]
    public async Task Local_session_queues_the_admitted_action_and_preserves_one_durable_refusal_on_retry()
    {
        var actions = new ConstructionUpgradeFixture();
        WorldSnapshot genesis = BreachFarmAdmission.CreateEmbedded("breach-session", "Traveler");
        using var files = new StorageFiles();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var time = new ManualSessionTimeProvider();
        EntityId player = Player(genesis);
        var delivery = ConstructionUpgradeFixture.Delivery(genesis, player);
        await using var session = new LocalWorldSession(authority, player, delivery, time);
        WorldActionCommand command = Mine(actions, genesis);
        session.SetReady(true);
        Task<WorldActionReceipt> pending = session.SubmitAction(command);
        Assert.False(pending.IsCompleted);
        await SessionFixture.Tick(time);
        WorldActionReceipt refusal = await pending;
        Assert.Equal(WorldActionRejection.Unavailable, refusal.Rejection);
        Assert.Equal(WorldReceiptCodecV1.Encode(refusal), WorldReceiptCodecV1.Encode(await session.SubmitAction(command)));
        Assert.Equal(1UL, authority.InspectForHost().Journal.CommandOutcomeCount);
        session.SetReady(false);
        var paused = session.ReadView();
        var pausedHash = authority.InspectForHost().State.ContentHash;
        await SessionFixture.Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(paused.Calendar, session.ReadView().Calendar);
        Assert.Equal(pausedHash, authority.InspectForHost().State.ContentHash);
        await session.StopAsync();
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(WorldReceiptCodecV1.Encode(refusal), WorldReceiptCodecV1.Encode(reopened.FindReceipt(command, delivery)!));
        Assert.Equal(0UL, reopened.InspectForHost().State.Excavation!.Sites.Single().CompletedWork);
    }

    private static WorldActionCommand Mine(ConstructionUpgradeFixture actions, WorldSnapshot world)
    {
        var site = world.Excavation!.Sites.Single();
        EntityId player = Player(world);
        var delivery = ConstructionUpgradeFixture.Delivery(world, player);
        var tool = world.Inventory.Custody.Lots.Single(lot => lot.CustodianId == world.GetResident(player).InventoryCustodianId
            && lot.Definition == site.RequiredTool && lot.ReservedBy is null);
        return new(actions.NextIdentity(), delivery.PrincipalId, delivery.SessionId, player, world.WorldId, world.BranchId,
            world.Space.GetActor(player).Revision, new ExcavateIntent(site.SiteId, site.Revision,
                world.Space.Regions.Single(region => region.Bounds.Contains(site.Cell)).Revision, tool.LotId, tool.Revision,
                world.Inventory.Custody.GetStorage(world.GetResident(player).InventoryCustodianId).Revision));
    }

    private static ulong Quantity(WorldSnapshot world, CellCoordinate coordinate) =>
        world.LiquidCells!.Quantities.Where(pair => pair.Key.Coordinate == coordinate).Aggregate(0UL, (sum, pair) => sum + pair.Value);
    private static TerrainCell Cell(WorldSnapshot world, CellCoordinate coordinate) =>
        world.Space.Regions.Single(region => region.Bounds.Contains(coordinate)).GetCell(coordinate);
    private static EntityId Player(WorldSnapshot world) => world.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player).ActorId;
    private static void AssertConserved(WorldSnapshot world)
    {
        ulong cursor = 0;
        Assert.Single(world.LiquidCells!.Portions.Select(portion => portion.OriginId).Distinct());
        foreach (var portion in world.LiquidCells.Portions.OrderBy(portion => portion.Start))
        {
            Assert.Equal(LiquidStockOrigin.RawSource, portion.Origin);
            Assert.Equal(cursor, portion.Start);
            cursor = portion.End;
        }
        Assert.Equal(6000UL, cursor);
        Assert.Equal(world.Calendar, world.LiquidCells.Boundary);
        Assert.Equal(world.AdvancingSteps, world.LiquidCells.Revision);
        Assert.Equal(world.Reservoirs!.ContentHash, world.LiquidCells.ReservoirHash);
    }
}
