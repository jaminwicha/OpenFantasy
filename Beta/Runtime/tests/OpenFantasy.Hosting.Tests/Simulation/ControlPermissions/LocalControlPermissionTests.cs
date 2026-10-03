using System.Buffers.Binary;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation.ActorSelection;
using OpenFantasy.Hosting.Tests.Simulation.EffectiveControl;

namespace OpenFantasy.Hosting.Tests.Simulation.ControlPermissions;

public sealed class LocalControlPermissionTests
{
    [Fact]
    public async Task Real_home_offer_named_accept_and_revoke_are_private_nonadvancing_and_keep_exact_original_receipts()
    {
        await using var fixture = await ActorSelectionFixture.Open();
        var owner = (await fixture.Join(fixture.Third)).Player;
        var grantee = (await fixture.Join(fixture.Fourth)).Player;
        var outsider = (await fixture.Join(fixture.Owner)).Player;
        var board = await fixture.Host.ReadControlPermissionsAsync(owner);
        Assert.Equal(fixture.Third, board.HomeActorId);
        Assert.Empty(board.Slots);
        Assert.Equal(fixture.Actors, board.HumanActorIds);
        Assert.Equal(fixture.State.Space.GetActor(fixture.Third).Revision, board.HomeActorRevision);
        var before = fixture.State;
        var scopes = WorldControlCapabilities.Inventory | WorldControlCapabilities.CareDisposition;
        var offer = fixture.Commands.Command(before, fixture.Third, new OfferControlPermissionIntent(fixture.Fourth, 0, scopes));
        var offeredReceipt = await fixture.Host.SubmitControlPermissionAsync(owner, offer);
        Assert.Equal(WorldActionRejection.None, offeredReceipt.Rejection);
        Assert.Equal(owner.Delivery.PrincipalId, offeredReceipt.PrincipalId);
        Assert.Equal(offer.ContentHash, offeredReceipt.CommandHash);
        AssertConsentOnly(before, fixture.State);
        Assert.False(owner.Ready);
        Assert.False(grantee.Ready);
        var incoming = Assert.Single((await fixture.Host.ReadControlPermissionsAsync(grantee)).Slots);
        Assert.Equal(WorldControlPermissionStatus.Offered, incoming.Status);
        Assert.Equal(scopes, incoming.Capabilities);
        Assert.Equal((WorldControlCapabilities)0, scopes & WorldControlCapabilities.Recovery);
        Assert.Null(incoming.Acquisition);
        Assert.Null(incoming.AcceptanceCommandId);
        Assert.DoesNotContain((await fixture.Host.ReadControlPermissionsAsync(outsider)).Slots,
            row => row.OwnerActorId == fixture.Third || row.GranteeActorId == fixture.Fourth);

        var accept = fixture.Commands.Command(fixture.State, fixture.Fourth,
            new AcceptControlPermissionIntent(fixture.Third, incoming.Revision, incoming.GrantCommandId!.Value, incoming.TermsHash!));
        before = fixture.State;
        var acceptedReceipt = await fixture.Host.SubmitControlPermissionAsync(grantee, accept);
        Assert.Equal(WorldActionRejection.None, acceptedReceipt.Rejection);
        AssertConsentOnly(before, fixture.State);
        var accepted = Assert.Single((await fixture.Host.ReadControlPermissionsAsync(owner)).Slots);
        Assert.Equal(WorldControlPermissionStatus.Accepted, accepted.Status);
        Assert.Equal(accept.CommandId, accepted.AcceptanceCommandId);
        Assert.Equal(offer.CommandId, accepted.GrantCommandId);
        Assert.Equal(incoming.TermsHash, accepted.TermsHash);

        var revoke = fixture.Commands.Command(fixture.State, fixture.Third,
            new RevokeControlPermissionIntent(fixture.Fourth, accepted.Revision));
        before = fixture.State;
        var revokedReceipt = await fixture.Host.SubmitControlPermissionAsync(owner, revoke);
        Assert.Equal(WorldActionRejection.None, revokedReceipt.Rejection);
        AssertConsentOnly(before, fixture.State);
        var revoked = Assert.Single((await fixture.Host.ReadControlPermissionsAsync(grantee)).Slots);
        Assert.Equal(WorldControlPermissionStatus.Revoked, revoked.Status);
        Assert.Equal(accepted.Revision + 1, revoked.Revision);
        Assert.Null(revoked.TermsHash);
        Assert.Null(revoked.Capabilities);
        before = fixture.State;
        Assert.Equal(WorldReceiptCodecV1.Encode(offeredReceipt), WorldReceiptCodecV1.Encode(
            await fixture.Host.SubmitControlPermissionAsync(owner, offer)));
        Assert.Equal(WorldReceiptCodecV1.Encode(acceptedReceipt), WorldReceiptCodecV1.Encode(
            await fixture.Host.SubmitControlPermissionAsync(grantee, accept)));
        Assert.Same(before, fixture.State);
    }

    [Fact]
    public async Task Stale_consent_is_one_receipt_but_foreign_actor_delivery_and_altered_retry_never_enter_the_journal()
    {
        await using var fixture = await ActorSelectionFixture.Open();
        var owner = (await fixture.Join(fixture.Owner)).Player;
        var other = (await fixture.Join(fixture.Third)).Player;
        var command = fixture.Commands.Command(fixture.State, fixture.Owner,
            new OfferControlPermissionIntent(fixture.Grantee, 0, WorldControlCapabilities.Trade));
        var before = fixture.State;
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => fixture.Host.SubmitControlPermissionAsync(other, command));
        var forgedActor = new WorldActionCommand(fixture.Commands.NextId(), other.Delivery.PrincipalId,
            other.Delivery.SessionId, fixture.Owner, before.WorldId, before.BranchId,
            before.Space.GetActor(fixture.Owner).Revision, command.Intent);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => fixture.Host.SubmitControlPermissionAsync(other, forgedActor));
        Assert.Same(before, fixture.State);
        Assert.False(fixture.Authority.IsIdentityOccupiedForHost(command.CommandId));
        Assert.False(fixture.Authority.IsIdentityOccupiedForHost(forgedActor.CommandId));
        var receipt = await fixture.Host.SubmitControlPermissionAsync(owner, command);
        Assert.Equal(WorldActionRejection.RevisionConflict, receipt.Rejection);
        Assert.Equal(before.ControlPermissions!.ContentHash, fixture.State.ControlPermissions!.ContentHash);
        AssertConsentOnly(before, fixture.State);
        before = fixture.State;
        var altered = new WorldActionCommand(command.CommandId, command.PrincipalId, command.OriginSessionId,
            command.ActorId, command.WorldId, command.BranchId, command.ExpectedActorRevision,
            new OfferControlPermissionIntent(fixture.Grantee, 0, WorldControlCapabilities.Inventory));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => fixture.Host.SubmitControlPermissionAsync(owner, altered));
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(
            await fixture.Host.SubmitControlPermissionAsync(owner, command)));
        Assert.Same(before, fixture.State);
        Assert.False(fixture.Host.Completion.IsCompleted);
    }

    [Fact]
    public async Task Provisional_and_replaced_ports_cannot_read_or_administer_home_consent()
    {
        await using var fixture = await ActorSelectionFixture.Open();
        var before = fixture.State;
        var command = fixture.Commands.Command(before, fixture.Third,
            new OfferControlPermissionIntent(fixture.Fourth, 0, WorldControlCapabilities.CombatAndSurvival));
        await using (var provisional = await fixture.Host.ConnectNetworkPlayerAsync(fixture.Third, fixture.Delivery(fixture.Third),
            fixture.Admission.Compatibility, TestContext.Current.CancellationToken))
        {
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => fixture.Host.ReadControlPermissionsAsync(provisional));
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => fixture.Host.SubmitControlPermissionAsync(provisional, command));
            Assert.Same(before, fixture.State);
        }
        var first = await fixture.Join(fixture.Third);
        await first.Channel.DisposeAsync();
        var replacement = await fixture.Join(fixture.Third);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => fixture.Host.ReadControlPermissionsAsync(first.Player));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => fixture.Host.SubmitControlPermissionAsync(first.Player, command));
        Assert.Equal(WorldActionRejection.None,
            (await fixture.Host.SubmitControlPermissionAsync(replacement.Player, command)).Rejection);
    }

    [Fact]
    public async Task Board_opportunity_requires_real_absence_expiry_and_custody_stays_visible_when_selection_returns_home()
    {
        await using var fixture = await ActorSelectionFixture.Open();
        var owner = await fixture.Join(fixture.Owner);
        var grantee = (await fixture.Join(fixture.Grantee)).Player;
        Assert.Null(Assert.Single((await fixture.Host.ReadControlPermissionsAsync(grantee)).Slots).Acquisition);
        await fixture.Lose(owner);
        Assert.Null(Assert.Single((await fixture.Host.ReadControlPermissionsAsync(grantee)).Slots).Acquisition);
        await fixture.Tick(30_000);
        var board = await fixture.Host.ReadControlPermissionsAsync(grantee);
        var acquisition = Assert.IsType<LocalControlAcquisition>(Assert.Single(board.Slots).Acquisition);
        var before = fixture.State;
        Assert.Equal(fixture.Owner, acquisition.OwnerActorId);
        Assert.Null(board.HeldWitness);
        var witness = await grantee.AcquireControlAsync(acquisition);
        board = await fixture.Host.ReadControlPermissionsAsync(grantee);
        Assert.Equal(witness, board.HeldWitness);
        Assert.Null(Assert.Single(board.Slots).Acquisition);
        Assert.Equal(fixture.Grantee, grantee.ReadSelectedObservation().Selection.ActorId);
        var selected = await grantee.SelectActorAsync(grantee.ReadSelectedObservation().Selection, fixture.Owner);
        await grantee.SelectActorAsync(selected.Selection, fixture.Grantee);
        Assert.Equal(witness, (await fixture.Host.ReadControlPermissionsAsync(grantee)).HeldWitness);
        Assert.Equal(before.AdvancingSteps, fixture.State.AdvancingSteps);
        Assert.Equal(before.Calendar, fixture.State.Calendar);
        await grantee.ReleaseControlAsync(witness);
        board = await fixture.Host.ReadControlPermissionsAsync(grantee);
        Assert.Null(board.HeldWitness);
        Assert.Equal(witness.AssignmentRevision + 1, Assert.IsType<LocalControlAcquisition>(Assert.Single(board.Slots).Acquisition).ExpectedAssignmentRevision);
    }

    [Fact]
    public async Task Returning_home_owner_revokes_while_restoration_is_pending_without_erasing_accepted_paid_work()
    {
        await using var fixture = await LiveEffectiveControlFixture.Open(combatApproach: true);
        await fixture.AcquireAfterRealLoss();
        var accepted = fixture.QueueAttack();
        var owner = (await fixture.Join(fixture.Owner)).Channel.Player;
        Assert.True(owner.NetworkEstablished);
        Assert.False(owner.ControlAvailable);
        Assert.Equal(PlayerControlState.Pending, owner.ReadControlStatus().State);
        Assert.Equal(WorldControllerMode.Autonomous, fixture.State.Controllers!.Get(fixture.Owner).Mode);
        var board = await fixture.Host.ReadControlPermissionsAsync(owner);
        var grant = Assert.Single(board.Slots);
        var before = fixture.State;
        var revoke = fixture.Source.Commands.Command(before, fixture.Owner,
            new RevokeControlPermissionIntent(fixture.Grantee, grant.Revision));
        var receipt = await fixture.Host.SubmitControlPermissionAsync(owner, revoke);
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
        AssertConsentOnly(before, fixture.State);
        Assert.Contains(fixture.State.EncounterActions!.Pending, command => command.ContentHash == accepted.Command.ContentHash);
        Assert.Equal(accepted.Mp, fixture.State.Progression!.GetActor(fixture.Owner).CurrentMp);
        Assert.False(owner.ControlAvailable);
        Assert.False(owner.Ready);
        Assert.Equal(WorldControlPermissionStatus.Revoked,
            Assert.Single((await fixture.Host.ReadControlPermissionsAsync(owner)).Slots).Status);
        before = fixture.State;
        var unavailable = fixture.Source.Commands.Command(before, fixture.Owner,
            new OfferControlPermissionIntent(fixture.Grantee, grant.Revision + 1, WorldControlCapabilities.Combat));
        Assert.Equal(WorldActionRejection.Unavailable,
            (await fixture.Host.SubmitControlPermissionAsync(owner, unavailable)).Rejection);
        Assert.Equal(before.ControlPermissions!.ContentHash, fixture.State.ControlPermissions!.ContentHash);
        AssertConsentOnly(before, fixture.State);
        before = fixture.State;
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(
            await fixture.Host.SubmitControlPermissionAsync(owner, revoke)));
        Assert.Same(before, fixture.State);
        Assert.NotNull(fixture.Authority.FindReceipt(accepted.Command, fixture.Source.Delivery(fixture.Grantee)));
    }

    [Fact]
    public async Task Consent_uses_home_even_while_another_actor_is_selected_and_preserves_that_inputs_readiness()
    {
        await using var fixture = await ActorSelectionFixture.Open();
        var (joined, _, _, witness) = await fixture.Acquire();
        var player = joined.Player;
        var selected = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        player.ResumeSelected(selected);
        player.SetSelectedMovement(selected.Selection, 32, 0);
        var board = await fixture.Host.ReadControlPermissionsAsync(player);
        Assert.Equal(fixture.Grantee, board.HomeActorId);
        Assert.Equal(witness, board.HeldWitness);
        var before = fixture.State;
        var offer = fixture.Commands.Command(before, fixture.Grantee,
            new OfferControlPermissionIntent(fixture.Third, 0, WorldControlCapabilities.PrivateHistory));
        Assert.Null(offer.ControlWitness);
        Assert.Equal(WorldActionRejection.None,
            (await fixture.Host.SubmitControlPermissionAsync(player, offer)).Rejection);
        AssertConsentOnly(before, fixture.State);
        Assert.Equal(selected.Selection, player.ReadSelectedObservation().Selection);
        Assert.True(player.Ready);
        Assert.Equal(32L, player.MovementX);
        Assert.Equal(witness, player.LiveControlWitness);
    }

    [Fact]
    public async Task Board_rehydration_copies_bounded_collections_and_refuses_another_homes_private_pair()
    {
        await using var fixture = await ActorSelectionFixture.Open();
        var player = (await fixture.Join(fixture.Grantee)).Player;
        var board = await fixture.Host.ReadControlPermissionsAsync(player);
        var actors = board.HumanActorIds.ToArray();
        var slots = board.Slots.ToArray();
        var copy = new LocalControlPermissionBoard(board.WorldId, board.BranchId, board.WorldRevision,
            board.HomeActorId, board.HomeActorRevision, board.PermissionRevision, actors, slots);
        actors[0] = fixture.Commands.NextId();
        slots[0] = null!;
        Assert.Equal(board.HumanActorIds, copy.HumanActorIds);
        Assert.Equal(board.Slots, copy.Slots);
        Assert.Throws<NotSupportedException>(() => ((IList<EntityId>)copy.HumanActorIds).Add(fixture.Commands.NextId()));
        Assert.Throws<NotSupportedException>(() => ((IList<LocalControlPermissionSlot>)copy.Slots).Clear());
        Assert.Throws<ArgumentException>(() => new LocalControlPermissionBoard(board.WorldId, board.BranchId,
            board.WorldRevision, fixture.Third, board.HomeActorRevision, board.PermissionRevision,
            board.HumanActorIds, board.Slots));
        Assert.Throws<ArgumentException>(() => new LocalControlPermissionBoard(board.WorldId, board.BranchId,
            board.WorldRevision, board.HomeActorId, board.HomeActorRevision, board.PermissionRevision,
            board.HumanActorIds.Reverse().ToArray(), board.Slots));
        Assert.DoesNotContain(typeof(LocalControlPermissionSlot).GetProperties(), property =>
            property.PropertyType == typeof(WorldControlPermissionTerms)
            || property.Name.Contains("Principal", StringComparison.Ordinal)
            || property.Name.Contains("Session", StringComparison.Ordinal)
            || property.Name.Contains("Delivery", StringComparison.Ordinal));
    }

    private static void AssertConsentOnly(WorldSnapshot before, WorldSnapshot after)
    {
        Assert.Equal(before.Revision + 1, after.Revision);
        var original = Fields(WorldSnapshotCodec.Encode(before));
        var current = Fields(WorldSnapshotCodec.Encode(after));
        Assert.Equal(31, original.Length);
        for (int index = 0; index < original.Length; index++)
            if (index is not 8 and not 21 and not 29) Assert.Equal(original[index], current[index]);
        var priorLifecycle = Fields(original[21]);
        var lifecycle = Fields(current[21]);
        for (int index = 0; index < priorLifecycle.Length; index++)
            if (index != 5) Assert.Equal(priorLifecycle[index], lifecycle[index]);
        Assert.Equal(before.Calendar, after.Calendar);
        Assert.Equal(before.AdvancingSteps, after.AdvancingSteps);
    }

    private static byte[][] Fields(byte[] bytes)
    {
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8)));
        int offset = 12;
        var fields = new byte[count][];
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
            offset += 4;
            fields[index] = bytes.AsSpan(offset, length).ToArray();
            offset += length;
        }
        Assert.Equal(bytes.Length, offset);
        return fields;
    }
}
