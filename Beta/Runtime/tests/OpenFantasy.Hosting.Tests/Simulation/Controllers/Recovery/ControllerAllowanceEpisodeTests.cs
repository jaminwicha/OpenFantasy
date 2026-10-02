using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.Controllers.Recovery;

public sealed class ControllerAllowanceEpisodeTests
{
    [Fact]
    public async Task Safe_return_clears_the_old_row_and_a_later_disconnect_earns_its_own_full_allowance_across_restart()
    {
        await using var journey = await ControllerRecoveryJourney.Open(profile: 38);
        var first = await journey.Join();
        await journey.Lose(first);
        EntityId previous = journey.Allowance(0).DisconnectId;
        await journey.Tick(20_000);
        journey.Allowance(20_000);
        var replacement = await journey.Join();
        Assert.Equal(WorldControllerMode.Human, journey.Control.Mode);
        Assert.Empty(journey.Authority.ReadControllerAllowancesForHost());
        var restored = replacement.Channel.Player.ReadControlStatus();
        Assert.Equal(PlayerControlState.Restored, restored.State);
        Assert.False(replacement.Channel.Player.Ready);
        replacement.Channel.Player.Resume(restored);
        Assert.True(replacement.Channel.Player.Ready);

        await first.Channel.Player.ConfirmTransportDisconnectedAsync(WorldControllerDisconnectReason.ProtocolFailure);
        Assert.Equal(WorldControllerMode.Human, journey.Control.Mode);
        Assert.Empty(journey.Authority.ReadControllerAllowancesForHost());
        await journey.Lose(replacement);
        EntityId current = journey.Allowance(0).DisconnectId;
        Assert.NotEqual(previous, current);
        Assert.Equal(3UL, journey.Control.Revision);
        WorldSnapshot secondLoss = journey.State;
        await journey.Tick(10_000); // The previous episode would have expired now.
        journey.Allowance(10_000);
        Assert.Equal(secondLoss.ContentHash, journey.State.ContentHash);
        await first.Channel.Player.ConfirmTransportDisconnectedAsync(WorldControllerDisconnectReason.TransportLost);
        journey.Allowance(10_000);
        await journey.Stop();
        journey.AssertStoppedAllowance(10_000);

        await journey.Reopen(new RecoverySessionClock());
        journey.Allowance(10_000);
        Assert.Equal(current, journey.Control.DisconnectId);
        await journey.Tick(19_950);
        journey.Allowance(29_950);
        journey.Time.Monotonic.Advance(TimeSpan.FromMilliseconds(49));
        Assert.Equal(WorldControllerMode.ReconnectAllowance, journey.Control.Mode);
        Assert.Equal(secondLoss.ContentHash, journey.State.ContentHash);
        await journey.Tick(1);
        Assert.Equal(WorldControllerMode.Autonomous, journey.Control.Mode);
        Assert.Equal(current, journey.Control.DisconnectId);
        Assert.Empty(journey.Authority.ReadControllerAllowancesForHost());
        WorldSnapshot final = journey.State;
        await journey.Stop();
        WorldStepBatch[] history = journey.StoppedBatches();
        Assert.Equal(2, history.Count(batch => batch.ControllerEvent?.Kind == WorldControllerEventKind.Disconnected));
        Assert.Single(history, batch => batch.ControllerEvent?.Kind == WorldControllerEventKind.Reconnected);
        var expiry = Assert.Single(history, batch => batch.ControllerEvent?.Kind == WorldControllerEventKind.AllowanceExpired);
        Assert.Equal(current, expiry.ControllerEvent!.DisconnectId);
        Assert.Equal(30_000UL, expiry.ControllerEvent.ElapsedHostMilliseconds);
        using var reopened = SqliteWorldAuthority.OpenExisting(journey.Path, WorldSnapshotCodec.Encode(journey.Genesis));
        Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.True(reopened.IsIdentityOccupiedForHost(previous));
        Assert.True(reopened.IsIdentityOccupiedForHost(current));
    }

    [Fact]
    public async Task A_durable_backup_reopens_only_the_committed_lower_bound_and_drops_the_unpublished_tail()
    {
        await using var journey = await ControllerRecoveryJourney.Open();
        var joined = await journey.Join();
        await journey.Lose(joined);
        await journey.Tick(12_000);
        journey.Allowance(12_000);
        WorldSnapshot lost = journey.State;
        journey.Time.Monotonic.Advance(TimeSpan.FromMilliseconds(25));
        using var backup = new StorageFiles();
        journey.Authority.BackupForHost(backup.Path);
        await journey.Stop();
        journey.AssertStoppedAllowance(12_025);

        // This copy is a real committed storage prefix, not an external-process crash claim.
        var authority = SqliteWorldAuthority.OpenExisting(backup.Path, WorldSnapshotCodec.Encode(journey.Genesis));
        Assert.Equal(12_000UL, Assert.Single(authority.ReadControllerAllowancesForHost()).ElapsedHostMilliseconds);
        Assert.Equal(WorldSnapshotCodec.Encode(lost), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
        var clock = new RecoverySessionClock { WallTime = DateTimeOffset.UnixEpoch.AddYears(90) };
        clock.Monotonic.Advance(TimeSpan.FromDays(500));
        await using (var host = new LocalWorldSession(authority, journey.Actor, journey.Delivery(journey.Actor), clock))
        {
            await host.Player.DisposeAsync();
            await SessionFixture.Until(() => clock.Monotonic.HasPendingTimer);
            await LiveControllerHostTests.Tick(clock.Monotonic, TimeSpan.FromMilliseconds(17_950));
            Assert.Equal(29_950UL, Assert.Single(authority.ReadControllerAllowancesForHost()).ElapsedHostMilliseconds);
            clock.Monotonic.Advance(TimeSpan.FromMilliseconds(25));
            Assert.Equal(WorldControllerMode.ReconnectAllowance, authority.ReadStateForHost().Controllers!.Get(journey.Actor).Mode);
            Assert.Equal(lost.ContentHash, authority.ReadStateForHost().ContentHash);
            clock.Monotonic.Advance(TimeSpan.FromMilliseconds(24));
            Assert.Equal(WorldControllerMode.ReconnectAllowance, authority.ReadStateForHost().Controllers!.Get(journey.Actor).Mode);
            await LiveControllerHostTests.Tick(clock.Monotonic, TimeSpan.FromMilliseconds(1));
            Assert.Equal(WorldControllerMode.Autonomous, authority.ReadStateForHost().Controllers!.Get(journey.Actor).Mode);
            Assert.Empty(authority.ReadControllerAllowancesForHost());
        }
        using (var recovered = SqliteWorldAuthority.OpenExisting(backup.Path, WorldSnapshotCodec.Encode(journey.Genesis)))
            Assert.Equal(WorldControllerMode.Autonomous, recovered.ReadStateForHost().Controllers!.Get(journey.Actor).Mode);
        using var database = WorldDatabase.Open(backup.Path, false);
        var expiry = Assert.Single(database.ReadJournal().Select(row => WorldStepCodec.Decode(row.AcceptedBatch)),
            batch => batch.ControllerEvent?.Kind == WorldControllerEventKind.AllowanceExpired);
        Assert.Equal(30_000UL, expiry.ControllerEvent!.ElapsedHostMilliseconds);
    }
}
