using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.Simulation.SharedPlayers;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.Networking.PlayerAdmission;

public sealed class PlayerAdmissionTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Two_credentials_select_actual_private_player_ports_and_never_steal_the_original_port()
    {
        using var fixture = new AdmissionFixture();
        await using LocalWorldSession host = fixture.Host;
        PlayerJoinCredential first = fixture.Service.Issue(Id(10), Delivery, TimeSpan.FromMinutes(1));
        PlayerJoinCredential second = fixture.Service.Issue(Id(11), SharedPlayerSessionTests.OtherDelivery, TimeSpan.FromMinutes(1));
        var occupied = InMemoryPlayerTransport.CreatePair();
        await SendHello(occupied.Client, fixture.Service.Compatibility, first);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => fixture.Service.AcceptAsync(occupied.Server, Cancellation));
        await occupied.Client.DisposeAsync();
        Assert.Equal(LocalWorldRunState.Paused, host.Player.ReadRunState());
        await host.Player.DisposeAsync();

        var primary = InMemoryPlayerTransport.CreatePair();
        var secondary = InMemoryPlayerTransport.CreatePair();
        await using var primaryClient = primary.Client;
        await using var secondaryClient = secondary.Client;
        await SendHello(primary.Client, fixture.Service.Compatibility, first);
        await SendHello(secondary.Client, fixture.Service.Compatibility, second);
        await using AuthenticatedPlayerChannel channel = await fixture.Service.AcceptAsync(primary.Server, Cancellation);
        await using AuthenticatedPlayerChannel peer = await fixture.Service.AcceptAsync(secondary.Server, Cancellation);
        PlayerMessage welcome = PlayerMessageCodec.Decode((await primary.Client.ReceiveAsync(Cancellation))!);
        Assert.Equal(7UL, welcome.RequestId);
        PlayerWelcome selected = PlayerHandshakeCodec.ReadWelcome(welcome);
        Assert.Equal(Id(10), selected.ActorId);
        Assert.Equal(Delivery, selected.Delivery);
        Assert.Equal(fixture.Service.Compatibility, selected.Compatibility);
        Assert.Equal(Id(60), channel.Player.ReadView().PlayerInventory.CustodianId);
        Assert.Equal(Id(61), peer.Player.ReadView().PlayerInventory.CustodianId);
        Assert.Equal(LocalWorldRunState.Paused, channel.Player.ReadRunState());
        Assert.Equal(LocalWorldRunState.Paused, peer.Player.ReadRunState());
        Assert.Equal(fixture.Genesis.ContentHash, fixture.Authority.ReadStateForHost().ContentHash);
        await channel.DisposeAsync();
        Assert.Equal(LocalWorldRunState.Paused, peer.Player.ReadRunState());
        Assert.False(host.Completion.IsCompleted);
    }

    [Theory]
    [InlineData(0)] // world
    [InlineData(16)] // branch
    [InlineData(33)] // profile
    [InlineData(34)] // admission
    [InlineData(66)] // packages
    [InlineData(98)] // secret
    public async Task Altered_hello_pins_or_credential_cannot_create_a_port_or_change_the_world(int offset)
    {
        using var fixture = new AdmissionFixture();
        await using LocalWorldSession host = fixture.Host;
        var credential = fixture.Service.Issue(Id(11), SharedPlayerSessionTests.OtherDelivery, TimeSpan.FromMinutes(1));
        byte[] hello = PlayerMessageCodec.Encode(PlayerHandshakeCodec.Hello(7, fixture.Service.Compatibility, credential.Export()));
        hello[PlayerMessageCodec.HeaderBytes + offset] ^= 2;
        var pair = InMemoryPlayerTransport.CreatePair();
        await using var client = pair.Client;
        await client.SendAsync(hello, Cancellation);
        await Assert.ThrowsAsync<PlayerAdmissionException>(() => fixture.Service.AcceptAsync(pair.Server, Cancellation));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => pair.Server.ReceiveAsync(Cancellation).AsTask());
        Assert.Equal(fixture.Genesis.ContentHash, fixture.Authority.ReadStateForHost().ContentHash);
        await using LocalPlayerConnection legitimate = await host.ConnectPlayerAsync(Id(11), SharedPlayerSessionTests.OtherDelivery);
        Assert.Equal(LocalWorldRunState.Paused, legitimate.ReadRunState());
    }

    [Fact]
    public async Task Issuance_requires_existing_human_control_and_does_not_reveal_secrets_in_diagnostics()
    {
        using var fixture = new AdmissionFixture();
        await using LocalWorldSession host = fixture.Host;
        Assert.Throws<PlayerAdmissionException>(() => fixture.Service.Issue(Id(11), Delivery, TimeSpan.FromMinutes(1)));
        Assert.Throws<PlayerAdmissionException>(() => fixture.Service.Issue(Id(11),
            SharedPlayerSessionTests.OtherDelivery with { SessionId = Id(999) }, TimeSpan.FromMinutes(1)));
        Assert.Throws<PlayerAdmissionException>(() => fixture.Service.Issue(Id(999), Delivery, TimeSpan.FromMinutes(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Service.Issue(Id(10), Delivery, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Service.Issue(Id(10), Delivery, TimeSpan.FromDays(2)));
        PlayerJoinCredential credential = fixture.Service.Issue(Id(10), Delivery, TimeSpan.FromMinutes(1));
        byte[] export = credential.Export();
        Assert.Equal(32, export.Length);
        Assert.DoesNotContain(Convert.ToHexString(export), credential.ToString(), StringComparison.OrdinalIgnoreCase);
        export[0] ^= 1;
        Assert.NotEqual(export, credential.Export());
        using var npcFiles = new SessionFiles();
        var npcAuthority = SqliteWorldAuthority.CreateNew(npcFiles.Path, Genesis());
        await using var npcHost = new LocalWorldSession(npcAuthority, Id(10), Delivery, new ManualSessionTimeProvider());
        var npcService = new PlayerAdmissionService(npcHost, []);
        Assert.Throws<PlayerAdmissionException>(() => npcService.Issue(Id(11), SharedPlayerSessionTests.OtherDelivery, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task Expiry_revocation_and_rotation_refuse_old_credentials_without_preventing_fresh_admission()
    {
        using var fixture = new AdmissionFixture();
        await using LocalWorldSession host = fixture.Host;
        var delivery = SharedPlayerSessionTests.OtherDelivery;
        PlayerJoinCredential expired = fixture.Service.Issue(Id(11), delivery, TimeSpan.FromSeconds(1));
        fixture.AuthenticationTime.Advance(TimeSpan.FromSeconds(1));
        await Refused(fixture, expired);
        PlayerJoinCredential revoked = fixture.Service.Issue(Id(11), delivery, TimeSpan.FromMinutes(1));
        fixture.Service.Revoke(Id(11));
        await Refused(fixture, revoked);
        PlayerJoinCredential rotated = fixture.Service.Issue(Id(11), delivery, TimeSpan.FromMinutes(1));
        PlayerJoinCredential current = fixture.Service.Issue(Id(11), delivery, TimeSpan.FromMinutes(1));
        Assert.NotEqual(rotated.Export(), current.Export());
        await Refused(fixture, rotated);
        var pair = InMemoryPlayerTransport.CreatePair();
        await using var client = pair.Client;
        await SendHello(client, fixture.Service.Compatibility, current);
        await using AuthenticatedPlayerChannel admitted = await fixture.Service.AcceptAsync(pair.Server, Cancellation);
        Assert.Equal(Id(11), admitted.Player.ActorId);
        Assert.Equal(fixture.Genesis.ContentHash, fixture.Authority.ReadStateForHost().ContentHash);
    }

    [Fact]
    public async Task Concurrent_reuse_has_one_winner_and_reconnect_requires_release_of_the_original_port()
    {
        using var fixture = new AdmissionFixture();
        await using LocalWorldSession host = fixture.Host;
        PlayerJoinCredential credential = fixture.Service.Issue(Id(11), SharedPlayerSessionTests.OtherDelivery, TimeSpan.FromMinutes(1));
        var first = InMemoryPlayerTransport.CreatePair();
        var second = InMemoryPlayerTransport.CreatePair();
        await using var firstClient = first.Client;
        await using var secondClient = second.Client;
        await SendHello(firstClient, fixture.Service.Compatibility, credential);
        await SendHello(secondClient, fixture.Service.Compatibility, credential);
        Task<AuthenticatedPlayerChannel>[] attempts = [
            Task.Run(() => fixture.Service.AcceptAsync(first.Server, Cancellation), Cancellation),
            Task.Run(() => fixture.Service.AcceptAsync(second.Server, Cancellation), Cancellation)];
        try { await Task.WhenAll(attempts); }
        catch (PlayerAdmissionException) { }
        _ = Assert.Single(attempts, task => task.IsCompletedSuccessfully);
        _ = Assert.Single(attempts, task => task.IsFaulted);
        AuthenticatedPlayerChannel winner = await attempts.Single(task => task.IsCompletedSuccessfully);
        Assert.Throws<PlayerAdmissionException>(() => fixture.Service.Issue(Id(11), SharedPlayerSessionTests.OtherDelivery, TimeSpan.FromMinutes(1)));
        await winner.DisposeAsync();
        var reconnect = InMemoryPlayerTransport.CreatePair();
        await using var reconnectClient = reconnect.Client;
        await SendHello(reconnectClient, fixture.Service.Compatibility, credential);
        await using AuthenticatedPlayerChannel rejoined = await fixture.Service.AcceptAsync(reconnect.Server, Cancellation);
        Assert.Equal(LocalWorldRunState.Paused, rejoined.Player.ReadRunState());
        Assert.Throws<ObjectDisposedException>(() => winner.Player.SetReady(true));
    }

    [Fact]
    public async Task Pending_handshakes_are_bounded_and_initial_silence_has_an_absolute_deadline()
    {
        using var fixture = new AdmissionFixture();
        await using LocalWorldSession host = fixture.Host;
        var pairs = Enumerable.Range(0, 5).Select(_ => InMemoryPlayerTransport.CreatePair()).ToArray();
        try
        {
            Task<AuthenticatedPlayerChannel>[] pending = pairs.Take(4).Select(pair => fixture.Service.AcceptAsync(pair.Server, Cancellation)).ToArray();
            Assert.All(pending, task => Assert.False(task.IsCompleted));
            await Assert.ThrowsAsync<PlayerAdmissionException>(() => fixture.Service.AcceptAsync(pairs[4].Server, Cancellation));
            fixture.AuthenticationTime.Advance(PlayerAdmissionService.HandshakeLimit);
            foreach (Task<AuthenticatedPlayerChannel> task in pending)
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            foreach (var pair in pairs)
                await Assert.ThrowsAsync<ObjectDisposedException>(() => pair.Server.ReceiveAsync(Cancellation).AsTask());
            Assert.Equal(fixture.Genesis.ContentHash, fixture.Authority.ReadStateForHost().ContentHash);
            var renewed = InMemoryPlayerTransport.CreatePair();
            await using var client = renewed.Client;
            var credential = fixture.Service.Issue(Id(11), SharedPlayerSessionTests.OtherDelivery, TimeSpan.FromMinutes(1));
            await SendHello(client, fixture.Service.Compatibility, credential);
            await using var admitted = await fixture.Service.AcceptAsync(renewed.Server, Cancellation);
        }
        finally { foreach (var pair in pairs) { await pair.Client.DisposeAsync(); await pair.Server.DisposeAsync(); } }
    }

    [Fact]
    public async Task A_blocked_welcome_cannot_extend_admission_and_failed_send_releases_the_real_player_port()
    {
        using var fixture = new AdmissionFixture();
        await using LocalWorldSession host = fixture.Host;
        PlayerJoinCredential credential = fixture.Service.Issue(Id(11), SharedPlayerSessionTests.OtherDelivery, TimeSpan.FromMinutes(1));
        var pair = InMemoryPlayerTransport.CreatePair();
        await using var client = pair.Client;
        for (int index = 0; index < InMemoryPlayerTransport.MaximumQueuedMessages; index++)
            await pair.Server.SendAsync(new byte[] { 1 }, Cancellation);
        await SendHello(client, fixture.Service.Compatibility, credential);
        Task<AuthenticatedPlayerChannel> pending = fixture.Service.AcceptAsync(pair.Server, Cancellation);
        Assert.False(pending.IsCompleted);
        // Admission reached the real port before Welcome became backpressured.
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => host.ConnectPlayerAsync(Id(11), SharedPlayerSessionTests.OtherDelivery));
        fixture.AuthenticationTime.Advance(PlayerAdmissionService.HandshakeLimit);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await using LocalPlayerConnection availableAgain = await host.ConnectPlayerAsync(Id(11), SharedPlayerSessionTests.OtherDelivery);
        Assert.Equal(fixture.Genesis.ContentHash, fixture.Authority.ReadStateForHost().ContentHash);
    }

    [Fact]
    public void Package_fingerprint_binds_dependencies_and_is_independent_of_enumeration_order()
    {
        var basePackage = new PackageManifest(new PackageId("base"), ContentHash.Compute("base"u8), []);
        var game = new PackageManifest(new PackageId("game"), ContentHash.Compute("game"u8), [new PackageDependency(basePackage.Id, basePackage.Version)]);
        Assert.Equal(PlayerWorldCompatibility.ResolvePackages([basePackage, game]), PlayerWorldCompatibility.ResolvePackages([game, basePackage]));
        Assert.NotEqual(PlayerWorldCompatibility.ResolvePackages([basePackage]), PlayerWorldCompatibility.ResolvePackages([basePackage, game]));
        Assert.Throws<InvalidDataException>(() => PlayerWorldCompatibility.ResolvePackages([game]));
        Assert.Throws<InvalidDataException>(() => PlayerWorldCompatibility.ResolvePackages([basePackage, basePackage]));
        var changed = new PackageManifest(basePackage.Id, ContentHash.Compute("changed"u8), []);
        Assert.Throws<InvalidDataException>(() => PlayerWorldCompatibility.ResolvePackages([changed, game]));
        byte[] expected = new byte[72];
        Convert.FromHexString("4F46505000010002").CopyTo(expected, 0);
        basePackage.Version.ToByteArray().CopyTo(expected, 8);
        game.Version.ToByteArray().CopyTo(expected, 40);
        Assert.Equal(ContentHash.Compute(expected), PlayerWorldCompatibility.ResolvePackages([game, basePackage]));
    }

    [Fact]
    public async Task Transport_disposal_failure_still_closes_the_actor_port_before_releasing_its_reservation()
    {
        using var fixture = new AdmissionFixture();
        await using LocalWorldSession host = fixture.Host;
        var pair = InMemoryPlayerTransport.CreatePair();
        await using var client = pair.Client;
        await using var server = pair.Server;
        var credential = fixture.Service.Issue(Id(11), SharedPlayerSessionTests.OtherDelivery, TimeSpan.FromMinutes(1));
        await SendHello(client, fixture.Service.Compatibility, credential);
        var throwingTransport = new ThrowingCloseTransport(server);
        AuthenticatedPlayerChannel channel = await fixture.Service.AcceptAsync(throwingTransport, Cancellation);
        await Assert.ThrowsAsync<IOException>(() => channel.DisposeAsync().AsTask());
        Assert.Equal(LocalWorldRunState.Unavailable, channel.Player.ReadRunState());
        await using LocalPlayerConnection rejoined = await host.ConnectPlayerAsync(Id(11), SharedPlayerSessionTests.OtherDelivery);
        Assert.Equal(LocalWorldRunState.Paused, rejoined.ReadRunState());
    }

    [Fact]
    public async Task Host_shutdown_during_welcome_cannot_return_a_successfully_admitted_dead_port()
    {
        using var fixture = new AdmissionFixture();
        await using LocalWorldSession host = fixture.Host;
        var pair = InMemoryPlayerTransport.CreatePair();
        await using var client = pair.Client;
        var credential = fixture.Service.Issue(Id(11), SharedPlayerSessionTests.OtherDelivery, TimeSpan.FromMinutes(1));
        for (int index = 0; index < InMemoryPlayerTransport.MaximumQueuedMessages; index++)
            await pair.Server.SendAsync(new byte[] { 1 }, Cancellation);
        await SendHello(client, fixture.Service.Compatibility, credential);
        Task<AuthenticatedPlayerChannel> pending = fixture.Service.AcceptAsync(pair.Server, Cancellation);
        Assert.False(pending.IsCompleted);
        await host.StopAsync();
        await client.ReceiveAsync(Cancellation);
        await Assert.ThrowsAsync<PlayerAdmissionException>(() => pending);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("kind")]
    [InlineData("request")]
    [InlineData("length")]
    public async Task Invalid_handshake_framing_is_terminal_without_reserving_an_actor(string corruption)
    {
        using var fixture = new AdmissionFixture();
        await using LocalWorldSession host = fixture.Host;
        var pair = InMemoryPlayerTransport.CreatePair();
        await using var client = pair.Client;
        var credential = fixture.Service.Issue(Id(11), SharedPlayerSessionTests.OtherDelivery, TimeSpan.FromMinutes(1));
        byte[] frame = PlayerMessageCodec.Encode(PlayerHandshakeCodec.Hello(7, fixture.Service.Compatibility, credential.Export()));
        switch (corruption)
        {
            case "version": frame[5] = 2; break;
            case "kind": frame[7] = (byte)PlayerMessageKind.Readiness; break;
            case "request": frame[15] = 0; break;
            case "length": frame = [.. frame, 0]; break;
        }
        await client.SendAsync(frame, Cancellation);
        Exception? error = await Record.ExceptionAsync(() => fixture.Service.AcceptAsync(pair.Server, Cancellation));
        Assert.True(error is FormatException or PlayerAdmissionException);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => pair.Server.ReceiveAsync(Cancellation).AsTask());
        Assert.Equal(fixture.Genesis.ContentHash, fixture.Authority.ReadStateForHost().ContentHash);
        await using LocalPlayerConnection legitimate = await host.ConnectPlayerAsync(Id(11), SharedPlayerSessionTests.OtherDelivery);
    }

    [Fact]
    public async Task Actual_loopback_websocket_handshake_reaches_the_same_host_selected_actor()
    {
        using var fixture = new AdmissionFixture();
        await using LocalWorldSession host = fixture.Host;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var clientTcp = new TcpClient();
        try
        {
            ValueTask connecting = clientTcp.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port, deadline.Token);
            using TcpClient serverTcp = await listener.AcceptTcpClientAsync(deadline.Token);
            await connecting;
            using WebSocket clientSocket = WebSocket.CreateFromStream(clientTcp.GetStream(), false, null, TimeSpan.FromSeconds(30));
            using WebSocket serverSocket = WebSocket.CreateFromStream(serverTcp.GetStream(), true, null, TimeSpan.FromSeconds(30));
            await using var client = new WebSocketPlayerTransport(clientSocket);
            var server = new WebSocketPlayerTransport(serverSocket);
            var credential = fixture.Service.Issue(Id(11), SharedPlayerSessionTests.OtherDelivery, TimeSpan.FromMinutes(1));
            await client.SendAsync(PlayerMessageCodec.Encode(PlayerHandshakeCodec.Hello(7, fixture.Service.Compatibility, credential.Export())), deadline.Token);
            await using AuthenticatedPlayerChannel channel = await fixture.Service.AcceptAsync(server, deadline.Token);
            PlayerWelcome welcome = PlayerHandshakeCodec.ReadWelcome(PlayerMessageCodec.Decode((await client.ReceiveAsync(deadline.Token))!));
            Assert.Equal(Id(11), welcome.ActorId);
            Assert.Equal(SharedPlayerSessionTests.OtherDelivery, welcome.Delivery);
            Assert.Equal(Id(61), channel.Player.ReadView().PlayerInventory.CustodianId);
            Assert.Equal(fixture.Genesis.ContentHash, fixture.Authority.ReadStateForHost().ContentHash);
        }
        finally { listener.Stop(); }
    }

    [Fact]
    public void Handshake_has_exact_independent_field_layout_and_does_not_accept_missing_or_extra_bytes()
    {
        var compatibility = new PlayerWorldCompatibility(new(1, 2), new(3, 4), 35,
            new ContentHash(new string('a', 64)), new ContentHash(new string('b', 64)));
        byte[] secret = Enumerable.Repeat((byte)0xcc, 32).ToArray();
        PlayerMessage hello = PlayerHandshakeCodec.Hello(7, compatibility, secret);
        byte[] payload = hello.Payload.ToArray();
        Assert.Equal(130, payload.Length);
        Assert.Equal("00000000000000010000000000000002000000000000000300000000000000040023", Convert.ToHexString(payload.AsSpan(0, 34)));
        Assert.All(payload[34..66], value => Assert.Equal(0xaa, value));
        Assert.All(payload[66..98], value => Assert.Equal(0xbb, value));
        Assert.All(payload[98..130], value => Assert.Equal(0xcc, value));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerHandshakeCodec.Hello(0, compatibility, secret));
        Assert.Throws<ArgumentException>(() => PlayerHandshakeCodec.Hello(7, compatibility, new byte[31]));
        byte[] welcome = new byte[146];
        payload.AsSpan(0, 98).CopyTo(welcome);
        BinaryPrimitives.WriteUInt64BigEndian(welcome.AsSpan(106), 10);
        BinaryPrimitives.WriteUInt64BigEndian(welcome.AsSpan(122), 100);
        BinaryPrimitives.WriteUInt64BigEndian(welcome.AsSpan(138), 110);
        PlayerWelcome decoded = PlayerHandshakeCodec.ReadWelcome(new PlayerMessage(PlayerMessageKind.Welcome, 7, welcome));
        Assert.Equal(new EntityId(0, 10), decoded.ActorId);
        Assert.Equal(new EntityId(0, 100), decoded.Delivery.PrincipalId);
        Assert.Equal(new EntityId(0, 110), decoded.Delivery.SessionId);
        Assert.Equal(compatibility, decoded.Compatibility);
        Assert.Throws<FormatException>(() => PlayerHandshakeCodec.ReadWelcome(new PlayerMessage(PlayerMessageKind.Hello, 7, welcome)));
        Assert.Throws<FormatException>(() => PlayerHandshakeCodec.ReadWelcome(new PlayerMessage(PlayerMessageKind.Welcome, 7, welcome[..145])));
        Assert.Throws<FormatException>(() => PlayerHandshakeCodec.ReadWelcome(new PlayerMessage(PlayerMessageKind.Welcome, 7, new byte[147])));
    }

    private static async Task SendHello(PlayerMessageTransport client, PlayerWorldCompatibility compatibility, PlayerJoinCredential credential) =>
        await client.SendAsync(PlayerMessageCodec.Encode(PlayerHandshakeCodec.Hello(7, compatibility, credential.Export())), Cancellation);

    private static async Task Refused(AdmissionFixture fixture, PlayerJoinCredential credential)
    {
        var pair = InMemoryPlayerTransport.CreatePair();
        await using var client = pair.Client;
        await SendHello(client, fixture.Service.Compatibility, credential);
        await Assert.ThrowsAsync<PlayerAdmissionException>(() => fixture.Service.AcceptAsync(pair.Server, Cancellation));
    }

    private sealed class AdmissionFixture : IDisposable
    {
        private readonly SessionFiles _files = new();
        internal AdmissionFixture()
        {
            Genesis = SharedPlayerSessionTests.TwoPlayers();
            Authority = SqliteWorldAuthority.CreateNew(_files.Path, Genesis);
            Host = new LocalWorldSession(Authority, Id(10), Delivery, new ManualSessionTimeProvider());
            Service = new PlayerAdmissionService(Host, [], AuthenticationTime);
        }
        internal WorldSnapshot Genesis { get; }
        internal SqliteWorldAuthority Authority { get; }
        internal LocalWorldSession Host { get; }
        internal ManualSessionTimeProvider AuthenticationTime { get; } = new();
        internal PlayerAdmissionService Service { get; }
        public void Dispose() => _files.Dispose();
    }

    private sealed class ThrowingCloseTransport(PlayerMessageTransport inner) : PlayerMessageTransport(PlayerMessageCodec.MaximumMessageBytes)
    {
        protected override ValueTask SendMessageAsync(byte[] message, CancellationToken cancellationToken) => inner.SendAsync(message, cancellationToken);
        protected override ValueTask<byte[]?> ReceiveMessageAsync(CancellationToken cancellationToken) => inner.ReceiveAsync(cancellationToken);
        protected override void CloseTransport() => throw new IOException("Injected transport close failure.");
    }
}
