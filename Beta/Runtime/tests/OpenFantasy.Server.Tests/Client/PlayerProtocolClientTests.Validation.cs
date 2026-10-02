using System.Buffers.Binary;
using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.Client;

namespace OpenFantasy.Server.Tests;

public sealed partial class PlayerProtocolClientTests
{
    [Theory]
    [InlineData("request")]
    [InlineData("actor")]
    [InlineData("world")]
    [InlineData("packages")]
    public async Task Startup_refuses_welcome_substitution_before_exposing_a_playable_client(string variant)
    {
        await using var fixture = await ProtocolClientFixture.Create();
        fixture.Transport.Incoming = message =>
        {
            if (message.Kind != PlayerMessageKind.Welcome) return message;
            PlayerWelcome welcome = PlayerHandshakeCodec.ReadWelcome(message);
            var original = welcome.Compatibility;
            var compatibility = new PlayerWorldCompatibility(variant == "world" ? new EntityId(700, 1) : original.WorldId,
                original.BranchId, original.ProfileVersion, original.AdmissionVersion,
                variant == "packages" ? ContentHash.Compute("wrong-package"u8) : original.PackagesVersion);
            return PlayerHandshakeCodec.Welcome(variant == "request" ? 99UL : message.RequestId, compatibility,
                variant == "actor" ? new EntityId(700, 2) : welcome.ActorId, welcome.Delivery);
        };
        await Assert.ThrowsAsync<FormatException>(() => fixture.Connect().WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation));
        Assert.Equal(0UL, fixture.World.Authority.ReadStateForHost().Revision);
    }

    [Theory]
    [InlineData("request")]
    [InlineData("kind")]
    [InlineData("scope")]
    public async Task Query_replies_must_match_the_actual_pending_request_and_authenticated_scope(string variant)
    {
        await using var fixture = await ProtocolClientFixture.Create();
        var client = await fixture.Connect();
        fixture.Transport.Incoming = message =>
        {
            if (message.Kind != PlayerMessageKind.QueryResult) return message;
            if (variant == "request") return new(message.Kind, message.RequestId + 100, message.Payload);
            if (variant == "kind") return new(PlayerMessageKind.Pong, message.RequestId, []);
            var value = PlayerQueryResultCodec.Decode(message.Payload);
            var foreign = new PlayerQueryResult(value.WorldId, value.BranchId, new EntityId(700, 10), value.Kind);
            return new(message.Kind, message.RequestId, PlayerQueryResultCodec.Encode(foreign));
        };
        await Assert.ThrowsAsync<FormatException>(() => client.QueryAsync(new(PlayerQueryKind.ReadTradeInquiry))
            .WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation));
        await Assert.ThrowsAsync<FormatException>(() => client.Completion.WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation));
        Assert.Equal(0UL, fixture.World.Authority.ReadStateForHost().Revision);
    }

    [Theory]
    [InlineData("command")]
    [InlineData("principal")]
    [InlineData("world")]
    [InlineData("hash")]
    public async Task Durable_receipt_substitution_is_rejected_even_when_the_transport_request_matches(string variant)
    {
        await using var fixture = await ProtocolClientFixture.Create();
        var client = await fixture.Connect();
        fixture.Transport.Incoming = message =>
        {
            if (message.Kind != PlayerMessageKind.Receipt) return message;
            byte[] bytes = message.Payload.ToArray();
            int field = variant switch { "command" => 2, "principal" => 3, "world" => 4, _ => 6 };
            byte[] replacement = variant == "hash" ? ContentHash.Compute("other-command"u8).ToByteArray()
                : new EntityId(700, 20).ToByteArray();
            int offset = 12; // OFC version-one frame header, followed by length-prefixed fields.
            for (int index = 0; index < field; index++)
                offset += 4 + checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
            Assert.Equal(replacement.Length, checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset))));
            replacement.CopyTo(bytes, offset + 4);
            // This remains a structurally valid canonical receipt. Correlation must reject it.
            Assert.Equal(bytes, WorldReceiptCodecV1.Encode(WorldReceiptCodecV1.Decode(bytes)));
            return new(message.Kind, message.RequestId, bytes);
        };
        client.SetReady(true);
        await client.PingAsync().WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
        WorldActionCommand command = fixture.Speak(20);
        await Assert.ThrowsAsync<FormatException>(() => client.SubmitActionAsync(command).WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation));
        Assert.Equal(CommandReceiptStatus.Applied, fixture.World.Authority.FindReceipt(command, fixture.World.Delivery)!.Status);
    }

    [Fact]
    public async Task Missing_delta_basis_requests_full_recovery_before_startup_publishes_a_view()
    {
        await using var fixture = await ProtocolClientFixture.Create();
        int altered = 0;
        fixture.Transport.Incoming = message =>
        {
            if (message.Kind != PlayerMessageKind.Observation || Interlocked.Exchange(ref altered, 1) != 0) return message;
            var frame = PlayerObservationFrameCodec.Decode(message.Payload);
            var missing = new PlayerObservationFrame(frame.StreamId, 2, 1, frame.ResultLength, 0, 0, frame.Replacement);
            return new(message.Kind, message.RequestId, PlayerObservationFrameCodec.Encode(missing));
        };
        var client = await fixture.Connect();
        Assert.Equal(fixture.World.Actor, client.ReadView().PlayerActorId);
        Assert.Equal(2, fixture.Transport.Sent.Count(message => message.Kind == PlayerMessageKind.Observation));
        Assert.Equal(0UL, fixture.World.Authority.ReadStateForHost().Revision);
    }

    [Fact]
    public async Task Backpressured_output_is_bounded_and_disposal_cancels_a_blocked_transport()
    {
        await using var fixture = await ProtocolClientFixture.Create();
        var client = await fixture.Connect();
        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Transport.BeforeSend = async (_, token) =>
        {
            blocked.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        };
        client.SetReady(true);
        await blocked.Task.WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
        Exception? overflow = null;
        for (int index = 0; index < 100; index++)
        {
            try { client.SetMovement(index % 2 == 0 ? 32 : -32, 0); }
            catch (Exception error) { overflow = error; break; }
        }
        Assert.NotNull(overflow);
        await client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
        Assert.True(client.Completion.IsCompleted);
        Assert.Equal(0UL, fixture.World.Authority.ReadStateForHost().Revision);
    }
}
