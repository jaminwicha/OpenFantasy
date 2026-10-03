using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.SelectedQueries;

public sealed class HistoryReadShutdownRaceTests
{
    [Fact]
    public async Task Admitted_read_that_acquires_serialization_before_stop_cancellation_is_cancelled_not_disposed()
    {
        using var files = new SessionFiles();
        WorldSnapshot genesis = WorldServiceFixture.Genesis();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var time = new ShutdownHandoffTimeProvider();
        await using var host = new LocalWorldSession(authority, WorldServiceFixture.Id(10), WorldServiceFixture.Delivery, time);
        await SessionFixture.Until(() => time.HasPendingTimer);
        using var releaseAuthority = new ManualResetEventSlim();
        using var executingFinished = new ManualResetEventSlim();
        var authorityEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task heldAuthority = Task.Run(() => authority.ReadForController(WorldServiceFixture.Id(10), WorldServiceFixture.Delivery, state =>
        {
            authorityEntered.SetResult();
            Assert.True(releaseAuthority.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            return state.Revision;
        }), TestContext.Current.CancellationToken);
        Task? observeExecuting = null;
        try
        {
            await authorityEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Task<LocalHistoryVerification> executing = host.VerifyRecordedHistoryAsync();
            observeExecuting = executing.ContinueWith(_ => executingFinished.Set(),
                TestContext.Current.CancellationToken, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            var subject = new EntityId(0x5a17d0, 1);
            Task<MaterialProposalView?> queued = host.InspectMaterialProposalAsync(subject);
            Assert.False(executing.IsCompleted);
            Assert.False(queued.IsCompleted);

            // Stop captures the clock while holding its input lock, before setting the stop flag.
            // Complete the real executing read here. Its finally transfers serialization to the
            // queued read, which cannot pass the input lock until shutdown sets and cancels stop.
            bool acquiredBeforeCancellation = false;
            time.BeforeNextTimestamp = () =>
            {
                releaseAuthority.Set();
                try
                {
                    acquiredBeforeCancellation = executingFinished.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                }
                catch (OperationCanceledException) when (TestContext.Current.CancellationToken.IsCancellationRequested)
                {
                    // Let Stop finish its cancellation even when the test runner is shutting down.
                }
            };
            Task stopping = host.StopAsync();
            Assert.True(acquiredBeforeCancellation, "The executing read did not release serialization inside the stop clock boundary.");
            LocalHistoryVerification verified = await executing.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(genesis.Revision, verified.Revision);
            Assert.True(verified.ReproducesHead);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
            Assert.True(queued.IsCanceled);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => host.InspectMaterialProposalAsync(subject));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => host.VerifyRecordedHistoryAsync());
            await stopping.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.True(host.Completion.IsCompletedSuccessfully);
            Assert.True(host.Player.ProposalInspection.Latest!.IsCompleted);

            // Successful reopen also proves Stop joined admitted work before releasing its writer.
            using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
            Assert.Equal(WorldSnapshotCodec.Encode(genesis), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        }
        finally
        {
            time.BeforeNextTimestamp = null;
            releaseAuthority.Set();
            await heldAuthority.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (observeExecuting is not null)
                await observeExecuting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
    }

    private sealed class ShutdownHandoffTimeProvider : TimeProvider
    {
        private readonly ManualSessionTimeProvider _clock = new();
        private Action? _beforeNextTimestamp;
        internal bool HasPendingTimer => _clock.HasPendingTimer;
        internal Action? BeforeNextTimestamp { set => Interlocked.Exchange(ref _beforeNextTimestamp, value); }
        public override long TimestampFrequency => _clock.TimestampFrequency;
        public override long GetTimestamp()
        {
            Interlocked.Exchange(ref _beforeNextTimestamp, null)?.Invoke();
            return _clock.GetTimestamp();
        }
        public override DateTimeOffset GetUtcNow() => _clock.GetUtcNow();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            _clock.CreateTimer(callback, state, dueTime, period);
    }
}
