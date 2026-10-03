using System.Diagnostics;
using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.Client;
using OpenFantasy.Hosting.Networking.Selection;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests.ConnectedControl;

public sealed class ConnectedControlTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Four_human_tls_applications_offer_accept_and_revoke_exact_scopes_without_disclosing_another_pairs_board()
    {
        await using var world = await ConnectedControlWorld.Open(prepareCombatApproach: false);
        try
        {
            var peers = new List<ConnectedControlWorld.Peer>();
            foreach (var actor in world.Actors) peers.Add(await world.Connect(actor));
            Assert.Equal(4, peers.Select(peer => peer.Application.Welcome.Delivery).Distinct().Count());
            var owner = peers[0];
            var grantee = peers[1];
            var before = await owner.Control.ReadControlPermissionsAsync();
            Assert.Empty(before.Slots);
            var scopes = WorldControlCapabilities.CombatAndSurvival | WorldControlCapabilities.CareDisposition;
            var offer = world.Permission(owner, before, new OfferControlPermissionIntent(world.Grantee, 0, scopes));
            var offerReceipt = await owner.Control.SubmitControlPermissionAsync(offer);
            Assert.Equal(WorldActionRejection.None, offerReceipt.Rejection);
            Assert.Equal(offer.ContentHash, offerReceipt.CommandHash);
            var incoming = Assert.Single((await grantee.Control.ReadControlPermissionsAsync()).Slots);
            Assert.Equal(scopes, incoming.Capabilities);
            Assert.Equal(WorldControlPermissionStatus.Offered, incoming.Status);
            Assert.Equal(offer.CommandId, incoming.GrantCommandId);
            foreach (var outsider in peers.Skip(2))
            {
                var privateBoard = await outsider.Control.ReadControlPermissionsAsync();
                Assert.Equal(outsider.Actor, privateBoard.HomeActorId);
                Assert.Empty(privateBoard.Slots);
                Assert.Null(privateBoard.HeldWitness);
            }
            var accept = world.Permission(grantee, await grantee.Control.ReadControlPermissionsAsync(),
                new AcceptControlPermissionIntent(world.Owner, incoming.Revision, incoming.GrantCommandId!.Value, incoming.TermsHash!));
            var accepted = await grantee.Control.SubmitControlPermissionAsync(accept);
            Assert.Equal(WorldActionRejection.None, accepted.Rejection);
            var board = await owner.Control.ReadControlPermissionsAsync();
            var grant = Assert.Single(board.Slots);
            Assert.Equal(WorldControlPermissionStatus.Accepted, grant.Status);
            Assert.Equal(accept.CommandId, grant.AcceptanceCommandId);
            Assert.Null(grant.Acquisition);
            var stale = world.Permission(owner, board, new OfferControlPermissionIntent(world.Grantee, 0, WorldControlCapabilities.Trade));
            Assert.Equal(WorldActionRejection.RevisionConflict, (await owner.Control.SubmitControlPermissionAsync(stale)).Rejection);
            var revoke = world.Permission(owner, await owner.Control.ReadControlPermissionsAsync(),
                new RevokeControlPermissionIntent(world.Grantee, grant.Revision));
            Assert.Equal(WorldActionRejection.None, (await owner.Control.SubmitControlPermissionAsync(revoke)).Rejection);
            board = await grantee.Control.ReadControlPermissionsAsync();
            Assert.Equal(WorldControlPermissionStatus.Revoked, Assert.Single(board.Slots).Status);
            Assert.Null(Assert.Single(board.Slots).TermsHash);
            var revision = board.WorldRevision;
            Assert.Equal(WorldReceiptCodecV1.Encode(offerReceipt), WorldReceiptCodecV1.Encode(
                await owner.Control.SubmitControlPermissionAsync(offer)));
            Assert.Equal(WorldReceiptCodecV1.Encode(accepted), WorldReceiptCodecV1.Encode(
                await grantee.Control.SubmitControlPermissionAsync(accept)));
            Assert.Equal(revision, (await grantee.Control.ReadControlPermissionsAsync()).WorldRevision);
            foreach (var peer in peers)
            {
                Assert.False(peer.Control.ReadSelectedObservation().IsReady);
                Assert.Equal(world.Genesis.Calendar, peer.Application.ReadView().Calendar);
            }
            await world.Stop();
            using var recovered = SqliteWorldAuthority.OpenExisting(world.Database, WorldSnapshotCodec.Encode(world.Genesis));
            Assert.Equal(0UL, recovered.ReadStateForHost().AdvancingSteps);
            Assert.Equal(WorldReceiptCodecV1.Encode(offerReceipt), WorldReceiptCodecV1.Encode(
                Assert.IsType<WorldActionReceipt>(recovered.FindReceipt(offer, owner.Application.Welcome.Delivery))));
        }
        finally { output.WriteLine(world.Describe()); }
    }

    [Fact]
    public async Task Actual_tls_loss_expiry_takeover_movement_and_paid_strike_keep_true_operator_and_receipts_after_owner_return()
    {
        await using var world = await ConnectedControlWorld.Open(prepareCombatApproach: true);
        try
        {
            var peers = new List<ConnectedControlWorld.Peer>();
            foreach (var actor in world.Actors) peers.Add(await world.Connect(actor));
            var owner = peers[0];
            var grantee = peers[1];
            var scopes = WorldControlCapabilities.Combat | WorldControlCapabilities.Traversal;
            var offer = world.Permission(owner, await owner.Control.ReadControlPermissionsAsync(),
                new OfferControlPermissionIntent(world.Grantee, 0, scopes));
            Assert.Equal(WorldActionRejection.None, (await owner.Control.SubmitControlPermissionAsync(offer)).Rejection);
            var board = await grantee.Control.ReadControlPermissionsAsync();
            var offered = Assert.Single(board.Slots);
            var accept = world.Permission(grantee, board,
                new AcceptControlPermissionIntent(world.Owner, offered.Revision, offered.GrantCommandId!.Value, offered.TermsHash!));
            Assert.Equal(WorldActionRejection.None, (await grantee.Control.SubmitControlPermissionAsync(accept)).Rejection);
            foreach (var outsider in peers.Skip(2)) Assert.Empty((await outsider.Control.ReadControlPermissionsAsync()).Slots);
            Assert.Null(Assert.Single((await grantee.Control.ReadControlPermissionsAsync()).Slots).Acquisition);

            var lossTime = Stopwatch.StartNew();
            await owner.DisposeAsync(); // The real client socket abort is observed and journaled by the server.
            var opportunity = await world.AwaitOriginalAllowance(grantee);
            Assert.True(lossTime.Elapsed >= TimeSpan.FromSeconds(30), "Acquisition must not precede the original measured 30-second allowance.");
            board = await grantee.Control.ReadControlPermissionsAsync();
            var revisionBeforeForeignAcquire = board.WorldRevision;
            await Assert.ThrowsAsync<PlayerRequestRefusedException>(() => peers[2].Control.AcquireControlAsync(opportunity));
            Assert.Equal(revisionBeforeForeignAcquire, (await grantee.Control.ReadControlPermissionsAsync()).WorldRevision);
            var witness = await grantee.Control.AcquireControlAsync(opportunity);
            Assert.Equal(world.Grantee, witness.GranteeActorId);
            Assert.Equal(world.Owner, witness.OwnerActorId);
            Assert.Equal(opportunity.DisconnectId, witness.DisconnectId);

            var first = await grantee.Control.SelectActorAsync(grantee.Control.ReadSelectedObservation().Selection, world.Owner);
            var operated = Assert.IsType<LocalControlledActorView>(first.ControlledView);
            Assert.Equal(world.Grantee, first.HomeView.PlayerActorId);
            Assert.Equal(world.Owner, operated.ActorId);
            Assert.Equal(first.HomeView.Calendar, operated.Calendar);
            Assert.Equal(scopes, operated.Capabilities);
            Assert.NotNull(operated.Combat);
            Assert.Null(operated.Inventory);
            Assert.Null(operated.Trade);
            Assert.Null(operated.Recovery);
            Assert.Null(operated.CareDisposition);
            Assert.Null(operated.Production);
            Assert.Null(operated.Conversation);
            Assert.False(first.IsReady);
            await Assert.ThrowsAsync<PlayerRequestRefusedException>(() => peers[2].Control.SelectActorAsync(
                peers[2].Control.ReadSelectedObservation().Selection, world.Owner));
            await Assert.ThrowsAsync<PlayerRequestRefusedException>(() => grantee.Control.InspectSelectedPaidQuoteHistoryAsync(first.Selection, world.NextId()));

            var home = await grantee.Control.SelectActorAsync(first.Selection, world.Grantee);
            var selected = await grantee.Control.SelectActorAsync(home.Selection, world.Owner);
            Assert.Equal(first.Selection.Generation + 2, selected.Selection.Generation);
            Assert.Equal(witness, selected.Selection.ControlWitness);
            Assert.Throws<SelectedInputUnavailableException>(() => grantee.Control.PauseSelected(first.Selection));
            Assert.Throws<SelectedInputUnavailableException>(() => grantee.Control.SetSelectedMovement(first.Selection, 0, 0));
            Assert.Throws<SelectedInputUnavailableException>(() => grantee.Control.ResumeSelected(first));
            Assert.False(grantee.Control.ReadSelectedObservation().IsReady);
            var start = selected.ControlledView!.Actors.Single(row => row.ActorId == world.Owner).Feet;
            grantee.Control.ResumeSelected(selected);
            await world.Until(() => grantee.Control.ReadSelectedObservation().IsReady, "Explicit Resume was not observed within five seconds.");
            grantee.Control.SetSelectedMovement(selected.Selection, world.EntryMovement.X, world.EntryMovement.Y);
            await world.Until(() => grantee.Control.ReadSelectedObservation().ControlledView!.Actors
                .Single(row => row.ActorId == world.Owner).Feet != start,
                "The single selected movement sample did not move the actual owner within five seconds.");
            grantee.Control.SetSelectedMovement(selected.Selection, 0, 0);
            grantee.Control.PauseSelected(selected.Selection);
            await world.Until(() => !grantee.Control.ReadSelectedObservation().IsReady, "Pause did not reach the selected observation.");
            Assert.Single(grantee.Tap.Sent.Where(message => message.Kind == PlayerMessageKind.SelectionInput)
                .Select(message => PlayerSelectedInputCodec.Decode(message.Payload)),
                input => input.Kind == PlayerSelectedInputKind.Movement && (input.DeltaX != 0 || input.DeltaY != 0));

            selected = grantee.Control.ReadSelectedObservation();
            var entry = selected.ControlledView!.Combat!.Lifecycle!.Entries.Single(row => row.ScenarioId == world.ScenarioId);
            Assert.True(entry.CanEnter, world.DescribeEntry(selected));
            grantee.Control.ResumeSelected(selected);
            await world.Until(() => grantee.Control.ReadSelectedObservation().IsReady, "Encounter Resume was not observed.");
            selected = grantee.Control.ReadSelectedObservation();
            var enter = world.Selected(grantee, selected, new EnterEncounterIntent(entry.ScenarioId, entry.EncounterId,
                selected.ControlledView!.Combat!.Lifecycle!.EncounterCount, entry.TargetRevision));
            Assert.Equal(WorldActionRejection.None,
                (await grantee.Control.SubmitSelectedAction(selected.Selection, enter).WaitAsync(TimeSpan.FromSeconds(30), ServerWorld.Cancellation)).Rejection);
            var strikeId = world.Genesis.EncounterLifecycle!.Abilities.Single(value => value.Key == EncounterFarmAdmission.StrikeKey).DefinitionId;
            await world.Until(() => grantee.Control.ReadSelectedObservation().ControlledView?.Combat?.Actions?.AbilityOffers
                .Any(value => value.AbilityDefinitionId == strikeId && value.TargetActorId == world.TargetActorId && value.CanQueue) == true,
                "The ordinary encounter did not reach the delegated actor's actual Strike choice.");
            selected = grantee.Control.ReadSelectedObservation();
            var combat = selected.ControlledView!.Combat!;
            var actions = combat.Actions!;
            var strike = actions.AbilityOffers.Single(value => value.AbilityDefinitionId == strikeId
                && value.TargetActorId == world.TargetActorId && value.CanQueue);
            Assert.True(strike.ReadinessDelay > 0);
            int ownerMp = combat.Resources!.Mp;
            int homeMp = selected.HomeView.Progression!.Mp;
            int targetHp = combat.Actors.Single(row => row.ActorId == world.TargetActorId).CurrentHp;
            var attack = world.Selected(grantee, selected,
                new QueueEncounterAbilityIntent(actions.EncounterId, actions.TimingRevision, strike.AbilityDefinitionId, strike.TargetActorId));
            var attackSelection = selected.Selection;
            var paidReceipt = await grantee.Control.SubmitSelectedAction(attackSelection, attack)
                .WaitAsync(TimeSpan.FromSeconds(30), ServerWorld.Cancellation);
            Assert.Equal(WorldActionRejection.None, paidReceipt.Rejection);
            Assert.Equal(grantee.Application.Welcome.Delivery.PrincipalId, paidReceipt.PrincipalId);
            Assert.Equal(attack.ContentHash, paidReceipt.CommandHash);

            // Acceptance frees this port's request slot, while the selected actor's paid decision
            // remains in the encounter. Every other required human supplies its own explicit choice.
            home = await grantee.Control.SelectActorAsync(grantee.Control.ReadSelectedObservation().Selection, world.Grantee);
            Assert.Null(home.ControlledView);
            var waits = new List<(ConnectedControlWorld.Peer Peer, WorldActionCommand Command, WorldActionReceipt Receipt)>();
            ulong previousTimingRevision = actions.TimingRevision;
            foreach (var peer in peers.Skip(1))
            {
                var own = peer.Control.ReadSelectedObservation();
                Assert.Equal(peer.Actor, own.Selection.ActorId);
                Assert.Null(own.Selection.ControlWitness);
                peer.Control.ResumeSelected(own);
                await world.Until(() => peer.Control.ReadSelectedObservation().IsReady
                    && peer.Control.ReadSelectedObservation().HomeView.EncounterActions is { CanWait: true } observedChoices
                    && observedChoices.TimingRevision > previousTimingRevision,
                    $"The real home actor {peer.Actor} did not expose its settled required Wait choice.");
                own = peer.Control.ReadSelectedObservation();
                var choices = own.HomeView.EncounterActions!;
                var wait = world.Selected(peer, own, new QueueEncounterWaitIntent(choices.EncounterId, choices.TimingRevision));
                var receipt = await peer.Control.SubmitSelectedAction(own.Selection, wait)
                    .WaitAsync(TimeSpan.FromSeconds(30), ServerWorld.Cancellation);
                Assert.Equal(WorldActionRejection.None, receipt.Rejection);
                Assert.Equal(peer.Application.Welcome.Delivery.PrincipalId, receipt.PrincipalId);
                Assert.Equal(wait.ContentHash, receipt.CommandHash);
                waits.Add((peer, wait, receipt));
                previousTimingRevision = choices.TimingRevision;
            }
            Assert.Equal(3, waits.Count);
            selected = await grantee.Control.SelectActorAsync(grantee.Control.ReadSelectedObservation().Selection, world.Owner);
            Assert.Equal(witness, selected.Selection.ControlWitness);
            grantee.Control.ResumeSelected(selected);
            await world.Until(() => grantee.Control.ReadSelectedObservation().ControlledView?.Combat?.Actors
                .SingleOrDefault(row => row.ActorId == world.TargetActorId) is { } target && target.CurrentHp < targetHp,
                "The accepted Strike did not publish its real target damage.");
            selected = grantee.Control.ReadSelectedObservation();
            Assert.Equal(ownerMp - (int)strike.ManaCost, selected.ControlledView!.Combat!.Resources!.Mp);
            Assert.Equal(homeMp, selected.HomeView.Progression!.Mp);
            foreach (var peer in peers.Skip(1))
                peer.Control.PauseSelected(peer.Control.ReadSelectedObservation().Selection);
            await world.Until(() => peers.Skip(1).All(peer => !peer.Control.ReadSelectedObservation().IsReady),
                "The three explicit post-action pauses were not observed.");

            var returnedOwner = await world.Connect(world.Owner);
            await world.Until(() => grantee.Control.ReadSelectedObservation().Selection.ActorId == world.Grantee,
                "Owner return did not force the delegate's paused home selection.");
            var forced = grantee.Control.ReadSelectedObservation();
            Assert.True(forced.Selection.Generation > attackSelection.Generation);
            Assert.Null(forced.ControlledView);
            Assert.False(forced.IsReady);
            Assert.Null((await grantee.Control.ReadControlPermissionsAsync()).HeldWitness);
            await world.Until(() => returnedOwner.Control.ReadSelectedObservation().ControlStatus.State != PlayerControlState.Pending,
                "The returned owner did not reach the ordinary safe restoration boundary.");
            board = await returnedOwner.Control.ReadControlPermissionsAsync();
            var revoke = world.Permission(returnedOwner, board,
                new RevokeControlPermissionIntent(world.Grantee, Assert.Single(board.Slots).Revision));
            Assert.Equal(WorldActionRejection.None, (await returnedOwner.Control.SubmitControlPermissionAsync(revoke)).Rejection);
            var revoked = await grantee.Control.ReadControlPermissionsAsync();
            Assert.Equal(WorldControlPermissionStatus.Revoked, Assert.Single(revoked.Slots).Status);
            var unchangedRevision = revoked.WorldRevision;
            Assert.Equal(WorldReceiptCodecV1.Encode(paidReceipt), WorldReceiptCodecV1.Encode(
                await grantee.Control.SubmitSelectedAction(attackSelection, attack)));
            var freshOldChoice = new WorldActionCommand(world.NextId(), attack.PrincipalId, attack.OriginSessionId,
                attack.ActorId, attack.WorldId, attack.BranchId, attack.ExpectedActorRevision, attack.Intent, attack.ControlWitness);
            await Assert.ThrowsAsync<PlayerRequestRefusedException>(() => grantee.Control.SubmitSelectedAction(attackSelection, freshOldChoice));
            Assert.Equal(unchangedRevision, (await grantee.Control.ReadControlPermissionsAsync()).WorldRevision);

            await world.Stop();
            using var recovered = SqliteWorldAuthority.OpenExisting(world.Database, WorldSnapshotCodec.Encode(world.Genesis));
            var final = recovered.ReadStateForHost();
            Assert.Equal(world.Genesis.Inventory.Controls, final.Inventory.Controls);
            Assert.Equal(world.Genesis.Residents.Select(row => (row.ActorId, row.ControllerKind)),
                final.Residents.Select(row => (row.ActorId, row.ControllerKind)));
            Assert.Equal(WorldReceiptCodecV1.Encode(paidReceipt), WorldReceiptCodecV1.Encode(
                Assert.IsType<WorldActionReceipt>(recovered.FindReceipt(attack, grantee.Application.Welcome.Delivery))));
            foreach (var wait in waits)
            {
                Assert.Equal(wait.Peer.Actor, wait.Command.ActorId);
                Assert.Null(wait.Command.ControlWitness);
                Assert.Equal(WorldReceiptCodecV1.Encode(wait.Receipt), WorldReceiptCodecV1.Encode(
                    Assert.IsType<WorldActionReceipt>(recovered.FindReceipt(wait.Command, wait.Peer.Application.Welcome.Delivery))));
            }
            Assert.False(recovered.IsIdentityOccupiedForHost(freshOldChoice.CommandId));
            var evidence = Assert.Single(final.ResourceHistory!.Evidence.OfType<WorldEncounterResourceEvidence>(),
                value => value.CommandId == attack.CommandId);
            Assert.Equal(WorldActionCodec.Encode(attack), WorldActionCodec.Encode(evidence.Request));
            Assert.Equal(witness, evidence.Request.ControlWitness);
            Assert.Equal(world.Owner, evidence.Resolution.ActorId);
            Assert.Equal(EncounterActionResolutionStatus.Damaged, evidence.Resolution.Status);
            Assert.Equal(strike.ManaCost, evidence.Resolution.ManaSpent);
            Assert.Equal(strike.ReadinessDelay, evidence.Resolution.Delay);
            Assert.True(evidence.Resolution.Damage > 0);
            Assert.Equal((uint)targetHp, evidence.Resolution.TargetHpBefore);
            Assert.Equal((uint)targetHp - evidence.Resolution.Damage, evidence.Resolution.TargetHpAfter);
            var paidState = recovered.ReadRecordedStateForHost(evidence.Resolution.WorldSequence).State;
            Assert.Equal(ownerMp - (int)strike.ManaCost, paidState.Progression!.GetActor(world.Owner).CurrentMp);
            Assert.Equal(evidence.Resolution.Delay, paidState.Pacing!.Encounter!.GetActor(world.Owner).RemainingDelay);
            Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(recovered.ReadRecordedStateForHost(final.Revision).State));
            recovered.Dispose();
            using var stored = WorldDatabase.Open(world.Database, create: false);
            var delegatedMovement = stored.ReadJournal().SelectMany(row => WorldStepCodec.Decode(row.AcceptedBatch).Movement)
                .Where(input => input.ControlWitness is not null).ToArray();
            Assert.NotEmpty(delegatedMovement);
            Assert.All(delegatedMovement, input =>
            {
                Assert.Equal(world.Owner, input.ActorId);
                Assert.Equal(grantee.Application.Welcome.Delivery, input.Delivery);
                Assert.Equal(witness, input.ControlWitness);
                Assert.Equal(world.EntryMovement, (input.DeltaX, input.DeltaY));
            });
        }
        finally { output.WriteLine(world.Describe()); }
    }
}
