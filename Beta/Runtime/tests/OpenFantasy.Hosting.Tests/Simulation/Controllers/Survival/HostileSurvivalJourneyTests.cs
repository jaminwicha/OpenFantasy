using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Worlds.Controllers;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.Controllers.Survival;

public sealed partial class HostileSurvivalJourneyTests
{
    [Fact]
    public async Task An_attacking_npc_holds_for_a_connected_human_without_wall_clock_takeover()
    {
        await using var fixture = await HostileSurvivalFixture.Open();
        var joined = await fixture.Join();
        await using var observer = await fixture.ConnectObserver();
        await fixture.EarnBiteAndHumanDecision(joined.Channel.Player);
        // Publish any due NPC choice first. The uncommitted human decision still owns Full Wait.
        await fixture.Tick();
        await fixture.Tick();
        WorldSnapshot held = fixture.State;
        Assert.True(WorldEncounterActions.HasRequiredDecisions(held));
        Assert.Contains(fixture.Resolutions, row => row.ActorId == fixture.Opponent
            && row.TargetActorId == fixture.Actor && row.Status == EncounterActionResolutionStatus.Damaged);
        await fixture.Tick(TimeSpan.FromHours(2));
        Assert.Equal(held.ContentHash, fixture.State.ContentHash);
        Assert.Equal(WorldControllerMode.Human, fixture.Control.Mode);
        Assert.Null(fixture.Control.DisconnectId);
        Assert.Null(fixture.State.Retreat!.Active);
        Assert.Equal(0UL, fixture.State.Retreat.AcceptedCount);
        Assert.Equal(LocalWorldRunState.Waiting, joined.Channel.Player.ReadRunState());
    }

    [Fact]
    public async Task Transport_loss_earns_a_paid_autonomous_escape_then_restores_unready_control_and_replays_exactly()
    {
        await using var fixture = await HostileSurvivalFixture.Open();
        var preparation = await PrepareFirstPaidSample(fixture);
        await using var observer = preparation.Observer;
        var accepted = preparation.Accepted;
        var retreatReceipt = preparation.Receipt;
        var allowance = preparation.Allowance;
        var disconnect = preparation.Disconnect;
        var initialBite = preparation.InitialBite;
        ulong acceptanceRevision = preparation.AcceptanceRevision;
        ulong sampleCharge = preparation.SampleCharge;
        List<SubunitPosition> walked = [accepted.Origin, fixture.State.Space.GetActor(fixture.Actor).Feet];

        // The observer made fresh selection possible. Returning-only continuation must finish the
        // already accepted route with no ready port and without selecting another human action.
        observer.SetReady(false);
        var returning = await fixture.Join();
        var port = returning.Channel.Player;
        var pendingControl = port.ReadControlStatus();
        Assert.Equal(PlayerControlState.Pending, pendingControl.State);
        Assert.Equal(fixture.Actor, pendingControl.ActorId);
        Assert.False(port.ControlAvailable);
        Assert.False(WorldControllerBoundaries.CanRestoreHuman(fixture.State, fixture.Actor));
        Assert.Equal(WorldControllerMode.Autonomous, fixture.Control.Mode);
        port.SetReady(true);
        port.SetMovement(32, 0);
        Assert.False(port.Ready);
        Assert.Throws<InvalidOperationException>(() => port.Resume(pendingControl));
        Assert.Equal(0L, port.MovementX);
        Assert.Equal(0L, port.MovementY);
        Assert.Equal(WorldReceiptCodecV1.Encode(retreatReceipt),
            WorldReceiptCodecV1.Encode(await port.SubmitAction(accepted.Command)));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => port.SubmitAction(fixture.Commands.Wait(fixture.State, fixture.Actor)));

        for (int cutoff = 0; fixture.Control.Mode != WorldControllerMode.Human; cutoff++)
        {
            Assert.True(cutoff < 600, "The accepted retreat must complete its bounded physical route before control returns.");
            Assert.False(observer.Ready);
            Assert.False(port.Ready);
            await fixture.Tick();
            var feet = fixture.State.Space.GetActor(fixture.Actor).Feet;
            if (feet != walked[^1]) walked.Add(feet);
            Assert.Equal(1UL, fixture.State.Retreat!.AcceptedCount);
        }

        WorldSnapshot restored = fixture.State;
        var outcome = Assert.IsType<EncounterRetreatOutcome>(restored.Retreat!.Last);
        Assert.Equal(EncounterRetreatStatus.Disengaged, outcome.Status);
        Assert.Equal(EncounterRetreatReason.None, outcome.Reason);
        Assert.Equal((fixture.Actor, accepted.Command.CommandId, accepted.EncounterId),
            (outcome.ActorId, outcome.CommandId, outcome.EncounterId));
        Assert.Equal(accepted.Intent.Route.Count, outcome.SamplesTaken);
        Assert.Equal(accepted.Intent.Route.Count, walked.Count - 1);
        Assert.All(walked.Select((feet, index) => (Feet: feet, Index: index)), row =>
            Assert.Equal(QueueEncounterRetreatIntent.Endpoint(accepted.Origin, accepted.Intent.Route.Take(row.Index)), row.Feet));
        Assert.Equal(sampleCharge * (ulong)outcome.SamplesTaken, outcome.ReadinessCharged);
        Assert.Equal(sampleCharge, restored.EncounterLifecycle!.GetDebt(fixture.Actor).RemainingDelay);
        Assert.False(WorldActorAvailability.CanAct(restored, fixture.Actor));
        Assert.Equal(EncounterLifecyclePhase.Exploration, restored.EncounterLifecycle.Phase);
        Assert.True(restored.EncounterLifecycle.TacticalTicks > allowance.EncounterLifecycle!.TacticalTicks);
        Assert.Null(restored.Pacing!.Encounter);
        Assert.Null(restored.Retreat.Active);
        Assert.Equal(accepted.Intent.Destination, restored.Space.GetActor(fixture.Actor).Feet);
        Assert.Equal(3UL, fixture.Control.Revision);
        Assert.Null(fixture.Control.DisconnectId);
        Assert.True(port.ControlAvailable);
        Assert.False(port.Ready);
        Assert.Equal(LocalWorldRunState.Paused, port.ReadRunState());
        Assert.Equal(0L, port.MovementX);
        Assert.Equal(0L, port.MovementY);
        Assert.Equal(CustodyEncodingV1.Encode(allowance.Inventory.Custody), CustodyEncodingV1.Encode(restored.Inventory.Custody));
        Assert.Equal(allowance.Inventory.Controls, restored.Inventory.Controls);
        Assert.Equal(allowance.Progression!.GetActor(fixture.Opponent).CurrentHp, restored.Progression!.GetActor(fixture.Opponent).CurrentHp);
        Assert.InRange(restored.Progression.GetActor(fixture.Actor).CurrentHp, 1, allowance.Progression.GetActor(fixture.Actor).CurrentHp);
        Assert.Equal(allowance.Progression.GetActor(fixture.Actor).CurrentMp, restored.Progression.GetActor(fixture.Actor).CurrentMp);
        Assert.Equal(fixture.Genesis.Progression!.GetActor(fixture.Actor).CurrentHp
            - fixture.Resolutions.Where(row => row.TargetActorId == fixture.Actor).Sum(row => (int)row.Damage),
            restored.Progression.GetActor(fixture.Actor).CurrentHp);

        await fixture.Tick(TimeSpan.FromMinutes(1));
        Assert.Equal(restored.ContentHash, fixture.State.ContentHash);
        var restoredControl = port.ReadControlStatus();
        Assert.Equal(PlayerControlState.Restored, restoredControl.State);
        Assert.Equal(pendingControl.PortId, restoredControl.PortId);
        Assert.Equal(fixture.Actor, restoredControl.ActorId);
        Assert.True(restoredControl.Generation > pendingControl.Generation);
        Assert.NotEqual(pendingControl, restoredControl);
        Assert.Throws<InvalidOperationException>(() => port.Resume(pendingControl));
        Assert.False(port.Ready);
        Assert.Equal(restored.ContentHash, fixture.State.ContentHash);
        port.Resume(restoredControl);
        Assert.True(port.Ready);
        Assert.Equal(PlayerControlState.Available, port.ReadControlStatus().State);
        Assert.Equal(LocalWorldRunState.Running, port.ReadRunState());
        port.SetReady(false);
        Assert.Equal(restored.ContentHash, fixture.State.ContentHash);

        await fixture.Host.StopAsync();
        AssertJournalAndRecovery(fixture, accepted, retreatReceipt, disconnect, initialBite, acceptanceRevision, restored);
    }

    private static async Task<EscapePreparation> PrepareFirstPaidSample(HostileSurvivalFixture fixture)
    {
        var joined = await fixture.Join();
        var observer = await fixture.ConnectObserver();
        await fixture.EarnBiteAndHumanDecision(joined.Channel.Player);
        Assert.NotEmpty(fixture.Waits);
        var initialBite = fixture.Resolutions.First(row => row.ActorId == fixture.Opponent
            && row.Status == EncounterActionResolutionStatus.Damaged);
        Assert.Equal(fixture.Actor, Assert.IsType<EntityId>(initialBite.TargetActorId));
        Assert.True(initialBite.Damage > 0);
        Assert.True(initialBite.Delay > 0);
        Assert.InRange(fixture.State.Progression!.GetActor(fixture.Actor).CurrentHp, 1,
            fixture.Genesis.Progression!.GetActor(fixture.Actor).CurrentHp - 1);

        WorldSnapshot beforeLoss = fixture.State;
        await fixture.LoseTransport(joined);
        long confirmedAt = fixture.Time.GetTimestamp();
        EntityId disconnect = Assert.IsType<EntityId>(fixture.Control.DisconnectId);
        ControllerJournalTests.AssertGameplayUnchanged(beforeLoss, fixture.State);
        Assert.True(observer.Ready);
        Assert.Equal(WorldControllerMode.Human, fixture.State.Controllers!.Get(fixture.Observer).Mode);

        // A possible due NPC command may be queued, but no human decision or tactical time is earned.
        await fixture.Tick(TimeSpan.FromMilliseconds(29_950));
        WorldSnapshot allowance = fixture.State;
        fixture.Time.Advance(TimeSpan.FromMilliseconds(49));
        Assert.Equal(TimeSpan.FromMilliseconds(29_999), fixture.Time.GetElapsedTime(confirmedAt, fixture.Time.GetTimestamp()));
        Assert.Equal(allowance.ContentHash, fixture.State.ContentHash);
        Assert.Equal(WorldControllerMode.ReconnectAllowance, fixture.Control.Mode);
        Assert.Null(allowance.Retreat!.Active);
        Assert.Equal(beforeLoss.EncounterLifecycle!.TacticalTicks, allowance.EncounterLifecycle!.TacticalTicks);
        Assert.Equal(beforeLoss.Progression!.ContentHash, allowance.Progression!.ContentHash);

        var advice = WorldEncounterRetreatAdvisor.Advise(allowance, fixture.Actor);
        Assert.True(advice.IsFound);
        await fixture.Tick(TimeSpan.FromMilliseconds(1));
        Assert.Equal(TimeSpan.FromSeconds(30), fixture.Time.GetElapsedTime(confirmedAt, fixture.Time.GetTimestamp()));
        Assert.Equal(WorldControllerMode.Autonomous, fixture.Control.Mode);
        Assert.Equal(disconnect, fixture.Control.DisconnectId);
        await fixture.Until(() => fixture.State.Retreat!.Active is not null, 4, "ordinary autonomous retreat acceptance");
        var accepted = Assert.IsType<EncounterRetreatContinuation>(fixture.State.Retreat!.Active);
        Assert.Equal(fixture.Actor, accepted.ActorId);
        Assert.Equal(advice.Destination, accepted.Intent.Destination);
        Assert.Equal(advice.Route.ToArray(), accepted.Intent.Route.ToArray());
        Assert.Equal(0, accepted.SamplesTaken);
        Assert.Equal(0UL, accepted.ReadinessCharged);
        Assert.Equal(allowance.Space.GetActor(fixture.Actor).Feet, accepted.Origin);
        Assert.Equal(allowance.Progression!.ContentHash, fixture.State.Progression!.ContentHash);
        Assert.Equal(1UL, fixture.State.Retreat.AcceptedCount);
        var retreatReceipt = Assert.IsType<WorldActionReceipt>(fixture.Authority.FindReceipt(accepted.Command, fixture.Delivery(fixture.Actor)));
        Assert.Equal(WorldActionRejection.None, retreatReceipt.Rejection);
        ulong acceptanceRevision = fixture.State.Revision;
        ulong sampleCharge = EncounterRetreatRules.SampleDelay(fixture.State.Pacing!.Encounter!.GetActor(fixture.Actor).NormalizedSpeed);
        await fixture.Until(() => fixture.State.Retreat!.Active is { SamplesTaken: > 0 }, 40, "first paid movement sample");
        var moving = Assert.IsType<EncounterRetreatContinuation>(fixture.State.Retreat!.Active);
        Assert.Equal(1, moving.SamplesTaken);
        Assert.Equal(sampleCharge, moving.ReadinessCharged);
        Assert.True(moving.RemainingSamples > 0);
        return new(observer, accepted, retreatReceipt, allowance, disconnect, initialBite, acceptanceRevision, sampleCharge);
    }

    private sealed record EscapePreparation(LocalPlayerConnection Observer, EncounterRetreatContinuation Accepted,
        WorldActionReceipt Receipt, WorldSnapshot Allowance, EntityId Disconnect, EncounterActionResolution InitialBite,
        ulong AcceptanceRevision, ulong SampleCharge);

    private static void AssertJournalAndRecovery(HostileSurvivalFixture fixture, EncounterRetreatContinuation accepted,
        WorldActionReceipt retreatReceipt, EntityId disconnect, EncounterActionResolution bite,
        ulong acceptanceRevision, WorldSnapshot expected)
    {
        WorldStepBatch[] batches;
        using (var database = WorldDatabase.Open(fixture.Path, false))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(fixture.Genesis), database.ReadGenesis());
            batches = database.ReadJournal().Select(row => WorldStepCodec.Decode(row.AcceptedBatch)).ToArray();
        }
        var disconnected = Assert.Single(batches, batch => batch.ControllerEvent?.Kind == WorldControllerEventKind.Disconnected);
        var expired = Assert.Single(batches, batch => batch.ControllerEvent?.Kind == WorldControllerEventKind.AllowanceExpired);
        var reconnected = Assert.Single(batches, batch => batch.ControllerEvent?.Kind == WorldControllerEventKind.Reconnected);
        Assert.Equal(disconnect, disconnected.CauseId);
        Assert.Equal(disconnect, expired.ControllerEvent!.DisconnectId);
        Assert.Equal(30_000UL, expired.ControllerEvent.ElapsedHostMilliseconds);
        Assert.Equal(disconnect, reconnected.ControllerEvent!.DisconnectId);
        Assert.Equal(fixture.Actor, reconnected.ControllerEvent.ActorId);
        Assert.All(new[] { disconnected, expired, reconnected }, batch => Assert.Empty(batch.Actions));
        WorldActionCommand[] commands = batches.SelectMany(batch => batch.Actions).ToArray();
        var retreat = Assert.Single(commands, command => command.Intent is QueueEncounterRetreatIntent);
        Assert.Equal(WorldActionCodec.Encode(accepted.Command), WorldActionCodec.Encode(retreat));
        var wolfAttack = Assert.Single(commands, command => command.CommandId == bite.DecisionCommandId);
        Assert.Equal(fixture.Opponent, wolfAttack.ActorId);
        Assert.Equal(fixture.Actor, Assert.IsType<QueueEncounterAbilityIntent>(wolfAttack.Intent).TargetActorId);
        Assert.DoesNotContain(commands, command => command.ActorId == fixture.Actor && command.Intent is QueueEncounterAbilityIntent);
        Assert.DoesNotContain(commands, command => command.ActorId == fixture.Observer);
        Assert.DoesNotContain(batches.Where(batch => batch.ExpectedRevision >= acceptanceRevision)
            .SelectMany(batch => batch.Actions), command => command.ActorId == fixture.Actor);

        using var reopened = SqliteWorldAuthority.OpenExisting(fixture.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        WorldSnapshot recovered = reopened.ReadStateForHost();
        Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(recovered));
        Assert.Equal(EncounterRetreatCodecV1.Encode(expected.Retreat!), EncounterRetreatCodecV1.Encode(recovered.Retreat!));
        Assert.Equal(WorldReceiptCodecV1.Encode(retreatReceipt),
            WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(reopened.FindReceipt(accepted.Command, fixture.Delivery(fixture.Actor)))));
        foreach (var wait in fixture.Waits)
            Assert.Equal(WorldReceiptCodecV1.Encode(wait.Receipt),
                WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(reopened.FindReceipt(wait.Command, fixture.Delivery(fixture.Actor)))));
        Assert.Equal(WorldActionRejection.None,
            Assert.IsType<WorldActionReceipt>(reopened.FindReceipt(wolfAttack, fixture.Delivery(fixture.Opponent))).Rejection);
        Assert.All(commands, command => Assert.True(reopened.IsIdentityOccupiedForHost(command.CommandId)));
        Assert.All(batches, batch => Assert.True(reopened.IsIdentityOccupiedForHost(batch.CauseId)));
    }
}
