using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class JoinedProcessBoundaryTests
{
    private static readonly Lazy<JoinedFixture> Prepared = new(Prepare);

    [Fact]
    public void Lawful_harvest_route_and_original_regrowth_delay_join_the_existing_water_due_boundary()
    {
        var f = Prepared.Value;
        Assert.Equal((ushort)9, f.Genesis.ProfileVersion);
        Assert.Equal(f.Genesis.Residents.Select(x => x.ActorId), f.Before.Residents.Select(x => x.ActorId));
        var node = f.Before.Gathering!.GetNode(f.Node);
        Assert.Equal(0, node.RemainingSwings);
        Assert.Equal(node.Definition.RegrowthDelaySteps, f.Before.AdvancingSteps + 1 - node.DepletedAtStep!.Value);
        Assert.Equal(node.Definition.MaxSwings, checked((int)node.NextUnissuedUnit));
        Assert.Equal(f.Before.Calendar.AddMilliseconds(1800), Assert.Single(f.Before.Transport!.Stages).DueAt);
        Assert.Single(f.Before.Gathering.DiscoverRegrowth(f.Before.ProcessIntervals + 1).Occurrences,
            x => x.NodeId == f.Node && x.CanRegrow);
        Assert.Single(f.Before.Transport.DiscoverDue(f.Before.Calendar.AddMilliseconds(1800)).Stages);
        Assert.All(f.Prefix.SelectMany(x => x.Movement), x => Assert.Equal(32L, Math.Abs(x.DeltaX) + Math.Abs(x.DeltaY)));
        AssertBoundary(f.Before, f.Expected.Candidate, f.Node);
    }

    [Theory]
    [InlineData(null)]
    [InlineData((int)AuthorityCommitBoundary.BeforeCommit)]
    [InlineData((int)AuthorityCommitBoundary.AfterCommit)]
    public void Regrowth_delivery_exposure_and_refused_action_recover_as_one_boundary(int? faultValue)
    {
        var f = Prepared.Value;
        using var files = new StorageFiles();
        var receipts = new List<(WorldActionCommand Command, WorldActionReceipt Receipt)>();
        AuthorityCommitBoundary? fault = faultValue is { } value ? (AuthorityCommitBoundary)value : null;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, f.Genesis))
        {
            foreach (var batch in f.Prefix)
            {
                var decision = authority.Commit(batch);
                receipts.AddRange(batch.Actions.Select(command => (command,
                    decision.Receipts.Single(receipt => receipt.CommandId == command.CommandId))));
            }
            Assert.Equal(WorldSnapshotCodec.Encode(f.Before), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            if (fault is null)
            {
                Assert.Equal(WorldSnapshotCodec.Encode(f.Expected.Candidate), WorldSnapshotCodec.Encode(authority.Commit(f.Final).Candidate));
            }
            else
            {
                Assert.Throws<IOException>(() => authority.Commit(f.Final, at =>
                {
                    if (at == fault) throw new IOException("Joined process publication fault");
                }));
                Assert.Throws<InvalidOperationException>(() => authority.ReadStateForHost());
            }
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        bool published = fault != AuthorityCommitBoundary.BeforeCommit;
        Assert.Equal(WorldSnapshotCodec.Encode(published ? f.Expected.Candidate : f.Before),
            WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.Equal(WorldSnapshotCodec.Encode(f.Genesis), WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost(0).State));
        Assert.Equal(WorldSnapshotCodec.Encode(f.Before), WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost(f.Before.Revision).State));
        foreach (var (command, receipt) in receipts)
        {
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(
                reopened.FindReceipt(command, ConstructionUpgradeFixture.Delivery(f.Before, command.ActorId)))));
        }
        var action = Assert.Single(f.Final.Actions);
        var retained = reopened.FindReceipt(action, ConstructionUpgradeFixture.Delivery(f.Before, action.ActorId));
        if (published) Assert.Equal(WorldReceiptCodecV1.Encode(Assert.Single(f.Expected.Receipts)),
            WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(retained)));
        else
        {
            Assert.Null(retained);
            // Explicit recovery scenario: the failed transaction published nothing; submit once to the reopened owner.
            Assert.Equal(WorldSnapshotCodec.Encode(f.Expected.Candidate), WorldSnapshotCodec.Encode(reopened.Commit(f.Final).Candidate));
        }
        AssertBoundary(f.Before, reopened.ReadStateForHost(), f.Node);
        Assert.Throws<AuthorityDispatchException>(() => reopened.Commit(f.Final));
        string directory = Path.GetDirectoryName(files.Path)!;
        File.WriteAllBytes(Path.Combine(directory, "genesis.ofc"), WorldSnapshotCodec.Encode(f.Genesis));
        File.WriteAllBytes(Path.Combine(directory, "before.ofc"), WorldSnapshotCodec.Encode(f.Before));
        File.WriteAllBytes(Path.Combine(directory, "after.ofc"), WorldSnapshotCodec.Encode(f.Expected.Candidate));
        File.WriteAllText(Path.Combine(directory, "joined.json"), JsonSerializer.Serialize(new
        {
            Fault = fault?.ToString(), PrefixCommits = f.Prefix.Length, Residents = f.Before.Residents.Count,
            DueStep = f.Before.AdvancingSteps + 1, DepletedAt = f.Before.Gathering!.GetNode(f.Node).DepletedAtStep,
            Calendar = f.Expected.Candidate.Calendar, ReceiptsChecked = receipts.Count + 1,
            BeforeHash = f.Before.ContentHash.Value, AfterHash = f.Expected.Candidate.ContentHash.Value
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static JoinedFixture Prepare()
    {
        var fixture = new ReservoirTransportWorldFixture(nearby: false, primaryReceiver: false);
        var genesis = fixture.Genesis;
        var world = genesis;
        var prefix = new List<WorldStepBatch>();
        var processor = new WorldStepProcessor();
        WorldStepDecision Commit(WorldActionIntent? intent = null, IEnumerable<WorldMovementInput>? movement = null)
        {
            var batch = fixture.Materials.Actions.Batch(world,
                intent is null ? [] : [fixture.Materials.Command(world, fixture.Player, intent)], movement ?? []);
            var decision = processor.Decide(world, batch);
            Assert.All(decision.Receipts, x => Assert.Equal(WorldActionRejection.None, x.Rejection));
            prefix.Add(batch);
            world = decision.Candidate;
            return decision;
        }
        var node = world.Gathering!.Nodes.First(x => x.Definition.Kind == GatherNodeKind.Berry);
        var approach = new[] { node.Feet.Offset(-256, 0, 0), node.Feet.Offset(256, 0, 0),
            node.Feet.Offset(0, -256, 0), node.Feet.Offset(0, 256, 0) }
            .First(target => world.Space.QueryGroundRoute(fixture.Player, target).Status == GroundRouteStatus.Reachable);
        for (int step = 0; world.Space.GetActor(fixture.Player).Feet != approach; step++)
        {
            Assert.True(step < 160, "The real resource route must fit the existing fixture bound.");
            var feet = world.Space.GetActor(fixture.Player).Feet;
            var route = world.Space.QueryGroundRoute(fixture.Player, approach);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            var waypoint = Assert.IsType<SubunitPosition>(route.NextWaypoint);
            long dx = Math.Clamp(waypoint.X - feet.X, -32, 32);
            long dy = dx == 0 ? Math.Clamp(waypoint.Y - feet.Y, -32, 32) : 0;
            Assert.True(dx != 0 || dy != 0);
            Commit(movement: [new(ConstructionUpgradeFixture.Delivery(world, fixture.Player), fixture.Player, dx, dy)]);
            Assert.Equal(feet.Offset(dx, dy, 0), world.Space.GetActor(fixture.Player).Feet);
        }
        var tool = world.Gathering.Tools.Single(x => x.Kind == node.Definition.RequiredToolKind);
        var lot = world.Inventory.Custody.Lots.Single(x => x.CustodianId == world.GetResident(fixture.Player).InventoryCustodianId
            && x.Definition == tool.Definition);
        for (int step = 0; world.Gathering!.GetNode(node.NodeId).RemainingSwings > 0; step++)
        {
            Assert.True(step < 512, "Actual harvesting must fit the retained effort budget.");
            if (world.Gathering.GetActorWork(fixture.Player).Effort >= GatherRulesV1.EffortPerSwing)
                Commit(new GatherIntent(node.NodeId, world.Gathering.GetNode(node.NodeId).Revision,
                    lot.LotId, world.Inventory.Custody.GetLot(lot.LotId).Revision));
            else Commit();
        }
        ulong due = world.Gathering!.GetNode(node.NodeId).RespawnAtAdvancingStep!.Value;
        while (world.AdvancingSteps < due - 2) Commit();
        Commit(fixture.Schedule(world));
        Assert.Equal(due - 1, world.AdvancingSteps);
        var final = fixture.Materials.Actions.Batch(world, [fixture.Materials.Command(world, fixture.Player, fixture.Schedule(world))], []);
        var expected = processor.Decide(world, final);
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(expected.Receipts).Rejection);
        return new(genesis, world, prefix.ToArray(), final, expected, node.NodeId);
    }

    private static void AssertBoundary(WorldSnapshot before, WorldSnapshot after, EntityId nodeId)
    {
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
        Assert.Equal(before.Calendar.AddMilliseconds(1800), after.Calendar);
        Assert.Equal(before.Residents.Select(x => (x.ActorId, x.ExperiencedTime.TotalMilliseconds + 1800)),
            after.Residents.Select(x => (x.ActorId, x.ExperiencedTime.TotalMilliseconds)));
        var node = after.Gathering!.GetNode(nodeId);
        Assert.Equal(node.Definition.MaxSwings, node.RemainingSwings);
        Assert.Equal(before.Gathering!.GetNode(nodeId).NextUnissuedUnit, node.NextUnissuedUnit);
        Assert.Equal(RunoffTransportStatus.Completed, Assert.Single(after.Transport!.Stages).Status);
        Assert.Equal(700UL, after.Inventory.Custody.GetStoredQuantity(ReservoirWorldFixture.SourceStore));
        Assert.Equal(650UL, after.Inventory.Custody.GetStoredQuantity(ReservoirWorldFixture.Receiver));
        Assert.Equal(before.Inventory.Custody.Lots.Where(x => x.Source.SourceId == node.SourceId),
            after.Inventory.Custody.Lots.Where(x => x.Source.SourceId == node.SourceId));
        Assert.Empty(after.Gathering.DiscoverRegrowth(after.ProcessIntervals).Occurrences);
        Assert.Empty(after.Transport.DiscoverDue(after.Calendar).Stages);
    }

    private sealed record JoinedFixture(WorldSnapshot Genesis, WorldSnapshot Before, WorldStepBatch[] Prefix,
        WorldStepBatch Final, WorldStepDecision Expected, EntityId Node);
}
