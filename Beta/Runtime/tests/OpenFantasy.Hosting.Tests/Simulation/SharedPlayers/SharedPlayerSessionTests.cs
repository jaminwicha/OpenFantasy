using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.Simulation.SharedPlayers;

public sealed class SharedPlayerSessionTests
{
    [Fact]
    public async Task Two_ready_players_share_one_clock_and_independent_movement_leases_and_views()
    {
        using var files = new SessionFiles();
        WorldSnapshot genesis = TwoPlayers();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var time = new ManualSessionTimeProvider();
        await using var host = new LocalWorldSession(authority, Id(10), Delivery, time);
        await using LocalPlayerConnection peer = await host.ConnectPlayerAsync(Id(11), OtherDelivery);
        Assert.Equal(Id(10), host.ReadView().PlayerActorId);
        Assert.Equal(Id(11), peer.ReadView().PlayerActorId);
        Assert.Equal(Id(60), host.ReadView().PlayerInventory.CustodianId);
        Assert.Equal(Id(61), peer.ReadView().PlayerInventory.CustodianId);
        await Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(genesis.ContentHash, authority.ReadStateForHost().ContentHash);
        host.SetReady(true);
        peer.SetReady(true);
        host.SetMovement(32, 0);
        peer.SetMovement(0, 32);
        await Tick(time);
        WorldSnapshot moved = authority.ReadStateForHost();
        Assert.Equal(1UL, moved.AdvancingSteps);
        Assert.Equal(genesis.Calendar.AddMilliseconds(1800), moved.Calendar);
        Assert.Equal(32, moved.Space.GetActor(Id(10)).Feet.X);
        Assert.Equal(32, moved.Space.GetActor(Id(11)).Feet.Y);
        Assert.Equal(host.ReadView().Calendar, peer.ReadView().Calendar);
        host.SetReady(false);
        await Tick(time);
        Assert.Equal(32, authority.ReadStateForHost().Space.GetActor(Id(10)).Feet.X);
        Assert.Equal(64, authority.ReadStateForHost().Space.GetActor(Id(11)).Feet.Y);
        await Tick(time, TimeSpan.FromSeconds(10));
        Assert.Equal(3UL, authority.ReadStateForHost().AdvancingSteps);
        Assert.Equal(64, authority.ReadStateForHost().Space.GetActor(Id(11)).Feet.Y);
        peer.SetReady(false);
        ContentHash held = authority.ReadStateForHost().ContentHash;
        await Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(held, authority.ReadStateForHost().ContentHash);
    }

    [Fact]
    public async Task Connections_reject_impersonation_duplicate_owners_and_queued_identity_collisions()
    {
        using var files = new SessionFiles();
        WorldSnapshot genesis = TwoPlayers();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var time = new ManualSessionTimeProvider();
        await using var host = new LocalWorldSession(authority, Id(10), Delivery, time);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => host.ConnectPlayerAsync(Id(11), Delivery));
        await using LocalPlayerConnection peer = await host.ConnectPlayerAsync(Id(11), OtherDelivery);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => host.ConnectPlayerAsync(Id(11), OtherDelivery));
        host.SetReady(true);
        peer.SetReady(true);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => peer.SubmitAction(Give(genesis)));
        WorldActionCommand forged = Action(genesis, Id(11), Delivery, 401);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => peer.SubmitAction(forged));
        Task<WorldActionReceipt> primary = host.SubmitAction(Give(genesis, 402, 1));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => peer.SubmitAction(Action(genesis, Id(11), OtherDelivery, 402)));
        Assert.False(primary.IsCompleted);
        await Tick(time);
        Assert.Equal(WorldActionRejection.None, (await primary).Rejection);
        Assert.Equal(1UL, authority.ReadStateForHost().Revision);
    }

    [Fact]
    public async Task Rotating_service_does_not_starve_a_peer_and_closing_one_port_preserves_the_other()
    {
        using var files = new SessionFiles();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, TwoPlayers());
        var time = new ManualSessionTimeProvider();
        await using var host = new LocalWorldSession(authority, Id(10), Delivery, time);
        LocalPlayerConnection peer = await host.ConnectPlayerAsync(Id(11), OtherDelivery);
        host.SetReady(true);
        peer.SetReady(true);
        Task<WorldActionReceipt> primary = host.SubmitAction(Give(authority.ReadStateForHost(), 501, 1));
        Task<WorldActionReceipt> secondary = peer.SubmitAction(Action(authority.ReadStateForHost(), Id(11), OtherDelivery, 502));
        await Tick(time);
        await Until(() => primary.IsCompleted);
        Assert.True(primary.IsCompletedSuccessfully);
        Assert.False(secondary.IsCompleted);
        Task<WorldActionReceipt> nextPrimary = host.SubmitAction(Give(authority.ReadStateForHost(), 503, 1));
        await Tick(time);
        await Until(() => secondary.IsCompleted);
        Assert.True(secondary.IsCompletedSuccessfully);
        Assert.False(nextPrimary.IsCompleted);
        await peer.DisposeAsync();
        await Tick(time);
        Assert.Equal(WorldActionRejection.None, (await nextPrimary).Rejection);
        Assert.Equal(LocalWorldRunState.Unavailable, peer.ReadRunState());
        Assert.Throws<ObjectDisposedException>(() => peer.SetReady(true));
        await using LocalPlayerConnection rejoined = await host.ConnectPlayerAsync(Id(11), OtherDelivery);
        Assert.Equal(LocalWorldRunState.Paused, rejoined.ReadRunState());
        // An old port cannot submit through a new connection for the same actor.
        await Assert.ThrowsAsync<ObjectDisposedException>(() => peer.SubmitAction(Action(authority.ReadStateForHost(), Id(11), OtherDelivery, 504)));
        rejoined.SetReady(true);
        Task<WorldActionReceipt> cancelled = rejoined.SubmitAction(Action(authority.ReadStateForHost(), Id(11), OtherDelivery, 505));
        await rejoined.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        Assert.False(host.Completion.IsCompleted);
        Assert.True((await host.VerifyRecordedHistoryAsync()).ReproducesHead);
    }

    internal static TrustedCommandDelivery OtherDelivery => new(Id(101), Id(111));
    internal static WorldSnapshot TwoPlayers()
    {
        WorldSnapshot state = Genesis();
        var residents = state.Residents.Select(resident => new WorldResident(resident.ActorId, resident.InventoryCustodianId,
            resident.DisplayName, ResidentControllerKind.Player, resident.HomeFeet, resident.WanderRadiusCells,
            resident.Comprehension, resident.ExperiencedTime, resident.OpeningUtterance));
        return new WorldSnapshot(state.Inventory, state.Space, residents, state.AdmissionVersion, state.Calendar,
            state.Revision, state.AdvancingSteps, state.Dialogues);
    }

    private static WorldActionCommand Action(WorldSnapshot state, EntityId actor, TrustedCommandDelivery delivery, ulong id) =>
        new(Id(id), delivery.PrincipalId, delivery.SessionId, actor, state.WorldId, state.BranchId,
            state.Space.GetActor(actor).Revision, new GiveItemIntent(Id(10), state.Space.GetActor(Id(10)).Revision,
                Id(70), state.Inventory.Custody.GetLot(Id(70)).Revision, Id(61), state.Inventory.Custody.GetStorage(Id(61)).Revision, 1));
}
