using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Tests.Authority;

public sealed class CountedTransferAuthorityTests
{
    [Fact]
    public void Legal_transfer_preserves_admitted_rights_and_returns_candidate_receipt()
    {
        CountedAuthorityState original = AuthorityFixture.Create();
        byte[] before = AuthorityEncodingV1.Encode(original);
        AuthorityDecision decision = AuthorityFixture.Decide(original);
        Assert.Equal(CommandReceiptStatus.Applied, decision.Receipt.Status);
        Assert.Equal(11UL, decision.Receipt.Commit.Sequence);
        Assert.Equal(original.ContentHash, decision.BaseStateHash);
        Assert.Equal(8UL, decision.Candidate.Custody.Revision);
        Assert.Equal(2UL, decision.Candidate.Custody.GetStoredQuantity(AuthorityFixture.Id(3)));
        Assert.Equal(6UL, decision.Candidate.Custody.GetStoredQuantity(AuthorityFixture.Id(4)));
        Assert.Equal(original.Sources, decision.Candidate.Sources);
        Assert.Equal(original.Controls, decision.Candidate.Controls);
        Assert.Equal(original.Policies, decision.Candidate.Policies);
        Assert.Equal(before, AuthorityEncodingV1.Encode(original));
    }

    [Fact]
    public void Full_transfer_keeps_lot_identity_and_source_interval()
    {
        CountedAuthorityState state = AuthorityFixture.Create();
        AuthorityDecision decision = AuthorityFixture.Decide(state, AuthorityFixture.Command(state, quantity: 8));
        Assert.Equal(CommandReceiptStatus.Applied, decision.Receipt.Status);
        Assert.Equal(AuthorityFixture.Id(5), decision.MovedLotId);
        Assert.Single(decision.Candidate.Custody.Lots);
        Assert.Equal(state.Custody.Lots[0].Source, decision.Candidate.Custody.Lots[0].Source);
    }

    [Fact]
    public void Fresh_admission_checks_authenticated_principal_origin_session_and_current_control()
    {
        CountedAuthorityState state = AuthorityFixture.Create();
        CommandEnvelope[] commands =
        [
            AuthorityFixture.Command(state, principal: AuthorityFixture.Id(40)),
            AuthorityFixture.Command(state, session: AuthorityFixture.Id(40)),
            AuthorityFixture.Command(state, actor: AuthorityFixture.Id(24))
        ];
        foreach (CommandEnvelope command in commands)
        {
            AssertRejectedUnchanged(state, AuthorityFixture.Decide(state, command), CommandRejectionReason.NotAuthorized);
        }
        AssertRejectedUnchanged(state, AuthorityFixture.Decide(state, delivery: new(AuthorityFixture.Id(21), AuthorityFixture.Id(40))),
            CommandRejectionReason.NotAuthorized);
        Assert.True(state.CanControl(AuthorityFixture.Delivery, AuthorityFixture.Id(23)));
        Assert.False(state.CanControl(AuthorityFixture.Delivery, AuthorityFixture.Id(24)));
    }

    [Fact]
    public void Source_ownership_and_destination_consent_precede_private_lot_and_revision_checks()
    {
        CountedAuthorityState deniedDestination = AuthorityFixture.Create(CustodyDepositRule.OwnerOnly);
        AssertRejectedUnchanged(deniedDestination, AuthorityFixture.Decide(deniedDestination), CommandRejectionReason.NotAuthorized);
        AssertRejectedUnchanged(deniedDestination, AuthorityFixture.Decide(deniedDestination,
            AuthorityFixture.Command(deniedDestination, lot: AuthorityFixture.Id(999), revisions: [])), CommandRejectionReason.NotAuthorized);

        CountedAuthorityState state = AuthorityFixture.Create();
        AssertRejectedUnchanged(state, AuthorityFixture.Decide(state,
            AuthorityFixture.Command(state, source: AuthorityFixture.Id(4), destination: AuthorityFixture.Id(3))), CommandRejectionReason.NotAuthorized);
        AssertRejectedUnchanged(state, AuthorityFixture.Decide(state,
            AuthorityFixture.Command(state, source: AuthorityFixture.Id(998), destination: AuthorityFixture.Id(999))), CommandRejectionReason.NotAuthorized);
    }

    [Fact]
    public void Extra_revision_targets_cannot_probe_unrelated_private_objects()
    {
        CountedAuthorityState state = AuthorityFixture.Create();
        foreach (EntityId target in new[] { AuthorityFixture.Id(24), AuthorityFixture.Id(999) })
        {
            ExpectedRevision[] revisions = [.. AuthorityFixture.Command(state).ExpectedRevisions,
                new(RevisionTargetKind.Entity, target, 0)];
            AssertRejectedUnchanged(state, AuthorityFixture.Decide(state,
                AuthorityFixture.Command(state, revisions: revisions)), CommandRejectionReason.ResourceUnavailable);
        }
    }

    [Fact]
    public void Private_planner_failures_share_one_public_reason_and_do_not_change_state()
    {
        CountedAuthorityState state = AuthorityFixture.Create();
        AssertRejectedUnchanged(state, AuthorityFixture.Decide(state, AuthorityFixture.Command(state, quantity: 9)),
            CommandRejectionReason.ResourceUnavailable);
        AssertRejectedUnchanged(state, AuthorityFixture.Decide(state, AuthorityFixture.Command(state, lot: AuthorityFixture.Id(999))),
            CommandRejectionReason.ResourceUnavailable);
        AssertRejectedUnchanged(state, AuthorityFixture.Decide(state, AuthorityFixture.Command(state, revisions: [])),
            CommandRejectionReason.ResourceUnavailable);
        foreach (CountedAuthorityState invalidAction in new[]
        {
            AuthorityFixture.Create(capacity: 5), AuthorityFixture.Create(protection: LotProtection.Equipped),
            AuthorityFixture.Create(reservedBy: AuthorityFixture.Id(80)), AuthorityFixture.Create(revision: ulong.MaxValue)
        })
        {
            AssertRejectedUnchanged(invalidAction, AuthorityFixture.Decide(invalidAction), CommandRejectionReason.ResourceUnavailable);
        }
    }

    [Fact]
    public void Split_child_collision_in_uncontrolled_actor_or_unused_source_is_rejected()
    {
        EntityId child = EntityId.DeriveSpawned(AuthorityFixture.Id(20), CustodyTransferPlanner.SplitChildOrdinal);
        CountedAuthorityState actorCollision = AuthorityFixture.Create(extraActors: [child]);
        CountedAuthorityState sourceCollision = AuthorityFixture.Create(extraSources:
            [new(child, AuthorityFixture.Wood, 1)]);
        AssertRejectedUnchanged(actorCollision, AuthorityFixture.Decide(actorCollision), CommandRejectionReason.ResourceUnavailable);
        AssertRejectedUnchanged(sourceCollision, AuthorityFixture.Decide(sourceCollision), CommandRejectionReason.ResourceUnavailable);
    }

    [Fact]
    public void Command_identity_cannot_alias_existing_domain_identity()
    {
        CountedAuthorityState state = AuthorityFixture.Create();
        AssertRejectedUnchanged(state, AuthorityFixture.Decide(state, AuthorityFixture.Command(state, commandId: AuthorityFixture.Id(24))),
            CommandRejectionReason.ResourceUnavailable);
    }

    [Fact]
    public void Human_and_npc_controller_use_identical_explicit_rules()
    {
        CountedAuthorityState humanState = AuthorityFixture.Create();
        var npcState = new CountedAuthorityState(humanState.Custody, humanState.AdmittedUnitCapacity,
            humanState.Actors, [new(AuthorityFixture.Id(61), AuthorityFixture.Id(62), AuthorityFixture.Id(23))],
            humanState.Policies, humanState.Sources);
        AuthorityDecision human = AuthorityFixture.Decide(humanState);
        AuthorityDecision npc = AuthorityFixture.Decide(npcState, AuthorityFixture.Command(npcState,
            principal: AuthorityFixture.Id(61), session: AuthorityFixture.Id(62)), new(AuthorityFixture.Id(61), AuthorityFixture.Id(62)));
        Assert.Equal(human.Receipt.Status, npc.Receipt.Status);
        Assert.Equal(CustodyEncodingV1.Encode(human.Candidate.Custody), CustodyEncodingV1.Encode(npc.Candidate.Custody));
    }

    [Fact]
    public void Owner_can_deposit_into_another_owned_storage_with_owner_only_policy()
    {
        CountedAuthorityState original = AuthorityFixture.Create();
        var state = new CountedAuthorityState(original.Custody, original.AdmittedUnitCapacity, original.Actors,
            original.Controls, [original.Policies[0], new(AuthorityFixture.Id(4), AuthorityFixture.Id(23), CustodyDepositRule.OwnerOnly)],
            original.Sources);
        Assert.Equal(CommandReceiptStatus.Applied, AuthorityFixture.Decide(state).Receipt.Status);
    }

    [Fact]
    public void Replacement_control_revokes_old_delivery_and_fresh_admission_needs_new_origin()
    {
        CountedAuthorityState original = AuthorityFixture.Create();
        var state = new CountedAuthorityState(original.Custody, original.AdmittedUnitCapacity, original.Actors,
            [new(AuthorityFixture.Id(21), AuthorityFixture.Id(32), AuthorityFixture.Id(23))], original.Policies, original.Sources);
        var reconnected = new TrustedCommandDelivery(AuthorityFixture.Id(21), AuthorityFixture.Id(32));
        Assert.False(state.CanControl(AuthorityFixture.Delivery, AuthorityFixture.Id(23)));
        Assert.True(state.CanControl(reconnected, AuthorityFixture.Id(23)));
        AssertRejectedUnchanged(state, AuthorityFixture.Decide(state), CommandRejectionReason.NotAuthorized);
        AssertRejectedUnchanged(state, AuthorityFixture.Decide(state, delivery: reconnected), CommandRejectionReason.NotAuthorized);
        Assert.Equal(CommandReceiptStatus.Applied, AuthorityFixture.Decide(state,
            AuthorityFixture.Command(state, session: AuthorityFixture.Id(32)), reconnected).Receipt.Status);
    }

    [Fact]
    public void Child_collision_checks_other_controllers_and_unused_reservations()
    {
        CountedAuthorityState original = AuthorityFixture.Create();
        EntityId child = EntityId.DeriveSpawned(AuthorityFixture.Id(20), CustodyTransferPlanner.SplitChildOrdinal);
        ActorControlBinding[][] otherControls =
        [
            [original.Controls[0], new(child, AuthorityFixture.Id(32), AuthorityFixture.Id(24))],
            [original.Controls[0], new(AuthorityFixture.Id(31), child, AuthorityFixture.Id(24))]
        ];
        foreach (ActorControlBinding[] controls in otherControls)
        {
            var state = new CountedAuthorityState(original.Custody, original.AdmittedUnitCapacity,
                original.Actors, controls, original.Policies, original.Sources);
            AssertRejectedUnchanged(state, AuthorityFixture.Decide(state), CommandRejectionReason.ResourceUnavailable);
        }
        var reservedIdentity = new CountedAuthorityState(original.Custody, original.AdmittedUnitCapacity,
            original.Actors, original.Controls, original.Policies, original.Sources, [child]);
        AssertRejectedUnchanged(reservedIdentity, AuthorityFixture.Decide(reservedIdentity), CommandRejectionReason.ResourceUnavailable);
    }

    [Fact]
    public void Wrong_branch_is_public_denial_but_wrong_world_is_a_dispatch_precondition()
    {
        CountedAuthorityState state = AuthorityFixture.Create();
        CommandEnvelope original = AuthorityFixture.Command(state);
        var wrongBranch = new CommandEnvelope(original.CommandId, original.PrincipalId, original.OriginSessionId,
            original.ControlledActorId, original.WorldId, AuthorityFixture.Id(99), [], original.Payload);
        AssertRejectedUnchanged(state, AuthorityFixture.Decide(state, wrongBranch), CommandRejectionReason.NotAuthorized);
        var wrongWorld = new CommandEnvelope(original.CommandId, original.PrincipalId, original.OriginSessionId,
            original.ControlledActorId, AuthorityFixture.Id(99), original.BranchId, [], original.Payload);
        Assert.Throws<ArgumentException>(() => AuthorityFixture.Decide(state, wrongWorld));
        Assert.Throws<ArgumentException>(() => new CountedTransferAuthority().Decide(state, original,
            AuthorityFixture.Delivery, new CommandCommitIdentity(AuthorityFixture.Id(99), 11)));
    }

    [Fact]
    public void Pure_processor_rechecks_stale_command_while_durable_retry_lookup_belongs_to_caller()
    {
        CountedAuthorityState state = AuthorityFixture.Create();
        CommandEnvelope command = AuthorityFixture.Command(state);
        AuthorityDecision applied = AuthorityFixture.Decide(state, command);
        AuthorityDecision staleFreshAttempt = AuthorityFixture.Decide(applied.Candidate, command);
        AssertRejectedUnchanged(applied.Candidate, staleFreshAttempt, CommandRejectionReason.ResourceUnavailable);
        Assert.Equal(CommandReceiptStatus.Applied, applied.Receipt.Status);
    }

    [Fact]
    public void Full_lot_profile_rejects_a_split_but_allows_whole_lot_transfer()
    {
        CountedAuthorityState template = AuthorityFixture.Create();
        var lots = Enumerable.Range(0, CustodySnapshot.MaximumLots).Select(index => new CountedSourceLot(
            AuthorityFixture.Id((ulong)index + 10_000), AuthorityFixture.Id(3), AuthorityFixture.Wood,
            new SourceInterval(AuthorityFixture.Id(6), index == 0 ? 0UL : (ulong)index + 1, (ulong)index + 2),
            index == 0 ? 3UL : 0UL));
        var custody = new CustodySnapshot(template.WorldId, template.BranchId, 7,
            [new(AuthorityFixture.Id(3), 5000, 4), new(AuthorityFixture.Id(4), 8, 5)], lots);
        var full = new CountedAuthorityState(custody, 10_000, template.Actors, template.Controls, template.Policies,
            [new(AuthorityFixture.Id(6), AuthorityFixture.Wood, 5000)]);
        AuthorityDecision split = AuthorityFixture.Decide(full, AuthorityFixture.Command(full,
            quantity: 1, lot: AuthorityFixture.Id(10_000)));
        AssertRejectedUnchanged(full, split, CommandRejectionReason.ResourceUnavailable);
        AuthorityDecision whole = AuthorityFixture.Decide(full, AuthorityFixture.Command(full,
            quantity: 2, lot: AuthorityFixture.Id(10_000)));
        Assert.Equal(CommandReceiptStatus.Applied, whole.Receipt.Status);
        Assert.Equal(CustodySnapshot.MaximumLots, whole.Candidate.Custody.Lots.Count);
        Assert.Equal(2UL, whole.Candidate.Custody.GetStoredQuantity(AuthorityFixture.Id(4)));
    }

    private static void AssertRejectedUnchanged(CountedAuthorityState state, AuthorityDecision decision,
        CommandRejectionReason reason)
    {
        Assert.Equal(CommandReceiptStatus.Rejected, decision.Receipt.Status);
        Assert.Equal(reason, decision.Receipt.RejectionReason);
        Assert.Null(decision.MovedLotId);
        Assert.Same(state, decision.Candidate);
        Assert.Equal(state.ContentHash, decision.BaseStateHash);
    }
}
