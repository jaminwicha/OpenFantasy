using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;

namespace OpenFantasy.Core.Markets;

public enum SpotExchangeRejection
{
    None = 0,
    NotAuthorized = 1,
    QuoteClosed = 2,
    IdentityConflict = 3,
    Expired = 4,
    RevisionConflict = 5,
    InsufficientFunds = 6,
    InsufficientCapacity = 7,
    StockUnavailable = 8,
    ProtectedStock = 9,
    UnsupportedHistory = 10,
    Overflow = 11
}

public enum SpotExchangeOutcome
{
    Applied = 1,

    /// <summary>The identical acceptance already settled this quote; nothing changes and the recorded fill is returned.</summary>
    Duplicate = 2,
    Rejected = 3
}

/// <summary>
/// One complete candidate of every affected owner, or the untouched originals. Only a durable caller may
/// commit it, and only onto the exact decided inventory, production, progression and quote-book bases.
/// </summary>
public sealed class SpotExchangePlan
{
    internal SpotExchangePlan(CountedAuthorityState inventory, ProductionSnapshot production, ProgressionSnapshot progression,
        SpotQuoteBook book, CountedAuthorityState candidateInventory, ProductionSnapshot candidateProduction,
        ProgressionSnapshot candidateProgression, SpotQuoteBook candidateBook, SpotExchangeOutcome outcome,
        SpotExchangeRejection rejection, SpotFill? fill, SpotSettlementEvidence? evidence = null)
    {
        if ((outcome == SpotExchangeOutcome.Rejected) == (rejection == SpotExchangeRejection.None)
            || (outcome == SpotExchangeOutcome.Rejected) == (fill is not null))
        {
            throw new ArgumentException("Exactly the settled or duplicate outcomes carry a fill and no rejection.");
        }

        BaseInventoryHash = inventory.ContentHash;
        BaseProductionHash = production.ContentHash;
        BaseProgressionHash = progression.ContentHash;
        BaseBookHash = book.ContentHash;
        CandidateInventory = candidateInventory;
        CandidateProduction = candidateProduction;
        CandidateProgression = candidateProgression;
        CandidateBook = candidateBook;
        Outcome = outcome;
        Rejection = rejection;
        Fill = fill;
        Evidence = evidence;
    }

    public ContentHash BaseInventoryHash { get; }
    public ContentHash BaseProductionHash { get; }
    public ContentHash BaseProgressionHash { get; }
    public ContentHash BaseBookHash { get; }
    public CountedAuthorityState CandidateInventory { get; }
    public ProductionSnapshot CandidateProduction { get; }
    public ProgressionSnapshot CandidateProgression { get; }
    public SpotQuoteBook CandidateBook { get; }
    public SpotExchangeOutcome Outcome { get; }
    public SpotExchangeRejection Rejection { get; }
    public SpotFill? Fill { get; }
    public SpotSettlementEvidence? Evidence { get; }
    public bool IsApplied => Outcome == SpotExchangeOutcome.Applied;

    public (CountedAuthorityState Inventory, ProductionSnapshot Production, ProgressionSnapshot Progression,
        SpotQuoteBook Book) ApplyTo(CountedAuthorityState inventory, ProductionSnapshot production,
        ProgressionSnapshot progression, SpotQuoteBook book)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(progression);
        ArgumentNullException.ThrowIfNull(book);
        if (!IsApplied)
        {
            throw new InvalidOperationException("Only a settled exchange has a candidate to apply.");
        }

        if (inventory.ContentHash != BaseInventoryHash || production.ContentHash != BaseProductionHash
            || progression.ContentHash != BaseProgressionHash || book.ContentHash != BaseBookHash)
        {
            throw new CustodyConflictException(CustodyConflict.StalePlan);
        }

        return (CandidateInventory, CandidateProduction, CandidateProgression, CandidateBook);
    }
}

/// <summary>A new quote-book candidate for one issuance, or the unchanged book; nothing else changes.</summary>
public sealed class SpotQuoteIssuePlan
{
    internal SpotQuoteIssuePlan(CountedAuthorityState inventory, ProductionSnapshot production, ProgressionSnapshot progression,
        SpotQuoteBook book, SpotQuoteBook candidate, SpotExchangeRejection rejection)
    {
        BaseBookHash = book.ContentHash;
        BaseInventoryHash = inventory.ContentHash;
        BaseProductionHash = production.ContentHash;
        BaseProgressionHash = progression.ContentHash;
        CandidateBook = candidate;
        Rejection = rejection;
    }

    public ContentHash BaseBookHash { get; }
    public ContentHash BaseInventoryHash { get; }
    public ContentHash BaseProductionHash { get; }
    public ContentHash BaseProgressionHash { get; }
    public SpotQuoteBook CandidateBook { get; }
    public SpotExchangeRejection Rejection { get; }
    public bool IsApplied => Rejection == SpotExchangeRejection.None;

    public SpotQuoteBook ApplyTo(CountedAuthorityState inventory, ProductionSnapshot production,
        ProgressionSnapshot progression, SpotQuoteBook book)
    {
        ArgumentNullException.ThrowIfNull(book);
        if (!IsApplied)
        {
            throw new InvalidOperationException("A refused issuance has no candidate to apply.");
        }

        if (book.ContentHash != BaseBookHash || inventory.ContentHash != BaseInventoryHash
            || production.ContentHash != BaseProductionHash || progression.ContentHash != BaseProgressionHash)
        {
            throw new CustodyConflictException(CustodyConflict.StalePlan);
        }

        return CandidateBook;
    }
}
