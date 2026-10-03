using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Retains only a bounded participant page while the ordinary complete replay authenticates history.</summary>
internal sealed class WorldPrivateHistoryReplaySelection(EntityId actor,
    LocalPrivateHistoryPageRequest request, ulong throughRevision)
{
    private readonly List<LocalPrivateHistoryEntry> _entries = [];

    internal void Observe(WorldSnapshot publication)
    {
        if (publication.Revision > throughRevision) return;
        if (publication.Coordination?.PaidExchange is { } quotes)
        {
            foreach (WorldPaidQuoteOutcome outcome in quotes.Outcomes)
            {
                if (outcome.WorldSequence != publication.Revision || !outcome.Quote.HasParticipant(actor)) continue;
                var terms = outcome.Quote.Quote;
                EntityId counterparty = terms.IssuerActorId == actor ? terms.CounterpartyActorId : terms.IssuerActorId;
                Retain(new(LocalPrivateHistoryKind.PaidQuote, outcome.Quote.QuoteId, counterparty,
                    outcome.WorldSequence, QuoteStatus(outcome.Kind)));
            }
        }
        if (publication.Coordination?.Proposals is { } proposals)
        {
            foreach (WorldMaterialProposalOutcome outcome in proposals.Outcomes)
            {
                if (outcome.WorldSequence != publication.Revision || !outcome.Proposal.HasParticipant(actor)) continue;
                var terms = outcome.Proposal.Terms;
                EntityId counterparty = terms.SenderActorId == actor ? terms.RecipientActorId : terms.SenderActorId;
                Retain(new(LocalPrivateHistoryKind.MaterialProposal, outcome.Proposal.ProposalId, counterparty,
                    outcome.WorldSequence, MaterialProposalStatus: ProposalStatus(outcome.Kind)));
            }
        }
    }

    private void Retain(LocalPrivateHistoryEntry entry)
    {
        if (request.Before is { } before && LocalPrivateHistoryOrder.Compare(entry, before) <= 0) return;
        int position = _entries.FindIndex(existing => LocalPrivateHistoryOrder.Compare(entry, existing) < 0);
        if (position < 0) position = _entries.Count;
        if (position > LocalPrivateHistoryPage.MaximumEntries) return;
        if (_entries.Count == LocalPrivateHistoryPage.MaximumEntries + 1)
            _entries.RemoveAt(_entries.Count - 1);
        _entries.Insert(position, entry);
    }

    /// <summary>Only the recovery owner calls this after the complete replay and retained tables validate.</summary>
    internal LocalPrivateHistoryPage Complete()
    {
        LocalPrivateHistoryEntry[] page = _entries.Take(LocalPrivateHistoryPage.MaximumEntries).ToArray();
        LocalPrivateHistoryCursor? next = _entries.Count > page.Length
            ? new(throughRevision, page[^1].ClosedAtSequence, page[^1].Kind, page[^1].SubjectId) : null;
        return new(request, throughRevision, page, next);
    }

    private static PaidQuoteStatus QuoteStatus(WorldPaidQuoteOutcomeKind kind) => kind switch
    {
        WorldPaidQuoteOutcomeKind.Filled => PaidQuoteStatus.Filled,
        WorldPaidQuoteOutcomeKind.Expired => PaidQuoteStatus.Expired,
        WorldPaidQuoteOutcomeKind.Superseded => PaidQuoteStatus.Superseded,
        WorldPaidQuoteOutcomeKind.Withdrawn => PaidQuoteStatus.Withdrawn,
        _ => throw new InvalidDataException("Unknown terminal quote outcome.")
    };

    private static MaterialProposalStatus ProposalStatus(WorldMaterialProposalOutcomeKind kind) => kind switch
    {
        WorldMaterialProposalOutcomeKind.Accepted => MaterialProposalStatus.Accepted,
        WorldMaterialProposalOutcomeKind.Declined => MaterialProposalStatus.Declined,
        WorldMaterialProposalOutcomeKind.Withdrawn => MaterialProposalStatus.Withdrawn,
        WorldMaterialProposalOutcomeKind.Expired => MaterialProposalStatus.Expired,
        _ => throw new InvalidDataException("Unknown terminal proposal outcome.")
    };
}
