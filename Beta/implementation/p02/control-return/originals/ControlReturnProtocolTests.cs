using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.Client;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation.Controllers.Conservative;

namespace OpenFantasy.Hosting.Tests.Networking.ControlReturn;

public sealed class ControlReturnProtocolTests
{
    [Fact]
    public async Task Explicit_readiness_after_dropped_early_readiness_reaches_the_restored_host()
    {
        await using var fixture = await ConservativeControllerFixture.Open();
        var first = await fixture.Join();
        first.Channel.Player.SetReady(true);
        await fixture.ChooseWaits(first.Channel.Player);
        await fixture.LoseTransport(first);
        await using var connection = await Connect(fixture);
        Assert.False(connection.Port.ControlAvailable);

        connection.Client.SetReady(true);
        await connection.Client.PingAsync(); // Ordered protocol barrier: early readiness was received and discarded.
        Assert.False(connection.Port.Ready);
        await using var opponent = await fixture.Connect(fixture.Opponent);
        await fixture.ChooseWaits(opponent);
        await fixture.Until(() => fixture.Control.Mode == WorldControllerMode.Human);
        opponent.SetReady(false);
        Assert.True(connection.Port.ControlAvailable);
        Assert.False(connection.Port.Ready);

        // This is a new deliberate request, not a retained request or automatic replay.
        connection.Client.SetReady(true);
        await connection.Client.PingAsync();
        Assert.True(connection.Port.Ready);
    }

    private static async Task<Connection> Connect(ConservativeControllerFixture fixture)
    {
        var admission = new PlayerAdmissionService(fixture.Host, []);
        var credential = admission.Issue(fixture.Actor, fixture.Delivery(fixture.Actor), TimeSpan.FromHours(1));
        var pair = InMemoryPlayerTransport.CreatePair();
        var invitation = new PlayerConnectionInvitation(new Uri("wss://localhost/player"),
            admission.Compatibility, fixture.Actor, credential.Export());
        Task<AuthenticatedPlayerChannel> accept = admission.AcceptAsync(pair.Server);
        Task<PlayerProtocolClient> connect = PlayerProtocolClient.ConnectAsync(pair.Client, invitation);
        AuthenticatedPlayerChannel channel = await accept;
        var stop = new CancellationTokenSource();
        Task run = new PlayerCommandSession(channel).RunAsync(stop.Token);
        return new(await connect, channel.Player, run, stop);
    }

    private sealed class Connection(PlayerProtocolClient client, LocalPlayerConnection port, Task run,
        CancellationTokenSource stop) : IAsyncDisposable
    {
        internal PlayerProtocolClient Client { get; } = client;
        internal LocalPlayerConnection Port { get; } = port;
        public async ValueTask DisposeAsync()
        {
            stop.Cancel();
            await Client.DisposeAsync();
            try { await run.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            stop.Dispose();
        }
    }
}
