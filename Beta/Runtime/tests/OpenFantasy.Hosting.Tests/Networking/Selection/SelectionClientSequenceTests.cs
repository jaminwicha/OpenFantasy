using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.Client;
using OpenFantasy.Hosting.Networking.Selection;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Networking.ControllerTransport;

namespace OpenFantasy.Hosting.Tests.Networking.Selection;

public sealed class SelectionClientSequenceTests(SelectionProtocolFrameFixture source) : IClassFixture<SelectionProtocolFrameFixture>
{
    [Theory]
    [InlineData("actor")]
    [InlineData("witness")]
    [InlineData("control-port")]
    [InlineData("control-generation")]
    public async Task Canonical_frames_cannot_replace_selection_or_control_identity_without_the_required_generation(string substitution)
    {
        PlayerSelectedObservation initial = source.Frame with
        {
            Observation = source.Frame.Observation with
            {
                ControlStatus = source.Frame.Observation.ControlStatus with { State = PlayerControlState.Restored, Generation = 1 }
            }
        };
        await using var peer = await ScriptedSelectionPeer.Open(source, initial);
        var observed = initial.Observation;
        var replaced = substitution switch
        {
            "actor" => observed with
            {
                Selection = observed.Selection with { ActorId = observed.Selection.HomeActorId, ControlWitness = null },
                ControlledView = null
            },
            "witness" => observed with { Selection = observed.Selection with { ControlWitness = ReplaceWitness(observed.Selection.ControlWitness!) } },
            "control-port" => observed with { ControlStatus = observed.ControlStatus with { PortId = new EntityId(0x5e1ec710, 1) } },
            "control-generation" => observed with { ControlStatus = observed.ControlStatus with { State = PlayerControlState.Pending, Generation = 0 } },
            _ => throw new ArgumentOutOfRangeException(nameof(substitution))
        };
        var malformedSequence = initial with { Revision = initial.Revision + 1, Observation = replaced };
        byte[] bytes = PlayerSelectedObservationCodec.Encode(malformedSequence);
        Assert.Equal(bytes, PlayerSelectedObservationCodec.Encode(PlayerSelectedObservationCodec.Decode(bytes)));
        await peer.Send(new(PlayerMessageKind.SelectionObservation, 0, bytes));
        await Assert.ThrowsAsync<FormatException>(() => peer.Client.Completion.WaitAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Spontaneous_restored_observation_requires_delivery_proof_then_the_same_capture_can_resume()
    {
        var initial = source.Frame with
        {
            Observation = source.Frame.Observation with
            {
                ControlStatus = source.Frame.Observation.ControlStatus with { State = PlayerControlState.Pending, Generation = 0 }
            }
        };
        await using var peer = await ScriptedSelectionPeer.Open(source, initial);
        var restored = initial with
        {
            Revision = initial.Revision + 1,
            Observation = initial.Observation with
            {
                ControlStatus = initial.Observation.ControlStatus with { State = PlayerControlState.Restored, Generation = 1 }
            }
        };
        await peer.Send(new(PlayerMessageKind.SelectionObservation, 0, PlayerSelectedObservationCodec.Encode(restored)));
        await peer.Barrier();
        LocalSelectedPlayerObservation captured = peer.Client.ReadSelectedObservation();
        Assert.Equal(PlayerControlState.Restored, captured.ControlStatus.State);
        Assert.Throws<SelectedInputUnavailableException>(() => peer.Client.ResumeSelected(captured));
        Assert.False(captured.IsReady);
        await peer.Barrier(); // Refused Resume emitted no selected input ahead of this Ping.

        await peer.SendProof(restored);
        await peer.Barrier();
        Assert.Same(captured, peer.Client.ReadSelectedObservation());
        peer.Client.ResumeSelected(captured);
        PlayerMessage sent = await peer.Receive();
        Assert.Equal(PlayerMessageKind.SelectionInput, sent.Kind);
        var input = PlayerSelectedInputCodec.Decode(sent.Payload);
        Assert.Equal(PlayerSelectedInputKind.Resume, input.Kind);
        Assert.Equal(captured.Selection, input.Selection);
        Assert.Equal(restored.StreamId, input.StreamId);
        Assert.Equal(restored.Revision, input.Revision);
        await peer.Send(new(PlayerMessageKind.Pong, sent.RequestId, []));
        await peer.Barrier();
    }

    private static WorldControlWitness ReplaceWitness(WorldControlWitness original) => new(
        original.OwnerActorId, original.GranteeActorId, original.GrantCommandId, original.AcceptanceCommandId,
        original.GrantRevision, original.Capabilities, original.TermsHash, new EntityId(0x5e1ec710, 2),
        original.AssignmentRevision + 1, original.AcquiredSequence + 1, original.DisconnectId, original.OwnerControlRevision);
}

/// <summary>One immutable lawful journal supplies scoped values; scripted peers exercise untrusted wire sequencing only.</summary>
public sealed class SelectionProtocolFrameFixture : IDisposable
{
    private readonly SelectedCodecFixture _source = new();
    internal PlayerSelectedObservation Frame => _source.Frame;
    internal PlayerWorldCompatibility Compatibility => new(_source.State.WorldId, _source.State.BranchId,
        _source.State.ProfileVersion, _source.State.AdmissionVersion, PlayerWorldCompatibility.ResolvePackages([]));
    internal TrustedCommandDelivery Delivery => _source.Source.Delivery(_source.Source.Grantee);
    public void Dispose() => _source.Dispose();
}

internal sealed class ScriptedSelectionPeer(PlayerMessageTransport server, PlayerProtocolClient client) : IAsyncDisposable
{
    internal PlayerProtocolClient Client { get; } = client;

    internal static async Task<ScriptedSelectionPeer> Open(SelectionProtocolFrameFixture source, PlayerSelectedObservation initial)
    {
        var pair = InMemoryPlayerTransport.CreatePair();
        var invitation = new PlayerConnectionInvitation(new Uri("wss://localhost/player"), source.Compatibility,
            initial.Observation.Selection.HomeActorId, new byte[32]);
        Task<PlayerProtocolClient> connecting = PlayerProtocolClient.ConnectAsync(pair.Client, invitation, TestContext.Current.CancellationToken);
        try
        {
            var hello = await ControllerTransportFixture.Receive(pair.Server);
            Assert.Equal(PlayerMessageKind.Hello, hello.Kind);
            await ControllerTransportFixture.Send(pair.Server, PlayerHandshakeCodec.Welcome(hello.RequestId,
                source.Compatibility, initial.Observation.Selection.HomeActorId, source.Delivery));
            var subscribe = await ControllerTransportFixture.Receive(pair.Server);
            Assert.Equal(PlayerMessageKind.SelectionSubscribe, subscribe.Kind);
            await ControllerTransportFixture.Send(pair.Server, new(PlayerMessageKind.SelectionObservation,
                subscribe.RequestId, PlayerSelectedObservationCodec.Encode(initial)));
            await ControllerTransportFixture.Send(pair.Server, new(PlayerMessageKind.SelectionDelivered, 0,
                PlayerSelectionDeliveryCodec.Encode(initial.StreamId, initial.Revision)));
            return new(pair.Server, await connecting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        }
        catch
        {
            await pair.Server.DisposeAsync();
            await pair.Client.DisposeAsync();
            try { await connecting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); }
            catch { /* Retain the first setup failure while joining failed startup. */ }
            throw;
        }
    }

    internal Task Send(PlayerMessage message) => ControllerTransportFixture.Send(server, message);
    internal Task<PlayerMessage> Receive() => ControllerTransportFixture.Receive(server);
    internal Task SendProof(PlayerSelectedObservation frame) => Send(new(PlayerMessageKind.SelectionDelivered, 0,
        PlayerSelectionDeliveryCodec.Encode(frame.StreamId, frame.Revision)));

    internal async Task Barrier()
    {
        Task ping = Client.PingAsync();
        PlayerMessage message = await Receive();
        Assert.Equal(PlayerMessageKind.Ping, message.Kind);
        await Send(new(PlayerMessageKind.Pong, message.RequestId, []));
        await ping.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        await server.DisposeAsync();
    }
}
