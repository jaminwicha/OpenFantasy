using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.Selection;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Networking.ControllerTransport;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.PartyWithdrawal;

public sealed class PartyWithdrawalProtocolTests
{
    [Fact]
    public async Task Authenticated_selected_stream_delivers_collective_choices_and_accepts_the_captured_cancellation()
    {
        var source = WithdrawalWorldFixture.Active();
        source.Choose(WorldEncounterActions.DueActors(source.State)
            .Where(actor => !source.Humans.Contains(actor.ActorId)).Select(actor => source.Wait(actor.ActorId)).ToArray());
        using var files = new StorageFiles();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, source.Genesis);
        foreach (var batch in source.Batches) authority.Commit(batch);
        var time = new ManualSessionTimeProvider();
        await using var host = new LocalWorldSession(authority, source.Humans[0], source.Delivery(source.Humans[0]), time);
        var admission = new PlayerAdmissionService(host, []);
        var actor = source.Humans[1];
        var credential = admission.Issue(actor, source.Delivery(actor), TimeSpan.FromMinutes(1));
        var pair = InMemoryPlayerTransport.CreatePair();
        await using var client = pair.Client;
        await ControllerTransportFixture.Send(client, PlayerHandshakeCodec.Hello(1, admission.Compatibility, credential.Export()));
        await using var channel = await admission.AcceptAsync(pair.Server, TestContext.Current.CancellationToken);
        Assert.Equal(PlayerMessageKind.Welcome, (await ControllerTransportFixture.Receive(client)).Kind);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task running = new PlayerCommandSession(channel).RunAsync(stop.Token);
        try
        {
            await ControllerTransportFixture.Send(client, new(PlayerMessageKind.SelectionSubscribe, 2, new byte[] { 1 }));
            var initial = await Observe();
            Assert.Equal(5, initial.PresentationVersion);
            Assert.Equal(actor, initial.Observation.HomeView.Retreat!.Withdrawal!.ActorId);
            var proof = await ControllerTransportFixture.Receive(client);
            Assert.Equal(PlayerMessageKind.SelectionDelivered, proof.Kind);
            Assert.Equal((initial.StreamId, initial.Revision), PlayerSelectionDeliveryCodec.Decode(proof.Payload));
            await Send(3, PlayerSelectedInput.Resume(initial.Observation.Selection, initial.StreamId, initial.Revision));
            await SessionFixture.Until(() => channel.Player.Ready);
            var route = initial.Observation.HomeView.Retreat!.Offer!;
            Assert.True(route.CanQueue);
            var queued = source.Command(actor, new QueueEncounterRetreatIntent(route.EncounterId, route.TimingRevision,
                route.Destination, route.Route));
            await Send(4, PlayerSelectedInput.Command(initial.Observation.Selection, queued));
            await SessionFixture.Until(() => channel.Player.Pending is not null);
            await SessionFixture.Tick(time);
            await SessionFixture.Until(() => channel.Player.Pending is null);
            PlayerSelectedObservation? committed = null;
            bool receiptSeen = false;
            for (int index = 0; committed is null || !receiptSeen; index++)
            {
                Assert.True(index < 12);
                var message = await ControllerTransportFixture.Receive(client);
                if (message.Kind == PlayerMessageKind.SelectionObservation)
                {
                    var candidate = PlayerSelectedObservationCodec.Decode(message.Payload);
                    if (candidate.Observation.HomeView.Retreat!.Withdrawal!.Cancellation is not null) committed = candidate;
                }
                if (message.Kind == PlayerMessageKind.Receipt && message.RequestId == 4)
                {
                    Assert.Equal(WorldActionRejection.None, WorldReceiptCodecV1.Decode(message.Payload).Rejection);
                    receiptSeen = true;
                }
                Assert.NotEqual(PlayerMessageKind.Refusal, message.Kind);
            }
            var cancel = committed.Observation.HomeView.Retreat!.Withdrawal!.Cancellation!;
            Assert.Equal(queued.CommandId, cancel.WithdrawalCommandId);
            var command = source.Command(actor, cancel);
            await Send(5, PlayerSelectedInput.Command(committed.Observation.Selection, command));
            await SessionFixture.Until(() => channel.Player.Pending is not null);
            await SessionFixture.Tick(time);
            await SessionFixture.Until(() => channel.Player.Pending is null);
            Assert.Equal(WorldActionRejection.None, authority.FindReceipt(command, source.Delivery(actor))!.Rejection);
            Assert.Null(authority.ReadStateForHost().Retreat!.Withdrawal!.FindMember(actor));
            Assert.Equal(source.State.Space.GetActor(actor).Feet, authority.ReadStateForHost().Space.GetActor(actor).Feet);
            Assert.Equal(source.State.Calendar, authority.ReadStateForHost().Calendar);
        }
        finally
        {
            stop.Cancel();
            try { await ControllerTransportFixture.Finish(running); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }

        async Task Send(ulong id, PlayerSelectedInput input) => await ControllerTransportFixture.Send(client,
            new(PlayerMessageKind.SelectionInput, id, PlayerSelectedInputCodec.Encode(input)));
        async Task<PlayerSelectedObservation> Observe()
        {
            var message = await ControllerTransportFixture.Receive(client);
            Assert.Equal(PlayerMessageKind.SelectionObservation, message.Kind);
            return PlayerSelectedObservationCodec.Decode(message.Payload);
        }
    }
}
