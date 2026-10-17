using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class WorldSpatialOccupancyIndexTests
{
    [Fact]
    public void Actual_saved_crossing_reconciles_all_pages_and_reconstructs_the_same_index_from_history()
    {
        var genesis = WorldServiceFixture.Genesis();
        var world = genesis;
        var index = WorldSpatialOccupancyIndex.Capture(genesis, 32);
        var originalIndex = index;
        var actorId = WorldServiceFixture.Id(10);
        var cargo = WorldActorCarriage.Capture(world, actorId);
        using var files = new StorageFiles();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            for (ulong step = 0; step < 4; step++)
            {
                var old = world;
                var actor = world.Space.GetActor(actorId);
                var footprint = MovementTerrainFootprint.Create(actor, world.Space.GroundSupport, -32, 0);
                var occupancy = new SpatialOccupancyReadPlan(world.Space.Scope, footprint.Bounds,
                    index.Directory.ReadPage, 8, 4, 0);
                var loaded = occupancy.Resolve(occupancy.Actors.Select(entry => world.Space.GetActor(entry.EntityId)), []);
                var terrain = new OpenFantasy.Core.Terrain.TerrainReadPlan(world.Space.Scope, footprint.Bounds,
                    world.Space.Regions.Select(OpenFantasy.Core.Terrain.TerrainRegionReference.Capture), 1);
                var prepared = SpatialMovementDependencies.FromDirectory(footprint, terrain, loaded, world.Space.StructureSupport);
                var geometry = prepared.Evaluate(terrain.Resolve(world.Space.Regions));
                Assert.Equal(SpatialMoveStatus.Prepared, geometry.Status);
                world = authority.Commit(WorldServiceFixture.Step(old, 980 + step,
                    movement: [new(WorldServiceFixture.Delivery, actorId, -32, 0)])).Candidate;
                index = index.Advance(old, world);
                Assert.Equal(geometry.Candidate!.Feet, world.Space.GetActor(actorId).Feet);
                cargo.ValidateCurrent(world);
                Assert.Throws<InvalidOperationException>(() => occupancy.ValidateCurrent(index.Directory.ReadPage));
                Assert.Equal(SpatialOccupancyReference.Capture(world.Space.GetActor(actorId)), index.Directory.Find(actorId));
            }
            Assert.Equal(-1, world.Space.GetActor(actorId).Feet.Cell.X);
            Assert.DoesNotContain(index.Directory.ReadPage(new(0, 0, 0)).Entries, entry => entry.EntityId == actorId);
            Assert.Contains(originalIndex.Directory.ReadPage(new(0, 0, 0)).Entries, entry => entry.EntityId == actorId);
            Assert.Equal(6UL, Assert.Single(cargo.CountedLots).Quantity);
        }
        using var recovered = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
        Assert.Equal(world.ContentHash, recovered.ReadStateForHost().ContentHash);
        var replay = WorldSpatialOccupancyIndex.Capture(genesis, 32);
        var previous = genesis;
        for (ulong revision = 1; revision <= world.Revision; revision++)
        {
            var historical = recovered.ReadRecordedStateForHost(revision).State;
            replay = replay.Advance(previous, historical);
            previous = historical;
        }
        Assert.Equal(index.SourceHash, replay.SourceHash);
        Assert.Equal(index.Directory.Revision, replay.Directory.Revision);
        foreach (var key in new[] { new SpatialOccupancyPageKey(-1, -1, 0), new(-1, 0, 0), new(0, -1, 0), new(0, 0, 0) })
            Assert.Equal(index.Directory.ReadPage(key).ContentHash, replay.Directory.ReadPage(key).ContentHash);
        Assert.Throws<InvalidOperationException>(() => originalIndex.Advance(genesis, world));
    }

    [Theory]
    [InlineData((int)AuthorityCommitBoundary.BeforeCommit)]
    [InlineData((int)AuthorityCommitBoundary.AfterCommit)]
    public void Index_is_rebuilt_only_from_durable_cargo_movement_and_receipt_history_after_interruption(int boundaryValue)
    {
        var genesis = WorldServiceFixture.Genesis();
        var index = WorldSpatialOccupancyIndex.Capture(genesis, 32);
        var command = WorldServiceFixture.Give(genesis, 994, 2);
        var batch = WorldServiceFixture.Step(genesis, 993, actions: [command],
            movement: [new(WorldServiceFixture.Delivery, command.ActorId, -32, 0)]);
        var boundary = (AuthorityCommitBoundary)boundaryValue;
        using var files = new StorageFiles();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            Assert.Throws<IOException>(() => authority.Commit(batch, at =>
            {
                if (at == boundary) throw new IOException("occupancy reconciliation interruption");
            }));
            Assert.Equal(genesis.ContentHash, index.SourceHash);
        }
        using var recovered = SqliteWorldAuthority.OpenExisting(files.Path);
        var durable = recovered.ReadStateForHost();
        if (boundary == AuthorityCommitBoundary.BeforeCommit)
        {
            Assert.Equal(genesis.ContentHash, durable.ContentHash);
            Assert.Null(recovered.FindReceipt(command, WorldServiceFixture.Delivery));
            durable = recovered.Commit(batch).Candidate;
        }
        else
        {
            Assert.Equal(1UL, durable.Revision);
            Assert.Throws<AuthorityDispatchException>(() => recovered.Commit(batch));
        }
        var receipt = recovered.FindReceipt(command, WorldServiceFixture.Delivery);
        Assert.NotNull(receipt);
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt),
            WorldReceiptCodecV1.Encode(recovered.FindReceipt(command, WorldServiceFixture.Delivery)!));
        var next = index.Advance(genesis, durable);
        Assert.Equal(-32, durable.Space.GetActor(command.ActorId).Feet.X);
        Assert.Equal(4UL, Assert.Single(WorldActorCarriage.Capture(durable, command.ActorId).CountedLots).Quantity);
        Assert.Equal(2UL, Assert.Single(WorldActorCarriage.Capture(durable, WorldServiceFixture.Id(11)).CountedLots).Quantity);
        var replayed = index.Advance(genesis, recovered.ReadRecordedStateForHost(1).State);
        Assert.Equal(next.SourceHash, replayed.SourceHash);
        Assert.Equal(next.Directory.ReadPage(new(-1, 0, 0)).ContentHash, replayed.Directory.ReadPage(new(-1, 0, 0)).ContentHash);
        Assert.Equal(0UL, index.Directory.Revision);
    }

    [Fact]
    public void Time_only_commits_reuse_geometry_but_bind_the_new_exact_world_and_refuse_a_forked_source()
    {
        var genesis = WorldServiceFixture.Genesis();
        var index = WorldSpatialOccupancyIndex.Capture(genesis, 32);
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var later = authority.Commit(WorldServiceFixture.Step(genesis, 995)).Candidate;
        var next = index.Advance(genesis, later);
        Assert.Same(index.Directory, next.Directory);
        Assert.NotEqual(index.SourceHash, next.SourceHash);
        Assert.Throws<InvalidOperationException>(() => next.Advance(genesis, later));
        var other = new WorldStepProcessor().Decide(genesis, WorldServiceFixture.Step(genesis, 996,
            movement: [new(WorldServiceFixture.Delivery, WorldServiceFixture.Id(10), -32, 0)])).Candidate;
        var following = authority.Commit(WorldServiceFixture.Step(later, 997)).Candidate;
        Assert.Equal(later.Revision, other.Revision);
        Assert.Throws<InvalidOperationException>(() => next.Advance(other, following));
    }
}
