using Microsoft.Data.Sqlite;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class ScheduledMaterialInspectionTests(MaterialProposalWorldFixture world)
    : IClassFixture<MaterialProposalWorldFixture>
{
    [Fact]
    public async Task Paused_inspection_filters_participants_and_does_not_advance_or_replay_a_command()
    {
        using var live = world.Open();
        var own = live.Save(world.PinnedRawTerms(live.State, world.Helper, world.Player));
        var outsider = live.State.Residents.First(value => value.ActorId != world.Player && value.ActorId != world.Helper);
        var foreign = live.Save(world.PinnedRawTerms(live.State, world.Helper, outsider.ActorId));
        var before = live.State;
        var usage = live.Authority.InspectForHost().Journal;
        var clock = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(live.Authority, world.Player,
            world.DeliveryOf(before, world.Player), clock);
        var found = await session.InspectMaterialProposalAsync(own.ProposalId);
        Assert.Equal((own.ProposalId, MaterialProposalStatus.Open, false),
            (found!.ProposalId, found.Status, found.ViewerIsSender));
        Assert.Null(await session.InspectMaterialProposalAsync(foreign.ProposalId));
        Assert.Null(await session.InspectMaterialProposalAsync(world.Player));
        await Tick(clock, TimeSpan.FromHours(1));
        Assert.Equal(before.ContentHash, live.State.ContentHash);
        Assert.Equal(usage, live.Authority.InspectForHost().Journal);
        Assert.Equal(LocalWorldRunState.Paused, session.ReadRunState());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inspection_returns_without_the_authority_lock_bounds_requests_and_finishes_before_stop(bool stopWhileReading)
    {
        using var live = world.Open();
        var own = live.Save(world.PinnedRawTerms(live.State, world.Helper, world.Player));
        var before = live.State;
        var delivery = world.DeliveryOf(before, world.Player);
        var clock = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(live.Authority, world.Player, delivery, clock);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task held = Task.Run(() => live.Authority.ReadForController(world.Player, delivery, state =>
        {
            entered.SetResult();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken), "The test must release the real authority lock within its deadline.");
            return state.Revision;
        }), TestContext.Current.CancellationToken);
        var returned = new TaskCompletionSource<Task<MaterialProposalView?>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? caller = null;
        Task? stopping = null;
        Task<MaterialProposalView?>? query = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            // Distinguish returning a pending task from blocking the invoking thread on storage.
            caller = Task.Run(() => returned.SetResult(session.InspectMaterialProposalAsync(own.ProposalId)), TestContext.Current.CancellationToken);
            query = await returned.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.False(query.IsCompleted);
            Assert.Same(query, session.InspectMaterialProposalAsync(own.ProposalId));
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.InspectMaterialProposalAsync(world.Player));
            Assert.Equal(own.ProposalId, Assert.Single(session.ReadView().MaterialProposals!.Offers).ProposalId);
            session.SetReady(false);
            if (stopWhileReading)
            {
                // The free serialization slot was selected before the caller returned its task;
                // the actual read is blocked by our held authority lock, not a timing assumption.
                stopping = session.StopAsync();
                Assert.False(stopping.IsCompleted);
            }
        }
        finally
        {
            release.Set();
            await held;
            if (caller is not null) { await caller; }
        }
        Assert.Equal(own.ProposalId, (await query!.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken))!.ProposalId);
        if (!stopWhileReading) { Assert.Equal(before.ContentHash, live.State.ContentHash); }
        await (stopping ?? session.StopAsync()).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.InspectMaterialProposalAsync(own.ProposalId));
        live.Reopen();
        Assert.Equal(before.ContentHash, live.State.ContentHash);
    }

    [Fact]
    public async Task Backend_failure_faults_a_new_inspection_and_completion_instead_of_returning_a_cached_result()
    {
        using var live = world.Open();
        var own = live.Save(world.PinnedRawTerms(live.State, world.Helper, world.Player));
        var before = live.State.ContentHash;
        var clock = new ManualSessionTimeProvider();
        var session = new LocalWorldSession(live.Authority, world.Player,
            world.DeliveryOf(live.State, world.Player), clock);
        Assert.Equal(own.ProposalId, (await session.InspectMaterialProposalAsync(own.ProposalId))!.ProposalId);
        // Deliberate backend failure, as in retained session tests; normal callers transfer ownership.
        live.Authority.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.InspectMaterialProposalAsync(own.ProposalId));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Equal(LocalWorldRunState.Unavailable, session.ReadRunState());
        Assert.Throws<InvalidOperationException>(() => session.SetReady(true));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.StopAsync());
        live.Reopen();
        Assert.Equal(before, live.State.ContentHash);
        Assert.Equal(MaterialProposalStatus.Open, live.Service.Find(own.ProposalId, world.Player,
            world.DeliveryOf(live.State, world.Player))!.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Archived_inspection_reopens_exactly_and_exclusive_storage_prevents_live_external_damage(bool damage)
    {
        using var live = world.Open();
        var own = live.Save(world.PinnedRawTerms(live.State, world.Helper, world.Player));
        Assert.Equal(WorldActionRejection.None, live.Submit(world.Player, new DeclineMaterialProposalIntent(own.ProposalId)));
        ulong closedAt = live.Book.FindOutcome(own.ProposalId)!.WorldSequence;
        for (int cycle = 0; live.Authority.InspectForHost().Journal.MaterialCareArchiveCount == 0; cycle++)
        {
            Assert.True(cycle <= WorldMaterialProposalBook.MaximumOutcomes, "Real pressure must reach the existing archive bound.");
            var offer = live.Save(world.PinnedRawTerms(live.State, world.Player, world.Helper));
            Assert.Equal(WorldActionRejection.None, live.Submit(world.Player, new WithdrawMaterialProposalIntent(offer.ProposalId)));
        }
        Assert.Equal(1UL, live.Authority.InspectForHost().Journal.MaterialCareArchiveCount);
        Assert.Null(live.Book.FindOutcome(own.ProposalId));
        live.Reopen();
        var before = live.State;
        var usage = live.Authority.InspectForHost().Journal;
        var clock = new ManualSessionTimeProvider();
        var session = new LocalWorldSession(live.Authority, world.Player,
            world.DeliveryOf(before, world.Player), clock);
        try
        {
            var archived = await session.InspectMaterialProposalAsync(own.ProposalId);
            Assert.Equal((own.ProposalId, MaterialProposalStatus.Declined, closedAt, false),
                (archived!.ProposalId, archived.Status, archived.ClosedAtSequence, archived.ViewerIsSender));
            Assert.DoesNotContain(session.ReadView().MaterialProposals!.Offers, value => value.ProposalId == own.ProposalId);
            Assert.Equal(before.ContentHash, live.State.ContentHash);
            Assert.Equal(usage, live.Authority.InspectForHost().Journal);
            if (damage)
            {
                Assert.Equal(5, Assert.Throws<SqliteException>(() => DamageArchive(live.Files.Path)).SqliteErrorCode);
                Assert.Equal(archived, await session.InspectMaterialProposalAsync(own.ProposalId));
            }
        }
        finally
        {
            await session.StopAsync();
        }
        if (damage)
        {
            DamageArchive(live.Files.Path);
            Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(live.Files.Path, world.GenesisBytes));
        }
    }

    private static void DamageArchive(string path)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = path, Pooling = false, DefaultTimeout = 1 }.ToString());
        connection.Open();
        using var mutation = connection.CreateCommand();
        mutation.CommandText = "UPDATE world_material_care_segments SET boundary_hash=zeroblob(32)";
        Assert.Equal(1, mutation.ExecuteNonQuery());
    }
}
