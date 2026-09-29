using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// The typed owner of saved material proposals in one complete world. It holds the open offers and
/// the terminal outcomes of the current segment. It owns no inventory, production, care permission
/// or command receipt, and it never evicts: a full bound refuses the next creation or closure.
/// </summary>
public sealed class WorldMaterialProposalBook
{
    /// <summary>Development envelope for a small hosted party; production scale is a separate gate.</summary>
    public const int MaximumOpen = 16;

    /// <summary>Terminal outcomes retained until the next coordinated cut archives them.</summary>
    public const int MaximumOutcomes = 64;

    public const int MaximumByteLength = (WorldMaterialProposal.MaximumByteLength + sizeof(uint)) * MaximumOpen
        + (WorldMaterialProposalOutcome.MaximumByteLength + sizeof(uint)) * MaximumOutcomes + 256;

    public WorldMaterialProposalBook(IEnumerable<WorldMaterialProposal> open,
        IEnumerable<WorldMaterialProposalOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(outcomes);
        HashSet<EntityId> identities = [];
        var pending = new List<WorldMaterialProposal>();
        foreach (WorldMaterialProposal proposal in open)
        {
            ArgumentNullException.ThrowIfNull(proposal);
            bool follows = pending.Count == 0 || proposal.ProposalId.CompareTo(pending[^1].ProposalId) > 0;
            if (pending.Count == MaximumOpen || !follows || !identities.Add(proposal.ProposalId)
                || !identities.Add(proposal.CreationCommandId))
            {
                throw new ArgumentException("Open proposals require bounded strict identity order and distinct identities.");
            }
            pending.Add(proposal);
        }
        var closed = new List<WorldMaterialProposalOutcome>();
        foreach (WorldMaterialProposalOutcome outcome in outcomes)
        {
            ArgumentNullException.ThrowIfNull(outcome);
            bool follows = closed.Count == 0 || outcome.WorldSequence >= closed[^1].WorldSequence;
            bool split = outcome.Settlement is { } settlement && settlement.MovedLotId != outcome.Proposal.Terms.LotId;
            if (closed.Count == MaximumOutcomes || !follows || !identities.Add(outcome.Proposal.ProposalId)
                || !identities.Add(outcome.Proposal.CreationCommandId)
                || outcome.TerminalCommandId is { } terminal && !identities.Add(terminal)
                || split && !identities.Add(outcome.Settlement!.MovedLotId))
            {
                throw new ArgumentException("Proposal outcomes require bounded world order and distinct permanent identities.");
            }
            closed.Add(outcome);
        }
        ValidateSettlementOrder(closed);
        Open = pending.AsReadOnly();
        Outcomes = closed.AsReadOnly();
        ContentHash = ContentHash.Compute(CanonicalEncodingV1.Frame("world.material-proposal-book.v1",
            CanonicalEncodingV1.Frame("world.material-proposals.open",
                pending.Select(value => (ReadOnlyMemory<byte>)value.ContentHash.ToByteArray()).ToArray()),
            CanonicalEncodingV1.Frame("world.material-proposals.outcomes",
                closed.Select(value => (ReadOnlyMemory<byte>)value.ContentHash.ToByteArray()).ToArray())));
    }

    public ReadOnlyCollection<WorldMaterialProposal> Open { get; }
    public ReadOnlyCollection<WorldMaterialProposalOutcome> Outcomes { get; }
    public ContentHash ContentHash { get; }
    public bool CanCreate => Open.Count < MaximumOpen;
    public bool CanClose => Outcomes.Count < MaximumOutcomes;

    /// <summary>The ordered produced-stock movements that production's custody validation replays.</summary>
    public IEnumerable<ProducedCustodyMovement> ProducedMovements => Outcomes
        .Select(value => value.Settlement?.ToProducedMovement()).OfType<ProducedCustodyMovement>();

    public WorldMaterialProposal? FindOpen(EntityId proposalId) =>
        Open.FirstOrDefault(value => value.ProposalId == proposalId);

    public WorldMaterialProposalOutcome? FindOutcome(EntityId proposalId) =>
        Outcomes.FirstOrDefault(value => value.Proposal.ProposalId == proposalId);

    public WorldMaterialProposalBook Create(WorldMaterialProposal proposal) =>
        new(Open.Append(proposal).OrderBy(value => value.ProposalId), Outcomes);

    public WorldMaterialProposalBook Close(WorldMaterialProposalOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        WorldMaterialProposal? open = FindOpen(outcome.Proposal.ProposalId);
        if (open is null || open.ContentHash != outcome.Proposal.ContentHash)
        {
            throw new ArgumentException("Only an exact open proposal can receive its terminal outcome.", nameof(outcome));
        }
        return new(Open.Where(value => value.ProposalId != open.ProposalId), [.. Outcomes, outcome]);
    }

    /// <summary>
    /// Closes every due offer, in identity order, at the time boundary that published
    /// <paramref name="advancingSteps"/>. The reserved window guarantees room; it never evicts.
    /// </summary>
    public WorldMaterialProposalBook ExpireDue(ulong advancingSteps, ulong worldSequence)
    {
        WorldMaterialProposal[] due = Open.Where(value => value.IsDue(advancingSteps)).ToArray();
        if (due.Length == 0)
        {
            return this;
        }
        return new(Open.Where(value => !value.IsDue(advancingSteps)), [.. Outcomes, .. due.Select(value =>
            new WorldMaterialProposalOutcome(value, WorldMaterialProposalOutcomeKind.Expired, worldSequence, null, null))]);
    }

    /// <summary>The successor of a coordinated cut: every open offer survives; outcomes stay archived.</summary>
    public WorldMaterialProposalBook ContinueAcrossCut() => new(Open, []);

    private static void ValidateSettlementOrder(IEnumerable<WorldMaterialProposalOutcome> outcomes)
    {
        foreach (var batch in outcomes.Where(value => value.Settlement is not null).GroupBy(value => value.WorldSequence))
        {
            uint? previous = null;
            foreach (WorldMaterialProposalOutcome outcome in batch)
            {
                uint index = outcome.Settlement!.AcceptanceActionIndex;
                if (previous is not null && index <= previous)
                {
                    throw new ArgumentException("Settlements of one world sequence follow their strict action order.");
                }
                previous = index;
            }
        }
    }
}
