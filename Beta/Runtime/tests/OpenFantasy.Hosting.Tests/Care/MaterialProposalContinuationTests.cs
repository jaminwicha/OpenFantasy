using System.Buffers.Binary;
using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>
/// Structural contracts of saved proposals against real lived roots: the actual coordinated anchor,
/// complete admission, the uniform revision contract and both participants' spent opportunities.
/// Refused roots are real canonical roots with one re-framed owner, never a substituted hash.
/// </summary>
public sealed class MaterialProposalContinuationTests(MaterialProposalWorldFixture world) : IClassFixture<MaterialProposalWorldFixture>
{
    private const string Forgotten = "Every carried proposal stays open or reaches one terminal outcome after its boundary.";
    private const string Changed = "A carried proposal must remain exact after its closing boundary.";
    private const string Invented = "A proposal the closing owner did not carry must follow its closing boundary.";
    private const string Capability = "A coordinated successor retains its exact saved-proposal interpretation.";
    private const string EarlyExpiry = "An expired proposal cannot precede its absolute expiry step.";
    private const string Participants = "A settlement requires both participants under their admitted control bindings.";
    private const string CreationTime = "A saved proposal requires this world's scope, time and admitted participants.";

    private static void Refused(Func<WorldSnapshot> decode, string reason)
    {
        Exception? error = Assert.Throws<FormatException>(() => decode());
        var reasons = new List<string>();
        for (; error is not null; error = error.InnerException) reasons.Add(error.Message);
        Assert.Contains(reasons, value => value.StartsWith(reason, StringComparison.Ordinal));
    }

    [Fact]
    public void The_actual_anchor_admits_exact_carried_offers_and_refuses_forgotten_changed_and_invented_grants()
    {
        using var live = world.Open();
        ConsentedMaterialTerms terms = world.SelectRawTerms(live.Authority);
        live.Save(terms, lifetime: 64);
        live.Step();
        WorldSnapshot closing = live.State;
        WorldMaterialProposal carried = Assert.Single(closing.Coordination!.Proposals!.Open);
        WorldMaterialCareArchiveSegment segment = WorldMaterialCareArchiveSegment.Seal(closing);
        WorldMaterialCareArchiveAnchor anchor = segment.Anchor;
        Assert.Equal(carried.ContentHash, Assert.Single(anchor.Proposals!.Carried).ContentHash);
        byte[] continued = WorldSnapshotCodec.Encode(segment.Continued);
        // The re-framing helper is exact: the real owner gives back the real bytes.
        Assert.Equal(continued, MaterialProposalFrames.WithBook(continued, segment.Continued.Coordination!.Proposals!));
        WorldSnapshot restored = WorldSnapshotCodec.DecodeCoordinated(continued, anchor);
        Assert.Equal(carried.ContentHash, Assert.Single(restored.Coordination!.Proposals!.Open).ContentHash);

        // After the boundary a new offer is created and the carried one settles, under the same anchor.
        var processor = new WorldStepProcessor();
        HashSet<EntityId> reserved = [];
        bool Occupied(EntityId identity) => reserved.Contains(identity) || live.Authority.IsIdentityOccupiedForHost(identity);
        WorldSnapshot root = segment.Continued;
        WorldActionCommand create = world.Actions.Command(root, terms.RecipientActorId, new ProposeMaterialIntent(
            world.PinnedRawTerms(root, terms.RecipientActorId, terms.SenderActorId), root.AdvancingSteps + 16));
        WorldStepDecision created = processor.Decide(root, world.Actions.Batch(root, WorldBatchKind.AdvancingStep, [create]), Occupied);
        Assert.Equal(WorldActionRejection.None, created.Receipts.Single().Rejection);
        reserved.UnionWith(created.Identities.Select(value => value.Identity));
        WorldSnapshot grown = created.Candidate;
        WorldMaterialProposalBook grownBook = WorldSnapshotCodec.DecodeCoordinated(
            WorldSnapshotCodec.Encode(grown), anchor).Coordination!.Proposals!;
        Assert.Equal(2, grownBook.Open.Count);
        Assert.True(grownBook.FindOpen(WorldMaterialProposal.DeriveIdentity(create.CommandId))!.CreatedSequence
            > anchor.ClosingWorldSequence);
        WorldActionCommand accept = world.Actions.Command(grown, terms.RecipientActorId,
            AcceptMaterialProposalIntent.Approving(carried, grown.Space.GetActor(terms.SenderActorId).Revision));
        WorldStepDecision settled = processor.Decide(grown, world.Actions.Batch(grown, WorldBatchKind.AdvancingStep, [accept]), Occupied);
        Assert.Equal(WorldActionRejection.None, settled.Receipts.Single().Rejection);
        WorldMaterialProposalBook closed = WorldSnapshotCodec.DecodeCoordinated(
            WorldSnapshotCodec.Encode(settled.Candidate), anchor).Coordination!.Proposals!;
        WorldMaterialProposalOutcome outcome = closed.FindOutcome(carried.ProposalId)!;
        Assert.Equal((WorldMaterialProposalOutcomeKind.Accepted, carried.ContentHash),
            (outcome.Kind, outcome.Proposal.ContentHash));

        // A forgotten carried grant.
        Refused(() => WorldSnapshotCodec.DecodeCoordinated(
            MaterialProposalFrames.WithBook(continued, new WorldMaterialProposalBook([], [])), anchor), Forgotten);
        // The same identity with a changed expiry.
        var changed = new WorldMaterialProposal(carried.Grant, carried.SenderDelivery, carried.CreatedSequence,
            carried.CreatedAdvancingStep, carried.ExpiresAtAdvancingStep + 1);
        Assert.Equal(carried.ProposalId, changed.ProposalId);
        Refused(() => WorldSnapshotCodec.DecodeCoordinated(
            MaterialProposalFrames.WithBook(continued, new WorldMaterialProposalBook([changed], [])), anchor), Changed);
        // A grant dated inside the closed segment that the closing owner never held.
        WorldActionCommand never = world.Actions.Command(closing, terms.SenderActorId,
            new ProposeMaterialIntent(terms, closing.AdvancingSteps + 16));
        var invented = new WorldMaterialProposal(((ProposeMaterialIntent)never.Intent).ToGrant(never),
            world.Actions.Delivery(never), closing.Revision, closing.AdvancingSteps, closing.AdvancingSteps + 16);
        Refused(() => WorldSnapshotCodec.DecodeCoordinated(MaterialProposalFrames.WithBook(continued,
            new WorldMaterialProposalBook(new[] { carried, invented }.OrderBy(value => value.ProposalId), [])), anchor), Invented);
        // The same successor under the paired interpretation drops the capability.
        Refused(() => WorldSnapshotCodec.DecodeCoordinated(MaterialProposalFrames.AsConsented(continued), anchor), Capability);
    }

    [Fact]
    public void A_consented_anchor_without_the_capability_refuses_a_saved_proposal_successor()
    {
        var access = new ConsentedWorldPublicationTests.DurableAccess();
        var ordinary = new OrdinaryCareWorldFixture(access.OrdinaryGenesis);
        using var files = new StorageFiles();
        using SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path,
            WorldConsentedMaterialAdmission.CreateFresh(access.OrdinaryGenesis));
        WorldSnapshot lived = ordinary.ReachFullDoseInjuredExploration(new CareWorldSession(authority));
        Assert.Equal(28, lived.ProfileVersion);
        WorldMaterialCareArchiveSegment segment = WorldMaterialCareArchiveSegment.Seal(lived);
        Assert.Null(segment.Anchor.Proposals);
        byte[] continued = WorldSnapshotCodec.Encode(segment.Continued);
        Assert.Equal(continued, WorldSnapshotCodec.Encode(WorldSnapshotCodec.DecodeCoordinated(continued, segment.Anchor)));
        // The same successor with an empty proposal owner gains a capability its boundary never had.
        Refused(() => WorldSnapshotCodec.DecodeCoordinated(MaterialProposalFrames.AsProposal(continued), segment.Anchor),
            Capability);
    }

    [Fact]
    public void Admission_refuses_an_early_expiry_an_impossible_creation_time_and_unadmitted_settlement_participants()
    {
        using var live = world.Open();
        ConsentedMaterialTerms terms = world.SelectRawTerms(live.Authority);
        var lapsing = live.Save(terms, lifetime: 3);
        WorldMaterialProposal open = Assert.Single(live.Book.Open);
        live.Step();
        WorldSnapshot early = live.State;
        byte[] earlyBytes = WorldSnapshotCodec.Encode(early);
        Assert.Equal(earlyBytes, MaterialProposalFrames.WithBook(earlyBytes, early.Coordination!.Proposals!));
        Assert.True(open.ExpiresAtAdvancingStep > early.AdvancingSteps);

        // The admitted count precedes the absolute expiry step.
        Refused(() => WorldSnapshotCodec.Decode(MaterialProposalFrames.WithBook(earlyBytes, new WorldMaterialProposalBook([],
            [new(open, WorldMaterialProposalOutcomeKind.Expired, early.Revision, null, null)]))), EarlyExpiry);
        // The same root admits the same offer closed at the same sequence by a cause that needs no time.
        EntityId cause = live.Command(terms.SenderActorId, new WithdrawMaterialProposalIntent(open.ProposalId)).CommandId;
        WorldSnapshot withdrawn = WorldSnapshotCodec.Decode(MaterialProposalFrames.WithBook(earlyBytes,
            new WorldMaterialProposalBook([], [new(open, WorldMaterialProposalOutcomeKind.Withdrawn, early.Revision, cause, null)])));
        Assert.Equal(WorldMaterialProposalOutcomeKind.Withdrawn, Assert.Single(withdrawn.Coordination!.Proposals!.Outcomes).Kind);

        // A count cannot exceed the revision that published it; the equal pair stays representable.
        WorldMaterialProposal Created(ulong sequence, ulong step) =>
            new(open.Grant, open.SenderDelivery, sequence, step, open.ExpiresAtAdvancingStep);
        Refused(() => WorldSnapshotCodec.Decode(MaterialProposalFrames.WithBook(earlyBytes,
            new WorldMaterialProposalBook([Created(1, 2)], []))), CreationTime);
        Assert.Equal(2UL, Assert.Single(WorldSnapshotCodec.Decode(MaterialProposalFrames.WithBook(earlyBytes,
            new WorldMaterialProposalBook([Created(2, 2)], []))).Coordination!.Proposals!.Open).CreatedAdvancingStep);

        // The owner's own expiry is the admitted control; one sequence earlier is impossible.
        live.Step();
        Assert.Single(live.Book.Open);
        live.Step();
        WorldSnapshot late = live.State;
        WorldMaterialProposalOutcome expired = Assert.Single(late.Coordination!.Proposals!.Outcomes);
        Assert.Equal((WorldMaterialProposalOutcomeKind.Expired, lapsing.ProposalId, late.Revision, open.CreatedSequence + 3),
            (expired.Kind, expired.Proposal.ProposalId, expired.WorldSequence, expired.WorldSequence));
        byte[] lateBytes = WorldSnapshotCodec.Encode(late);
        Assert.Equal(lateBytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(lateBytes)));
        Refused(() => WorldSnapshotCodec.Decode(MaterialProposalFrames.WithBook(lateBytes, new WorldMaterialProposalBook([],
            [new(open, WorldMaterialProposalOutcomeKind.Expired, late.Revision - 1, null, null)]))), EarlyExpiry);

        // A real settlement is admitted; the same material facts under another binding are not.
        ConsentedMaterialTerms next = world.SelectRawTerms(live.Authority);
        var offer = live.Save(next);
        TrustedCommandDelivery recipient = world.DeliveryOf(live.State, next.RecipientActorId);
        Assert.Equal(WorldActionRejection.None, live.Submit(next.RecipientActorId,
            live.Service.Find(offer.ProposalId, next.RecipientActorId, recipient)!.PrepareAcceptance()));
        WorldSnapshot settled = live.State;
        byte[] settledBytes = WorldSnapshotCodec.Encode(settled);
        WorldMaterialProposalBook book = settled.Coordination!.Proposals!;
        Assert.Equal(settledBytes, MaterialProposalFrames.WithBook(settledBytes, book));
        WorldMaterialProposalOutcome real = book.FindOutcome(offer.ProposalId)!;
        WorldMaterialProposalSettlement moved = real.Settlement!;
        // The sender's own binding cannot stand in for the recipient's.
        TrustedCommandDelivery foreign = real.Proposal.SenderDelivery;
        Assert.NotEqual(recipient, foreign);
        var acceptance = new ConsentedMaterialAcceptance(moved.Acceptance.AcceptanceId, foreign.PrincipalId,
            foreign.SessionId, real.Proposal.CreationCommandId, real.Proposal.GrantHash, real.Proposal.Terms);
        var substituted = new WorldMaterialProposalOutcome(real.Proposal, WorldMaterialProposalOutcomeKind.Accepted,
            real.WorldSequence, real.TerminalCommandId, new WorldMaterialProposalSettlement(moved.AcceptanceActionIndex,
                acceptance, foreign, moved.MovedLotId, moved.RightsOwnerId, moved.OutputIndex, moved.MovedStart,
                moved.MovedEnd, moved.OriginalInventoryHash, moved.OriginalProductionHash,
                moved.CandidateInventoryHash, moved.CandidateProductionHash));
        Refused(() => WorldSnapshotCodec.Decode(MaterialProposalFrames.WithBook(settledBytes, new WorldMaterialProposalBook(
            book.Open, book.Outcomes.Select(value => ReferenceEquals(value, real) ? substituted : value)))), Participants);
    }

    [Fact]
    public void A_stale_actor_revision_refuses_every_submission_before_any_proposal_is_consulted()
    {
        using var live = world.Open();
        ConsentedMaterialTerms terms = world.SelectRawTerms(live.Authority);
        EntityId sender = terms.SenderActorId, recipient = terms.RecipientActorId;
        var offer = live.Save(terms, lifetime: 256);
        EntityId missing = WorldMaterialProposal.DeriveIdentity(offer.ProposalId);
        ulong staleSender = MoveAndReturn(live, sender);
        ulong staleRecipient = MoveAndReturn(live, recipient);
        MaterialProposalView view = live.Service.Find(offer.ProposalId, recipient, world.DeliveryOf(live.State, recipient))!;

        void Refuses(EntityId actor, ulong stale, WorldActionIntent intent)
        {
            WorldSnapshot before = live.State;
            WorldActionCommand current = live.Command(actor, intent);
            var command = new WorldActionCommand(current.CommandId, current.PrincipalId, current.OriginSessionId,
                current.ActorId, current.WorldId, current.BranchId, stale, intent);
            Assert.Equal(WorldActionRejection.RevisionConflict,
                live.Service.Submit(command, world.Actions.Delivery(command)).Rejection);
            WorldSnapshot after = live.State;
            Assert.Equal((before.Inventory.ContentHash, before.Production!.ContentHash, before.Coordination!.Proposals!.ContentHash),
                (after.Inventory.ContentHash, after.Production!.ContentHash, after.Coordination!.Proposals!.ContentHash));
            // The refusal is an ordinary elapsed step, like every other refused submission.
            Assert.Equal((before.Revision + 1, before.AdvancingSteps + 1), (after.Revision, after.AdvancingSteps));
        }

        Refuses(sender, staleSender, new ProposeMaterialIntent(terms, live.State.AdvancingSteps + 64));
        Refuses(recipient, staleRecipient, new DeclineMaterialProposalIntent(offer.ProposalId));
        Refuses(sender, staleSender, new WithdrawMaterialProposalIntent(offer.ProposalId));
        Refuses(recipient, staleRecipient, view.PrepareAcceptance());
        // A missing offer gives the same conflict: the caller's own revision precedes every lookup.
        Refuses(recipient, staleRecipient, new DeclineMaterialProposalIntent(missing));
        Refuses(sender, staleSender, new WithdrawMaterialProposalIntent(missing));
        Assert.Equal(offer.ProposalId, Assert.Single(live.Book.Open).ProposalId);
        Assert.Empty(live.Book.Outcomes);

        // The same submissions at the current revision are decided on their merits.
        var declined = live.Save(terms, lifetime: 256);
        Assert.Equal(WorldActionRejection.None, live.Submit(recipient, new DeclineMaterialProposalIntent(declined.ProposalId)));
        var withdrawn = live.Save(terms, lifetime: 256);
        Assert.Equal(WorldActionRejection.None, live.Submit(sender, new WithdrawMaterialProposalIntent(withdrawn.ProposalId)));
        Assert.Equal(WorldActionRejection.None, live.Submit(recipient, live.Service.Find(offer.ProposalId, recipient,
            world.DeliveryOf(live.State, recipient))!.PrepareAcceptance()));
        Assert.Equal(WorldMaterialProposalOutcomeKind.Accepted, live.Book.FindOutcome(offer.ProposalId)!.Kind);
    }

    [Fact]
    public void A_settlement_spends_the_absent_senders_own_action_and_movement_opportunity()
    {
        using var live = world.Open();
        ConsentedMaterialTerms terms = world.SelectRawTerms(live.Authority);
        EntityId sender = terms.SenderActorId, recipient = terms.RecipientActorId;
        var offer = live.Save(terms, lifetime: 256);
        TrustedCommandDelivery recipientDelivery = world.DeliveryOf(live.State, recipient);
        TrustedCommandDelivery senderDelivery = world.DeliveryOf(live.State, sender);
        WorldActionRejection Outcome(WorldStepDecision decision, WorldActionCommand command) =>
            decision.Receipts.Single(value => value.CommandId == command.CommandId).Rejection;

        // The sender's own action in the same step keeps its opportunity, in either order.
        foreach (bool senderFirst in new[] { true, false })
        {
            WorldSnapshot state = live.State;
            WorldActionCommand competing = world.Actions.Command(state, sender,
                new ProposeMaterialIntent(terms, state.AdvancingSteps + 64));
            WorldActionCommand blocked = world.Actions.Command(state, recipient,
                live.Service.Find(offer.ProposalId, recipient, recipientDelivery)!.PrepareAcceptance());
            WorldStepDecision decision = live.Authority.Commit(world.Actions.Batch(state, WorldBatchKind.AdvancingStep,
                senderFirst ? [competing, blocked] : [blocked, competing]));
            Assert.Equal((WorldActionRejection.None, WorldActionRejection.Unavailable),
                (Outcome(decision, competing), Outcome(decision, blocked)));
            Assert.Equal(state.Inventory.ContentHash, live.State.Inventory.ContentHash);
            Assert.NotNull(live.Book.FindOpen(offer.ProposalId));
        }
        Assert.Empty(live.Book.Outcomes);

        // Control: the sender's own movement sample alone is applied.
        WorldStepBatch Moving(WorldSnapshot state, long x, long y, params WorldActionCommand[] actions) =>
            new(world.NextCause(state), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
                WorldBatchKind.AdvancingStep, [new WorldMovementInput(senderDelivery, sender, x, y)], actions,
                state.ProfileVersion);
        var rest = live.State.Space.GetActor(sender).Feet;
        (long X, long Y)? free = null;
        foreach ((long x, long y) in new (long, long)[] { (32, 0), (-32, 0), (0, 32), (0, -32) })
        {
            live.Authority.Commit(Moving(live.State, x, y));
            if (live.State.Space.GetActor(sender).Feet.Equals(rest)) continue;
            live.Authority.Commit(Moving(live.State, -x, -y));
            free = (x, y);
            break;
        }
        Assert.NotNull(free);
        Assert.Equal(rest, live.State.Space.GetActor(sender).Feet);

        // The same sample beside the recipient's acceptance is spent by the settlement.
        WorldSnapshot ready = live.State;
        var presence = ready.Space.GetActor(sender);
        WorldActionCommand accept = world.Actions.Command(ready, recipient,
            live.Service.Find(offer.ProposalId, recipient, recipientDelivery)!.PrepareAcceptance());
        WorldStepDecision settled = live.Authority.Commit(Moving(ready, free.Value.X, free.Value.Y, accept));
        Assert.Equal(WorldActionRejection.None, Outcome(settled, accept));
        var after = live.State.Space.GetActor(sender);
        Assert.Equal((presence.Feet, presence.Revision), (after.Feet, after.Revision));
        Assert.Equal(terms.DestinationCustodianId,
            live.State.Inventory.Custody.GetLot(EntityId.DeriveSpawned(offer.Command.CommandId, 0)).CustodianId);
        Assert.Equal(WorldMaterialProposalOutcomeKind.Accepted, live.Book.FindOutcome(offer.ProposalId)!.Kind);
    }

    /// <summary>One accepted step away and one back: the presence revision is genuinely stale afterwards.</summary>
    private ulong MoveAndReturn(MaterialProposalWorldFixture.LiveWorld live, EntityId actor)
    {
        ulong stale = live.State.Space.GetActor(actor).Revision;
        TrustedCommandDelivery delivery = world.DeliveryOf(live.State, actor);
        void Walk(long x, long y)
        {
            WorldSnapshot state = live.State;
            live.Authority.Commit(new WorldStepBatch(world.NextCause(state), state.WorldId, state.BranchId,
                state.Revision, state.AdvancingSteps, WorldBatchKind.AdvancingStep,
                [new WorldMovementInput(delivery, actor, x, y)], [], state.ProfileVersion));
        }
        foreach ((long x, long y) in new (long, long)[] { (32, 0), (-32, 0), (0, 32), (0, -32) })
        {
            Walk(x, y);
            if (live.State.Space.GetActor(actor).Revision == stale) continue;
            Walk(-x, -y);
            break;
        }
        Assert.NotEqual(stale, live.State.Space.GetActor(actor).Revision);
        return stale;
    }
}

/// <summary>
/// Re-frames one owner inside real canonical root bytes. It computes no hash and no binding: every
/// other field, including the stored anchor binding, stays the exact bytes the real owner wrote.
/// </summary>
internal static class MaterialProposalFrames
{
    private const int ProfileField = 1;
    private const int CoordinationField = 22;
    private const int BookField = 5;
    private const string ProposalTag = "world.coordinated-proposal-care.v1";
    private const string ConsentedTag = "world.coordinated-consented-care.v1";

    internal static byte[] WithBook(byte[] root, WorldMaterialProposalBook book) =>
        Rewrite(root, 30, ProposalTag, fields => fields[BookField] = WorldMaterialProposalCodec.Encode(book));

    internal static byte[] AsConsented(byte[] root) =>
        Rewrite(root, 28, ConsentedTag, fields => fields.RemoveAt(BookField));

    internal static byte[] AsProposal(byte[] root) => Rewrite(root, 30, ProposalTag,
        fields => fields.Add(WorldMaterialProposalCodec.Encode(new WorldMaterialProposalBook([], []))));

    private static byte[] Rewrite(byte[] root, ulong profile, string tag, Action<List<byte[]>> edit)
    {
        List<byte[]> fields = Split(root);
        byte[] version = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(version, profile);
        fields[ProfileField] = version;
        List<byte[]> coordination = Split(fields[CoordinationField]);
        coordination[0] = Encoding.UTF8.GetBytes(tag);
        edit(coordination);
        fields[CoordinationField] = Join(coordination);
        return Join(fields);
    }

    private static List<byte[]> Split(ReadOnlySpan<byte> frame)
    {
        Assert.True(frame[..4].SequenceEqual("OFC\0"u8));
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(frame[8..]));
        List<byte[]> fields = [];
        int offset = 12;
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(frame[offset..]));
            fields.Add(frame.Slice(offset + 4, length).ToArray());
            offset += 4 + length;
        }
        Assert.Equal(frame.Length, offset);
        return fields;
    }

    private static byte[] Join(IReadOnlyList<byte[]> fields)
    {
        using var stream = new MemoryStream();
        byte[] number = new byte[sizeof(uint)];
        stream.Write("OFC\0"u8);
        BinaryPrimitives.WriteUInt32BigEndian(number, 1);
        stream.Write(number);
        BinaryPrimitives.WriteUInt32BigEndian(number, checked((uint)fields.Count));
        stream.Write(number);
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(number, checked((uint)field.Length));
            stream.Write(number);
            stream.Write(field);
        }
        return stream.ToArray();
    }
}
