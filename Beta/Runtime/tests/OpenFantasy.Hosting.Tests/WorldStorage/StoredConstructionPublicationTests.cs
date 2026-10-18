using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction.Ownership;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed partial class SeparatedTerrainStorageTests
{
    [Fact]
    public void Selected_stored_publication_completes_the_original_saved_project_after_collection_and_intervention()
    {
        using var files = new StorageFiles();
        var ready = StartStoredPublication(files.Path);
        byte[] complete;
        using (var authority = ready.Authority)
        {
            Collected(ready.References);
            var world = ready.Oracle;
            int terrainReads = authority.TerrainArchiveReads;
            int selectedReads = 0;
            authority.BeforeTerrainRead = () => throw new IOException("Partial work must not fetch terrain");
            authority.BeforeOwnerRead = hash => Assert.DoesNotContain(hash, ready.ExcludedOwners);
            for (int i = 0; i < 200; i++)
            {
                if (i == 40)
                {
                    authority.BeforeOwnerRead = null;
                    authority.BeforeTerrainRead = null;
                    Assert.Equal(WorldSnapshotCodec.Encode(world), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
                    var player = world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
                    var movement = ready.Fixture.Batch(world, [], [new(ConstructionUpgradeFixture.Delivery(world, player), player, -32, 0)]);
                    world = authority.Commit(movement).Candidate;
                    var next = Batch(world.Inventory, world.Space.Actors, world.Construction!, world.Revision, world.AdvancingSteps, ready.Fixture);
                    Assert.Throws<AuthorityDispatchException>(() => authority.CommitStoredConstruction(next));
                    authority.EvictForStoredConstruction(next.Actions.Single());
                    terrainReads = authority.TerrainArchiveReads;
                    authority.BeforeTerrainRead = () => throw new IOException("Renewed partial work must not fetch terrain");
                    authority.BeforeOwnerRead = hash => Assert.DoesNotContain(hash, ready.ExcludedOwners);
                }
                var batch = Batch(world.Inventory, world.Space.Actors, world.Construction!, world.Revision, world.AdvancingSteps, ready.Fixture);
                var expected = new WorldStepProcessor().Decide(world, batch);
                int beforeReads = authority.OwnerArchiveReads;
                if (i == 199) { authority.BeforeTerrainRead = null; authority.BeforeOwnerRead = null; }
                var receipt = authority.CommitStoredConstruction(batch);
                Assert.Equal(WorldReceiptCodecV1.Encode(expected.Receipts.Single()), WorldReceiptCodecV1.Encode(receipt));
                if (i < 199)
                {
                    selectedReads = authority.OwnerArchiveReads - beforeReads;
                    Assert.Equal(ready.RequiredOwners, selectedReads);
                    Assert.True(selectedReads < ready.CompleteOwners);
                    Assert.Equal(terrainReads, authority.TerrainArchiveReads);
                }
                world = expected.Candidate;
            }
            AssertCompleted(world);
            Assert.True(authority.TerrainArchiveReads > terrainReads);
            complete = WorldSnapshotCodec.Encode(world);
            Assert.Equal(complete, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            Assert.Equal(ready.GenesisHash, authority.ReadRecordedStateForHost(0).State.ContentHash);
            output.WriteLine($"Stored partial publication: {selectedReads}/{ready.CompleteOwners} owner payload reads per partial step; all resident exposure and due gathering retained, zero terrain reads for partial steps; full final admission and intervening action included.");
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(complete, WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
    }

    [Theory]
    [InlineData(1, (int)AuthorityCommitBoundary.BeforeCommit)]
    [InlineData(1, (int)AuthorityCommitBoundary.AfterCommit)]
    [InlineData(200, (int)AuthorityCommitBoundary.BeforeCommit)]
    [InlineData(200, (int)AuthorityCommitBoundary.AfterCommit)]
    public void Selected_stored_publication_fault_retains_exact_old_or_new_boundary(int target, int boundary)
    {
        using var files = new StorageFiles();
        var ready = StartStoredPublication(files.Path);
        var world = ready.Oracle;
        for (int i = 1; i < target; i++)
        {
            var preceding = Batch(world.Inventory, world.Space.Actors, world.Construction!, world.Revision, world.AdvancingSteps, ready.Fixture);
            Assert.Equal(WorldActionRejection.None, ready.Authority.CommitStoredConstruction(preceding).Rejection);
            world = new WorldStepProcessor().Decide(world, preceding).Candidate;
        }
        var batch = Batch(world.Inventory, world.Space.Actors, world.Construction!, world.Revision, world.AdvancingSteps, ready.Fixture);
        var expected = new WorldStepProcessor().Decide(world, batch);
        using (var authority = ready.Authority)
        {
            Assert.Throws<IOException>(() => authority.CommitStoredConstruction(batch, at =>
            {
                if ((int)at == boundary) throw new IOException("Stored publication fault");
            }));
            Assert.Throws<InvalidOperationException>(() => authority.ReadStateForHost());
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        var recovered = reopened.ReadStateForHost();
        Assert.Equal(WorldSnapshotCodec.Encode(boundary == (int)AuthorityCommitBoundary.BeforeCommit ? world : expected.Candidate),
            WorldSnapshotCodec.Encode(recovered));
        var receipt = reopened.FindReceipt(batch.Actions.Single(), ConstructionUpgradeFixture.Delivery(world, batch.Actions.Single().ActorId));
        if (boundary == (int)AuthorityCommitBoundary.BeforeCommit) Assert.Null(receipt);
        else Assert.Equal(WorldReceiptCodecV1.Encode(expected.Receipts.Single()), WorldReceiptCodecV1.Encode(receipt!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Injected_owner_read_failure_fences_work_without_consuming_the_command(bool sqliteFailure)
    {
        using var files = new StorageFiles();
        var ready = StartStoredPublication(files.Path);
        using var authority = ready.Authority;
        var world = ready.Oracle;
        var batch = Batch(world.Inventory, world.Space.Actors, world.Construction!, world.Revision, world.AdvancingSteps, ready.Fixture);
        authority.BeforeOwnerRead = _ =>
        {
            if (sqliteFailure) throw new SqliteException("Injected owner read failure", 10);
            throw new InvalidDataException("Injected missing owner read");
        };
        if (sqliteFailure) Assert.Throws<SqliteException>(() => authority.CommitStoredConstruction(batch));
        else Assert.Throws<InvalidDataException>(() => authority.CommitStoredConstruction(batch));
        Assert.Throws<InvalidOperationException>(() => authority.ReadStateForHost());
        authority.Dispose();
        using var recovered = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(world), WorldSnapshotCodec.Encode(recovered.ReadStateForHost()));
        Assert.Null(recovered.FindReceipt(batch.Actions.Single(), ConstructionUpgradeFixture.Delivery(world, batch.Actions.Single().ActorId)));
    }

    [Fact]
    public void Switching_to_complete_locality_eviction_discards_stored_interval_metadata()
    {
        using var files = new StorageFiles();
        var ready = StartStoredPublication(files.Path);
        using var authority = ready.Authority;
        var world = ready.Oracle;
        var batch = Batch(world.Inventory, world.Space.Actors, world.Construction!, world.Revision, world.AdvancingSteps, ready.Fixture);
        authority.CommitStoredConstruction(batch);
        var expected = new WorldStepProcessor().Decide(world, batch).Candidate;
        var locality = authority.EvictConstructionTerrain(expected.Construction!.Projects.Single().ProjectId, 512);
        var next = Batch(locality, ready.Fixture);
        authority.CommitWithoutTerrain(next);
        expected = new WorldStepProcessor().Decide(expected, next).Candidate;
        Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
        Assert.Throws<AuthorityDispatchException>(() => authority.CommitStoredConstruction(next));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Read_only_stored_assessment_also_fences_owner_read_failures(bool sqliteFailure)
    {
        using var files = new StorageFiles();
        var ready = Start(files.Path, typed: true);
        using var authority = ready.Authority;
        var cohort = authority.CaptureStoredWorkCohort(Batch(ready.Locality, ready.Fixture).Actions.Single());
        authority.BeforeOwnerRead = _ =>
        {
            if (sqliteFailure) throw new SqliteException("Injected assessment read failure", 10);
            throw new InvalidDataException("Injected missing assessment owner");
        };
        if (sqliteFailure) Assert.Throws<SqliteException>(() => authority.AssessStoredWorkCohort(cohort));
        else Assert.Throws<InvalidDataException>(() => authority.AssessStoredWorkCohort(cohort));
        Assert.Throws<InvalidOperationException>(() => authority.ReadStateForHost());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static StoredReady StartStoredPublication(string path)
    {
        var ready = Start(path, typed: true);
        var world = ready.Authority.ReadStateForHost();
        var command = Batch(ready.Locality, ready.Fixture).Actions.Single();
        var directory = ConstructionOwnerDirectory.Capture(world);
        var cohort = ConstructionWorkCohort.Capture(world, command);
        var holders = cohort.RequiredOwners.Where(owner => owner.Key.Kind == ConstructionOwnerKind.Custodian).Select(owner => owner.Key.SubjectId).ToHashSet();
        var unrelated = world.Inventory.Custody.Lots.Where(lot => !holders.Contains(lot.CustodianId)).ToArray();
        Assert.NotEmpty(unrelated);
        var excluded = directory.Owners.Where(owner => owner.Key.Kind is ConstructionOwnerKind.InventoryBasis or ConstructionOwnerKind.InventoryLedger
            or ConstructionOwnerKind.ProductionHistory or ConstructionOwnerKind.ProductionBatch or ConstructionOwnerKind.Recipe
            || owner.Key.Kind == ConstructionOwnerKind.Custodian && !holders.Contains(owner.Key.SubjectId)).Select(owner => owner.Hash).ToArray();
        var interval = ready.Authority.EvictForStoredConstruction(command);
        return new(ready.Authority, ready.Fixture, WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(world)),
            [new(world), new(world.Inventory), new(world.Inventory.Custody), new(world.Production!), new(world.Space), new(world.IdentityIndex),
                new(ready.Locality), .. world.Space.Regions.Select(region => new WeakReference(region)), .. unrelated.Select(lot => new WeakReference(lot))],
            excluded, interval.RequiredOwnerCount, interval.CompleteOwnerCount, ready.GenesisHash);
    }

    private sealed record StoredReady(SqliteWorldAuthority Authority, ConstructionUpgradeFixture Fixture, WorldSnapshot Oracle,
        WeakReference[] References, ContentHash[] ExcludedOwners, int RequiredOwners, int CompleteOwners, ContentHash GenesisHash);
}
