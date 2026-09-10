using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class LocalWorldSessionTests
{
    [Fact]
    public async Task Readiness_pause_clears_movement_and_never_adds_offline_time()
    {
        using var files = new SessionFiles();
        WorldSnapshot genesis = Genesis();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, Id(10), Delivery, time);
        await Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(genesis.Calendar, session.ReadView().Calendar);
        session.SetReady(true);
        session.SetMovement(32, 0);
        session.SetReady(false);
        await Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(genesis.Calendar, session.ReadView().Calendar);
        session.SetReady(true);
        await Tick(time);
        Assert.Equal(genesis.Calendar.AddMilliseconds(1800), session.ReadView().Calendar);
        Assert.Equal(genesis.Space.GetActor(Id(10)).Feet, session.ReadView().Actors.Single(actor => actor.ActorId == Id(10)).Feet);
    }

    [Fact]
    public async Task Expired_movement_and_late_host_deadlines_do_not_create_catch_up_steps()
    {
        using var files = new SessionFiles();
        WorldSnapshot genesis = Genesis();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, Id(10), Delivery, time);
        session.SetReady(true);
        session.SetMovement(32, 0);
        await Tick(time, TimeSpan.FromMilliseconds(500));
        Assert.Equal(1UL, authority.ReadStateForHost().AdvancingSteps);
        Assert.Equal(genesis.Space.GetActor(Id(10)).Feet, session.ReadView().Actors[0].Feet);
        await Task.Delay(20, TestContext.Current.CancellationToken);
        Assert.Equal(1UL, authority.ReadStateForHost().AdvancingSteps);
        session.SetMovement(32, 0);
        await Tick(time);
        Assert.Equal(32L, session.ReadView().Actors[0].Feet.X);
        session.SetMovement(0, 0);
        await Tick(time);
        Assert.Equal(32L, session.ReadView().Actors[0].Feet.X);
        Assert.Equal(3UL, authority.ReadStateForHost().AdvancingSteps);
        Assert.Equal(ExposurePacingV1.FromExplorationSteps(3), authority.ReadStateForHost().Residents[0].ExperiencedTime);
    }

    [Fact]
    public async Task Pending_actions_coalesce_without_early_ack_and_exact_durable_retry_survives_restart()
    {
        using var files = new SessionFiles();
        WorldSnapshot genesis = Genesis();
        WorldActionCommand action = Give(genesis);
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, Id(10), Delivery, time);
        session.SetReady(true);
        Task<WorldActionReceipt> pending = session.SubmitAction(action);
        Task<WorldActionReceipt> duplicate = session.SubmitAction(action);
        Assert.False(pending.IsCompleted);
        Assert.False(duplicate.IsCompleted);
        Assert.Null(authority.FindReceipt(action, Delivery));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(Give(genesis, command: 202)));
        LocalWorldView old = session.ReadView();
        await Tick(time);
        WorldActionReceipt accepted = await pending;
        Assert.Equal(CommandReceiptStatus.Applied, accepted.Status);
        Assert.Equal(accepted.ResultId, (await duplicate).ResultId);
        Assert.Equal(accepted.ResultId, (await session.SubmitAction(action)).ResultId);
        Assert.Equal(1UL, authority.InspectForHost().Journal.CommandOutcomeCount);
        Assert.Equal(6UL, old.PlayerInventory.Quantity);
        Assert.Empty(session.ReadView().PlayerInventory.Lots);
        Assert.Equal(0UL, session.ReadView().PlayerInventory.Quantity);
        await session.StopAsync();
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(accepted.ResultId, reopened.FindReceipt(action, Delivery)!.ResultId);
        Assert.Equal(1UL, reopened.ReadStateForHost().AdvancingSteps);
    }

    [Fact]
    public async Task Addressed_reading_hold_suppresses_movement_and_held_close_advances_no_clocks()
    {
        using var files = new SessionFiles();
        WorldSnapshot genesis = Genesis();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, Id(10), Delivery, time);
        session.SetReady(true);
        session.SetMovement(32, 0);
        Task<WorldActionReceipt> opening = session.SubmitAction(Speak(genesis));
        await Tick(time);
        Assert.Equal(CommandReceiptStatus.Applied, (await opening).Status);
        LocalWorldView held = session.ReadView();
        Assert.True(held.HasReadingHold);
        Assert.Equal("The farm needs six wood.", held.Dialogue!.Text);
        Assert.Equal(Id(11), held.Dialogue.SpeakerActorId);
        Assert.Equal(0L, held.Actors[0].Feet.X);
        Assert.Equal(genesis.Calendar.AddMilliseconds(1800), held.Calendar);
        await Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(held.Calendar, session.ReadView().Calendar);
        Assert.Equal(1UL, authority.ReadStateForHost().AdvancingSteps);
        var close = new WorldActionCommand(Id(202), Delivery.PrincipalId, Delivery.SessionId, Id(10), held.WorldId,
            held.BranchId, held.Actors[0].PresenceRevision, new CloseDialogueIntent(held.Dialogue.DialogueId));
        Task<WorldActionReceipt> closing = session.SubmitAction(close);
        await Tick(time);
        Assert.Equal(CommandReceiptStatus.Applied, (await closing).Status);
        Assert.False(session.ReadView().HasReadingHold);
        Assert.Null(session.ReadView().Dialogue);
        Assert.Equal(held.Calendar, session.ReadView().Calendar);
        await Tick(time);
        Assert.Equal(held.Calendar.AddMilliseconds(1800), session.ReadView().Calendar);
        Assert.Equal(0L, session.ReadView().Actors[0].Feet.X);
    }

    [Fact]
    public async Task Occupied_command_identity_is_rejected_without_stalling_clock_or_host_npc()
    {
        using var files = new SessionFiles();
        WorldSnapshot genesis = WithAmblingNpc(Genesis());
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, Id(10), Delivery, time);
        session.SetReady(true);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(Give(genesis, command: 11)));
        await Tick(time);
        Assert.Equal(genesis.Calendar.AddMilliseconds(1800), session.ReadView().Calendar);
        Assert.NotEqual(genesis.Space.GetActor(Id(11)).Feet, session.ReadView().Actors.Single(actor => actor.ActorId == Id(11)).Feet);
        Assert.False(session.Completion.IsCompleted);
        Assert.Equal(0UL, authority.InspectForHost().Journal.CommandOutcomeCount);
    }

    [Fact]
    public async Task Predictable_result_collision_faults_only_that_request_and_commits_an_unrelated_step()
    {
        using var files = new SessionFiles();
        WorldSnapshot original = WithAmblingNpc(Genesis());
        WorldActionCommand action = Give(original);
        WorldSnapshot genesis = WithOccupiedAppliedResult(original, action);
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, Id(10), Delivery, time);
        session.SetReady(true);
        Task<WorldActionReceipt> pending = session.SubmitAction(action);
        await Tick(time);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => pending);
        Assert.False(session.Completion.IsCompleted);
        Assert.Equal(1UL, authority.ReadStateForHost().AdvancingSteps);
        Assert.Equal(6UL, session.ReadView().PlayerInventory.Quantity);
        Assert.Equal(0UL, authority.InspectForHost().Journal.CommandOutcomeCount);
        Assert.NotEqual(genesis.Space.GetActor(Id(11)).Feet, session.ReadView().Actors.Single(actor => actor.ActorId == Id(11)).Feet);
    }

    [Fact]
    public async Task Stopping_cancels_uncommitted_input_and_closes_the_owned_authority()
    {
        using var files = new SessionFiles();
        WorldSnapshot genesis = Genesis();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var time = new ManualSessionTimeProvider();
        var session = new LocalWorldSession(authority, Id(10), Delivery, time);
        session.SetReady(true);
        Task<WorldActionReceipt> pending = session.SubmitAction(Give(genesis));
        await session.StopAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(session.Completion.IsCompletedSuccessfully);
        Assert.Throws<ObjectDisposedException>(() => authority.ReadStateForHost());
        Assert.Throws<ObjectDisposedException>(() => session.SetReady(true));
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(genesis.Calendar, reopened.ReadStateForHost().Calendar);
        Assert.Null(reopened.FindReceipt(Give(genesis), Delivery));
        await session.DisposeAsync();
    }

    [Fact]
    public async Task Backend_failure_faults_completion_and_pending_task_without_a_gameplay_receipt()
    {
        using var files = new SessionFiles();
        WorldSnapshot genesis = Genesis();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var time = new ManualSessionTimeProvider();
        var session = new LocalWorldSession(authority, Id(10), Delivery, time);
        session.SetReady(true);
        Task<WorldActionReceipt> pending = session.SubmitAction(Give(genesis));
        await Until(() => time.HasPendingTimer);
        // Deliberate backend fault injection; normal callers transfer exclusive ownership to the session.
        authority.Dispose();
        time.Advance(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.Completion);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.StopAsync());
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Null(reopened.FindReceipt(Give(genesis), Delivery));
        Assert.Equal(genesis.Calendar, reopened.ReadStateForHost().Calendar);
    }

    [Fact]
    public async Task Player_projection_is_read_only_and_session_never_accepts_another_actors_claims()
    {
        using var files = new SessionFiles();
        WorldSnapshot genesis = Genesis();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, Id(10), Delivery, time);
        LocalWorldView view = session.ReadView();
        Assert.Equal(Id(10), view.PlayerActorId);
        Assert.Equal(Id(60), view.PlayerInventory.CustodianId);
        Assert.Single(view.PlayerInventory.Lots);
        Assert.Equal(2, view.Actors.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<LocalActorView>)view.Actors).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<LocalItemLotView>)view.PlayerInventory.Lots).Clear());
        session.SetReady(true);
        var foreign = new WorldActionCommand(Id(202), Id(101), Id(111), Id(11), genesis.WorldId, genesis.BranchId, 0,
            new SpeakToActorIntent(Id(10), 0));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(foreign));
        var wrongSession = new WorldActionCommand(Id(203), Delivery.PrincipalId, Id(999), Id(10), genesis.WorldId,
            genesis.BranchId, 0, new SpeakToActorIntent(Id(11), 0));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(wrongSession));
        foreach ((long x, long y) in new (long, long)[] { (1, 0), (32, 32), (long.MinValue, 0) })
        {
            Assert.Throws<ArgumentException>(() => session.SetMovement(x, y));
        }
    }
}
