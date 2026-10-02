using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Server.Tests;

public sealed class PlayerWebSocketServerTests
{
    [Fact]
    public async Task Actual_tls_upgrade_delivers_authenticated_regional_observation_queries_and_controls()
    {
        await using var world = await ServerWorld.Open();
        var before = world.Authority.ReadStateForHost();
        await using var transport = await world.Connect();
        await ServerWorld.Send(transport, new(PlayerMessageKind.Observation, 2, []));
        var observation = await ServerWorld.Receive(transport);
        var frame = PlayerObservationFrameCodec.Decode(observation.Payload);
        var replica = new PlayerObservationReplica(before.WorldId, before.BranchId, world.Actor);
        Assert.Equal(PlayerObservationApplyResult.Applied, replica.Apply(frame));
        Assert.Equal(world.Actor, replica.ReadView()!.PlayerActorId);
        Assert.True(replica.ReadView()!.IsRegional);
        var request = new PlayerQuery(PlayerQueryKind.ReadTradeInquiry);
        await ServerWorld.Send(transport, new(PlayerMessageKind.Query, 3, PlayerQueryCodec.Encode(request)));
        var response = await ServerWorld.Receive(transport);
        Assert.Equal((PlayerMessageKind.QueryResult, 3UL), (response.Kind, response.RequestId));
        var result = PlayerQueryResultCodec.Decode(response.Payload);
        result.ValidateFor(request, before.WorldId, before.BranchId, world.Actor);
        Assert.Null(result.Inquiry);
        await ServerWorld.Send(transport, PlayerGameplayCodec.Readiness(4, false));
        await ServerWorld.Send(transport, new(PlayerMessageKind.Ping, 5, []));
        Assert.Equal(PlayerMessageKind.Pong, (await ServerWorld.Receive(transport)).Kind);
        Assert.Equal(before.ContentHash, world.Authority.ReadStateForHost().ContentHash);
    }

    [Fact]
    public async Task Normal_certificate_validation_rejects_untrusted_server_and_pin_does_not_ignore_wrong_identity()
    {
        await using var world = await ServerWorld.Open();
        using var untrusted = world.NewSocket(trust: false);
        await Assert.ThrowsAsync<WebSocketException>(() => untrusted.ConnectAsync(world.Server.Endpoint, ServerWorld.Cancellation));
        using var other = ServerWorld.CreateCertificate();
        Assert.False(world.TrustTestCertificate(this, other, null, System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.False(world.TrustTestCertificate(this, world.Certificate, null, System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch));
        await using var valid = await world.Connect();
        await ServerWorld.Send(valid, new(PlayerMessageKind.Ping, 2, []));
        Assert.Equal(PlayerMessageKind.Pong, (await ServerWorld.Receive(valid)).Kind);
    }

    [Theory]
    [InlineData("origin")]
    [InlineData("query")]
    [InlineData("subprotocol")]
    [InlineData("path")]
    public async Task Http_admission_refuses_unintended_shapes_before_gameplay(string variation)
    {
        await using var world = await ServerWorld.Open();
        using var socket = world.NewSocket(subprotocol: variation != "subprotocol");
        if (variation == "origin") socket.Options.SetRequestHeader("Origin", "https://foreign.invalid");
        var uri = new UriBuilder(world.Server.Endpoint);
        if (variation == "query") uri.Query = "credential=must-not-be-used";
        if (variation == "path") uri.Path = "/elsewhere";
        await Assert.ThrowsAsync<WebSocketException>(() => socket.ConnectAsync(uri.Uri, ServerWorld.Cancellation));
        Assert.Equal(variation == "path" ? HttpStatusCode.NotFound : HttpStatusCode.BadRequest, socket.HttpStatusCode);
        Assert.False(world.Server.Completion.IsCompleted);
    }

    [Fact]
    public async Task Ordinary_http_get_cannot_upgrade_and_websocket_compression_is_not_negotiated()
    {
        await using var world = await ServerWorld.Open();
        using var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => world.TrustTestCertificate(request, cert, chain, errors);
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        var uri = new UriBuilder(world.Server.Endpoint) { Scheme = "https" }.Uri;
        var response = await client.GetAsync(uri, ServerWorld.Cancellation);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(ServerWorld.Cancellation));
        using var socket = world.NewSocket();
        socket.Options.DangerousDeflateOptions = new WebSocketDeflateOptions();
        await socket.ConnectAsync(world.Server.Endpoint, ServerWorld.Cancellation);
        Assert.False(socket.HttpResponseHeaders!.ContainsKey("Sec-WebSocket-Extensions"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Wrong_secret_or_package_fingerprint_never_attaches_a_player(bool wrongPackages)
    {
        await using var world = await ServerWorld.Open();
        using var socket = world.NewSocket();
        await socket.ConnectAsync(world.Server.Endpoint, ServerWorld.Cancellation);
        await using var transport = new WebSocketPlayerTransport(socket);
        var compatibility = world.Admission.Compatibility;
        if (wrongPackages) compatibility = new(compatibility.WorldId, compatibility.BranchId, compatibility.ProfileVersion,
            compatibility.AdmissionVersion, ContentHash.Compute("wrong"u8));
        byte[] credential = wrongPackages ? world.Credential.Export() : new byte[32];
        await ServerWorld.Send(transport, PlayerHandshakeCodec.Hello(1, compatibility, credential));
        await Assert.ThrowsAsync<WebSocketException>(() => transport.ReceiveAsync(ServerWorld.Cancellation).AsTask());
        Assert.False(world.Server.Completion.IsCompleted);
        await using var accepted = await world.Connect();
    }

    [Fact]
    public async Task Stopping_listener_closes_active_and_pending_peers_without_disposing_the_world()
    {
        await using var world = await ServerWorld.Open();
        await using var active = await world.Connect();
        using var waiting = world.NewSocket();
        await waiting.ConnectAsync(world.Server.Endpoint, ServerWorld.Cancellation);
        await world.Server.StopAsync(ServerWorld.Cancellation).WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
        Assert.True(world.Server.Completion.IsCompletedSuccessfully);
        Assert.False(world.Admission.HostCompletion.IsCompleted);
        Assert.Equal(0UL, world.Authority.ReadStateForHost().AdvancingSteps);
        await using var replacement = await PlayerWebSocketServer.StartAsync(world.Admission,
            new(IPAddress.Loopback, 0, world.Certificate), ServerWorld.Cancellation);
        using var socket = world.NewSocket();
        await socket.ConnectAsync(replacement.Endpoint, ServerWorld.Cancellation);
        await using var transport = new WebSocketPlayerTransport(socket);
        await ServerWorld.Send(transport, PlayerHandshakeCodec.Hello(1, world.Admission.Compatibility, world.Credential.Export()));
        Assert.Equal(PlayerMessageKind.Welcome, (await ServerWorld.Receive(transport)).Kind);
    }

    [Fact]
    public async Task Host_termination_propagates_to_listener_even_without_clients()
    {
        await using var world = await ServerWorld.Open();
        await world.Host.StopAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => world.Server.Completion.WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation));
        await world.DisposeFailedServer<InvalidOperationException>();
    }
}
