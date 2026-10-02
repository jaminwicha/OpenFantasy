using System.Diagnostics;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography;
using OpenFantasy.Core;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Host;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.Client;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests;

public sealed class PlayerRosterHostTests
{
    [Fact]
    public void Operator_creates_four_real_players_and_refuses_to_overwrite_their_save()
    {
        using var files = new ServerFiles();
        string database = Path.Combine(files.DirectoryPath, "roster.sqlite3");
        using var output = new StringWriter();
        var application = new HostApplication(output);
        string[] arguments = ["--create-cargo-roster", database, CargoOutcomeWorldAdmission.SeedText, "Amber", "Birch", "Cedar", "Dawn"];
        Assert.Equal(0, application.Run(arguments));
        EntityId[] ids;
        using (var authority = SqliteWorldAuthority.OpenExisting(database))
        {
            var state = authority.ReadStateForHost();
            Assert.Equal(0UL, state.Revision);
            ids = state.Residents.Where(actor => actor.ControllerKind == ResidentControllerKind.Player)
                .Select(actor => actor.ActorId).Order().ToArray();
            Assert.Equal(4, ids.Length);
            Assert.Equal(12, state.EncounterLifecycle!.Scenarios.Count);
        }
        Assert.Equal(1, application.Run(arguments));
        using var reopened = SqliteWorldAuthority.OpenExisting(database);
        Assert.Equal(ids, reopened.ReadStateForHost().Residents.Where(actor => actor.ControllerKind == ResidentControllerKind.Player)
            .Select(actor => actor.ActorId).Order());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Operator_refuses_invalid_roster_count_without_creating_a_save(int count)
    {
        using var files = new ServerFiles();
        string database = Path.Combine(files.DirectoryPath, "invalid.sqlite3");
        using var output = new StringWriter();
        Assert.Equal(1, new HostApplication(output).Run(["--create-cargo-roster", database,
            CargoOutcomeWorldAdmission.SeedText, .. Enumerable.Range(0, count).Select(i => $"Player {i}")]));
        Assert.False(File.Exists(database));
    }

    [Fact]
    public async Task Four_production_players_connect_move_and_reconnect_over_real_tls_without_converting_npcs()
    {
        using var files = new ServerFiles();
        string database = Path.Combine(files.DirectoryPath, "network.sqlite3");
        var genesis = CargoOutcomeWorldAdmission.CreateEmbedded(CargoOutcomeWorldAdmission.SeedText,
            new[] { "Amber", "Birch", "Cedar", "Dawn" });
        using (var authority = SqliteWorldAuthority.CreateNew(database, genesis)) { }
        EntityId[] humans = genesis.Residents.Where(actor => actor.ControllerKind == ResidentControllerKind.Player)
            .Select(actor => actor.ActorId).Order().ToArray();
        using var certificate = ServerWorld.CreateCertificate();
        var clients = new List<PlayerProtocolClient>();
        var invitations = new List<PlayerConnectionInvitation>();
        var positions = new Dictionary<EntityId, SubunitPosition>();
        var fullViews = new Dictionary<EntityId, ConcurrentQueue<LocalWorldView>>();
        await using (var host = await CargoPlayerHost.OpenExistingAsync(database, ServerWorld.Cancellation))
        await using (var server = await PlayerWebSocketServer.StartAsync(host.Admission,
            new(IPAddress.Loopback, 0, certificate), ServerWorld.Cancellation))
        {
            try
            {
                Assert.Equal(humans, host.Players);
                foreach (EntityId actor in humans)
                {
                    var credential = host.Issue(actor, TimeSpan.FromMinutes(2));
                    var invitation = new PlayerConnectionInvitation(server.Endpoint, host.Admission.Compatibility, actor, credential.Export());
                    invitations.Add(invitation);
                    var client = await Connect(invitation);
                    clients.Add(client);
                    Assert.Equal(actor, client.ReadView().PlayerActorId);
                    var bootstrap = await client.QueryAsync(new(PlayerQueryKind.ReadPresentationBootstrap));
                    Assert.NotNull(bootstrap.Presentation);
                    var status = await client.QueryAsync(new(PlayerQueryKind.ReadParticipantStatus));
                    Assert.Equal(LocalWorldRunState.Paused, status.ParticipantStatus!.RunState);
                }
                Assert.Equal(4, clients.Select(client => client.Welcome.Delivery).Distinct().Count());
                foreach (var client in clients) client.SetReady(true);
                foreach (var client in clients)
                {
                    EntityId actor = client.Welcome.ActorId;
                    SubunitPosition start = genesis.Space.GetActor(actor).Feet;
                    var direction = new (int X, int Y)[] { (0, -32), (-32, 0), (32, 0), (0, 32) }
                        .First(step => genesis.Space.QueryGroundRoute(actor, start.Offset(step.X, step.Y, 0)).Status == GroundRouteStatus.Reachable);
                    client.SetMovement(direction.X, direction.Y);
                    await Until(() => client.ReadView().Actors.Single(row => row.ActorId == actor).Feet != start);
                    client.SetMovement(0, 0);
                    client.SetReady(false);
                    var pausedStatus = await client.QueryAsync(new(PlayerQueryKind.ReadParticipantStatus));
                    Assert.Equal(LocalWorldRunState.Paused, pausedStatus.ParticipantStatus!.RunState);
                    // Status acquires the host serialization gate; a requested full frame then observes
                    // the settled movement boundary. Pong alone is not a view-publication barrier.
                    var frames = fullViews[actor];
                    while (frames.TryDequeue(out _)) { }
                    client.RequestFullObservation();
                    await Until(() => frames.TryPeek(out var frame)
                        && client.ReadView().Actors.Single(row => row.ActorId == actor).Feet
                            == frame.Actors.Single(row => row.ActorId == actor).Feet);
                    Assert.True(frames.TryDequeue(out var settled));
                    positions[actor] = settled.Actors.Single(row => row.ActorId == actor).Feet;
                }
                // Reconnect the same saved actor with its invitation, starting unready.
                await clients[0].DisposeAsync();
                clients[0] = await Connect(invitations[0]);
                await clients[0].PingAsync();
                Assert.Equal(humans[0], clients[0].Welcome.ActorId);
                Assert.Equal(positions[humans[0]], clients[0].ReadView().Actors.Single(row => row.ActorId == humans[0]).Feet);
                var paused = await clients[0].QueryAsync(new(PlayerQueryKind.ReadParticipantStatus));
                Assert.Equal(LocalWorldRunState.Paused, paused.ParticipantStatus!.RunState);
            }
            finally { foreach (var client in clients) await client.DisposeAsync(); }

            async Task<PlayerProtocolClient> Connect(PlayerConnectionInvitation invitation)
            {
                var socket = new ClientWebSocket();
                socket.Options.AddSubProtocol(PlayerWebSocketConnector.Subprotocol);
                socket.Options.RemoteCertificateValidationCallback = (_, peer, _, errors) => peer is not null
                    && (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) == SslPolicyErrors.None
                    && peer.GetCertHashString(HashAlgorithmName.SHA256) == certificate.GetCertHashString(HashAlgorithmName.SHA256);
                try
                {
                    await socket.ConnectAsync(invitation.Endpoint, ServerWorld.Cancellation);
                    var frames = new ConcurrentQueue<LocalWorldView>();
                    fullViews[invitation.ActorId] = frames;
                    var tap = new ProtocolTap(new WebSocketPlayerTransport(socket))
                    {
                        Incoming = message =>
                        {
                            if (message.Kind == PlayerMessageKind.Observation)
                            {
                                var frame = PlayerObservationFrameCodec.Decode(message.Payload);
                                if (frame.IsFull) frames.Enqueue(PlayerViewCodec.Decode(frame.Reconstruct([])));
                            }
                            return message;
                        }
                    };
                    return await PlayerProtocolClient.ConnectAsync(tap, invitation, ServerWorld.Cancellation);
                }
                catch { socket.Dispose(); throw; }
            }
        }
        using var recovered = SqliteWorldAuthority.OpenExisting(database);
        var final = recovered.ReadStateForHost();
        Assert.Equal(genesis.AdmissionVersion, final.AdmissionVersion);
        Assert.Equal(genesis.Residents.Select(actor => (actor.ActorId, actor.ControllerKind)),
            final.Residents.Select(actor => (actor.ActorId, actor.ControllerKind)));
        foreach (var actor in humans) Assert.NotEqual(genesis.Space.GetActor(actor).Feet, final.Space.GetActor(actor).Feet);
        Assert.Equal(2, final.EncounterLifecycle!.CatalogVersion);
    }

    private static async Task Until(Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(5))
            await Task.Delay(10, ServerWorld.Cancellation);
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(5) && condition(), "The actual protocol observation did not arrive within five seconds.");
    }
}
