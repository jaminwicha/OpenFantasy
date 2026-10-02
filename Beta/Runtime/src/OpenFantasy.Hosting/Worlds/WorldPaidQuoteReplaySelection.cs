using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>
/// Retains at most one speculative publishing root during complete journal replay. The retrieved
/// archive must identify the same exact terminal outcome before the root can serve an inspection.
/// </summary>
internal sealed class WorldPaidQuoteReplaySelection(EntityId quoteId)
{
    private WorldSnapshot? _publishingState;

    internal void Observe(WorldSnapshot candidate)
    {
        WorldPaidQuoteOutcome? outcome = candidate.Coordination?.PaidExchange?.FindOutcome(quoteId);
        if (outcome is null || outcome.WorldSequence != candidate.Revision)
        {
            return;
        }
        if (_publishingState is not null)
        {
            throw new InvalidDataException("A paid quote has more than one publishing root.");
        }
        _publishingState = candidate;
    }

    /// <summary>Called only after the entire journal, archives, head and retained tables validate.</summary>
    internal WorldSnapshot? ResolveArchived(WorldPaidQuoteOutcome? archived)
    {
        if (archived is null)
        {
            // A live terminal result has no archived interpretation through this query.
            return null;
        }
        if (_publishingState is not { } publishing || archived.Quote.QuoteId != quoteId
            || publishing.Revision != archived.WorldSequence
            || publishing.Coordination?.PaidExchange?.FindOutcome(quoteId) is not { } captured
            || !captured.Encode().AsSpan().SequenceEqual(archived.Encode()))
        {
            throw new InvalidDataException("The archived quote differs from its exact replayed publishing root.");
        }
        return publishing;
    }
}
