using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.Selection;
using OpenFantasy.Hosting.Tests.Networking.ControllerTransport;
using OpenFantasy.Hosting.Tests.Simulation.EffectiveControl;

namespace OpenFantasy.Hosting.Tests.Networking.Selection;

public sealed class SelectionDeliveryProtocolTests
{
    [Fact]
    public async Task Queued_selected_frame_cannot_authorize_resume_before_its_bounded_transport_delivery()
    {
        await using var world = await LiveEffectiveControlFixture.Open();
        var pair = InMemoryPlayerTransport.CreatePair();
        await using var client = pair.Client;
        await using var transport = new SelectedFrameBarrierTransport(pair.Server);
        var credential = world.Admission.Issue(world.Grantee, world.Source.Delivery(world.Grantee), TimeSpan.FromHours(1));
        await ControllerTransportFixture.Send(client,
            PlayerHandshakeCodec.Hello(1, world.Admission.Compatibility, credential.Export()));
        await using var channel = await world.Admission.AcceptAsync(transport, TestContext.Current.CancellationToken);
        Assert.Equal(PlayerMessageKind.Welcome, (await ControllerTransportFixture.Receive(client)).Kind);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task run = new PlayerCommandSession(channel).RunAsync(stop.Token);
        try
        {
            var before = world.State.ContentHash;
            await ControllerTransportFixture.Send(client, new(PlayerMessageKind.SelectionSubscribe, 2, new byte[] { 1 }));
            PlayerSelectedObservation queued = await transport.BlockedFrame.Task.WaitAsync(
                TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.False(channel.Player.Ready);

            // Give the raw peer even the exact unpublished bytes. Knowing or guessing the next
            // stream revision cannot replace completion of the actual server transport send.
            byte[] resume = PlayerSelectedInputCodec.Encode(PlayerSelectedInput.Resume(
                queued.Observation.Selection, queued.StreamId, queued.Revision));
            await ControllerTransportFixture.Send(client, new(PlayerMessageKind.SelectionInput, 3, resume));
            await ControllerTransportFixture.Send(client, new(PlayerMessageKind.Ping, 4, []));
            await transport.AfterEarlyResume.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.False(channel.Player.Ready);
            Assert.Equal(before, world.State.ContentHash);

            transport.Release.TrySetResult();
            var replies = new List<PlayerMessage>();
            for (int index = 0; index < 4; index++) replies.Add(await ControllerTransportFixture.Receive(client));
            var delivered = Assert.Single(replies, reply => reply.Kind == PlayerMessageKind.SelectionObservation);
            Assert.Equal(PlayerSelectedObservationCodec.Encode(queued), delivered.Payload.ToArray());
            var refusal = Assert.Single(replies, reply => reply.Kind == PlayerMessageKind.Refusal);
            Assert.Equal(3UL, refusal.RequestId);
            Assert.Equal(PlayerRequestRefusal.NotAdmitted, PlayerGameplayCodec.ReadRefusal(refusal));
            Assert.Equal(4UL, Assert.Single(replies, reply => reply.Kind == PlayerMessageKind.Pong).RequestId);
            var proof = Assert.Single(replies, reply => reply.Kind == PlayerMessageKind.SelectionDelivered);
            Assert.Equal(0UL, proof.RequestId);
            Assert.Equal((queued.StreamId, queued.Revision), PlayerSelectionDeliveryCodec.Decode(proof.Payload));
            Assert.False(channel.Player.Ready);

            await ControllerTransportFixture.Send(client, new(PlayerMessageKind.SelectionInput, 5, resume));
            await ControllerTransportFixture.Send(client, new(PlayerMessageKind.Ping, 6, []));
            for (int index = 0; index < 4; index++)
            {
                var reply = await ControllerTransportFixture.Receive(client);
                if (reply.Kind == PlayerMessageKind.Pong && reply.RequestId == 6) break;
                Assert.True(index < 3, "The accepted Resume did not reach its ordered protocol barrier.");
                Assert.True(reply.Kind is PlayerMessageKind.Pong or PlayerMessageKind.SelectionObservation);
            }
            Assert.True(channel.Player.Ready);
            Assert.Equal(before, world.State.ContentHash); // No fake-clock cutoff or gameplay was granted.
        }
        finally
        {
            transport.Release.TrySetResult();
            stop.Cancel();
            try { await ControllerTransportFixture.Finish(run); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }
    }

    private sealed class SelectedFrameBarrierTransport(PlayerMessageTransport inner)
        : PlayerMessageTransport(PlayerMessageCodec.MaximumMessageBytes)
    {
        internal TaskCompletionSource<PlayerSelectedObservation> BlockedFrame { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource AfterEarlyResume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _blocked;

        protected override async ValueTask SendMessageAsync(byte[] bytes, CancellationToken cancellationToken)
        {
            var message = PlayerMessageCodec.Decode(bytes);
            if (!_blocked && message.Kind == PlayerMessageKind.SelectionObservation)
            {
                _blocked = true;
                BlockedFrame.TrySetResult(PlayerSelectedObservationCodec.Decode(message.Payload));
                await Release.Task.WaitAsync(cancellationToken);
            }
            await inner.SendAsync(bytes, cancellationToken);
        }

        protected override async ValueTask<byte[]?> ReceiveMessageAsync(CancellationToken cancellationToken)
        {
            byte[]? bytes = await inner.ReceiveAsync(cancellationToken);
            if (bytes is not null && PlayerMessageCodec.Decode(bytes) is { Kind: PlayerMessageKind.Ping, RequestId: 4 })
                AfterEarlyResume.TrySetResult(); // Receive loop has finished applying the preceding request.
            return bytes;
        }

        protected override void CloseTransport() => inner.DisposeAsync().GetAwaiter().GetResult();
    }
}
