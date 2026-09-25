using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class EncounterCombatSessionTests
{
    [Fact]
    public async Task Automatic_wolf_choice_waits_for_player_and_queued_attack_restarts_without_early_damage_or_catchup()
    {
        using var files = new StorageFiles();
        var fixture = new EncounterCombatWorldFixture();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var time = new ManualSessionTimeProvider();
        WorldActionCommand playerCommand;
        WorldActionReceipt originalReceipt;
        WorldSnapshot queued;
        await using (var session = new LocalWorldSession(authority, fixture.Player, fixture.Delivery, time))
        {
            await Tick(time, TimeSpan.FromHours(1));
            Assert.Equal(fixture.Genesis.ContentHash, authority.ReadStateForHost().ContentHash);
            session.SetReady(true);
            session.SetMovement(32, 0);
            await Tick(time);
            var waiting = authority.ReadStateForHost();
            var wolfCommand = Assert.Single(waiting.EncounterActions!.Pending);
            Assert.Equal(fixture.Wolf, wolfCommand.ActorId);
            var intent = Assert.IsType<QueueEncounterAbilityIntent>(wolfCommand.Intent);
            Assert.Equal(fixture.Bite, intent.AbilityDefinitionId);
            Assert.Equal(fixture.Player, intent.TargetActorId);
            Assert.Equal(WorldActionRejection.None, WorldEncounterActions.EvaluateAbility(waiting, fixture.Wolf, intent));
            Assert.Equal(LocalWorldRunState.Waiting, session.ReadRunState());
            Assert.DoesNotContain(waiting.Pacing!.Encounter!.Decisions, decision => decision.ActorId == fixture.Wren);
            Assert.Equal(fixture.Genesis.Progression!.ContentHash, waiting.Progression!.ContentHash);
            await Tick(time, TimeSpan.FromHours(1));
            Assert.Equal(waiting.ContentHash, authority.ReadStateForHost().ContentHash);

            playerCommand = fixture.Attack(waiting, fixture.Player, fixture.Wolf);
            Task<WorldActionReceipt> submitted = session.SubmitAction(playerCommand);
            Task<WorldActionReceipt> duplicate = session.SubmitAction(playerCommand);
            await Tick(time);
            originalReceipt = await submitted;
            Assert.Equal(CommandReceiptStatus.Applied, originalReceipt.Status);
            Assert.Equal(WorldReceiptCodecV1.Encode(originalReceipt), WorldReceiptCodecV1.Encode(await duplicate));
            queued = authority.ReadStateForHost();
            Assert.Equal(2, queued.EncounterActions!.Pending.Count);
            Assert.Equal(2, queued.Pacing!.Encounter!.Decisions.Count);
            Assert.Equal(fixture.Genesis.Progression.ContentHash, queued.Progression!.ContentHash);
            Assert.Equal(fixture.Genesis.Calendar, queued.Calendar);
            Assert.Equal(0UL, queued.EncounterActions.Random.NextCounter);
            Assert.Equal(2UL, authority.InspectForHost().Journal.CommandOutcomeCount);
            session.SetReady(false);
            await Tick(time, TimeSpan.FromHours(1));
            Assert.Equal(queued.ContentHash, authority.ReadStateForHost().ContentHash);
            Assert.Equal(WorldReceiptCodecV1.Encode(originalReceipt),
                WorldReceiptCodecV1.Encode(await session.SubmitAction(playerCommand)));
        }

        var restored = SqliteWorldAuthority.OpenExisting(files.Path);
        var clock = new ManualSessionTimeProvider();
        await using var reopened = new LocalWorldSession(restored, fixture.Player, fixture.Delivery, clock);
        await Tick(clock, TimeSpan.FromHours(1));
        Assert.Equal(WorldSnapshotCodec.Encode(queued), WorldSnapshotCodec.Encode(restored.ReadStateForHost()));
        Assert.Equal(WorldReceiptCodecV1.Encode(originalReceipt),
            WorldReceiptCodecV1.Encode(await reopened.SubmitAction(playerCommand)));
        reopened.SetReady(true);
        for (int resolution = 0; resolution < 2; resolution++)
        {
            var before = restored.ReadStateForHost();
            await Tick(clock);
            var after = restored.ReadStateForHost();
            var outcome = after.EncounterActions!.LastResolution!;
            Assert.Equal(EncounterActionResolutionStatus.Damaged, outcome.Status);
            Assert.Equal(after.Revision, outcome.WorldSequence);
            Assert.Equal(before.Calendar, after.Calendar);
            var random = before.EncounterActions!.CreateRandom();
            Assert.Equal(900U + (uint)random.NextUInt64(201), outcome.VariationPermille);
            Assert.Equal(random.CaptureState(), after.EncounterActions.Random);
            Assert.Equal(before.EncounterActions.Pending.Count - 1, after.EncounterActions.Pending.Count);
            Assert.True(after.Pacing!.Encounter!.GetActor(outcome.ActorId).RemainingDelay > 0);
            Assert.Equal(2UL, restored.InspectForHost().Journal.CommandOutcomeCount);
        }
        Assert.Empty(restored.ReadStateForHost().EncounterActions!.Pending);
        await Tick(clock);
        var advanced = restored.ReadStateForHost();
        Assert.Equal(fixture.Genesis.Calendar.AddMilliseconds(50), advanced.Calendar);
        Assert.Equal(1UL, advanced.AdvancingSteps);
        Assert.Equal(0UL, advanced.ProcessIntervals);
        Assert.Equal(fixture.Genesis.LiquidCells!.ContentHash, advanced.LiquidCells!.ContentHash);
        Assert.Equal(fixture.Genesis.Inventory.ContentHash, advanced.Inventory.ContentHash);
        Assert.Equal(fixture.Genesis.Construction!.ContentHash, advanced.Construction!.ContentHash);
        Assert.Equal(fixture.Genesis.Excavation!.ContentHash, advanced.Excavation!.ContentHash);
        Assert.Equal(fixture.Genesis.Space.Actors.Select(actor => actor.Feet), advanced.Space.Actors.Select(actor => actor.Feet));
        Assert.Equal(fixture.Genesis.Progression.GetActor(fixture.Wren).CurrentHp, advanced.Progression!.GetActor(fixture.Wren).CurrentHp);
        Assert.False(reopened.Completion.IsCompleted);
    }

    [Fact]
    public async Task Stale_and_unauthenticated_choices_cannot_replace_the_human_barrier_and_Wait_incurred_debt_has_no_draw()
    {
        using var files = new StorageFiles();
        var fixture = new EncounterCombatWorldFixture();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, fixture.Player, fixture.Delivery, time);
        session.SetReady(true);
        var command = fixture.Attack(fixture.Genesis, fixture.Player, fixture.Wolf);
        var wrongOrigin = new WorldActionCommand(command.CommandId, command.PrincipalId, fixture.NextId(), command.ActorId,
            command.WorldId, command.BranchId, command.ExpectedActorRevision, command.Intent);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(wrongOrigin));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(fixture.Attack(fixture.Genesis, fixture.Wolf, fixture.Player)));
        var ordinary = new WorldActionCommand(fixture.NextId(), command.PrincipalId, command.OriginSessionId, command.ActorId,
            command.WorldId, command.BranchId, command.ExpectedActorRevision,
            new SpeakToActorIntent(fixture.Wren, fixture.Genesis.Space.GetActor(fixture.Wren).Revision));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(ordinary));
        Assert.Equal(fixture.Genesis.ContentHash, authority.ReadStateForHost().ContentHash);
        Task<WorldActionReceipt> stale = session.SubmitAction(fixture.Attack(fixture.Genesis, fixture.Player, fixture.Wolf, timingRevision: 99));
        await Tick(time);
        Assert.Equal(WorldActionRejection.RevisionConflict, (await stale).Rejection);
        var waiting = authority.ReadStateForHost();
        Assert.True(WorldEncounterActions.HasRequiredDecisions(waiting));
        Assert.Equal(fixture.Genesis.Progression!.ContentHash, waiting.Progression!.ContentHash);
        Assert.Equal(fixture.Genesis.Calendar, waiting.Calendar);
        await Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(waiting.ContentHash, authority.ReadStateForHost().ContentHash);

        Task<WorldActionReceipt> wait = session.SubmitAction(fixture.Wait(waiting, fixture.Player));
        await Tick(time);
        Assert.Equal(CommandReceiptStatus.Applied, (await wait).Status);
        bool confirmedWait = false;
        for (int boundary = 0; boundary < 2; boundary++)
        {
            var before = authority.ReadStateForHost();
            await Tick(time);
            var after = authority.ReadStateForHost();
            var result = after.EncounterActions!.LastResolution!;
            if (result.ActorId != fixture.Player) continue;
            confirmedWait = true;
            Assert.Equal(EncounterActionResolutionStatus.Wait, result.Status);
            Assert.Equal(before.Progression!.ContentHash, after.Progression!.ContentHash);
            Assert.Equal(before.EncounterActions!.Random, after.EncounterActions.Random);
            Assert.Equal(before.Pacing!.Encounter!.GetActor(fixture.Player).StandardDelay, result.Delay);
            Assert.True(result.Delay > 0);
            Assert.Equal(result.Delay, after.Pacing!.Encounter!.GetActor(fixture.Player).RemainingDelay);
            Assert.Equal(before.Calendar, after.Calendar);
        }
        Assert.True(confirmedWait);
        Assert.Equal(0UL, authority.ReadStateForHost().AdvancingSteps);
        Assert.False(session.Completion.IsCompleted);
    }
}
