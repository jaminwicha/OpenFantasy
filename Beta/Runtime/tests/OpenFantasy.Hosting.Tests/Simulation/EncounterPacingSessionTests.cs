using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class EncounterPacingSessionTests
{
    [Fact]
    public async Task Queued_Wait_is_receipted_before_resolution_and_restart_retains_the_required_decision_barrier()
    {
        using var files = new StorageFiles();
        var fixture = new EncounterPacingWorldFixture();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var time = new ManualSessionTimeProvider();
        var session = new LocalWorldSession(authority, fixture.Player, fixture.Delivery, time);
        WorldActionCommand command;
        WorldSnapshot queuedState;
        await using (session)
        {
            Assert.True(session.ReadView().IsInEncounter);
            Assert.False(session.ReadView().HasReadingHold);
            await Tick(time, TimeSpan.FromHours(1));
            Assert.Equal(0UL, authority.ReadStateForHost().Revision);
            session.SetReady(true);
            session.SetMovement(32, 0);
            await Tick(time); // The host retains its automatic choice but cannot cross the human barrier.
            var waiting = authority.ReadStateForHost();
            Assert.Equal(LocalWorldRunState.Waiting, session.ReadRunState());
            Assert.Single(waiting.Pacing!.Encounter!.Decisions);
            Assert.Equal(fixture.Genesis.Calendar, waiting.Calendar);
            await Tick(time, TimeSpan.FromHours(1));
            Assert.Equal(waiting.ContentHash, authority.ReadStateForHost().ContentHash);
            command = fixture.Wait(waiting, fixture.Player);
            Task<WorldActionReceipt> pending = session.SubmitAction(command);
            Task<WorldActionReceipt> duplicate = session.SubmitAction(command);
            await Tick(time);
            var receipt = await pending;
            Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
            Assert.Equal(receipt.ResultId, (await duplicate).ResultId);
            queuedState = authority.ReadStateForHost();
            Assert.Equal(2, queuedState.Pacing!.Encounter!.Decisions.Count);
            Assert.All(queuedState.Pacing.Encounter.Actors, actor => Assert.Equal(0UL, actor.RemainingDelay));
            Assert.Equal(fixture.Genesis.Calendar, queuedState.Calendar);
            Assert.Equal(2UL, authority.InspectForHost().Journal.CommandOutcomeCount);
            session.SetReady(false);
            await Tick(time, TimeSpan.FromHours(1));
            Assert.Equal(queuedState.ContentHash, authority.ReadStateForHost().ContentHash);
            Assert.Equal(receipt.ResultId, (await session.SubmitAction(command)).ResultId);
        }

        var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        var restoredTime = new ManualSessionTimeProvider();
        await using var restored = new LocalWorldSession(reopened, fixture.Player, fixture.Delivery, restoredTime);
        await Tick(restoredTime, TimeSpan.FromHours(1));
        Assert.Equal(queuedState.ContentHash, reopened.ReadStateForHost().ContentHash);
        restored.SetReady(true);
        for (int actor = 0; actor < fixture.Genesis.Residents.Count; actor++)
        {
            await Tick(restoredTime);
            Assert.Equal(fixture.Genesis.Calendar, restored.ReadView().Calendar);
        }
        Assert.Empty(reopened.ReadStateForHost().Pacing!.Encounter!.Decisions);
        Assert.All(reopened.ReadStateForHost().Pacing!.Encounter!.Actors, actor => Assert.True(actor.RemainingDelay > 0));
        await Tick(restoredTime);
        WorldSnapshot advanced = reopened.ReadStateForHost();
        Assert.Equal(fixture.Genesis.Calendar.AddMilliseconds(50), advanced.Calendar);
        Assert.Equal(0UL, advanced.Pacing!.ProcessIntervals);
        Assert.Equal(fixture.Genesis.LiquidCells!.ContentHash, advanced.LiquidCells!.ContentHash);
        Assert.Equal(fixture.Genesis.Space.Actors.Select(actor => actor.Feet), advanced.Space.Actors.Select(actor => actor.Feet));
        Assert.Equal(fixture.Genesis.Construction!.ContentHash, advanced.Construction!.ContentHash);
        Assert.Equal(fixture.Genesis.Inventory.ContentHash, advanced.Inventory.ContentHash);
        Assert.Equal(2UL, reopened.InspectForHost().Journal.CommandOutcomeCount);
        Assert.Equal(CommandReceiptStatus.Applied, (await restored.SubmitAction(command)).Status);
    }

    [Fact]
    public async Task Active_encounter_refuses_ordinary_actions_and_stale_choices_without_exploration_fallback()
    {
        using var files = new StorageFiles();
        var fixture = new EncounterPacingWorldFixture();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, fixture.Player, fixture.Delivery, time);
        session.SetReady(true);
        var speaker = fixture.Genesis.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Host);
        var ordinary = new WorldActionCommand(fixture.NextId(), fixture.Delivery.PrincipalId, fixture.Delivery.SessionId,
            fixture.Player, fixture.Genesis.WorldId, fixture.Genesis.BranchId, 0,
            new SpeakToActorIntent(speaker.ActorId, fixture.Genesis.Space.GetActor(speaker.ActorId).Revision));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(ordinary));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(fixture.Wait(fixture.Genesis, speaker.ActorId)));
        WorldActionCommand selected = fixture.Wait(fixture.Genesis, fixture.Player);
        var wrongSession = new WorldActionCommand(selected.CommandId, selected.PrincipalId, fixture.NextId(),
            selected.ActorId, selected.WorldId, selected.BranchId, selected.ExpectedActorRevision, selected.Intent);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(wrongSession));
        Assert.Equal(fixture.Genesis.ContentHash, authority.ReadStateForHost().ContentHash);
        var stale = fixture.Wait(fixture.Genesis, fixture.Player, timingRevision: 99);
        Task<WorldActionReceipt> pending = session.SubmitAction(stale);
        await Tick(time);
        Assert.NotEqual(CommandReceiptStatus.Applied, (await pending).Status);
        var state = authority.ReadStateForHost();
        Assert.Equal(fixture.Genesis.Calendar, state.Calendar);
        Assert.Equal(0UL, state.AdvancingSteps);
        Assert.Equal(fixture.Genesis.LiquidCells!.ContentHash, state.LiquidCells!.ContentHash);
        Assert.Equal(fixture.Genesis.Construction!.ContentHash, state.Construction!.ContentHash);
        Assert.DoesNotContain(state.Pacing!.Encounter!.Decisions, decision => decision.ActorId == fixture.Player);
        Assert.False(session.Completion.IsCompleted);
        await Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(state.ContentHash, authority.ReadStateForHost().ContentHash);
    }
}
