using System.Collections;
using System.Reflection;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.Client;
using OpenFantasy.Hosting.Networking.Selection;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Server.Tests.ConnectedControl;

namespace OpenFantasy.Server.Tests.PrivateHistoryDirectory;

public sealed class PrivateHistoryDirectoryTlsTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Fresh_tls_session_discovers_prior_terminal_records_and_history_only_delegate_loses_queued_read_on_return_and_revoke()
    {
        await using var world = await ConnectedControlWorld.Open(prepareCombatApproach: false);
        try
        {
            var original = await world.Connect(world.Owner);
            var grantee = await world.Connect(world.Grantee);
            var initial = original.Control.ReadSelectedObservation();
            original.Control.ResumeSelected(initial);
            await world.Until(() => original.Control.ReadSelectedObservation().IsReady, "Original participant did not Resume.");
            initial = original.Control.ReadSelectedObservation();
            var preparation = Assert.IsType<LocalMaterialProposalPreparation>(initial.HomeView.MaterialProposals!.Preparation);
            var lot = preparation.Lots.First();
            var intent = Assert.IsType<ProposeMaterialIntent>(preparation.Prepare(lot.Kind, lot.LotId, world.Actors[2], 1));
            var offer = world.Selected(original, initial, intent);
            Assert.Equal(WorldActionRejection.None,
                (await original.Control.SubmitSelectedAction(initial.Selection, offer).WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation)).Rejection);
            EntityId expectedSubject = WorldMaterialProposal.DeriveIdentity(offer.CommandId);
            await world.Until(() => original.Control.ReadSelectedObservation().HomeView.MaterialProposals!.Offers
                .Any(row => row.ProposalId == expectedSubject && row.Status == MaterialProposalStatus.Open), "Created proposal was not observed.");
            var beforeClose = original.Control.ReadSelectedObservation();
            var close = world.Selected(original, beforeClose, new WithdrawMaterialProposalIntent(expectedSubject));
            Assert.Equal(WorldActionRejection.None,
                (await original.Control.SubmitSelectedAction(beforeClose.Selection, close).WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation)).Rejection);
            original.Control.PauseSelected(beforeClose.Selection);
            await world.Until(() => !original.Control.ReadSelectedObservation().IsReady, "Original participant did not pause.");
            ulong beforeDisconnect = (await grantee.Control.ReadControlPermissionsAsync()).WorldRevision;
            await original.DisposeAsync();
            await world.Until(() => AdmissionReleased(world.Host.Admission, world.Owner),
                "The server did not release the original participant's actual admission after transport loss.");
            var afterDisconnect = await grantee.Control.ReadControlPermissionsAsync();
            Assert.True(afterDisconnect.WorldRevision > beforeDisconnect,
                "The settled original transport loss did not publish its durable controller event.");

            var owner = await world.Connect(world.Owner);
            Assert.NotEqual(initial.Selection.PortId, owner.Control.ReadSelectedObservation().Selection.PortId);
            IPrivateHistoryPlayerSession history = owner.Application;
            Assert.True(history.SupportsPrivateHistoryDirectory);
            var firstRequest = new LocalPrivateHistoryPageRequest();
            var observation = history.ReadPrivateHistoryObservation();
            // This request contains no remembered subject. The returned directory supplies it to inspection.
            var discovered = await history.ReadSelectedPrivateHistoryAsync(observation.Selection, firstRequest)
                .WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
            discovered.ValidateFor(observation, LocalSelectedQueryOperation.ReadPrivateHistory);
            discovered.Value.ValidateFor(firstRequest);
            var terminal = Assert.Single(discovered.Value.Entries);
            Assert.Equal(expectedSubject, terminal.SubjectId);
            Assert.Equal(world.Actors[2], terminal.CounterpartyActorId);
            Assert.Equal(MaterialProposalStatus.Withdrawn, terminal.MaterialProposalStatus);
            var inspected = await owner.Control.InspectSelectedMaterialProposalHistoryAsync(observation.Selection, terminal.SubjectId);
            Assert.Equal(terminal.ClosedAtSequence, inspected.Value!.ClosedAtSequence);
            var sentDirectory = Assert.Single(owner.Tap.Sent.Where(message => message.Kind == PlayerMessageKind.SelectionInput)
                .Select(message => PlayerSelectedInputCodec.Decode(message.Payload)),
                input => input.Operation == LocalSelectedQueryOperation.ReadPrivateHistory);
            Assert.Null(sentDirectory.SubjectId);
            Assert.Null(sentDirectory.DirectoryRequest!.Before);

            var outsiderResult = await ((IPrivateHistoryPlayerSession)grantee.Application).ReadSelectedPrivateHistoryAsync(
                grantee.Control.ReadSelectedObservation().Selection, firstRequest);
            Assert.Empty(outsiderResult.Value.Entries);
            var board = await owner.Control.ReadControlPermissionsAsync();
            var grant = world.Permission(owner, board,
                new OfferControlPermissionIntent(world.Grantee, 0, WorldControlCapabilities.PrivateHistory));
            Assert.Equal(WorldActionRejection.None, (await owner.Control.SubmitControlPermissionAsync(grant)).Rejection);
            var incoming = await grantee.Control.ReadControlPermissionsAsync();
            var offered = Assert.Single(incoming.Slots);
            var accept = world.Permission(grantee, incoming,
                new AcceptControlPermissionIntent(world.Owner, offered.Revision, offered.GrantCommandId!.Value, offered.TermsHash!));
            Assert.Equal(WorldActionRejection.None, (await grantee.Control.SubmitControlPermissionAsync(accept)).Rejection);
            await owner.DisposeAsync();
            var acquisition = await world.AwaitOriginalAllowance(grantee);
            await grantee.Control.AcquireControlAsync(acquisition);
            var selected = await grantee.Control.SelectActorAsync(grantee.Control.ReadSelectedObservation().Selection, world.Owner);
            var controlled = Assert.IsType<LocalControlledActorView>(selected.ControlledView);
            Assert.Equal(WorldControlCapabilities.PrivateHistory, controlled.Capabilities);
            Assert.Null(controlled.Inventory); Assert.Null(controlled.Trade); Assert.Null(controlled.Recovery);
            IPrivateHistoryPlayerSession delegatedHistory = grantee.Application;
            Assert.True(delegatedHistory.SupportsPrivateHistoryDirectory);
            Assert.Equal(selected, delegatedHistory.ReadPrivateHistoryObservation());
            var delegated = await delegatedHistory.ReadSelectedPrivateHistoryAsync(selected.Selection, firstRequest)
                .WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
            Assert.Equal(discovered.Value.Entries, delegated.Value.Entries);
            await Assert.ThrowsAsync<PlayerRequestRefusedException>(() => grantee.Control.InspectSelectedOpenMaterialProposalAsync(
                selected.Selection, terminal.SubjectId));

            var home = await grantee.Control.SelectActorAsync(selected.Selection, world.Grantee);
            var selectedAgain = await grantee.Control.SelectActorAsync(home.Selection, world.Owner);
            Assert.Throws<AuthorityDispatchException>(() => delegated.ValidateFor(selectedAgain, LocalSelectedQueryOperation.ReadPrivateHistory));
            Assert.Equal(selected.Selection.Generation + 2, selectedAgain.Selection.Generation);

            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            grantee.Tap.BeforeSend = async (message, stop) =>
            {
                if (message.Kind == PlayerMessageKind.SelectionInput
                    && PlayerSelectedInputCodec.Decode(message.Payload).Operation == LocalSelectedQueryOperation.ReadPrivateHistory)
                {
                    entered.TrySetResult();
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(5), stop);
                }
            };
            var queued = delegatedHistory.ReadSelectedPrivateHistoryAsync(selectedAgain.Selection, firstRequest);
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
                var returning = await world.Connect(world.Owner);
                var current = await returning.Control.ReadControlPermissionsAsync();
                var revoke = world.Permission(returning, current,
                    new RevokeControlPermissionIntent(world.Grantee, Assert.Single(current.Slots).Revision));
                Assert.Equal(WorldActionRejection.None, (await returning.Control.SubmitControlPermissionAsync(revoke)).Rejection);
                await world.Until(() => grantee.Control.ReadSelectedObservation().Selection != selectedAgain.Selection,
                    "Owner return/revocation did not invalidate the captured selected lifetime.");
            }
            finally { release.TrySetResult(); grantee.Tap.BeforeSend = null; }
            var refusal = await Assert.ThrowsAsync<PlayerRequestRefusedException>(() => queued);
            Assert.Equal(PlayerRequestRefusal.NotAdmitted, refusal.Refusal);
            Assert.False(grantee.Protocol.Completion.IsCompleted);
        }
        finally { output.WriteLine(world.Describe()); }
    }

    private static bool AdmissionReleased(PlayerAdmissionService admission, EntityId actor)
    {
        // Read only the exact admission lifecycle under its owning lock. A public board can observe
        // the loss publication before channel cleanup releases this credential; neither client
        // disposal nor an arbitrary delay proves that the server is ready for one new admission.
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags properties = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        object gate = typeof(PlayerAdmissionService).GetField("_gate", fields)!.GetValue(admission)!;
        lock (gate)
        {
            var credentials = (IEnumerable)typeof(PlayerAdmissionService).GetField("_credentials", fields)!.GetValue(admission)!;
            object binding = Assert.Single(credentials.Cast<object>(), candidate =>
                (EntityId)candidate.GetType().GetProperty("ActorId", properties)!.GetValue(candidate)! == actor);
            return !(bool)binding.GetType().GetProperty("Active", properties)!.GetValue(binding)!;
        }
    }
}
