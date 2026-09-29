using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedCustody;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;
using static OpenFantasy.Core.Tests.Authority.ConsentedCustody.ConsentedTransferFixture;

namespace OpenFantasy.Core.Tests.Authority.ConsentedCustody;

public sealed class ConsentedTransferAuthorityTests
{
    // The ordinary authority's independent oracle pins spawn ordinal 0 of command identity 20.
    private static readonly EntityId SplitChild = EntityId.Parse("b7250ae9037ee65abcdb9083b0d96574");

    [Fact]
    public void Ordinary_unilateral_transfer_into_another_owner_only_bag_stays_forbidden()
    {
        CountedAuthorityState state = Create();
        var ordinary = new CommandEnvelope(CommandId, SenderDelivery.PrincipalId, SenderDelivery.SessionId, Sender,
            World, Branch,
            [
                new(RevisionTargetKind.Branch, Branch, 7), new(RevisionTargetKind.Entity, Lot, 3),
                new(RevisionTargetKind.Entity, SenderStorage, 4), new(RevisionTargetKind.Entity, RecipientStorage, 5)
            ],
            new TransferLotIntent(Lot, SenderStorage, RecipientStorage, 6));
        AuthorityDecision decision = new CountedTransferAuthority().Decide(state, ordinary, SenderDelivery,
            new CommandCommitIdentity(World, 11));
        Assert.Equal(CommandRejectionReason.NotAuthorized, decision.Receipt.RejectionReason);
        Assert.Same(state, decision.Candidate);
    }

    [Fact]
    public void Partial_gift_between_owner_only_bags_moves_the_exact_source_prefix_under_both_approvals()
    {
        CountedAuthorityState original = Create();
        byte[] before = AuthorityEncodingV1.Encode(original);
        var terms = new ConsentedTransferTerms(World, Branch, Sender, Recipient, Lot, SenderStorage,
            RecipientStorage, 6, 7, 3, 4, 5);
        ConsentedTransferCommand command = Command(terms);
        ConsentedTransferAcceptance acceptance = Acceptance(command);

        ConsentedTransferDecision decision = Decide(original, command, acceptance);

        Assert.True(decision.IsApplied);
        Assert.Equal(CommandRejectionReason.None, decision.Receipt.RejectionReason);
        Assert.Equal(original.ContentHash, decision.BaseStateHash);
        Assert.Equal(SplitChild, decision.MovedLotId);
        CustodySnapshot custody = decision.Candidate.Custody;
        CountedSourceLot moved = custody.GetLot(SplitChild);
        CountedSourceLot remainder = custody.GetLot(Lot);
        Assert.Equal(2, custody.Lots.Count);
        Assert.Equal(new SourceInterval(Source, 10, 16), moved.Source);
        Assert.Equal(new SourceInterval(Source, 16, 18), remainder.Source);
        Assert.Equal(RecipientStorage, moved.CustodianId);
        Assert.Equal(SenderStorage, remainder.CustodianId);
        Assert.Equal(Wood, moved.Definition);
        Assert.Equal(Wood, remainder.Definition);
        Assert.Equal(0UL, moved.Revision);
        Assert.Equal(4UL, remainder.Revision);
        Assert.Equal(8UL, custody.Revision);
        Assert.Equal(5UL, custody.GetStorage(SenderStorage).Revision);
        Assert.Equal(6UL, custody.GetStorage(RecipientStorage).Revision);
        Assert.Equal(0UL, custody.GetStorage(SenderSpareStorage).Revision);
        Assert.Equal(0UL, custody.GetStorage(BystanderStorage).Revision);
        Assert.Equal(2UL, custody.GetStoredQuantity(SenderStorage));
        Assert.Equal(6UL, custody.GetStoredQuantity(RecipientStorage));

        Assert.Equal(original.Policies, decision.Candidate.Policies);
        Assert.All(decision.Candidate.Policies, policy => Assert.Equal(CustodyDepositRule.OwnerOnly, policy.DepositRule));
        Assert.Equal(original.Controls, decision.Candidate.Controls);
        Assert.Equal(original.Sources, decision.Candidate.Sources);
        Assert.Equal(original.Actors, decision.Candidate.Actors);
        Assert.Equal(before, AuthorityEncodingV1.Encode(original));
    }

    [Fact]
    public void Applied_evidence_names_both_approvals_the_commit_and_the_moved_lot()
    {
        CountedAuthorityState state = Create();
        ConsentedTransferCommand command = Command(Terms(state));
        ConsentedTransferAcceptance acceptance = Acceptance(command);
        ConsentedTransferReceipt receipt = Decide(state, command, acceptance).Receipt;
        Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
        Assert.Equal(World, receipt.WorldId);
        Assert.Equal(Branch, receipt.BranchId);
        Assert.Equal(11UL, receipt.Commit.Sequence);
        Assert.Equal(CommandId, receipt.CommandId);
        Assert.Equal(SenderDelivery.PrincipalId, receipt.SenderPrincipalId);
        Assert.Equal(command.ComputeContentHash(), receipt.CommandHash);
        Assert.Equal(AcceptanceId, receipt.AcceptanceId);
        Assert.Equal(RecipientDelivery.PrincipalId, receipt.RecipientPrincipalId);
        Assert.Equal(acceptance.ComputeContentHash(), receipt.AcceptanceHash);
        Assert.NotEqual(receipt.CommandHash, receipt.AcceptanceHash);
        Assert.Equal(SplitChild, receipt.MovedLotId);
        Assert.True(receipt.Records(command, acceptance));
        Assert.False(receipt.Records(command, Acceptance(command, acceptanceId: Id(33))));
        Assert.False(receipt.Records(Command(Terms(state, quantity: 5)), acceptance));
    }

    [Fact]
    public void Whole_lot_gift_keeps_the_lot_identity_and_source_interval()
    {
        CountedAuthorityState state = Create();
        ConsentedTransferCommand command = Command(Terms(state, quantity: 8));
        ConsentedTransferDecision decision = Decide(state, command, Acceptance(command));
        Assert.True(decision.IsApplied);
        Assert.Equal(Lot, decision.MovedLotId);
        CountedSourceLot only = Assert.Single(decision.Candidate.Custody.Lots);
        Assert.Equal(new SourceInterval(Source, 10, 18), only.Source);
        Assert.Equal(RecipientStorage, only.CustodianId);
        Assert.Equal(4UL, only.Revision);
        Assert.Equal(0UL, decision.Candidate.Custody.GetStoredQuantity(SenderStorage));
    }

    [Fact]
    public void Forged_or_borrowed_delivery_on_either_side_is_not_authorized()
    {
        CountedAuthorityState state = Create();
        ConsentedTransferCommand command = Command(Terms(state));
        ConsentedTransferAcceptance acceptance = Acceptance(command);
        (TrustedCommandDelivery Sender, TrustedCommandDelivery Recipient)[] deliveries =
        [
            (new(Id(90), SenderDelivery.SessionId), RecipientDelivery),
            (new(SenderDelivery.PrincipalId, Id(90)), RecipientDelivery),
            (SenderDelivery, new(Id(90), RecipientDelivery.SessionId)),
            (SenderDelivery, new(RecipientDelivery.PrincipalId, Id(90))),
            (RecipientDelivery, SenderDelivery),
            (SenderDelivery, SenderDelivery),
            (RecipientDelivery, RecipientDelivery),
            (SenderDelivery, BystanderDelivery)
        ];
        foreach ((TrustedCommandDelivery sender, TrustedCommandDelivery recipient) in deliveries)
        {
            AssertRejectedUnchanged(state, Decide(state, command, acceptance, sender, recipient),
                CommandRejectionReason.NotAuthorized);
        }
    }

    [Fact]
    public void Submitted_identifiers_never_become_credentials()
    {
        CountedAuthorityState state = Create();
        ConsentedTransferCommand command = Command(Terms(state));

        // The sender approves for the recipient through the sender's own verified delivery.
        ConsentedTransferAcceptance selfApproved = Acceptance(command, principal: SenderDelivery.PrincipalId,
            session: SenderDelivery.SessionId);
        AssertRejectedUnchanged(state, Decide(state, command, selfApproved, recipientDelivery: SenderDelivery),
            CommandRejectionReason.NotAuthorized);

        // A bystander's genuine delivery approves a transfer naming somebody else as recipient.
        ConsentedTransferAcceptance bystander = Acceptance(command, principal: BystanderDelivery.PrincipalId,
            session: BystanderDelivery.SessionId);
        AssertRejectedUnchanged(state, Decide(state, command, bystander, recipientDelivery: BystanderDelivery),
            CommandRejectionReason.NotAuthorized);

        // Claims copy the real participants' identifiers while the deliveries belong to others.
        ConsentedTransferCommand claimsRecipient = Command(Terms(state), principal: RecipientDelivery.PrincipalId,
            session: RecipientDelivery.SessionId);
        AssertRejectedUnchanged(state, Decide(state, claimsRecipient, Acceptance(claimsRecipient)),
            CommandRejectionReason.NotAuthorized);
        ConsentedTransferAcceptance staleSession = Acceptance(command, session: Id(90));
        AssertRejectedUnchanged(state, Decide(state, command, staleSession), CommandRejectionReason.NotAuthorized);
    }

    [Fact]
    public void Wrong_owner_recipient_or_branch_is_not_authorized()
    {
        CountedAuthorityState state = Create();
        ConsentedTransferTerms[] refused =
        [
            Terms(state, source: RecipientStorage, destination: SenderStorage),
            Terms(state, source: BystanderStorage),
            Terms(state, destination: BystanderStorage),
            Terms(state, destination: SenderSpareStorage),
            Terms(state, recipient: Bystander),
            Terms(state, sender: Bystander),
            Terms(state, source: Id(998), destination: Id(999)),
            Terms(state, branch: Id(99))
        ];
        foreach (ConsentedTransferTerms terms in refused)
        {
            ConsentedTransferCommand command = Command(terms);
            AssertRejectedUnchanged(state, Decide(state, command, Acceptance(command)),
                CommandRejectionReason.NotAuthorized);
        }
    }

    [Fact]
    public void Wrong_world_is_a_dispatch_precondition_while_a_mismatched_acceptance_world_is_refused()
    {
        CountedAuthorityState state = Create();
        ConsentedTransferCommand command = Command(Terms(state));
        ConsentedTransferCommand elsewhere = Command(Terms(state, world: Id(99)));
        Assert.Throws<ArgumentException>(() => Decide(state, elsewhere, Acceptance(elsewhere)));
        Assert.Throws<ArgumentException>(() => new ConsentedTransferAuthority().Decide(state,
            new(command, SenderDelivery), new(Acceptance(command), RecipientDelivery),
            new CommandCommitIdentity(Id(99), 11), NothingRetired));
        var otherWorld = new ConsentedTransferAcceptance(AcceptanceId, RecipientDelivery.PrincipalId,
            RecipientDelivery.SessionId, command.CommandId, command.ComputeContentHash(), Terms(state, world: Id(99)));
        AssertRejectedUnchanged(state, Decide(state, command, otherWorld), CommandRejectionReason.NotAuthorized);
    }

    [Fact]
    public void Acceptance_binds_one_exact_sender_command_and_every_term()
    {
        CountedAuthorityState state = Create(recipientCapacity: 10);
        ConsentedTransferCommand approved = Command(Terms(state));
        ConsentedTransferAcceptance acceptance = Acceptance(approved);
        ConsentedTransferCommand[] substituted =
        [
            Command(Terms(state, quantity: 8)),
            Command(Terms(state, quantity: 5)),
            Command(Terms(state, destination: RecipientSpareStorage)),
            Command(Terms(state, branchRevision: 8)),
            Command(Terms(state, lotRevision: 4)),
            Command(Terms(state, sourceRevision: 5)),
            Command(Terms(state, destinationRevision: 6)),
            Command(Terms(state), commandId: Id(40))
        ];
        foreach (ConsentedTransferCommand command in substituted)
        {
            Assert.False(acceptance.Binds(command));
            AssertRejectedUnchanged(state, Decide(state, command, acceptance), CommandRejectionReason.NotAuthorized);
        }

        // The substituted amount and destination are legal once their own exact acceptance exists.
        Assert.True(Decide(state, substituted[0], Acceptance(substituted[0])).IsApplied);
        Assert.True(Decide(state, substituted[2], Acceptance(substituted[2])).IsApplied);
        Assert.True(acceptance.Binds(approved));
        Assert.True(Decide(state, approved, acceptance).IsApplied);
    }

    [Fact]
    public void Neither_a_matching_hash_nor_matching_terms_alone_approves_a_command()
    {
        CountedAuthorityState state = Create(recipientCapacity: 10);
        ConsentedTransferCommand command = Command(Terms(state));
        var otherHash = new ConsentedTransferAcceptance(AcceptanceId, RecipientDelivery.PrincipalId,
            RecipientDelivery.SessionId, command.CommandId, Command(Terms(state, quantity: 8)).ComputeContentHash(),
            command.Terms);
        var otherTerms = new ConsentedTransferAcceptance(AcceptanceId, RecipientDelivery.PrincipalId,
            RecipientDelivery.SessionId, command.CommandId, command.ComputeContentHash(), Terms(state, quantity: 8));
        var otherCommand = new ConsentedTransferAcceptance(AcceptanceId, RecipientDelivery.PrincipalId,
            RecipientDelivery.SessionId, Id(40), command.ComputeContentHash(), command.Terms);
        foreach (ConsentedTransferAcceptance acceptance in new[] { otherHash, otherTerms, otherCommand })
        {
            AssertRejectedUnchanged(state, Decide(state, command, acceptance), CommandRejectionReason.NotAuthorized);
        }
    }

    [Fact]
    public void Earlier_command_acceptance_and_retired_lot_identities_stay_occupied()
    {
        CountedAuthorityState state = Create();
        ConsentedTransferCommand command = Command(Terms(state));
        ConsentedTransferAcceptance acceptance = Acceptance(command);
        foreach (EntityId retired in new[] { CommandId, AcceptanceId, SplitChild })
        {
            AssertRejectedUnchanged(state, Decide(state, command, acceptance, history: identity => identity == retired),
                CommandRejectionReason.ResourceUnavailable);
        }

        // A whole lot spawns no child, so a retired identity equal to the unused child does not matter.
        ConsentedTransferCommand whole = Command(Terms(state, quantity: 8));
        Assert.True(Decide(state, whole, Acceptance(whole), history: identity => identity == SplitChild).IsApplied);
        Assert.True(Decide(state, command, acceptance, history: identity => identity == Id(777)).IsApplied);
    }

    [Fact]
    public void Fresh_identities_cannot_alias_each_other_or_any_admitted_identity()
    {
        CountedAuthorityState state = Create();
        foreach (EntityId occupied in new[] { World, Branch, Recipient, Bystander, Lot, Source, SenderStorage,
            Wood.DefinitionId, SenderDelivery.PrincipalId, RecipientDelivery.SessionId })
        {
            ConsentedTransferCommand aliasedCommand = Command(Terms(state), commandId: occupied);
            AssertRejectedUnchanged(state, Decide(state, aliasedCommand, Acceptance(aliasedCommand)),
                CommandRejectionReason.ResourceUnavailable);
            ConsentedTransferCommand command = Command(Terms(state));
            AssertRejectedUnchanged(state, Decide(state, command, Acceptance(command, acceptanceId: occupied)),
                CommandRejectionReason.ResourceUnavailable);
        }

        ConsentedTransferCommand original = Command(Terms(state));
        AssertRejectedUnchanged(state, Decide(state, original, Acceptance(original, acceptanceId: CommandId)),
            CommandRejectionReason.ResourceUnavailable);
        AssertRejectedUnchanged(state, Decide(state, original, Acceptance(original, acceptanceId: SplitChild)),
            CommandRejectionReason.ResourceUnavailable);
        CountedAuthorityState childIsAnActor = Create(extraActors: [SplitChild]);
        AssertRejectedUnchanged(childIsAnActor, Decide(childIsAnActor, original, Acceptance(original)),
            CommandRejectionReason.ResourceUnavailable);
    }

    [Fact]
    public void Replayed_approvals_cannot_move_stock_again()
    {
        CountedAuthorityState state = Create();
        ConsentedTransferCommand command = Command(Terms(state));
        ConsentedTransferAcceptance acceptance = Acceptance(command);
        ConsentedTransferDecision applied = Decide(state, command, acceptance);
        HashSet<EntityId> history = [command.CommandId, acceptance.AcceptanceId, applied.MovedLotId!.Value];
        CountedAuthorityState published = applied.Candidate;

        AssertRejectedUnchanged(state, Decide(state, command, acceptance, history: history.Contains),
            CommandRejectionReason.ResourceUnavailable);
        AssertRejectedUnchanged(published, Decide(published, command, acceptance, history: history.Contains),
            CommandRejectionReason.ResourceUnavailable);
        AssertRejectedUnchanged(published, Decide(published, command, acceptance),
            CommandRejectionReason.ResourceUnavailable);

        // Current revisions do not free an earlier command identity or the live spawned child.
        ConsentedTransferCommand reused = Command(Terms(published, quantity: 2));
        AssertRejectedUnchanged(published, Decide(published, reused, Acceptance(reused, acceptanceId: Id(34)),
            history: history.Contains), CommandRejectionReason.ResourceUnavailable);
        ConsentedTransferCommand next = Command(Terms(published, quantity: 2), commandId: Id(60));
        AssertRejectedUnchanged(published, Decide(published, next, Acceptance(next, acceptanceId: SplitChild),
            history: history.Contains), CommandRejectionReason.ResourceUnavailable);
        ConsentedTransferDecision second = Decide(published, next, Acceptance(next, acceptanceId: Id(61)),
            history: history.Contains);
        Assert.True(second.IsApplied);
        Assert.Equal(8UL, second.Candidate.Custody.GetStoredQuantity(RecipientStorage));
    }

    [Fact]
    public void Protected_reserved_missing_and_oversized_requests_share_one_public_reason()
    {
        CountedAuthorityState[] unavailable =
        [
            Create(protection: LotProtection.Equipped), Create(reservedBy: Id(80)),
            Create(recipientCapacity: 5), Create(branchRevision: ulong.MaxValue)
        ];
        foreach (CountedAuthorityState state in unavailable)
        {
            ConsentedTransferCommand command = Command(Terms(state));
            AssertRejectedUnchanged(state, Decide(state, command, Acceptance(command)),
                CommandRejectionReason.ResourceUnavailable);
        }

        CountedAuthorityState stocked = Create(recipientCapacity: 10);
        ConsentedTransferTerms[] refused =
        [
            Terms(stocked, quantity: 9),
            Terms(stocked, lot: Id(999)),
            Terms(stocked, source: SenderSpareStorage),
            Terms(stocked, lotRevision: 2),
            Terms(stocked, destinationRevision: 4)
        ];
        foreach (ConsentedTransferTerms terms in refused)
        {
            ConsentedTransferCommand command = Command(terms);
            AssertRejectedUnchanged(stocked, Decide(stocked, command, Acceptance(command)),
                CommandRejectionReason.ResourceUnavailable);
        }
    }

    [Fact]
    public void Approval_is_stale_after_an_intervening_transfer()
    {
        CountedAuthorityState original = Create();
        ConsentedTransferCommand command = Command(Terms(original));
        ConsentedTransferAcceptance acceptance = Acceptance(command);
        ConsentedTransferDecision decidedEarlier = Decide(original, command, acceptance);

        var intervening = new CommandEnvelope(Id(50), SenderDelivery.PrincipalId, SenderDelivery.SessionId, Sender,
            World, Branch,
            [
                new(RevisionTargetKind.Branch, Branch, 7), new(RevisionTargetKind.Entity, Lot, 3),
                new(RevisionTargetKind.Entity, SenderStorage, 4), new(RevisionTargetKind.Entity, SenderSpareStorage, 0)
            ],
            new TransferLotIntent(Lot, SenderStorage, SenderSpareStorage, 2));
        AuthorityDecision unilateral = new CountedTransferAuthority().Decide(original, intervening, SenderDelivery,
            new CommandCommitIdentity(World, 11));
        Assert.Equal(CommandReceiptStatus.Applied, unilateral.Receipt.Status);
        CountedAuthorityState current = unilateral.Candidate;

        AssertRejectedUnchanged(current, Decide(current, command, acceptance),
            CommandRejectionReason.ResourceUnavailable);
        Assert.Equal(CustodyConflict.StalePlan,
            Assert.Throws<CustodyConflictException>(() => decidedEarlier.ApplyTo(current)).Conflict);
        Assert.Same(decidedEarlier.Candidate, decidedEarlier.ApplyTo(Create()));

        ConsentedTransferCommand renewed = Command(Terms(current), commandId: Id(60));
        ConsentedTransferDecision decision = Decide(current, renewed, Acceptance(renewed, acceptanceId: Id(61)));
        Assert.True(decision.IsApplied);
        Assert.Equal(Lot, decision.MovedLotId);
        Assert.Equal(new SourceInterval(Source, 12, 18), decision.Candidate.Custody.GetLot(Lot).Source);
        Assert.Equal(2UL, decision.Candidate.Custody.GetStoredQuantity(SenderSpareStorage));
        Assert.Equal(6UL, decision.Candidate.Custody.GetStoredQuantity(RecipientStorage));
    }

    [Fact]
    public void Changed_control_makes_a_decided_plan_stale()
    {
        CountedAuthorityState original = Create();
        ConsentedTransferCommand command = Command(Terms(original));
        ConsentedTransferDecision decision = Decide(original, command, Acceptance(command));
        CountedAuthorityState reconnected = Create(controls:
        [
            new(SenderDelivery.PrincipalId, SenderDelivery.SessionId, Sender),
            new(RecipientDelivery.PrincipalId, Id(39), Recipient)
        ]);
        Assert.Throws<CustodyConflictException>(() => decision.ApplyTo(reconnected));
        AssertRejectedUnchanged(reconnected, Decide(reconnected, command, Acceptance(command)),
            CommandRejectionReason.NotAuthorized);
    }

    [Fact]
    public void Changed_storage_owner_makes_a_decided_plan_stale_and_requires_the_new_owners_approval()
    {
        CountedAuthorityState original = Create();
        ConsentedTransferCommand command = Command(Terms(original));
        ConsentedTransferAcceptance acceptance = Acceptance(command);
        ConsentedTransferDecision decided = Decide(original, command, acceptance);
        var reassigned = new CountedAuthorityState(original.Custody, original.AdmittedUnitCapacity,
            original.Actors, original.Controls, original.Policies.Select(policy =>
                policy.CustodianId == RecipientStorage
                    ? new CustodianAccessPolicy(RecipientStorage, Bystander, policy.DepositRule) : policy),
            original.Sources, original.ReservationIds);

        Assert.Equal(CustodyConflict.StalePlan,
            Assert.Throws<CustodyConflictException>(() => decided.ApplyTo(reassigned)).Conflict);
        AssertRejectedUnchanged(reassigned, Decide(reassigned, command, acceptance),
            CommandRejectionReason.NotAuthorized);
    }

    [Theory]
    [InlineData(LotProtection.UserReserved)]
    [InlineData(LotProtection.Heirloom)]
    [InlineData(LotProtection.Favorite)]
    public void Explicit_bilateral_transfer_preserves_other_flags_under_existing_manual_movement_rules(LotProtection flag)
    {
        CountedAuthorityState original = Create(protection: flag);
        ConsentedTransferCommand command = Command(Terms(original));
        ConsentedTransferDecision result = Decide(original, command, Acceptance(command));
        Assert.True(result.IsApplied);
        Assert.Equal(flag, result.Candidate.Custody.GetLot(result.MovedLotId!.Value).Protection);
        Assert.Equal(flag, result.Candidate.Custody.GetLot(Lot).Protection);
    }

    [Fact]
    public void Player_and_npc_participants_use_the_same_control_contract()
    {
        var npc = new TrustedCommandDelivery(Id(71), Id(72));
        CountedAuthorityState players = Create();
        CountedAuthorityState npcRecipient = Create(controls:
        [
            new(SenderDelivery.PrincipalId, SenderDelivery.SessionId, Sender),
            new(npc.PrincipalId, npc.SessionId, Recipient)
        ]);
        CountedAuthorityState npcSender = Create(controls:
        [
            new(npc.PrincipalId, npc.SessionId, Sender),
            new(RecipientDelivery.PrincipalId, RecipientDelivery.SessionId, Recipient)
        ]);
        ConsentedTransferCommand command = Command(Terms(players));
        ConsentedTransferCommand npcCommand = Command(Terms(npcSender), principal: npc.PrincipalId,
            session: npc.SessionId);
        ConsentedTransferAcceptance npcAcceptance = Acceptance(command, principal: npc.PrincipalId,
            session: npc.SessionId);

        ConsentedTransferDecision betweenPlayers = Decide(players, command, Acceptance(command));
        ConsentedTransferDecision toNpc = Decide(npcRecipient, command, npcAcceptance, recipientDelivery: npc);
        ConsentedTransferDecision fromNpc = Decide(npcSender, npcCommand, Acceptance(npcCommand),
            senderDelivery: npc);
        byte[] expected = CustodyEncodingV1.Encode(betweenPlayers.Candidate.Custody);
        Assert.True(betweenPlayers.IsApplied);
        Assert.True(toNpc.IsApplied);
        Assert.True(fromNpc.IsApplied);
        Assert.Equal(expected, CustodyEncodingV1.Encode(toNpc.Candidate.Custody));
        Assert.Equal(expected, CustodyEncodingV1.Encode(fromNpc.Candidate.Custody));

        // The controller kind earns no exception: each side still needs its own current verified delivery.
        AssertRejectedUnchanged(npcRecipient, Decide(npcRecipient, command, Acceptance(command)),
            CommandRejectionReason.NotAuthorized);
        AssertRejectedUnchanged(npcRecipient, Decide(npcRecipient, command, npcAcceptance),
            CommandRejectionReason.NotAuthorized);
        AssertRejectedUnchanged(npcSender, Decide(npcSender, npcCommand, Acceptance(npcCommand)),
            CommandRejectionReason.NotAuthorized);
        AssertRejectedUnchanged(players, Decide(players, command, npcAcceptance, recipientDelivery: npc),
            CommandRejectionReason.NotAuthorized);
    }

    [Fact]
    public void One_controller_of_both_actors_still_supplies_two_exact_approvals()
    {
        CountedAuthorityState party = Create(controls:
        [
            new(SenderDelivery.PrincipalId, SenderDelivery.SessionId, Sender),
            new(SenderDelivery.PrincipalId, SenderDelivery.SessionId, Recipient)
        ]);
        ConsentedTransferCommand command = Command(Terms(party));
        ConsentedTransferAcceptance acceptance = Acceptance(command, principal: SenderDelivery.PrincipalId,
            session: SenderDelivery.SessionId);
        Assert.True(Decide(party, command, acceptance, recipientDelivery: SenderDelivery).IsApplied);
        ConsentedTransferAcceptance differentAmount = Acceptance(Command(Terms(party, quantity: 5)),
            principal: SenderDelivery.PrincipalId, session: SenderDelivery.SessionId);
        AssertRejectedUnchanged(party, Decide(party, command, differentAmount, recipientDelivery: SenderDelivery),
            CommandRejectionReason.NotAuthorized);
    }

    [Fact]
    public void Full_lot_profile_refuses_a_split_but_allows_a_whole_lot()
    {
        CountedAuthorityState template = Create();
        EntityId firstLot = Id(10_000);
        var lots = Enumerable.Range(0, CustodySnapshot.MaximumLots).Select(index => new CountedSourceLot(
            Id((ulong)index + 10_000), SenderStorage, Wood,
            new SourceInterval(Source, index == 0 ? 0UL : (ulong)index + 1, (ulong)index + 2),
            index == 0 ? 3UL : 0UL));
        var custody = new CustodySnapshot(World, Branch, 7,
            template.Custody.Storages.Select(storage => storage.CustodianId == SenderStorage
                ? storage with { UnitCapacity = 5000 } : storage), lots);
        var full = new CountedAuthorityState(custody, 10_000, template.Actors, template.Controls,
            template.Policies, [new(Source, Wood, 5000)]);

        ConsentedTransferCommand split = Command(Terms(full, quantity: 1, lot: firstLot));
        AssertRejectedUnchanged(full, Decide(full, split, Acceptance(split)),
            CommandRejectionReason.ResourceUnavailable);
        ConsentedTransferCommand whole = Command(Terms(full, quantity: 2, lot: firstLot));
        ConsentedTransferDecision decision = Decide(full, whole, Acceptance(whole));
        Assert.True(decision.IsApplied);
        Assert.Equal(CustodySnapshot.MaximumLots, decision.Candidate.Custody.Lots.Count);
        Assert.Equal(2UL, decision.Candidate.Custody.GetStoredQuantity(RecipientStorage));
    }

    [Fact]
    public void Terms_and_delivered_pairs_refuse_meaningless_construction()
    {
        CountedAuthorityState state = Create();
        Assert.Throws<ArgumentOutOfRangeException>(() => Terms(state, quantity: 0));
        Assert.Throws<ArgumentException>(() => Terms(state, recipient: Sender));
        Assert.Throws<ArgumentException>(() => Terms(state, destination: SenderStorage));
        Assert.Throws<ArgumentException>(() => Terms(state, lot: SenderStorage));
        Assert.Throws<ArgumentException>(() => Terms(state, lot: RecipientStorage));
        ConsentedTransferCommand command = Command(Terms(state));
        Assert.Throws<ArgumentNullException>(() => new DeliveredTransferCommand(command, null!));
        Assert.Throws<ArgumentNullException>(() => new DeliveredTransferAcceptance(null!, RecipientDelivery));
        Assert.Throws<ArgumentNullException>(() => new ConsentedTransferAuthority().Decide(state,
            new(command, SenderDelivery), new(Acceptance(command), RecipientDelivery),
            new CommandCommitIdentity(World, 11), null!));
    }

    private static void AssertRejectedUnchanged(CountedAuthorityState state, ConsentedTransferDecision decision,
        CommandRejectionReason reason)
    {
        Assert.False(decision.IsApplied);
        Assert.Equal(CommandReceiptStatus.Rejected, decision.Receipt.Status);
        Assert.Equal(reason, decision.Receipt.RejectionReason);
        Assert.Null(decision.MovedLotId);
        Assert.Same(state, decision.Candidate);
        Assert.Equal(state.ContentHash, decision.BaseStateHash);
    }
}
