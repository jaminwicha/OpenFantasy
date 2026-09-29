using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Worlds;

/// <summary>Canonical frames, profile routing and exact collection edges of saved material proposals.</summary>
public sealed class MaterialProposalCodecTests
{
    private static readonly EntityId Root = EntityId.FromBytes(Enumerable.Range(1, 16).Select(value => (byte)value).ToArray());
    private static EntityId Id(ulong ordinal) => EntityId.DeriveSpawned(Root, ordinal);
    private static ContentHash Hash(byte value) => ContentHash.Compute([value]);

    private static ConsentedMaterialTerms Terms(ulong quantity = 2) => new(Id(1), Id(2), Id(3), Id(4),
        ConsentedMaterialLotKind.CountedSource, Id(5), new CountedItemDefinition(Id(6), Hash(6)), Id(7), Id(8), quantity, 9);

    private static WorldActionCommand Command(ulong ordinal, WorldActionIntent intent, EntityId? actor = null) =>
        new(Id(ordinal), Id(10), Id(11), actor ?? Id(3), Id(1), Id(2), 5, intent);

    private static WorldMaterialProposal Proposal(ulong ordinal, ulong sequence = 1, ulong step = 1, ulong expiry = 3)
    {
        WorldActionCommand creation = Command(ordinal, new ProposeMaterialIntent(Terms(), expiry));
        return new WorldMaterialProposal(((ProposeMaterialIntent)creation.Intent).ToGrant(creation),
            new TrustedCommandDelivery(creation.PrincipalId, creation.OriginSessionId), sequence, step, expiry);
    }

    private static WorldMaterialProposalOutcome Accepted(WorldMaterialProposal proposal, ulong ordinal, ulong sequence,
        uint index = 0)
    {
        var acceptance = new ConsentedMaterialAcceptance(Id(ordinal), Id(12), Id(13), proposal.CreationCommandId,
            proposal.GrantHash, proposal.Terms);
        var settlement = new WorldMaterialProposalSettlement(index, acceptance, new TrustedCommandDelivery(Id(12), Id(13)),
            proposal.SplitChildId, Id(20), 0, 4, 6, Hash(1), Hash(2), Hash(3), Hash(4));
        return new WorldMaterialProposalOutcome(proposal, WorldMaterialProposalOutcomeKind.Accepted, sequence,
            acceptance.AcceptanceId, settlement);
    }

    public static TheoryData<int> Intents => new() { 0, 1, 2, 3 };

    private static WorldActionIntent Intent(int kind) => kind switch
    {
        0 => new ProposeMaterialIntent(Terms(), 40),
        1 => AcceptMaterialProposalIntent.Approving(Proposal(30), 7),
        2 => new DeclineMaterialProposalIntent(Id(31)),
        _ => new WithdrawMaterialProposalIntent(Id(32))
    };

    [Theory]
    [MemberData(nameof(Intents))]
    public void Every_submission_round_trips_canonically_and_refuses_changed_frames(int kind)
    {
        WorldActionCommand command = Command(50, Intent(kind), kind is 1 or 2 ? Id(4) : Id(3));
        byte[] bytes = WorldActionCodec.Encode(command);
        Assert.Equal(bytes, WorldActionCodecV16.Encode(command));
        WorldActionCommand restored = WorldActionCodec.Decode(bytes);
        Assert.Equal(command.Intent, restored.Intent);
        Assert.Equal(bytes, WorldActionCodec.Encode(restored));
        Assert.Equal(command.ContentHash, restored.ContentHash);

        Assert.Throws<FormatException>(() => WorldActionCodecV16.Decode([.. bytes, 0]));
        Assert.Throws<FormatException>(() => WorldActionCodecV16.Decode(bytes.AsSpan(0, bytes.Length - 1)));
        // The earlier paired codec neither writes nor reads a saved proposal submission.
        Assert.Throws<ArgumentException>(() => WorldActionCodecV15.Encode(command));
        Assert.Throws<FormatException>(() => WorldActionCodecV15.Decode(bytes));
    }

    [Fact]
    public void Only_the_named_profile_and_an_advancing_step_select_saved_proposals()
    {
        WorldActionCommand propose = Command(60, Intent(0));
        WorldStepBatch Batch(ushort profile, WorldBatchKind kind, WorldActionCommand action) =>
            new(Id(61), Id(1), Id(2), 0, 0, kind, [], [action], profile);

        WorldStepBatch admitted = Batch(30, WorldBatchKind.AdvancingStep, propose);
        byte[] bytes = WorldStepCodec.Encode(admitted);
        Assert.Equal(bytes, WorldStepCodecV30.Encode(admitted));
        Assert.Equal(bytes, WorldStepCodec.Encode(WorldStepCodec.Decode(bytes)));
        Assert.Throws<FormatException>(() => WorldStepCodecV28.Decode(bytes));

        const string refusal = "Saved material proposals require their explicit complete-world profile.";
        foreach (ushort profile in new ushort[] { 26, 27, 28 })
        {
            Assert.Equal(refusal, Assert.Throws<ArgumentException>(() =>
                Batch(profile, WorldBatchKind.AdvancingStep, propose)).Message);
        }
        Assert.Equal(refusal, Assert.Throws<ArgumentException>(() =>
            Batch(30, WorldBatchKind.HeldInteraction, propose)).Message);
        // Only named interpretations exist: the unassigned number below and the next above are refused.
        Assert.Throws<ArgumentException>(() => Batch(29, WorldBatchKind.AdvancingStep, propose));
        Assert.Throws<ArgumentException>(() => Batch(31, WorldBatchKind.AdvancingStep, propose));
        Assert.Throws<ArgumentException>(() => new WorldStepBatch(Id(61), Id(1), Id(2), 0, 0,
            WorldBatchKind.AdvancingStep, [], [], 29));

        // The saved-proposal profile does not interpret the simultaneous pair.
        WorldActionCommand pair = Command(62, new OfferMaterialIntent(Terms(), 3));
        Assert.Equal("Paired material transfer requires its explicit complete-world profile.",
            Assert.Throws<ArgumentException>(() => Batch(30, WorldBatchKind.AdvancingStep, pair)).Message);
        Assert.Equal(WorldBatchKind.AdvancingStep, Batch(28, WorldBatchKind.AdvancingStep, pair).Kind);
    }

    [Fact]
    public void A_proposal_keeps_distinct_identities_and_an_exact_bounded_lifetime()
    {
        WorldMaterialProposal proposal = Proposal(70, sequence: 4, step: 9, expiry: 10);
        Assert.Equal(WorldMaterialProposal.DeriveIdentity(proposal.CreationCommandId), proposal.ProposalId);
        // The pinned child is the ordinary split child of the creation command.
        Assert.Equal(EntityId.DeriveSpawned(proposal.CreationCommandId, 0), proposal.SplitChildId);
        Assert.Equal(3, new[] { proposal.ProposalId, proposal.CreationCommandId, proposal.SplitChildId }.Distinct().Count());
        Assert.False(proposal.IsDue(9));
        Assert.True(proposal.IsDue(10));

        Assert.Throws<ArgumentException>(() => Proposal(71, step: 9, expiry: 9));
        Assert.Equal(9 + WorldMaterialProposal.MaximumLifetimeSteps,
            Proposal(72, step: 9, expiry: 9 + WorldMaterialProposal.MaximumLifetimeSteps).ExpiresAtAdvancingStep);
        Assert.Throws<ArgumentException>(() => Proposal(73, step: 9, expiry: 10 + WorldMaterialProposal.MaximumLifetimeSteps));
        // A recorded delivery that is not the grant's own principal and session is refused.
        WorldActionCommand creation = Command(74, new ProposeMaterialIntent(Terms(), 10));
        Assert.Throws<ArgumentException>(() => new WorldMaterialProposal(
            ((ProposeMaterialIntent)creation.Intent).ToGrant(creation), new TrustedCommandDelivery(Id(12), Id(11)), 4, 9, 10));
    }

    [Fact]
    public void The_owner_is_usable_at_its_exact_bounds_and_refuses_the_next_entry()
    {
        var book = new WorldMaterialProposalBook([], []);
        for (ulong index = 0; index < WorldMaterialProposalBook.MaximumOpen; index++)
        {
            Assert.True(book.CanCreate);
            book = book.Create(Proposal(100 + index));
        }
        Assert.False(book.CanCreate);
        Assert.Equal(WorldMaterialProposalBook.MaximumOpen, book.Open.Count);
        Assert.Throws<ArgumentException>(() => book.Create(Proposal(200)));
        Assert.Throws<ArgumentException>(() => book.Create(book.Open[0]));

        // A full outcome window with every open slot occupied still encodes inside its frame bound.
        var outcomes = new List<WorldMaterialProposalOutcome>();
        for (ulong index = 0; index < WorldMaterialProposalBook.MaximumOutcomes; index++)
        {
            outcomes.Add(Accepted(Proposal(300 + index), 400 + index, 2 + index));
        }
        var full = new WorldMaterialProposalBook(book.Open, outcomes);
        Assert.False(full.CanClose);
        byte[] bytes = WorldMaterialProposalCodec.Encode(full);
        Assert.InRange(bytes.Length, 1, WorldMaterialProposalBook.MaximumByteLength);
        WorldMaterialProposalBook restored = WorldMaterialProposalCodec.Decode(bytes);
        Assert.Equal(bytes, WorldMaterialProposalCodec.Encode(restored));
        Assert.Equal(full.ContentHash, restored.ContentHash);
        Assert.Throws<ArgumentException>(() => new WorldMaterialProposalBook(book.Open,
            [.. outcomes, Accepted(Proposal(500), 501, 900)]));

        // A cut keeps every open offer and archives the outcomes.
        WorldMaterialProposalBook continued = full.ContinueAcrossCut();
        Assert.Equal(full.Open.Select(value => value.ProposalId), continued.Open.Select(value => value.ProposalId));
        Assert.Empty(continued.Outcomes);
    }

    [Fact]
    public void Outcomes_close_exactly_one_open_offer_and_expiry_follows_the_step_boundary()
    {
        WorldMaterialProposal first = Proposal(600, expiry: 3);
        WorldMaterialProposal later = Proposal(601, expiry: 4);
        var book = new WorldMaterialProposalBook([], []).Create(first).Create(later);
        Assert.Same(book, book.ExpireDue(2, 5));
        WorldMaterialProposalBook expired = book.ExpireDue(3, 5);
        WorldMaterialProposalOutcome outcome = Assert.Single(expired.Outcomes);
        Assert.Equal((first.ProposalId, WorldMaterialProposalOutcomeKind.Expired, 5UL, (EntityId?)null),
            (outcome.Proposal.ProposalId, outcome.Kind, outcome.WorldSequence, outcome.TerminalCommandId));
        Assert.Equal(later.ProposalId, Assert.Single(expired.Open).ProposalId);

        // A closed identity cannot be closed or opened again.
        Assert.Throws<ArgumentException>(() => expired.Close(new(first, WorldMaterialProposalOutcomeKind.Withdrawn, 6, Id(602), null)));
        Assert.Throws<ArgumentException>(() => expired.Create(first));
        // Terminal causes keep their roles: expiry has none, a command outcome requires one.
        Assert.Throws<ArgumentException>(() => new WorldMaterialProposalOutcome(later,
            WorldMaterialProposalOutcomeKind.Expired, 6, Id(603), null));
        Assert.Throws<ArgumentException>(() => new WorldMaterialProposalOutcome(later,
            WorldMaterialProposalOutcomeKind.Declined, 6, null, null));
        Assert.Throws<ArgumentException>(() => new WorldMaterialProposalOutcome(later,
            WorldMaterialProposalOutcomeKind.Withdrawn, later.CreatedSequence, Id(604), null));
        Assert.Throws<ArgumentException>(() => new WorldMaterialProposalOutcome(later,
            WorldMaterialProposalOutcomeKind.Withdrawn, 6, later.CreationCommandId, null));
        WorldMaterialProposalOutcome accepted = Accepted(later, 605, 6);
        Assert.Throws<ArgumentException>(() => new WorldMaterialProposalOutcome(first,
            WorldMaterialProposalOutcomeKind.Accepted, 6, accepted.TerminalCommandId, accepted.Settlement));
    }

    [Fact]
    public void Decoding_refuses_reordered_truncated_and_foreign_owner_frames()
    {
        WorldMaterialProposal[] open = [Proposal(700), Proposal(701)];
        WorldMaterialProposal[] ordered = open.OrderBy(value => value.ProposalId).ToArray();
        var book = new WorldMaterialProposalBook(ordered, [Accepted(Proposal(702), 703, 2)]);
        byte[] bytes = WorldMaterialProposalCodec.Encode(book);
        Assert.Equal(bytes, WorldMaterialProposalCodec.Encode(WorldMaterialProposalCodec.Decode(bytes)));

        Assert.Throws<ArgumentException>(() => new WorldMaterialProposalBook(ordered.Reverse(), []));
        Assert.Throws<FormatException>(() => WorldMaterialProposalCodec.Decode(bytes.AsSpan(0, bytes.Length - 1)));
        Assert.Throws<FormatException>(() => WorldMaterialProposalCodec.Decode([.. bytes, 0]));
        Assert.Throws<FormatException>(() => WorldMaterialProposalCodec.Decode(
            WorldConsentedMaterialCodec.Encode(new WorldConsentedMaterialHistory([]))));
        byte[] header = (byte[])bytes.Clone();
        header[0] ^= 1;
        Assert.Throws<FormatException>(() => WorldMaterialProposalCodec.Decode(header));
        // Decoding proves shape only. A changed recorded owner hash is another canonical value;
        // refusing it belongs to complete replay by the durable owner, never to this codec.
        byte[] changed = (byte[])bytes.Clone();
        changed[^1] ^= 1;
        WorldMaterialProposalBook different = WorldMaterialProposalCodec.Decode(changed);
        Assert.Equal(changed, WorldMaterialProposalCodec.Encode(different));
        Assert.NotEqual(book.ContentHash, different.ContentHash);
    }
}
