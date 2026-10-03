using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Host;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.Client;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests;

public sealed class AdventureEntryTests
{
    [Fact]
    public async Task Four_adventure_players_join_over_tls_move_and_reconnect_without_converting_Wren()
    {
        using var files = new ServerFiles();
        string database = Path.Combine(files.DirectoryPath, "shared.sqlite");
        var genesis = AdventureWorldAdmission.CreateEmbedded(AdventureWorldAdmission.SeedText, ["Rowan", "Mira", "Ash", "Pip"]);
        using (var authority = SqliteWorldAuthority.CreateNew(database, genesis)) { }
        using var certificate = ServerWorld.CreateCertificate();
        var clients = new List<PlayerProtocolClient>();
        var invitations = new List<PlayerConnectionInvitation>();
        await using (var host = await CargoPlayerHost.OpenExistingAsync(database, ServerWorld.Cancellation))
        await using (var server = await PlayerWebSocketServer.StartAsync(host.Admission,
            new(IPAddress.Loopback, 0, certificate), ServerWorld.Cancellation))
        {
            try
            {
                foreach (var actor in host.Players)
                {
                    var credential = host.Issue(actor, TimeSpan.FromMinutes(2));
                    var invitation = new PlayerConnectionInvitation(server.Endpoint, host.Admission.Compatibility, actor, credential.Export());
                    invitations.Add(invitation);
                    var client = await Connect(invitation);
                    clients.Add(client);
                    Assert.Equal(actor, client.ReadView().PlayerActorId);
                    Assert.NotNull((await client.QueryAsync(new(PlayerQueryKind.ReadPresentationBootstrap))).Presentation);
                    Assert.Equal(LocalWorldRunState.Paused,
                        (await client.QueryAsync(new(PlayerQueryKind.ReadParticipantStatus))).ParticipantStatus!.RunState);
                }
                Assert.Equal(4, clients.Select(client => client.Welcome.Delivery).Distinct().Count());
                foreach (var client in clients) client.ResumeSelected(client.ReadSelectedObservation());
                foreach (var client in clients)
                {
                    var actor = client.Welcome.ActorId;
                    var start = client.ReadView().Actors.Single(value => value.ActorId == actor).Feet;
                    var direction = new (int X, int Y)[] { (0, -32), (-32, 0), (32, 0), (0, 32) }
                        .First(step => genesis.Space.QueryGroundRoute(actor, start.Offset(step.X, step.Y, 0)).Status
                            == OpenFantasy.Core.Spatial.GroundRouteStatus.Reachable);
                    client.SetSelectedMovement(client.ReadSelectedObservation().Selection, direction.X, direction.Y);
                    var watch = Stopwatch.StartNew();
                    while (client.ReadView().Actors.Single(value => value.ActorId == actor).Feet == start
                        && watch.Elapsed < TimeSpan.FromSeconds(5))
                    {
                        await Task.Delay(10, ServerWorld.Cancellation);
                    }
                    Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), "Adventure movement exceeded the five-second observation bound.");
                    Assert.NotEqual(start, client.ReadView().Actors.Single(value => value.ActorId == actor).Feet);
                    client.SetSelectedMovement(client.ReadSelectedObservation().Selection, 0, 0);
                    client.PauseSelected(client.ReadSelectedObservation().Selection);
                    Assert.Equal(LocalWorldRunState.Paused,
                        (await client.QueryAsync(new(PlayerQueryKind.ReadParticipantStatus))).ParticipantStatus!.RunState);
                }
                await clients[0].DisposeAsync();
                clients[0] = await Connect(invitations[0]);
                Assert.Equal(invitations[0].ActorId, clients[0].Welcome.ActorId);
                Assert.Equal(LocalWorldRunState.Paused,
                    (await clients[0].QueryAsync(new(PlayerQueryKind.ReadParticipantStatus))).ParticipantStatus!.RunState);
            }
            finally
            {
                foreach (var client in clients) await client.DisposeAsync();
            }

            async Task<PlayerProtocolClient> Connect(PlayerConnectionInvitation invitation)
            {
                var socket = new ClientWebSocket();
                socket.Options.AddSubProtocol(PlayerWebSocketServer.Subprotocol);
                socket.Options.RemoteCertificateValidationCallback = (_, actual, _, errors) => actual is not null
                    && (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) == SslPolicyErrors.None
                    && actual.GetCertHashString(HashAlgorithmName.SHA256) == certificate.GetCertHashString(HashAlgorithmName.SHA256);
                try
                {
                    await socket.ConnectAsync(server.Endpoint, ServerWorld.Cancellation)
                        .WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
                    return await PlayerProtocolClient.ConnectAsync(new WebSocketPlayerTransport(socket), invitation, ServerWorld.Cancellation);
                }
                catch { socket.Dispose(); throw; }
            }
        }
        using var recovered = SqliteWorldAuthority.OpenExisting(database);
        var final = recovered.ReadStateForHost();
        Assert.Equal(genesis.AdmissionVersion, final.AdmissionVersion);
        Assert.Equal(genesis.Residents.Select(actor => (actor.ActorId, actor.ControllerKind)),
            final.Residents.Select(actor => (actor.ActorId, actor.ControllerKind)));
    }

    [Fact]
    public async Task Operator_creates_a_hostable_four_player_adventure_with_Wren_still_autonomous()
    {
        using var files = new ServerFiles();
        string database = Path.Combine(files.DirectoryPath, "adventure.sqlite");
        using var output = new StringWriter();
        var application = new HostApplication(output);
        string[] names = ["Rowan", "Mira", "Ash", "Pip"];
        string[] arguments = ["--create-adventure", database, AdventureWorldAdmission.SeedText, .. names];
        Assert.Equal(0, application.Run(arguments));
        var genesis = AdventureWorldAdmission.CreateEmbedded(AdventureWorldAdmission.SeedText, names);
        using (var authority = SqliteWorldAuthority.OpenExisting(database))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(genesis), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
        }
        byte[] original = File.ReadAllBytes(database);
        Assert.Equal(1, application.Run(arguments));
        Assert.Equal(original, File.ReadAllBytes(database));
        await using var host = await CargoPlayerHost.OpenExistingAsync(database, ServerWorld.Cancellation);
        Assert.Equal(4, host.Players.Count);
        var builder = genesis.Construction!.Projects.Single().BuilderActorId;
        Assert.DoesNotContain(builder, host.Players);
        foreach (var player in host.Players)
        {
            Assert.NotNull(host.Issue(player, TimeSpan.FromMinutes(1)));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Invalid_roster_refuses_creation(int count)
    {
        using var files = new ServerFiles();
        string database = Path.Combine(files.DirectoryPath, "invalid.sqlite");
        using var output = new StringWriter();
        Assert.Equal(1, new HostApplication(output).Run(["--create-adventure", database,
            AdventureWorldAdmission.SeedText, .. Enumerable.Range(0, count).Select(index => $"Player {index}")]));
        Assert.False(File.Exists(database));
    }
}
