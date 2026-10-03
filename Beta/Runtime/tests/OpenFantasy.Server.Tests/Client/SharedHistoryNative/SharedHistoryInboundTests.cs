using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.Selection;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Server.Tests.ConnectedControl;

namespace OpenFantasy.Server.Tests.SharedHistoryNative;

public sealed class SharedHistoryInboundTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Authorized_real_tls_reply_held_inbound_cannot_populate_a_closed_and_reopened_history_visit()
    {
        await using var world = await ConnectedControlWorld.Open(prepareCombatApproach: false);
        var model = new FarmHistoryModel();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var owner = await world.Connect(world.Owner);
            var observed = owner.Control.ReadSelectedObservation();
            owner.Control.ResumeSelected(observed);
            await world.Until(() => owner.Control.ReadSelectedObservation().IsReady, "The owner did not explicitly Resume.");
            observed = owner.Control.ReadSelectedObservation();
            var preparation = Assert.IsType<LocalMaterialProposalPreparation>(observed.HomeView.MaterialProposals!.Preparation);
            var lot = preparation.Lots.First();
            var intent = Assert.IsType<ProposeMaterialIntent>(preparation.Prepare(lot.Kind, lot.LotId, world.Actors[2], 1));
            var proposal = world.Selected(owner, observed, intent);
            Assert.Equal(WorldActionRejection.None, (await owner.Control.SubmitSelectedAction(observed.Selection, proposal)
                .WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation)).Rejection);
            EntityId subject = WorldMaterialProposal.DeriveIdentity(proposal.CommandId);
            await world.Until(() => owner.Control.ReadSelectedObservation().HomeView.MaterialProposals!.Offers
                .Any(row => row.ProposalId == subject && row.Status == MaterialProposalStatus.Open), "The actual gift was not observed.");
            observed = owner.Control.ReadSelectedObservation();
            var withdrawal = world.Selected(owner, observed, new WithdrawMaterialProposalIntent(subject));
            Assert.Equal(WorldActionRejection.None, (await owner.Control.SubmitSelectedAction(observed.Selection, withdrawal)
                .WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation)).Rejection);
            owner.Control.PauseSelected(observed.Selection);
            await world.Until(() => !owner.Control.ReadSelectedObservation().IsReady, "The owner did not pause before reading.");

            var held = new TaskCompletionSource<PlayerSelectedQueryResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            owner.Tap.BeforeReceive = async (message, stop) =>
            {
                if (message.Kind != PlayerMessageKind.SelectionQueryResult) return;
                var response = PlayerSelectedQueryResponseCodec.Decode(message.Payload);
                if (response.Operation != LocalSelectedQueryOperation.ReadPrivateHistory) return;
                held.TrySetResult(response);
                await release.Task.WaitAsync(TimeSpan.FromSeconds(5), stop);
            };
            IPrivateHistoryPlayerSession history = owner.Application;
            model.Open(history);
            var originalCapture = model.Capture;
            var response = await held.Task.WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
            var sent = DirectoryInputs(owner).Single();
            response.ValidateFor(sent, world.Genesis.WorldId, world.Genesis.BranchId, history.ReadPrivateHistoryObservation().Selection);
            Assert.Null(sent.SubjectId);
            Assert.Null(sent.DirectoryRequest!.Before);
            var actualRecord = Assert.Single(response.Directory!.Entries);
            Assert.Equal(subject, actualRecord.SubjectId);
            Assert.Equal(MaterialProposalStatus.Withdrawn, actualRecord.MaterialProposalStatus);
            Assert.True(model.IsBusy);
            Assert.Null(model.Page); // The real authorized server result has arrived at TLS but not at the application.

            model.Close();
            Assert.False(model.IsOpen);
            Assert.Null(model.Page);
            model.Open(history);
            Assert.NotEqual(originalCapture, model.Capture);
            Assert.True(model.IsBusy);
            Assert.Equal(FarmHistoryState.Busy, model.State);
            Assert.False(model.Refresh(originalCapture));
            Assert.Null(model.Page);
            Assert.Null(model.SelectedEntry);
            Assert.Null(model.Detail);
            Assert.Single(DirectoryInputs(owner)); // Reopening does not enqueue a duplicate read behind the held one.
            owner.Tap.BeforeReceive = null;
            release.TrySetResult();
            await world.Until(() => { model.Update(); return !model.IsBusy; }, "The delayed inbound read was not observed and drained.");
            Assert.True(model.IsOpen);
            Assert.Null(model.Page);
            Assert.Null(model.SelectedEntry);
            Assert.Null(model.Detail);
            Assert.Empty(model.Entries);
            Assert.Single(DirectoryInputs(owner));
            Assert.False(owner.Protocol.Completion.IsCompleted);

            Assert.True(model.Refresh(model.Capture));
            await world.Until(() => { model.Update(); return !model.IsBusy && model.Page is not null; },
                "Explicit Refresh after the stale inbound reply did not read a new page.");
            Assert.Equal(2, DirectoryInputs(owner).Length);
            var entry = Assert.Single(model.Entries);
            Assert.Equal(actualRecord, entry);
            Assert.True(model.Select(model.Capture, entry.Kind, entry.SubjectId));
            Assert.Null(model.Detail);
            Assert.True(model.Inspect(model.Capture, entry.Kind, entry.SubjectId));
            await world.Until(() => { model.Update(); return !model.IsBusy && model.Detail is not null; },
                "Explicit Inspect did not resolve the freshly discovered terminal detail.");
            var detail = Assert.IsType<FarmHistoryDetail>(model.Detail);
            Assert.Equal(entry, detail.Entry);
            Assert.Equal(subject, detail.MaterialProposal!.ProposalId);
            Assert.Equal(intent.Terms, detail.MaterialProposal.Terms);
            Assert.Equal(MaterialProposalStatus.Withdrawn, detail.MaterialProposal.Status);
            Assert.Equal(entry.ClosedAtSequence, detail.MaterialProposal.ClosedAtSequence);
            Assert.Null(detail.MaterialProposal.SenderPresenceRevision);
            Assert.Null(detail.PaidQuote);
            Assert.Equal(2, owner.Tap.Sent.Count(message => message.Kind == PlayerMessageKind.SelectionInput
                && PlayerSelectedInputCodec.Decode(message.Payload).Kind == PlayerSelectedInputKind.Command));
            output.WriteLine($"Held real inbound authorized record {entry.Kind}/{entry.SubjectId} closed at {entry.ClosedAtSequence}; "
                + "old opening discarded; exactly two explicit directory queries and two original gameplay submissions. No owner-return delivery claim.");
        }
        finally
        {
            release.TrySetResult();
            await model.DrainAsync().WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
            output.WriteLine(world.Describe());
        }
    }

    private static PlayerSelectedInput[] DirectoryInputs(ConnectedControlWorld.Peer peer) => peer.Tap.Sent
        .Where(message => message.Kind == PlayerMessageKind.SelectionInput)
        .Select(message => PlayerSelectedInputCodec.Decode(message.Payload))
        .Where(input => input.Kind == PlayerSelectedInputKind.Query && input.Operation == LocalSelectedQueryOperation.ReadPrivateHistory)
        .ToArray();
}
