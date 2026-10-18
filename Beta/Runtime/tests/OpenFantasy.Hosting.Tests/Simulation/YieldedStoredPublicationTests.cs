using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed partial class CombinedStoredDueTests
{
    [Fact]
    public async Task Yielded_reads_release_the_authority_gate_and_publish_the_complete_due_boundary_once()
    {
        var fixture = Fixture.Value;
        using var files = new RetainedFiles("yielded-complete");
        using var authority = CreateCombinedAuthority(files.Path);
        var commands = Select(fixture.World);
        var interval = authority.EvictForStoredConstruction(commands);
        var batch = Batch(fixture.World, commands);
        var publication = authority.BeginStoredConstruction(batch);
        Assert.Throws<AuthorityDispatchException>(() => authority.PublishStoredConstruction(publication));
        Assert.Throws<ArgumentOutOfRangeException>(() => authority.ReadStoredConstruction(publication, 0));
        Assert.Equal(0, publication.LoadedOwnerCount);
        int reads = authority.OwnerArchiveReads;
        authority.BeforeTerrainRead = () => throw new IOException("Sliced owner preparation must not read terrain");
        while (!publication.IsReady)
        {
            int loaded = publication.LoadedOwnerCount;
            // Require a different executing thread, rather than assuming a thread-pool hop proves release.
            int callerThread = Environment.CurrentManagedThreadId;
            int readerThread = await Task.Factory.StartNew(() =>
                {
                    authority.ReadStoredConstruction(publication, 1);
                    return Environment.CurrentManagedThreadId;
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.NotEqual(callerThread, readerThread);
            Assert.Equal(loaded + 1, publication.LoadedOwnerCount);
        }
        Assert.Equal(interval.RequiredOwnerCount, authority.OwnerArchiveReads - reads);
        authority.BeforeTerrainRead = null;
        Assert.Equal(WorldSnapshotCodec.Encode(fixture.World), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
        Assert.All(commands, command => Assert.Null(authority.FindReceipt(command,
            ConstructionUpgradeFixture.Delivery(fixture.World, command.ActorId))));
        var expected = new WorldStepProcessor().Decide(fixture.World, batch, fixture.Identities.Contains);
        var receipts = authority.PublishStoredConstruction(publication);
        Assert.Equal(WorldReceiptCodecV1.EncodeBatch(expected.Receipts), WorldReceiptCodecV1.EncodeBatch(receipts));
        Assert.True(publication.IsRetired);
        Assert.Throws<AuthorityDispatchException>(() => authority.PublishStoredConstruction(publication));
        Assert.Throws<AuthorityDispatchException>(() => authority.ReadStoredConstruction(publication, 1));
        AssertCombinedBoundary(fixture.World, authority.ReadStateForHost(), fixture);
        Assert.Equal(WorldSnapshotCodec.Encode(expected.Candidate), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_real_intervening_boundary_invalidates_partly_or_fully_loaded_publication(bool fullyLoaded)
    {
        var fixture = Fixture.Value;
        using var files = new RetainedFiles("yielded-intervened-" + fullyLoaded);
        using var authority = CreateCombinedAuthority(files.Path);
        var commands = Select(fixture.World);
        authority.EvictForStoredConstruction(commands);
        var publication = authority.BeginStoredConstruction(Batch(fixture.World, commands));
        authority.ReadStoredConstruction(publication, fullyLoaded ? publication.RequiredOwnerCount : 1);
        var intervening = Batch(fixture.World, []);
        var changed = authority.Commit(intervening).Candidate;
        Assert.Equal(fixture.World.AdvancingSteps + 1, changed.AdvancingSteps);
        Assert.Throws<AuthorityDispatchException>(() => authority.PublishStoredConstruction(publication));
        Assert.True(publication.IsRetired);
        Assert.Throws<AuthorityDispatchException>(() => authority.ReadStoredConstruction(publication, 1));
        Assert.Equal(WorldSnapshotCodec.Encode(changed), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
        Assert.All(commands, command => Assert.Null(authority.FindReceipt(command,
            ConstructionUpgradeFixture.Delivery(changed, command.ActorId))));
        var freshCommands = Select(changed);
        authority.EvictForStoredConstruction(freshCommands);
        var freshBatch = Batch(changed, freshCommands);
        var fresh = authority.BeginStoredConstruction(freshBatch);
        authority.ReadStoredConstruction(fresh, fresh.RequiredOwnerCount);
        authority.PublishStoredConstruction(fresh);
        Assert.Equal(WorldSnapshotCodec.Encode(new WorldStepProcessor().Decide(changed, freshBatch,
            identity => fixture.Identities.Contains(identity) || identity == intervening.CauseId).Candidate),
            WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
    }

    [Fact]
    public void Cancelled_preparation_earns_nothing_and_cannot_be_used_by_another_authority()
    {
        var fixture = Fixture.Value;
        using var firstFiles = new RetainedFiles("yielded-cancelled");
        using var otherFiles = new RetainedFiles("yielded-other-authority");
        using var authority = CreateCombinedAuthority(firstFiles.Path);
        using var other = CreateCombinedAuthority(otherFiles.Path);
        var commands = Select(fixture.World);
        authority.EvictForStoredConstruction(commands);
        other.EvictForStoredConstruction(commands);
        var batch = Batch(fixture.World, commands);
        var preparation = authority.BeginStoredConstruction(batch);
        authority.ReadStoredConstruction(preparation, 1);
        Assert.Throws<AuthorityDispatchException>(() => other.ReadStoredConstruction(preparation, 1));
        Assert.Throws<AuthorityDispatchException>(() => other.PublishStoredConstruction(preparation));
        Assert.Throws<AuthorityDispatchException>(() => other.CancelStoredConstruction(preparation));
        Assert.False(preparation.IsRetired);
        authority.CancelStoredConstruction(preparation);
        authority.CancelStoredConstruction(preparation);
        Assert.Throws<AuthorityDispatchException>(() => authority.PublishStoredConstruction(preparation));
        Assert.Equal(WorldSnapshotCodec.Encode(fixture.World), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
        var fresh = authority.BeginStoredConstruction(batch);
        authority.ReadStoredConstruction(fresh, fresh.RequiredOwnerCount);
        authority.PublishStoredConstruction(fresh);
        AssertCombinedBoundary(fixture.World, authority.ReadStateForHost(), fixture);
        Assert.Equal(WorldSnapshotCodec.Encode(fixture.World), WorldSnapshotCodec.Encode(other.ReadStateForHost()));
    }

    [Fact]
    public void Failed_slice_fences_the_authority_and_restart_preserves_the_unpublished_origin()
    {
        var fixture = Fixture.Value;
        using var files = new RetainedFiles("yielded-read-failure");
        using (var authority = CreateCombinedAuthority(files.Path))
        {
            var commands = Select(fixture.World);
            authority.EvictForStoredConstruction(commands);
            var preparation = authority.BeginStoredConstruction(Batch(fixture.World, commands));
            authority.ReadStoredConstruction(preparation, 1);
            authority.BeforeOwnerRead = _ => throw new IOException("Injected second-slice owner failure");
            Assert.Throws<IOException>(() => authority.ReadStoredConstruction(preparation, 1));
            Assert.True(preparation.IsRetired);
            Assert.Throws<InvalidOperationException>(() => authority.ReadStateForHost());
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(fixture.World), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.Equal(WorldSnapshotCodec.Encode(fixture.Genesis), WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost(0).State));
    }

    [Theory]
    [InlineData((int)AuthorityCommitBoundary.BeforeCommit)]
    [InlineData((int)AuthorityCommitBoundary.AfterCommit)]
    public void Yielded_publication_fault_recovers_all_selected_work_and_due_effects_or_none(int boundary)
    {
        var fixture = Fixture.Value;
        using var files = new RetainedFiles("yielded-fault-" + boundary);
        var commands = Select(fixture.World);
        var batch = Batch(fixture.World, commands);
        var expected = new WorldStepProcessor().Decide(fixture.World, batch, fixture.Identities.Contains);
        using (var authority = CreateCombinedAuthority(files.Path))
        {
            authority.EvictForStoredConstruction(commands);
            var preparation = authority.BeginStoredConstruction(batch);
            while (!preparation.IsReady) authority.ReadStoredConstruction(preparation, 2);
            Assert.Throws<IOException>(() => authority.PublishStoredConstruction(preparation, at =>
            {
                if ((int)at == boundary) throw new IOException("Injected yielded publication fault");
            }));
            Assert.True(preparation.IsRetired);
            Assert.Throws<InvalidOperationException>(() => authority.ReadStateForHost());
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        bool published = boundary == (int)AuthorityCommitBoundary.AfterCommit;
        Assert.Equal(WorldSnapshotCodec.Encode(published ? expected.Candidate : fixture.World),
            WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        foreach (var command in commands)
        {
            var receipt = reopened.FindReceipt(command, ConstructionUpgradeFixture.Delivery(fixture.World, command.ActorId));
            if (published)
                Assert.Equal(WorldReceiptCodecV1.Encode(expected.Receipts.Single(value => value.CommandId == command.CommandId)),
                    WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(receipt)));
            else Assert.Null(receipt);
        }
        Assert.Equal(WorldSnapshotCodec.Encode(fixture.Genesis), WorldSnapshotCodec.Encode(reopened.ReadRecordedStateForHost(0).State));
    }

    private static SqliteWorldAuthority CreateCombinedAuthority(string path)
    {
        var fixture = Fixture.Value;
        var authority = SqliteWorldAuthority.CreateWithTypedOwners(path, fixture.Genesis);
        try
        {
            foreach (var batch in fixture.Batches) authority.Commit(batch);
            Assert.Equal(WorldSnapshotCodec.Encode(fixture.World), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            return authority;
        }
        catch { authority.Dispose(); throw; }
    }
}
