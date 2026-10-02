using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;
using OpenFantasy.Hosting.Tests.Client.Cargo;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.Simulation.SharedPlayers;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.Networking.PlayerObservations;

public sealed class PlayerObservationSessionTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Production_regional_view_crosses_authenticated_local_and_actual_loopback_socket_channels(bool sockets)
    {
        await using var fixture = await ObservationSessionFixture.Open(CargoTestWorld.Genesis, sockets: sockets);
        await fixture.Subscribe(1);
        await fixture.Ping(2);
        fixture.NetworkTime.Advance(PlayerCommandSession.ObservationInterval);
        PlayerObservationFrame frame = await fixture.ReceiveObservation();
        Assert.True(frame.IsFull);
        var expected = fixture.Channel.Player.ReadView();
        var replica = new PlayerObservationReplica(expected.WorldId, expected.BranchId, expected.PlayerActorId);
        Assert.Equal(PlayerObservationApplyResult.Applied, replica.Apply(frame));
        LocalWorldView view = replica.ReadView()!;
        Assert.True(view.IsRegional);
        Assert.NotNull(view.Regional);
        Assert.Equal(expected.Actors.Single(actor => actor.ActorId == expected.PlayerActorId).Feet, view.Regional.PlayerFeet);
        Assert.Equal(PlayerViewCodec.Encode(expected), PlayerViewCodec.Encode(view));
        Assert.True(frame.ResultLength < PlayerObservationFrameCodec.MaximumViewBytes);
        await fixture.Send(new PlayerMessage(PlayerMessageKind.Goodbye, 3, []));
        await fixture.Completion.WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
    }

    [Fact]
    public async Task Subscription_and_recovery_are_coalesced_bounded_and_do_not_repeat_unchanged_views()
    {
        await using var fixture = await ObservationSessionFixture.Open();
        fixture.NetworkTime.Advance(TimeSpan.FromSeconds(1));
        await fixture.Ping(1); // There is no unsolicited observation ahead of this response.
        await fixture.Subscribe(2);
        await fixture.Ping(3);
        fixture.NetworkTime.Advance(TimeSpan.FromMilliseconds(99));
        await fixture.ExpectQuiet();
        fixture.NetworkTime.Advance(TimeSpan.FromMilliseconds(1));
        PlayerObservationFrame first = await fixture.ReceiveObservation();
        for (ulong request = 4; request <= 20; request++) await fixture.Subscribe(request);
        await fixture.Ping(21);
        fixture.NetworkTime.Advance(TimeSpan.FromMilliseconds(99));
        await fixture.ExpectQuiet();
        fixture.NetworkTime.Advance(TimeSpan.FromMilliseconds(1));
        PlayerObservationFrame recovery = await fixture.ReceiveObservation();
        Assert.True(recovery.IsFull);
        Assert.Equal(first.StreamId, recovery.StreamId);
        Assert.Equal(first.Revision, recovery.Revision);
        Assert.Equal(first.Replacement.ToArray(), recovery.Replacement.ToArray());
        fixture.NetworkTime.Advance(TimeSpan.FromSeconds(1));
        await fixture.ExpectQuiet();
        Assert.Equal(0UL, fixture.Authority.ReadStateForHost().Revision);
    }

    [Fact]
    public async Task Real_committed_actions_and_movement_publish_coherent_deltas_and_recover_a_missing_basis()
    {
        await using var fixture = await ObservationSessionFixture.Open();
        await fixture.Subscribe(1);
        await fixture.Ping(2);
        fixture.NetworkTime.Advance(PlayerCommandSession.ObservationInterval);
        PlayerObservationFrame first = await fixture.ReceiveObservation();
        var expected = fixture.Channel.Player.ReadView();
        var replica = new PlayerObservationReplica(expected.WorldId, expected.BranchId, expected.PlayerActorId);
        replica.Apply(first);
        WorldActionCommand command = Give(fixture.Authority.ReadStateForHost(), 440, 1);
        await fixture.Send(PlayerGameplayCodec.Readiness(3, true));
        await fixture.Send(PlayerGameplayCodec.Command(4, command));
        await Until(() => fixture.Channel.Player.Pending is not null);
        await Tick(fixture.HostTime);
        PlayerMessage receipt = await fixture.Receive();
        Assert.Equal(PlayerMessageKind.Receipt, receipt.Kind);
        Assert.Equal(WorldReceiptCodecV1.Encode(fixture.Authority.FindReceipt(command, Delivery)!), receipt.Payload.ToArray());
        fixture.NetworkTime.Advance(PlayerCommandSession.ObservationInterval);
        PlayerObservationFrame second = await fixture.ReceiveObservation();
        Assert.False(second.IsFull);
        Assert.Equal(2UL, second.Revision);
        // Deliberately discard one complete application frame; TCP itself remains ordered.
        await fixture.Send(PlayerGameplayCodec.Movement(5, 32, 0));
        await fixture.Ping(6);
        await Tick(fixture.HostTime);
        fixture.NetworkTime.Advance(PlayerCommandSession.ObservationInterval);
        PlayerObservationFrame third = await fixture.ReceiveObservation();
        Assert.Equal(3UL, third.Revision);
        Assert.False(third.IsFull);
        Assert.Equal(PlayerObservationApplyResult.RequiresFull, replica.Apply(third));
        Assert.Equal(6UL, replica.ReadView()!.PlayerInventory.Quantity);
        await fixture.Subscribe(7);
        await fixture.Ping(8);
        fixture.NetworkTime.Advance(PlayerCommandSession.ObservationInterval);
        PlayerObservationFrame recovered = await fixture.ReceiveObservation();
        Assert.True(recovered.IsFull);
        Assert.Equal(3UL, recovered.Revision);
        Assert.Equal(PlayerObservationApplyResult.Applied, replica.Apply(recovered));
        LocalWorldView view = replica.ReadView()!;
        Assert.Equal(5UL, view.PlayerInventory.Quantity);
        Assert.Equal(32, view.Actors.Single(actor => actor.ActorId == Id(10)).Feet.X);
        Assert.Equal(view.Actors.Single(actor => actor.ActorId == Id(10)).Feet, view.Regional!.PlayerFeet);
        Assert.Equal(fixture.Authority.ReadStateForHost().Calendar, view.Calendar);
        Assert.Equal(PlayerViewCodec.Encode(fixture.Channel.Player.ReadView()), PlayerViewCodec.Encode(view));
        Assert.True((await fixture.Host.VerifyRecordedHistoryAsync()).ReproducesHead);
    }

    [Fact]
    public async Task Blocked_observation_is_single_pending_work_and_does_not_hold_host_or_input_processing()
    {
        await using var fixture = await ObservationSessionFixture.Open(blockObservations: true);
        await using LocalPlayerConnection peer = await fixture.Host.ConnectPlayerAsync(Id(11), SharedPlayerSessionTests.OtherDelivery);
        peer.SetReady(true);
        peer.SetMovement(0, 32);
        await fixture.Subscribe(1);
        await fixture.Ping(2);
        fixture.NetworkTime.Advance(PlayerCommandSession.ObservationInterval);
        await fixture.Gate!.ObservationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
        WorldActionCommand command = Give(fixture.Authority.ReadStateForHost(), 441, 1);
        await fixture.Send(PlayerGameplayCodec.Readiness(3, true));
        await fixture.Send(PlayerGameplayCodec.Command(4, command));
        await Until(() => fixture.Channel.Player.Pending is not null);
        await Tick(fixture.HostTime);
        await Until(() => fixture.Authority.FindReceipt(command, Delivery) is not null);
        Assert.Equal(32, fixture.Authority.ReadStateForHost().Space.GetActor(Id(11)).Feet.Y);
        Assert.Equal(5UL, fixture.Authority.ReadStateForHost().Inventory.Custody.GetLot(Id(70)).Quantity);
        await fixture.Send(PlayerGameplayCodec.Readiness(5, false));
        await Until(() => fixture.Channel.Player.ReadRunState() == LocalWorldRunState.Paused);
        for (ulong request = 6; request < 18; request++)
        {
            await fixture.Subscribe(request);
            fixture.NetworkTime.Advance(PlayerCommandSession.ObservationInterval);
            await Task.Delay(5, Cancellation);
        }
        Assert.Equal(1, fixture.Gate.ObservationSends);
        Assert.False(fixture.Completion.IsCompleted);
        await fixture.Send(new PlayerMessage(PlayerMessageKind.Goodbye, 18, []));
        await fixture.Completion.WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
        Assert.False(fixture.Host.Completion.IsCompleted);
        Assert.Equal(LocalWorldRunState.Running, peer.ReadRunState());
    }

    [Fact]
    public async Task Blocked_observation_keeps_the_existing_absolute_send_deadline()
    {
        await using var fixture = await ObservationSessionFixture.Open(blockObservations: true);
        await fixture.Subscribe(1);
        await fixture.Ping(2);
        fixture.NetworkTime.Advance(PlayerCommandSession.ObservationInterval);
        await fixture.Gate!.ObservationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
        fixture.NetworkTime.Advance(PlayerCommandSession.SendLimit);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Completion.WaitAsync(TimeSpan.FromSeconds(5), Cancellation));
        Assert.Equal(LocalWorldRunState.Unavailable, fixture.Channel.Player.ReadRunState());
        Assert.Equal(0UL, fixture.Authority.ReadStateForHost().Revision);
    }

    [Fact]
    public async Task Client_cannot_supply_an_observation_body_or_choose_another_actors_regional_observer()
    {
        await using var fixture = await ObservationSessionFixture.Open();
        var service = new PlayerAdmissionService(fixture.Host, []);
        Assert.Throws<ArgumentException>(() => service.Issue(Id(10), Delivery, TimeSpan.FromMinutes(1), new LocalRegionObserver(Id(11))));
        await fixture.Send(new PlayerMessage(PlayerMessageKind.Observation, 1, [1]));
        await Assert.ThrowsAsync<FormatException>(() => fixture.Completion.WaitAsync(TimeSpan.FromSeconds(5), Cancellation));
        Assert.Equal(0UL, fixture.Authority.ReadStateForHost().Revision);
    }
}
