using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.Controllers.Recovery;

public sealed class ControllerAllowanceRestartTests
{
    [Fact]
    public async Task A_saved_allowance_retains_paid_work_and_only_its_remaining_monotonic_time_expires_after_restart()
    {
        await using var journey = await ControllerRecoveryJourney.Open(profile: 38, combat: true);
        var first = await journey.Join();
        var paid = await journey.Wait(first.Channel.Player);
        WorldSnapshot accepted = journey.State;
        Assert.Equal(paid.Command.CommandId, Assert.Single(accepted.Pacing!.Encounter!.Decisions).DecisionId);
        await journey.Lose(first);
        EntityId episode = Assert.IsType<EntityId>(journey.Control.DisconnectId);
        journey.Allowance(0);
        WorldSnapshot lost = journey.State;
        await journey.Tick(12_300);
        journey.Allowance(12_300);
        journey.Time.Monotonic.Advance(TimeSpan.FromMilliseconds(45));
        journey.Allowance(12_300); // The sub-cutoff tail is not yet durable.
        Assert.Equal(lost.ContentHash, journey.State.ContentHash);
        await journey.Stop(TimeSpan.FromDays(400)); // No time after the stop request belongs to this host segment.
        journey.AssertStoppedAllowance(12_345);

        var nextClock = new RecoverySessionClock { WallTime = DateTimeOffset.UnixEpoch.AddYears(60) };
        nextClock.Monotonic.Advance(TimeSpan.FromDays(9)); // A fresh process may already have any monotonic origin.
        await journey.Reopen(nextClock);
        journey.Allowance(12_345);
        Assert.Equal(WorldSnapshotCodec.Encode(lost), WorldSnapshotCodec.Encode(journey.State));
        nextClock.WallTime = DateTimeOffset.UnixEpoch.AddYears(-40);
        Assert.Equal(lost.ContentHash, journey.State.ContentHash);
        await journey.Tick(17_605);
        journey.Allowance(29_950);
        nextClock.Monotonic.Advance(TimeSpan.FromMilliseconds(49));
        Assert.Equal(WorldControllerMode.ReconnectAllowance, journey.Control.Mode);
        Assert.Equal(lost.ContentHash, journey.State.ContentHash);
        await journey.Tick(1);
        Assert.Equal(WorldControllerMode.Autonomous, journey.Control.Mode);
        Assert.Equal(episode, journey.Control.DisconnectId);
        Assert.Empty(journey.Authority.ReadControllerAllowancesForHost());
        ControllerRecoveryJourney.AssertGameplayUnchanged(accepted, journey.State);
        Assert.Equal(paid.Command.CommandId, Assert.Single(journey.State.Pacing!.Encounter!.Decisions).DecisionId);

        var returned = await journey.Join();
        var pending = returned.Channel.Player.ReadControlStatus();
        Assert.Equal(PlayerControlState.Pending, pending.State);
        Assert.Throws<InvalidOperationException>(() => returned.Channel.Player.Resume(pending));
        Assert.Equal(WorldReceiptCodecV1.Encode(paid.Receipt),
            WorldReceiptCodecV1.Encode(await returned.Channel.Player.SubmitAction(paid.Command)));
        await using var opponent = await journey.Host.ConnectPlayerAsync(journey.Opponent, journey.Delivery(journey.Opponent));
        await journey.Wait(opponent);
        opponent.SetReady(false);
        for (int cutoff = 0; journey.Control.Mode != WorldControllerMode.Human; cutoff++)
        {
            Assert.True(cutoff < 8, "The originally accepted waits must resolve before human restoration.");
            await journey.Tick();
        }
        WorldSnapshot restored = journey.State;
        Assert.True(restored.Pacing!.Encounter!.GetActor(journey.Actor).RemainingDelay > 0);
        Assert.Equal(accepted.Pacing!.Encounter!.GetActor(journey.Actor).StandardDelay,
            restored.Pacing!.Encounter!.GetActor(journey.Actor).RemainingDelay);
        Assert.DoesNotContain(restored.Pacing!.Encounter!.Decisions, row => row.ActorId == journey.Actor);
        Assert.Equal(accepted.Progression!.ContentHash, restored.Progression!.ContentHash);
        Assert.Equal(accepted.Inventory.ContentHash, restored.Inventory.ContentHash);
        Assert.Equal(accepted.Calendar, restored.Calendar);
        Assert.Equal(accepted.AdvancingSteps, restored.AdvancingSteps);
        Assert.Equal(accepted.EncounterLifecycle!.TacticalTicks, restored.EncounterLifecycle!.TacticalTicks);
        Assert.Empty(journey.Authority.ReadControllerAllowancesForHost());
        Assert.False(returned.Channel.Player.Ready);
        Assert.Equal(PlayerControlState.Restored, returned.Channel.Player.ReadControlStatus().State);
        Assert.Throws<InvalidOperationException>(() => returned.Channel.Player.Resume(pending));
        await journey.Tick(60_000);
        Assert.Equal(restored.ContentHash, journey.State.ContentHash);
        returned.Channel.Player.Resume(returned.Channel.Player.ReadControlStatus());
        Assert.True(returned.Channel.Player.Ready);
        returned.Channel.Player.SetReady(false);
        await journey.Stop();

        WorldStepBatch[] history = journey.StoppedBatches();
        var expiry = Assert.Single(history, batch => batch.ControllerEvent?.Kind == WorldControllerEventKind.AllowanceExpired);
        Assert.Equal(30_000UL, expiry.ControllerEvent!.ElapsedHostMilliseconds);
        Assert.Equal(episode, expiry.ControllerEvent.DisconnectId);
        Assert.Single(history, batch => batch.ControllerEvent?.Kind == WorldControllerEventKind.Disconnected);
        Assert.Single(history, batch => batch.ControllerEvent?.Kind == WorldControllerEventKind.Reconnected);
        using var reopened = SqliteWorldAuthority.OpenExisting(journey.Path, WorldSnapshotCodec.Encode(journey.Genesis));
        Assert.Equal(WorldSnapshotCodec.Encode(restored), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.Equal(WorldReceiptCodecV1.Encode(paid.Receipt),
            WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(reopened.FindReceipt(paid.Command, journey.Delivery(journey.Actor)))));
        Assert.True(reopened.IsIdentityOccupiedForHost(episode));
        Assert.True(reopened.IsIdentityOccupiedForHost(paid.Command.CommandId));
        Assert.True(reopened.IsIdentityOccupiedForHost(paid.Receipt.ResultId));
    }

    [Fact]
    public async Task Repeated_restart_and_checkpoints_add_each_segment_once_and_never_credit_UTC_or_downtime()
    {
        await using var journey = await ControllerRecoveryJourney.Open(profile: 37);
        var first = await journey.Join();
        await journey.Lose(first);
        WorldSnapshot lost = journey.State;
        await journey.Tick(10_000);
        journey.Allowance(10_000);
        await journey.Stop();

        var secondClock = new RecoverySessionClock { WallTime = DateTimeOffset.UnixEpoch.AddYears(100) };
        secondClock.Monotonic.Advance(TimeSpan.FromDays(200));
        await journey.Reopen(secondClock);
        journey.Allowance(10_000);
        Assert.Equal(lost.ContentHash, journey.State.ContentHash);
        await journey.Tick(5_000);
        journey.Allowance(15_000);
        secondClock.WallTime = DateTimeOffset.UnixEpoch.AddYears(-100);
        await journey.Tick(2_000);
        journey.Allowance(17_000); // The original base is fixed, not replaced by the last saved total.
        await journey.Tick(300);
        journey.Time.Monotonic.Advance(TimeSpan.FromMilliseconds(45));
        await journey.Stop();
        journey.AssertStoppedAllowance(17_345);

        await journey.Reopen(new RecoverySessionClock());
        journey.Allowance(17_345);
        await journey.Tick(12_605);
        journey.Allowance(29_950);
        journey.Time.Monotonic.Advance(TimeSpan.FromMilliseconds(49));
        Assert.Equal(WorldControllerMode.ReconnectAllowance, journey.Control.Mode);
        Assert.Equal(lost.ContentHash, journey.State.ContentHash);
        await journey.Tick(1);
        Assert.Equal(WorldControllerMode.Autonomous, journey.Control.Mode);
        ControllerRecoveryJourney.AssertGameplayUnchanged(lost, journey.State);
        Assert.Empty(journey.Authority.ReadControllerAllowancesForHost());
        await journey.Stop();
        Assert.Single(journey.StoppedBatches(), batch => batch.ControllerEvent?.Kind == WorldControllerEventKind.AllowanceExpired);
    }

    [Fact]
    public async Task Human_without_a_socket_stays_human_and_an_already_autonomous_reopen_cannot_expire_twice()
    {
        await using var journey = await ControllerRecoveryJourney.Open();
        WorldSnapshot original = journey.State;
        Assert.Empty(journey.Authority.ReadControllerAllowancesForHost());
        await journey.Tick(TimeSpan.FromDays(20).TotalMilliseconds);
        Assert.Equal(original.ContentHash, journey.State.ContentHash);
        await journey.Stop();
        await journey.Reopen(new RecoverySessionClock { WallTime = DateTimeOffset.UnixEpoch.AddYears(70) });
        Assert.Equal(WorldControllerMode.Human, journey.Control.Mode);
        Assert.Empty(journey.Authority.ReadControllerAllowancesForHost());
        Assert.Equal(original.ContentHash, journey.State.ContentHash);

        var joined = await journey.Join();
        await journey.Lose(joined);
        await journey.Tick(30_000);
        Assert.Equal(WorldControllerMode.Autonomous, journey.Control.Mode);
        WorldSnapshot autonomous = journey.State;
        Assert.Empty(journey.Authority.ReadControllerAllowancesForHost());
        await journey.Stop();
        await journey.Reopen(new RecoverySessionClock());
        await journey.Tick(TimeSpan.FromDays(30).TotalMilliseconds);
        Assert.Equal(autonomous.ContentHash, journey.State.ContentHash);
        Assert.Empty(journey.Authority.ReadControllerAllowancesForHost());
        await journey.Stop();
        Assert.Single(journey.StoppedBatches(), batch => batch.ControllerEvent?.Kind == WorldControllerEventKind.Disconnected);
        Assert.Single(journey.StoppedBatches(), batch => batch.ControllerEvent?.Kind == WorldControllerEventKind.AllowanceExpired);
    }
}
