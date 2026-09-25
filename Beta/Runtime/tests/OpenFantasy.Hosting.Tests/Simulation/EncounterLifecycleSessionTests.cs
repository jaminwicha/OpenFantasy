using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class EncounterLifecycleSessionTests
{
    [Fact]
    public async Task Entry_survives_unrelated_clock_ticks_and_restart_stages_only_when_ready_without_time_or_effect_grants()
    {
        using var files = new StorageFiles();
        var fixture = new EncounterJourneyWorldFixture();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var time = new ManualSessionTimeProvider();
        WorldSnapshot confrontation;
        var entry = fixture.Enter(fixture.Genesis, fixture.FirstScenario);
        WorldActionReceipt receipt;
        await using (var session = new LocalWorldSession(authority, fixture.Player, fixture.Delivery, time))
        {
            Assert.False(session.ReadView().IsInEncounter);
            await Tick(time, TimeSpan.FromHours(1));
            Assert.Equal(fixture.Genesis.ContentHash, authority.ReadStateForHost().ContentHash);
            var wrongOrigin = new WorldActionCommand(entry.CommandId, entry.PrincipalId, fixture.NextId(), entry.ActorId,
                entry.WorldId, entry.BranchId, entry.ExpectedActorRevision, entry.Intent);
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(wrongOrigin));
            session.SetReady(true);
            await Tick(time); // Entry captures count and actor revisions; this unrelated clock tick does not stale it.
            var explored = authority.ReadStateForHost();
            Assert.Equal(1UL, explored.EncounterLifecycle!.ExplorationSteps);
            Task<WorldActionReceipt> pending = session.SubmitAction(entry);
            Task<WorldActionReceipt> duplicate = session.SubmitAction(entry);
            await Tick(time);
            receipt = await pending;
            Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(await duplicate));
            confrontation = authority.ReadStateForHost();
            Assert.Equal(EncounterLifecyclePhase.Confrontation, confrontation.EncounterLifecycle!.Phase);
            Assert.Equal(explored.Calendar, confrontation.Calendar);
            Assert.Equal(explored.Progression!.ContentHash, confrontation.Progression!.ContentHash);
            Assert.True(session.ReadView().IsInEncounter);
            Assert.Equal(1UL, authority.InspectForHost().Journal.CommandOutcomeCount);
            session.SetReady(false);
            await Tick(time, TimeSpan.FromHours(1));
            Assert.Equal(confrontation.ContentHash, authority.ReadStateForHost().ContentHash);
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(await session.SubmitAction(entry)));
        }
        var restored = SqliteWorldAuthority.OpenExisting(files.Path);
        var clock = new ManualSessionTimeProvider();
        await using var resumed = new LocalWorldSession(restored, fixture.Player, fixture.Delivery, clock);
        await Tick(clock, TimeSpan.FromHours(1));
        Assert.Equal(WorldSnapshotCodec.Encode(confrontation), WorldSnapshotCodec.Encode(restored.ReadStateForHost()));
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(await resumed.SubmitAction(entry)));
        var ordinary = fixture.Command(confrontation, fixture.Player,
            new SpeakToActorIntent(fixture.Wren, confrontation.Space.GetActor(fixture.Wren).Revision));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => resumed.SubmitAction(ordinary));
        resumed.SetReady(true);
        Task<WorldActionReceipt> premature = resumed.SubmitAction(fixture.Release(confrontation));
        await Tick(clock);
        Assert.Equal(WorldActionRejection.Unavailable, (await premature).Rejection);
        Assert.Equal(EncounterLifecyclePhase.Confrontation, restored.ReadStateForHost().EncounterLifecycle!.Phase);
        Assert.Equal(confrontation.Calendar, restored.ReadStateForHost().Calendar);
        await Tick(clock);
        Assert.Equal(EncounterLifecyclePhase.Staging, restored.ReadStateForHost().EncounterLifecycle!.Phase);
        await Tick(clock);
        var active = restored.ReadStateForHost();
        Assert.Equal(EncounterLifecyclePhase.Active, active.EncounterLifecycle!.Phase);
        Assert.Equal(confrontation.Calendar, active.Calendar);
        Assert.Equal(confrontation.Inventory.ContentHash, active.Inventory.ContentHash);
        Assert.Equal(confrontation.Progression!.ContentHash, active.Progression!.ContentHash);
        Assert.Equal(confrontation.LiquidCells!.ContentHash, active.LiquidCells!.ContentHash);
        Assert.Equal(confrontation.Space.Actors.Select(actor => (actor.ActorId, actor.Feet)), active.Space.Actors.Select(actor => (actor.ActorId, actor.Feet)));
        Assert.All(active.EncounterLifecycle!.Debts, debt => Assert.Equal(0UL, debt.RemainingDelay));
        resumed.SetMovement(32, 0);
        await Tick(clock);
        var waiting = restored.ReadStateForHost();
        var chosen = Assert.Single(waiting.EncounterActions!.Pending);
        Assert.Equal(fixture.WolfFirst, chosen.ActorId);
        var attack = Assert.IsType<QueueEncounterAbilityIntent>(chosen.Intent);
        Assert.Equal(fixture.Player, attack.TargetActorId);
        Assert.Equal(WorldActionRejection.None, WorldEncounterActions.EvaluateAbility(waiting, chosen.ActorId, attack));
        Assert.DoesNotContain(waiting.Pacing!.Encounter!.Decisions, choice => choice.ActorId == fixture.Wren || choice.ActorId == fixture.WolfSecond);
        Assert.True(WorldEncounterActions.HasRequiredDecisions(waiting));
        Assert.Equal(LocalWorldRunState.Waiting, resumed.ReadRunState());
        await Tick(clock, TimeSpan.FromHours(1));
        Assert.Equal(waiting.ContentHash, restored.ReadStateForHost().ContentHash);
        Assert.Equal(confrontation.Calendar, waiting.Calendar);
        EncounterLifecycleWorldTests.AssertClock(waiting);
        Assert.False(resumed.Completion.IsCompleted);
    }
}
