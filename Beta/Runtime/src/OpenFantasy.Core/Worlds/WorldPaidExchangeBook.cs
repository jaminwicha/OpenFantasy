using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// The typed owner of paid spot quotes in one complete world: open quotes, the terminal outcomes of the
/// current segment and every issuer's market revision. It holds no balance and no stock; goods stay with
/// inventory and production and money with progression. It never evicts: a full bound refuses the next
/// issuance or closure. Construction checks local invariants only; it is not durable authorization.
/// </summary>
/// <remarks>
/// Every open quote names its issuer's current market revision: a fill advances that revision and, in the
/// same transaction, supersedes the issuer's other open quotes. Outcomes follow one strict canonical order
/// (see OutcomePosition); construction refuses any other permutation rather than normalizing it.
/// </remarks>
public sealed class WorldPaidExchangeBook
{
    /// <summary>Development envelope for a small hosted party; production scale is a separate gate.</summary>
    public const int MaximumOpen = 16;

    /// <summary>Terminal outcomes retained until the next coordinated cut archives them.</summary>
    public const int MaximumOutcomes = 64;

    /// <summary>
    /// Bound of the never-pruned issuer roster. Issuers must be actual world actors, and a complete world
    /// admits at most <see cref="WorldStepBatch.MaximumActorsPerBatch"/> immutable residents, so this bound
    /// cannot be reached today. A future roster transition that admits more actors must first revisit this
    /// bound; revisions are never reset or pruned, because that could revive stale quoted terms.
    /// </summary>
    public const int MaximumIssuers = 128;

    public const int MaximumByteLength = (WorldPaidQuote.MaximumByteLength + sizeof(uint)) * MaximumOpen
        + (WorldPaidQuoteOutcome.MaximumByteLength + sizeof(uint)) * MaximumOutcomes + 48 * MaximumIssuers + 256;

    private readonly Dictionary<EntityId, ulong> _issuerRevisions;

    public WorldPaidExchangeBook(IEnumerable<WorldPaidQuote> open, IEnumerable<WorldPaidQuoteOutcome> outcomes,
        IEnumerable<KeyValuePair<EntityId, ulong>> issuerRevisions)
    {
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(outcomes);
        ArgumentNullException.ThrowIfNull(issuerRevisions);
        HashSet<EntityId> identities = [];
        var pending = new List<WorldPaidQuote>();
        foreach (WorldPaidQuote quote in open)
        {
            ArgumentNullException.ThrowIfNull(quote);
            bool follows = pending.Count == 0 || quote.QuoteId.CompareTo(pending[^1].QuoteId) > 0;
            if (pending.Count == MaximumOpen || !follows || !identities.Add(quote.QuoteId)
                || !identities.Add(quote.CreationCommandId))
            {
                throw new ArgumentException("Open quotes require bounded strict identity order and distinct identities.");
            }
            pending.Add(quote);
        }
        var closed = new List<WorldPaidQuoteOutcome>();
        foreach (WorldPaidQuoteOutcome outcome in outcomes)
        {
            ArgumentNullException.ThrowIfNull(outcome);
            bool split = outcome.Settlement is { } settlement && settlement.MovedLotId != outcome.Quote.Quote.LotId;
            if (closed.Count == MaximumOutcomes || !identities.Add(outcome.Quote.QuoteId)
                || !identities.Add(outcome.Quote.CreationCommandId)
                || outcome.Settlement is { } filled && !identities.Add(filled.Acceptance.AcceptanceId)
                || outcome.Withdrawal is { } withdrawn && !identities.Add(withdrawn.CommandId)
                || split && !identities.Add(outcome.Settlement!.MovedLotId))
            {
                throw new ArgumentException("Quote outcomes require bounded world order and distinct permanent identities.");
            }
            closed.Add(outcome);
        }
        _issuerRevisions = [];
        EntityId? previousIssuer = null;
        foreach ((EntityId issuer, ulong revision) in issuerRevisions)
        {
            if (_issuerRevisions.Count == MaximumIssuers || revision == 0
                || previousIssuer is { } prior && prior.CompareTo(issuer) >= 0 || !_issuerRevisions.TryAdd(issuer, revision))
            {
                throw new ArgumentException("Issuer market revisions are bounded, positive and in strict issuer order.");
            }
            previousIssuer = issuer;
        }
        foreach (WorldPaidQuote quote in pending.Concat(closed.Select(value => value.Quote)))
        {
            if (quote.Quote.MarketRevision > GetIssuerRevision(quote.Quote.IssuerActorId))
            {
                throw new ArgumentException("A quote cannot name a market revision its issuer has not reached.");
            }
        }
        foreach (WorldPaidQuote quote in pending)
        {
            // A fill supersedes every sibling, so no stale quote can remain open and hold capacity.
            if (quote.Quote.MarketRevision != GetIssuerRevision(quote.Quote.IssuerActorId))
            {
                throw new ArgumentException("An open quote must name its issuer's current market revision.");
            }
        }
        foreach (WorldPaidQuoteOutcome filled in closed.Where(value => value.Settlement is not null))
        {
            if (GetIssuerRevision(filled.Quote.Quote.IssuerActorId) <= filled.Quote.Quote.MarketRevision)
            {
                throw new ArgumentException("Every fill advances its issuer's market revision.");
            }
        }
        ValidateCanonicalOrder(closed);
        Open = pending.AsReadOnly();
        Outcomes = closed.AsReadOnly();
        IssuerRevisions = _issuerRevisions.OrderBy(value => value.Key).ToList().AsReadOnly();
        ContentHash = ContentHash.Compute(WorldPaidExchangeCodec.Encode(this));
    }

    public static WorldPaidExchangeBook Empty { get; } = new([], [], []);

    public ReadOnlyCollection<WorldPaidQuote> Open { get; }
    public ReadOnlyCollection<WorldPaidQuoteOutcome> Outcomes { get; }
    public ReadOnlyCollection<KeyValuePair<EntityId, ulong>> IssuerRevisions { get; }
    public ContentHash ContentHash { get; }
    public bool CanIssue => Open.Count < MaximumOpen;
    public bool CanClose => Outcomes.Count < MaximumOutcomes;

    /// <summary>Every fill of this segment in authoritative (world revision, action index) order.</summary>
    public IEnumerable<WorldPaidQuoteOutcome> Fills => Outcomes.Where(value => value.Settlement is not null);

    public ulong GetIssuerRevision(EntityId issuerActorId) => _issuerRevisions.GetValueOrDefault(issuerActorId);

    public WorldPaidQuote? FindOpen(EntityId quoteId) => Open.FirstOrDefault(value => value.QuoteId == quoteId);

    public WorldPaidQuoteOutcome? FindOutcome(EntityId quoteId) =>
        Outcomes.FirstOrDefault(value => value.Quote.QuoteId == quoteId);

    public WorldPaidExchangeBook Issue(WorldPaidQuote quote) =>
        new(Open.Append(quote).OrderBy(value => value.QuoteId), Outcomes, IssuerRevisions);

    /// <summary>Every other open quote of the same issuer: the quotes a fill of this one would supersede.</summary>
    public IEnumerable<WorldPaidQuote> SiblingsOf(WorldPaidQuote quote)
    {
        ArgumentNullException.ThrowIfNull(quote);
        return Open.Where(value => value.QuoteId != quote.QuoteId && value.Quote.IssuerActorId == quote.Quote.IssuerActorId);
    }

    /// <summary>Whether the outcome window holds a fill of this quote together with every supersession it causes.</summary>
    public bool CanFill(WorldPaidQuote quote) => Outcomes.Count + 1 + SiblingsOf(quote).Count() <= MaximumOutcomes;

    /// <summary>
    /// Closes one exact open quote with its fill, advances that issuer's market revision once and, in the
    /// same owner, supersedes each of the issuer's other open quotes in identity order. A supersession keeps
    /// the exact terms and names the fill as its cause; it moves nothing and advances no revision.
    /// </summary>
    public WorldPaidExchangeBook Fill(WorldPaidQuoteOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        WorldPaidQuote? open = FindOpen(outcome.Quote.QuoteId);
        if (open is null || open.ContentHash != outcome.Quote.ContentHash || outcome.Settlement is null)
        {
            throw new ArgumentException("Only an exact open quote can receive its fill.", nameof(outcome));
        }
        EntityId issuer = open.Quote.IssuerActorId;
        ulong next = checked(GetIssuerRevision(issuer) + 1);
        var cause = new WorldPaidSupersession(outcome.Settlement.Acceptance.AcceptanceId, open.QuoteId);
        WorldPaidQuote[] superseded = SiblingsOf(open).ToArray();
        return new(Open.Where(value => value.Quote.IssuerActorId != issuer),
            [.. Outcomes, outcome, .. superseded.Select(value => new WorldPaidQuoteOutcome(value, outcome.WorldSequence, cause))],
            IssuerRevisions.Where(value => value.Key != issuer).Append(new(issuer, next)).OrderBy(value => value.Key));
    }

    /// <summary>Closes one exact open quote with its issuer's withdrawal; no revision, money or goods change.</summary>
    public WorldPaidExchangeBook Withdraw(WorldPaidQuoteOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        WorldPaidQuote? open = FindOpen(outcome.Quote.QuoteId);
        if (open is null || open.ContentHash != outcome.Quote.ContentHash || outcome.Withdrawal is null)
        {
            throw new ArgumentException("Only an exact open quote can receive its withdrawal.", nameof(outcome));
        }
        return new(Open.Where(value => value.QuoteId != open.QuoteId), [.. Outcomes, outcome], IssuerRevisions);
    }

    /// <summary>
    /// Closes every due quote, in identity order, at the time boundary that published
    /// <paramref name="advancingSteps"/>. The reserved window guarantees room; it never evicts.
    /// </summary>
    public WorldPaidExchangeBook ExpireDue(ulong advancingSteps, ulong worldSequence)
    {
        WorldPaidQuote[] due = Open.Where(value => value.IsDue(advancingSteps)).ToArray();
        if (due.Length == 0)
        {
            return this;
        }
        return new(Open.Where(value => !value.IsDue(advancingSteps)), [.. Outcomes, .. due.Select(value =>
            new WorldPaidQuoteOutcome(value, WorldPaidQuoteOutcomeKind.Expired, worldSequence, null))], IssuerRevisions);
    }

    /// <summary>The successor of a coordinated cut: open quotes and market revisions survive; outcomes stay archived.</summary>
    public WorldPaidExchangeBook ContinueAcrossCut() => new(Open, [], IssuerRevisions);

    /// <summary>The ordered produced-stock movements of this segment's fills, keyed by their world position.</summary>
    internal IEnumerable<(ulong Sequence, uint Index, ProducedCustodyMovement Movement)> ProducedMovements => Fills
        .Select(value => (value.WorldSequence, value.Settlement!.ActionIndex, Movement: value.ToProducedMovement()))
        .Where(value => value.Movement is not null)
        .Select(value => (value.WorldSequence, value.ActionIndex, value.Movement!));

    /// <summary>
    /// Requires the one canonical outcome order the runtime produces, and that every supersession names an
    /// actual fill of the same issuer, terms revision and step inside this owner.
    /// </summary>
    private static void ValidateCanonicalOrder(IReadOnlyList<WorldPaidQuoteOutcome> outcomes)
    {
        Dictionary<EntityId, WorldPaidQuoteOutcome> fills = outcomes.Where(value => value.Settlement is not null)
            .ToDictionary(value => value.Settlement!.Acceptance.AcceptanceId);
        OutcomePosition? previous = null;
        foreach (WorldPaidQuoteOutcome outcome in outcomes)
        {
            uint ordinal;
            if (outcome.Supersession is { } cause)
            {
                if (!fills.TryGetValue(cause.AcceptanceId, out WorldPaidQuoteOutcome? fill)
                    || fill.Quote.QuoteId != cause.FilledQuoteId || fill.WorldSequence != outcome.WorldSequence
                    || fill.Quote.Quote.IssuerActorId != outcome.Quote.Quote.IssuerActorId
                    || fill.Quote.Quote.MarketRevision != outcome.Quote.Quote.MarketRevision)
                {
                    throw new ArgumentException("A superseded quote names its issuer's actual fill of the same step and revision.");
                }
                ordinal = fill.Settlement!.ActionIndex;
            }
            else
            {
                ordinal = outcome.CommandOrdinal ?? 0;
            }
            var position = new OutcomePosition(outcome, ordinal);
            if (previous is { } prior && prior.CompareTo(position) >= 0)
            {
                throw new ArgumentException("Quote outcomes must follow their one strict canonical world order.");
            }
            previous = position;
        }
    }

    /// <summary>
    /// The canonical position of one outcome: world sequence, then every command-caused outcome before the
    /// time boundary's expirations. Command outcomes follow their batch position (which also orders currency
    /// replay); at one position the command's own fill or withdrawal precedes the supersessions that fill
    /// caused, in quote identity order. Expirations follow in quote identity order. Two primary outcomes at
    /// one command position compare equal, so neither order of such a pair is admitted.
    /// </summary>
    private readonly record struct OutcomePosition : IComparable<OutcomePosition>
    {
        public OutcomePosition(WorldPaidQuoteOutcome outcome, uint ordinal)
        {
            Sequence = outcome.WorldSequence;
            Expiry = outcome.Kind == WorldPaidQuoteOutcomeKind.Expired;
            Ordinal = Expiry ? 0 : ordinal;
            Superseded = outcome.Kind == WorldPaidQuoteOutcomeKind.Superseded;
            Quote = Expiry || Superseded ? outcome.Quote.QuoteId : default;
        }

        private ulong Sequence { get; }
        private bool Expiry { get; }
        private uint Ordinal { get; }
        private bool Superseded { get; }
        private EntityId Quote { get; }

        public int CompareTo(OutcomePosition other)
        {
            int order = Sequence.CompareTo(other.Sequence);
            if (order == 0) order = Expiry.CompareTo(other.Expiry);
            if (order == 0) order = Ordinal.CompareTo(other.Ordinal);
            if (order == 0) order = Superseded.CompareTo(other.Superseded);
            return order != 0 ? order : Quote.CompareTo(other.Quote);
        }
    }
}

/// <summary>
/// Bounded paid-exchange context of one coordinated closing boundary: the exact quotes open in the actual
/// closing owner and every issuer's market revision. It retains no closing root, outcome or earlier context.
/// Its presence is the paid-exchange capability; an empty roster still selects that capability.
/// </summary>
public sealed class WorldPaidExchangeContinuationContext
{
    private WorldPaidExchangeContinuationContext(WorldPaidExchangeBook closing)
    {
        Carried = closing.Open;
        IssuerRevisions = closing.IssuerRevisions;
        ContentHash = ContentHash.Compute(WorldPaidExchangeCodec.Encode(closing.ContinueAcrossCut()));
    }

    /// <summary>Every quote that was open at the closing boundary, in strict identity order.</summary>
    public ReadOnlyCollection<WorldPaidQuote> Carried { get; }
    public ReadOnlyCollection<KeyValuePair<EntityId, ulong>> IssuerRevisions { get; }
    public ContentHash ContentHash { get; }

    internal static WorldPaidExchangeContinuationContext Capture(WorldPaidExchangeBook closing)
    {
        ArgumentNullException.ThrowIfNull(closing);
        return new(closing);
    }

    internal ulong GetIssuerRevision(EntityId issuer) =>
        IssuerRevisions.FirstOrDefault(value => value.Key == issuer).Value;

    /// <summary>
    /// Every carried quote stays exact, open or in one terminal outcome; any other quote follows the
    /// boundary. Each issuer's market revision equals its carried revision plus this segment's fills.
    /// </summary>
    internal void ValidateSuccessor(WorldPaidExchangeBook successor, ulong closingWorldSequence)
    {
        ArgumentNullException.ThrowIfNull(successor);
        Dictionary<EntityId, WorldPaidQuote> pending = Carried.ToDictionary(value => value.QuoteId);
        foreach (WorldPaidQuote quote in successor.Open.Concat(successor.Outcomes.Select(value => value.Quote)))
        {
            if (pending.Remove(quote.QuoteId, out WorldPaidQuote? carried))
            {
                if (carried.ContentHash != quote.ContentHash)
                {
                    throw new ArgumentException("A carried quote must remain exact after its closing boundary.");
                }
            }
            else if (quote.CreatedSequence <= closingWorldSequence)
            {
                throw new ArgumentException("A quote the closing owner did not carry must follow its closing boundary.");
            }
        }
        if (pending.Count != 0)
        {
            throw new ArgumentException("Every carried quote stays open or reaches one terminal outcome after its boundary.");
        }
        WorldPaidExchangeAdmission.ValidateIssuerRevisions(successor, GetIssuerRevision,
            IssuerRevisions.Select(value => value.Key));
        // Outcomes already follow the boundary, so no carried quote can take a terminal cause from before it.
        WorldPaidExchangeAdmission.ValidateLifecycle(successor, GetIssuerRevision, closingWorldSequence);
    }
}
