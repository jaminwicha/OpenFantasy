using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Markets;

/// <summary>
/// Single-fill policy: an open quote accepts exactly one fill of at most its allowance and then closes,
/// retiring any unused allowance. A fill advances the quoted lot's revision, so a quote priced against
/// that revision could never truthfully settle a second fill; a new quote is required.
/// </summary>
public enum SpotQuoteStatus
{
    Open = 1,
    Filled = 2
}

/// <summary>The settled outcome of one acceptance: exact amounts, the moved lot and its book sequence.</summary>
public sealed record SpotFill
{
    public SpotFill(ulong sequence, EntityId acceptanceId, ContentHash acceptanceHash, ulong quantity,
        EntityId movedLotId, ulong payment, ulong fees)
    {
        ArgumentNullException.ThrowIfNull(acceptanceHash);
        ArgumentOutOfRangeException.ThrowIfZero(sequence);
        ArgumentOutOfRangeException.ThrowIfZero(quantity);
        Sequence = sequence;
        AcceptanceId = acceptanceId;
        AcceptanceHash = acceptanceHash;
        Quantity = quantity;
        MovedLotId = movedLotId;
        Payment = payment;
        Fees = fees;
    }

    /// <summary>The book revision this fill produced; fills are ordered by it.</summary>
    public ulong Sequence { get; }
    public EntityId AcceptanceId { get; }
    public ContentHash AcceptanceHash { get; }
    public ulong Quantity { get; }

    /// <summary>The whole quoted lot, or the child spawned from the acceptance identity for a partial amount.</summary>
    public EntityId MovedLotId { get; }
    public ulong Payment { get; }
    public ulong Fees { get; }
}

/// <summary>One issued quote with the host delivery recorded at issuance and its single-fill state.</summary>
public sealed class SpotQuoteEntry
{
    public SpotQuoteEntry(SpotQuote quote, TrustedCommandDelivery issuerDelivery, SpotFill? fill = null)
    {
        ArgumentNullException.ThrowIfNull(quote);
        ArgumentNullException.ThrowIfNull(issuerDelivery);
        if (fill is not null && (fill.Quantity > quote.QuantityAllowance || fill.Payment != quote.PaymentFor(fill.Quantity)
            || fill.Fees != quote.FeesFor(fill.Quantity) || fill.AcceptanceId == quote.QuoteId))
        {
            throw new ArgumentException("A fill must match its quote's exact terms and allowance.", nameof(fill));
        }

        if (issuerDelivery.PrincipalId != quote.IssuerPrincipalId || issuerDelivery.SessionId != quote.IssuerSessionId
            || fill is not null && fill.MovedLotId != quote.LotId
                && fill.MovedLotId != EntityId.DeriveSpawned(fill.AcceptanceId, OpenFantasy.Core.Custody.CustodyTransferPlanner.SplitChildOrdinal))
        {
            throw new ArgumentException("Recorded delivery claims and moved identity must match the quote and fill.");
        }
        Quote = quote;
        IssuerDelivery = issuerDelivery;
        Fill = fill;
    }

    public SpotQuote Quote { get; }

    /// <summary>The issuer's host-verified delivery recorded at issuance, compared with current control later.</summary>
    public TrustedCommandDelivery IssuerDelivery { get; }
    public SpotFill? Fill { get; }
    public SpotQuoteStatus Status => Fill is null ? SpotQuoteStatus.Open : SpotQuoteStatus.Filled;
}

/// <summary>
/// The canonical owner of issued quotes, remaining allowances, fills and per-issuer market revisions for
/// one world/branch. It holds no balance and no stock: custody, produced provenance and currency stay
/// with their own owners. Construction checks local invariants only; it is not durable authorization.
/// </summary>
public sealed class SpotQuoteBook
{
    public const int MaximumQuotes = 256;
    private readonly Dictionary<EntityId, SpotQuoteEntry> _entries;
    private readonly Dictionary<EntityId, ulong> _issuerRevisions;
    private readonly HashSet<EntityId> _identities = [];

    public SpotQuoteBook(EntityId worldId, EntityId branchId, ulong revision, IEnumerable<SpotQuoteEntry> entries,
        IEnumerable<KeyValuePair<EntityId, ulong>> issuerRevisions)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(issuerRevisions);
        if (worldId == branchId)
        {
            throw new ArgumentException("World and branch identities must differ.", nameof(branchId));
        }

        _entries = [];
        foreach (SpotQuoteEntry entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
            SpotQuote quote = entry.Quote;
            if (_entries.Count == MaximumQuotes || quote.WorldId != worldId || quote.BranchId != branchId
                || !_identities.Add(quote.QuoteId) || entry.Fill is { } fill && !_identities.Add(fill.AcceptanceId))
            {
                throw new ArgumentException("Quotes must be bounded, in scope and use distinct permanent identities.",
                    nameof(entries));
            }

            _entries.Add(quote.QuoteId, entry);
        }

        _issuerRevisions = [];
        foreach ((EntityId issuer, ulong issuerRevision) in issuerRevisions)
        {
            if (_issuerRevisions.Count == MaximumQuotes || issuerRevision == 0
                || !_issuerRevisions.TryAdd(issuer, issuerRevision))
            {
                throw new ArgumentException("Issuer revisions are bounded, positive and unique.", nameof(issuerRevisions));
            }
        }

        SpotFill[] fills = _entries.Values.Where(value => value.Fill is not null).Select(value => value.Fill!)
            .OrderBy(value => value.Sequence).ToArray();
        if (revision != (ulong)(_entries.Count + fills.Length)
            || _issuerRevisions.Any(value => value.Value != (ulong)_entries.Values.Count(entry =>
                entry.Quote.IssuerActorId == value.Key && entry.Fill is not null)))
        {
            throw new ArgumentException("An unpruned book retains exactly one revision per issue and fill.");
        }
        for (int index = 0; index < fills.Length; index++)
        {
            if (fills[index].Sequence > revision || index > 0 && fills[index].Sequence == fills[index - 1].Sequence)
            {
                throw new ArgumentException("Fill sequences are distinct and within the book revision.", nameof(entries));
            }
        }

        foreach (IGrouping<EntityId, SpotQuoteEntry> issuer in _entries.Values.GroupBy(value => value.Quote.IssuerActorId))
        {
            ulong filled = (ulong)issuer.Count(value => value.Fill is not null);
            if (_issuerRevisions.GetValueOrDefault(issuer.Key) < filled
                || issuer.Any(value => value.Quote.MarketRevision > _issuerRevisions.GetValueOrDefault(issuer.Key)))
            {
                throw new ArgumentException("Issuer revisions must cover every fill and issued market revision.",
                    nameof(issuerRevisions));
            }
        }

        WorldId = worldId;
        BranchId = branchId;
        Revision = revision;
        Entries = _entries.Values.OrderBy(value => value.Quote.QuoteId).ToList().AsReadOnly();
        IssuerRevisions = _issuerRevisions.OrderBy(value => value.Key).ToList().AsReadOnly();
        Fills = Array.AsReadOnly(fills);
    }

    public static SpotQuoteBook Empty(EntityId worldId, EntityId branchId) => new(worldId, branchId, 0, [], []);

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public ulong Revision { get; }
    public ReadOnlyCollection<SpotQuoteEntry> Entries { get; }
    public ReadOnlyCollection<KeyValuePair<EntityId, ulong>> IssuerRevisions { get; }

    /// <summary>Every settled fill in book-sequence order.</summary>
    public ReadOnlyCollection<SpotFill> Fills { get; }
    public ContentHash ContentHash => ContentHash.Compute(SpotExchangeCodecV1.Encode(this));

    public bool ContainsIdentity(EntityId identity) => _identities.Contains(identity);
    public ulong GetIssuerRevision(EntityId issuerActorId) => _issuerRevisions.GetValueOrDefault(issuerActorId);

    public SpotQuoteEntry? FindEntry(EntityId quoteId) => _entries.GetValueOrDefault(quoteId);

    /// <summary>
    /// The ordered produced-custody projection of settled fills, for the complete world's partition replay.
    /// It grants nothing and must be merged with every other ordered movement the world records.
    /// </summary>
    public ProducedCustodyMovement[] ProducedMovements() => Fills
        .Select(fill => _entries.Values.Single(entry => entry.Fill == fill))
        .Where(entry => entry.Quote.LotKind == SpotLotKind.Produced)
        .Select(entry => new ProducedCustodyMovement(entry.Quote.LotId, entry.Quote.ExpectedLotRevision,
            entry.Fill!.MovedLotId, entry.Quote.SellerCustodianId, entry.Quote.BuyerCustodianId, entry.Fill.Quantity,
            expectedDefinition: entry.Quote.Definition))
        .ToArray();

    internal SpotQuoteBook WithIssued(SpotQuoteEntry entry) =>
        new(WorldId, BranchId, checked(Revision + 1), [.. Entries, entry], IssuerRevisions);

    internal SpotQuoteBook WithFill(SpotQuoteEntry filled)
    {
        EntityId issuer = filled.Quote.IssuerActorId;
        ulong next = checked(GetIssuerRevision(issuer) + 1);
        return new(WorldId, BranchId, checked(Revision + 1),
            Entries.Select(value => value.Quote.QuoteId == filled.Quote.QuoteId ? filled : value),
            IssuerRevisions.Where(value => value.Key != issuer).Append(new(issuer, next)));
    }
}
