using Microsoft.Data.Sqlite;
using OpenFantasy.Core;
using OpenFantasy.Core.Construction.Ownership;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed partial class SeparatedTerrainStorageTests
{
    [Fact]
    public void Typed_owners_keep_carried_custody_stable_and_publish_gift_work_return_and_history()
    {
        using var files = new StorageFiles();
        var observed = new List<(string Kind, ConstructionOwnerDirectory Before, ConstructionOwnerDirectory After)>();
        var ready = Start(files.Path, typed: true, observe: (before, after, kind) =>
            observed.Add((kind, ConstructionOwnerDirectory.Capture(before), ConstructionOwnerDirectory.Capture(after))));
        var movement = Assert.Single(observed, value => value.Kind == "movement");
        Assert.Equal(movement.Before.Owners.Select(owner => owner.Key), movement.After.Owners.Select(owner => owner.Key));
        Assert.Empty(Changed(movement.Before, movement.After, ConstructionOwnerKind.Custodian));
        Assert.NotEmpty(Changed(movement.Before, movement.After, ConstructionOwnerKind.Actor));
        var gift = Assert.Single(observed, value => value.Kind == "gift");
        Assert.Equal(2, Changed(gift.Before, gift.After, ConstructionOwnerKind.Custodian).Count);
        byte[] finalBytes;
        ContentHash finalDirectory;
        using (var authority = ready.Authority)
        {
            Collected(ready.References);
            var locality = ready.Locality;
            var initial = authority.ReadOwnersForHost();
            int reads = authority.TerrainArchiveReads;
            authority.BeforeTerrainRead = () => throw new InvalidOperationException("Typed partial work must not load terrain.");
            for (int i = 0; i < 40; i++) locality = Work(authority, ready.Fixture, locality).Candidate;
            var partial = authority.ReadOwnersForHost();
            Assert.Equal(reads, authority.TerrainArchiveReads);
            Assert.Empty(Changed(initial, partial, ConstructionOwnerKind.Custodian));
            Assert.Single(Changed(initial, partial, ConstructionOwnerKind.Project));
            Assert.Single(Changed(initial, partial, ConstructionOwnerKind.Timing));
            var initialHashes = initial.Owners.ToDictionary(owner => owner.Key, owner => owner.Hash);
            var dirty = partial.Owners.Where(owner => !initialHashes.TryGetValue(owner.Key, out var hash) || hash != owner.Hash).ToArray();
            output.WriteLine($"After40 earned work commits: {dirty.Length} changed current owners, {dirty.Sum(owner => owner.ByteLength)} changed-current-payload bytes; cumulative archive bytes are measured separately. Unchanged custodian images retain exact hashes.");
            authority.BeforeTerrainRead = null;
            var returned = ReturnAndIntervene(authority, ready.Fixture, locality);
            locality = returned.Locality;
            Collected(returned.References);
            reads = authority.TerrainArchiveReads;
            authority.BeforeTerrainRead = () => throw new InvalidOperationException("Resumed typed work must not load terrain.");
            for (int i = 40; i < 199; i++) locality = Work(authority, ready.Fixture, locality).Candidate;
            Assert.Equal(reads, authority.TerrainArchiveReads);
            authority.BeforeTerrainRead = null;
            locality = Work(authority, ready.Fixture, locality).Candidate;
            var final = authority.ReadStateForHost();
            AssertCompleted(final);
            finalBytes = WorldSnapshotCodec.Encode(final);
            var directory = authority.ReadOwnersForHost();
            finalDirectory = directory.Hash;
            Assert.Equal(ConstructionOwnerDirectory.Capture(locality).CopyBytes(), directory.CopyBytes());
            Assert.NotEmpty(Changed(partial, directory, ConstructionOwnerKind.Custodian));
            Assert.Single(Changed(partial, directory, ConstructionOwnerKind.ProductionHistory));
            Assert.Equal(ready.GenesisHash, authority.ReadRecordedStateForHost(0).State.ContentHash);
            Assert.Equal(finalBytes, WorldSnapshotCodec.Encode(authority.ReadRecordedStateForHost(final.Revision).State));
            var payload = directory.Owners[0];
            byte[] copy = payload.CopyBytes();
            copy[0] ^= 0xff;
            Assert.NotEqual(copy, payload.CopyBytes());
            Assert.Equal(payload.Hash, ContentHash.Compute(payload.CopyBytes()));
            output.WriteLine($"Typed directory: {directory.Owners.Count} current owners, {directory.CopyBytes().Length} directory bytes, {directory.Owners.Sum(owner => owner.ByteLength)} current payload bytes; full world {finalBytes.Length} bytes. Actor exposure still changes actor images on advancing work. No independent-detail loading or scale claim.");
        }
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = files.Path, Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*),SUM(length(state)) FROM world_owners";
            using var reader = command.ExecuteReader();
            Assert.True(reader.Read());
            output.WriteLine($"Complete retained typed archive: {reader.GetInt64(0)} unique payloads, {reader.GetInt64(1)} payload bytes; directory/head/journal/SQLite overhead excluded. All historical payloads must be verified during reopen.");
        }
        using var recovered = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(finalBytes, WorldSnapshotCodec.Encode(recovered.ReadStateForHost()));
        Assert.Equal(finalDirectory, recovered.ReadOwnersForHost().Hash);
    }

    [Theory]
    [InlineData((int)AuthorityCommitBoundary.BeforeCommit)]
    [InlineData((int)AuthorityCommitBoundary.AfterCommit)]
    public void Typed_cross_owner_gift_fault_recovers_both_custodians_and_one_receipt(int boundary)
    {
        using var files = new StorageFiles();
        var ready = Start(files.Path, typed: true);
        WorldStepBatch batch;
        WorldActionCommand command;
        byte[] before, expected, receipt;
        using (var authority = ready.Authority)
        {
            var world = authority.ReadStateForHost();
            var player = world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
            var builder = world.Construction!.Projects.Single().BuilderActorId;
            for (int i = 0; world.Space.QueryInteraction(player, builder) != SpatialInteraction.Reachable; i++)
            {
                Assert.True(i < 512, "The actual second gift approach must remain bounded and lawful.");
                var actor = world.Space.GetActor(player);
                var target = world.Space.GetActor(builder).Feet;
                long dx = Math.Sign(target.X - actor.Feet.X) * WorldMovementInput.DisplacementPerStep;
                long dy = dx == 0 ? Math.Sign(target.Y - actor.Feet.Y) * WorldMovementInput.DisplacementPerStep : 0;
                world = authority.Commit(ready.Fixture.Batch(world, [],
                    [new(ConstructionUpgradeFixture.Delivery(world, player), player, dx, dy)])).Candidate;
                Assert.NotEqual(actor.Feet, world.Space.GetActor(player).Feet);
            }
            before = WorldSnapshotCodec.Encode(world);
            var bag = world.GetResident(player).InventoryCustodianId;
            var wood = world.Inventory.Custody.Lots.Single(value => value.CustodianId == bag && value.Definition == world.Quests!.Definitions.Single().Item);
            Assert.Equal(1UL, wood.Quantity);
            var delivery = ConstructionUpgradeFixture.Delivery(world, player);
            command = new(ready.Fixture.NextIdentity(), delivery.PrincipalId, delivery.SessionId, player,
                world.WorldId, world.BranchId, world.Space.GetActor(player).Revision,
                new GiveItemIntent(builder, world.Space.GetActor(builder).Revision, wood.LotId, wood.Revision,
                    bag, world.Inventory.Custody.GetStorage(bag).Revision, 1));
            batch = ready.Fixture.Batch(world, [command], []);
            var decision = new WorldStepProcessor().Decide(world, batch, _ => false);
            Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
            Assert.Equal(2, Changed(ConstructionOwnerDirectory.Capture(world), ConstructionOwnerDirectory.Capture(decision.Candidate), ConstructionOwnerKind.Custodian).Count);
            expected = WorldSnapshotCodec.Encode(decision.Candidate);
            receipt = WorldReceiptCodecV1.Encode(decision.Receipts[0]);
            Assert.Throws<IOException>(() => authority.Commit(batch, at =>
            {
                if ((int)at == boundary) throw new IOException("Interrupt coupled owner transfer");
            }));
            Assert.Throws<InvalidOperationException>(() => authority.ReadStateForHost());
        }
        using var recovered = SqliteWorldAuthority.OpenExisting(files.Path);
        if (boundary == (int)AuthorityCommitBoundary.BeforeCommit)
        {
            Assert.Equal(before, WorldSnapshotCodec.Encode(recovered.ReadStateForHost()));
            recovered.Commit(batch);
        }
        var restored = recovered.ReadStateForHost();
        Assert.Equal(expected, WorldSnapshotCodec.Encode(restored));
        Assert.Equal(receipt, WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(recovered.FindReceipt(command,
            ConstructionUpgradeFixture.Delivery(restored, command.ActorId)))));
        Assert.Throws<AuthorityDispatchException>(() => recovered.Commit(batch));
    }

    [Theory]
    [InlineData("DELETE FROM world_owners WHERE hash=(SELECT hash FROM world_owners LIMIT 1)")]
    [InlineData("UPDATE world_owners SET state=zeroblob(length(state)) WHERE hash=(SELECT hash FROM world_owners LIMIT 1)")]
    [InlineData("INSERT INTO world_owners VALUES(zeroblob(32),X'01')")]
    public void Missing_corrupt_or_extra_historical_owner_images_refuse_recovery(string mutation)
    {
        using var files = new StorageFiles();
        var ready = Start(files.Path, typed: true);
        using (var authority = ready.Authority) Work(authority, ready.Fixture, ready.Locality);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = files.Path, Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = mutation;
            Assert.True(command.ExecuteNonQuery() > 0);
        }
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path));
    }

    [Fact]
    public void Typed_owner_storage_cannot_drop_current_profile_facets()
    {
        using var files = new StorageFiles();
        var world = AdventureWorldAdmission.CreateEmbedded(AdventureWorldAdmission.SeedText, ["Rowan"]);
        Assert.Throws<ArgumentException>(() => ConstructionOwnerDirectory.Capture(world));
        Assert.Throws<ArgumentException>(() => SqliteWorldAuthority.CreateWithTypedOwners(files.Path, world));
        Assert.False(File.Exists(files.Path));
    }

    private static IReadOnlyList<ConstructionOwnerKey> Changed(ConstructionOwnerDirectory before,
        ConstructionOwnerDirectory after, ConstructionOwnerKind kind)
    {
        var old = before.Owners.Where(owner => owner.Key.Kind == kind).ToDictionary(owner => owner.Key, owner => owner.Hash);
        var next = after.Owners.Where(owner => owner.Key.Kind == kind).ToDictionary(owner => owner.Key, owner => owner.Hash);
        return old.Keys.Concat(next.Keys).Distinct().Where(key => !old.TryGetValue(key, out var previous)
            || !next.TryGetValue(key, out var current) || previous != current).ToArray();
    }
}
