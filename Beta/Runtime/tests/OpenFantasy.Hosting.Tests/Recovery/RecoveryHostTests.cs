using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Networking.ControllerTransport;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.Simulation.Controllers;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Recovery;

public sealed class RecoveryHostTests
{
    [Fact]
    public async Task Actual_transport_loss_expires_without_healing_and_ready_coplayer_releases_recoverable_outcome()
    {
        using var files = new StorageFiles();
        var fixture = new RecoveryJournalFixture(humanObserver: true);
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis);
        WorldSnapshot defeated = fixture.Defeat(authority);
        var time = new ManualSessionTimeProvider();
        var networkTime = new ManualSessionTimeProvider();
        var patientDelivery = ConstructionUpgradeFixture.Delivery(fixture.Genesis, fixture.Patient);
        await using var host = new LocalWorldSession(authority, fixture.Patient, patientDelivery, time);
        await host.Player.DisposeAsync();
        await SessionFixture.Until(() => time.HasPendingTimer);
        var admission = new PlayerAdmissionService(host, [], networkTime);
        var credential = admission.Issue(fixture.Patient, patientDelivery, TimeSpan.FromHours(1));
        var pair = InMemoryPlayerTransport.CreatePair();
        await using var client = pair.Client;
        await ControllerTransportFixture.Send(client, PlayerHandshakeCodec.Hello(1, admission.Compatibility, credential.Export()));
        await using var joined = await admission.AcceptAsync(pair.Server, TestContext.Current.CancellationToken);
        Assert.Equal(PlayerMessageKind.Welcome, (await ControllerTransportFixture.Receive(client)).Kind);
        await using var observer = await host.ConnectPlayerAsync(fixture.Observer,
            ConstructionUpgradeFixture.Delivery(fixture.Genesis, fixture.Observer));
        Task receiver = new PlayerCommandSession(joined, networkTime).RunAsync(TestContext.Current.CancellationToken);
        await client.DisposeAsync();
        await ControllerTransportFixture.Finish(receiver);
        Assert.Equal(WorldControllerMode.ReconnectAllowance, authority.ReadStateForHost().Controllers!.Get(fixture.Patient).Mode);
        await LiveControllerHostTests.Tick(time, TimeSpan.FromMilliseconds(29_950));
        Assert.Empty(authority.ReadStateForHost().EncounterOutcome!.Acknowledgments);
        await LiveControllerHostTests.Tick(time, TimeSpan.FromMilliseconds(50));
        WorldSnapshot expired = authority.ReadStateForHost();
        Assert.Equal(WorldControllerMode.Autonomous, expired.Controllers!.Get(fixture.Patient).Mode);
        Assert.Empty(expired.EncounterOutcome!.Acknowledgments);
        await LiveControllerHostTests.Tick(time, TimeSpan.FromHours(1));
        Assert.Equal(expired.ContentHash, authority.ReadStateForHost().ContentHash);

        observer.SetReady(true);
        await LiveControllerHostTests.Tick(time);
        WorldSnapshot acknowledged = authority.ReadStateForHost();
        Assert.True(acknowledged.EncounterOutcome!.HasAcknowledged(fixture.Patient));
        Assert.Equal(EncounterLifecyclePhase.Outcome, acknowledged.EncounterLifecycle!.Phase);
        await LiveControllerHostTests.Tick(time);
        WorldSnapshot completed = authority.ReadStateForHost();
        observer.SetReady(false);
        Assert.Equal(EncounterLifecyclePhase.Exploration, completed.EncounterLifecycle!.Phase);
        Assert.Equal(defeated.Progression!.ContentHash, completed.Progression!.ContentHash);
        Assert.Equal(defeated.Space.Actors, completed.Space.Actors);
        Assert.Equal(defeated.Inventory.ContentHash, completed.Inventory.ContentHash);
        Assert.Equal(defeated.EncounterLifecycle!.Debts, completed.EncounterLifecycle.Debts);
        Assert.Equal(defeated.Calendar, completed.Calendar);
        Assert.Empty(completed.Production!.ConsumedItems);
        Assert.Equal(0, completed.Progression.GetActor(fixture.Patient).CurrentHp);

        var returning = InMemoryPlayerTransport.CreatePair();
        await using var returningClient = returning.Client;
        await ControllerTransportFixture.Send(returningClient,
            PlayerHandshakeCodec.Hello(1, admission.Compatibility, credential.Export()));
        await using var rejoined = await admission.AcceptAsync(returning.Server, TestContext.Current.CancellationToken);
        Assert.Equal(PlayerMessageKind.Welcome, (await ControllerTransportFixture.Receive(returningClient)).Kind);
        Assert.Equal(LocalWorldRunState.Paused, rejoined.Player.ReadRunState());
        Assert.Equal(WorldControllerMode.Human, authority.ReadStateForHost().Controllers!.Get(fixture.Patient).Mode);
        Assert.True(rejoined.Player.ReadView().EncounterOutcome!.AllowsAssistedRecovery);
        Assert.False(rejoined.Player.ReadView().EncounterOutcome!.IsTerminal);
        Assert.Equal(0, rejoined.Player.ReadView().Progression!.Hp);
        byte[] saved = WorldSnapshotCodec.Encode(authority.ReadStateForHost());
        await host.StopAsync();
        using var recovered = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal(saved, WorldSnapshotCodec.Encode(recovered.ReadStateForHost()));
    }
}
