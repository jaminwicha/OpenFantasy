using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Observations;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// The market basis of one paid quote issued under a market rule: the market, the rule version and the
/// usable stock count its fixed price was derived from. The quote itself, its allowance and its lifecycle
/// stay in <see cref="WorldPaidExchangeBook"/>; this record only states which market state priced it.
/// </summary>
public sealed class MarketQuoteRecord
{
    public const int MaximumByteLength = 192;

    public MarketQuoteRecord(EntityId quoteId, EntityId marketId, ContentHash policyVersion, ulong stockBasis)
    {
        ArgumentNullException.ThrowIfNull(policyVersion);
        if (quoteId == marketId || stockBasis > MarketPricePolicy.MaximumStock)
        {
            throw new ArgumentException("A market quote record names a distinct quote and a bounded stock basis.");
        }
        QuoteId = quoteId;
        MarketId = marketId;
        PolicyVersion = policyVersion;
        StockBasis = stockBasis;
    }

    public EntityId QuoteId { get; }
    public EntityId MarketId { get; }
    public ContentHash PolicyVersion { get; }

    /// <summary>The merchant's usable stock when the quote was issued; any other count makes it stale.</summary>
    public ulong StockBasis { get; }

    public byte[] Encode() => CanonicalEncodingV1.Frame("world.market-quote-record.v1", QuoteId.ToByteArray(),
        MarketId.ToByteArray(), PolicyVersion.ToByteArray(), CanonicalEncodingV1.Unsigned64(StockBasis));
}

/// <summary>
/// The typed owner of local markets in one complete world (profile 33): the admitted immutable market
/// bindings, the market basis of each market quote in the current segment, every actor's latest dated
/// price observation per market and every seller's latest explicit lot disclosure per market. It holds no
/// stock, money or quote terms. Every bound is finite and is never satisfied by eviction: one observation
/// and one disclosure per actor and market fit the admitted roster, and quote records mirror the paid
/// book's own open and outcome bounds.
/// </summary>
public sealed class WorldMarketBook
{
    public const int MaximumMarkets = 4;
    public const int MaximumQuoteRecords = WorldPaidExchangeBook.MaximumOpen + WorldPaidExchangeBook.MaximumOutcomes;
    public const int MaximumObservations = MaximumMarkets * WorldStepBatch.MaximumActorsPerBatch;
    public const int MaximumDisclosures = MaximumMarkets * WorldStepBatch.MaximumActorsPerBatch;

    public const int MaximumByteLength = (1_024 + sizeof(uint)) * MaximumMarkets
        + (MarketQuoteRecord.MaximumByteLength + sizeof(uint)) * MaximumQuoteRecords
        + (MarketPriceObservation.MaximumByteLength + sizeof(uint)) * MaximumObservations
        + (MarketLotDisclosure.MaximumByteLength + sizeof(uint)) * MaximumDisclosures + 256;

    public WorldMarketBook(IEnumerable<MarketBinding> markets, IEnumerable<MarketQuoteRecord> quotes,
        IEnumerable<MarketPriceObservation> knowledge, IEnumerable<MarketLotDisclosure>? disclosures = null)
    {
        ArgumentNullException.ThrowIfNull(markets);
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(knowledge);
        List<MarketBinding> bound = [];
        foreach (MarketBinding market in markets)
        {
            ArgumentNullException.ThrowIfNull(market);
            if (bound.Count == MaximumMarkets || bound.Count != 0 && market.MarketId.CompareTo(bound[^1].MarketId) <= 0)
            {
                throw new ArgumentException("Markets are bounded and in strict identity order.");
            }
            if (bound.Any(existing => existing.CustodianId == market.CustodianId
                && existing.Commodity.Equals(market.Commodity)))
            {
                // Independent rules over the same goods could buy and resell a unit at different
                // targets while restoring the very same stock. Each priceable pool has one rule.
                throw new ArgumentException("A custodian's commodity stock pool has exactly one market binding.");
            }
            bound.Add(market);
        }
        List<MarketQuoteRecord> records = [];
        foreach (MarketQuoteRecord record in quotes)
        {
            ArgumentNullException.ThrowIfNull(record);
            MarketBinding? market = bound.FirstOrDefault(value => value.MarketId == record.MarketId);
            if (records.Count == MaximumQuoteRecords || records.Count != 0 && record.QuoteId.CompareTo(records[^1].QuoteId) <= 0
                || market is null || market.Policy.Version != record.PolicyVersion)
            {
                throw new ArgumentException("Market quote records are bounded, ordered and name an admitted market rule.");
            }
            records.Add(record);
        }
        List<MarketPriceObservation> learned = [];
        foreach (MarketPriceObservation observation in knowledge)
        {
            ArgumentNullException.ThrowIfNull(observation);
            MarketBinding? market = bound.FirstOrDefault(value => value.MarketId == observation.MarketId);
            bool follows = learned.Count == 0 || KnowledgeOrder(learned[^1], observation) < 0;
            if (learned.Count == MaximumObservations || !follows || market is null
                || market.MerchantActorId != observation.MerchantActorId || !market.Commodity.Equals(observation.Commodity)
                || market.Policy.Version != observation.PolicyVersion || market.Policy.UnitFee != observation.UnitFee)
            {
                throw new ArgumentException("Price knowledge is bounded, one per actor and market, and pins its market rule.");
            }
            learned.Add(observation);
        }
        List<MarketLotDisclosure> shown = [];
        foreach (MarketLotDisclosure disclosure in disclosures ?? [])
        {
            ArgumentNullException.ThrowIfNull(disclosure);
            MarketBinding? market = bound.FirstOrDefault(value => value.MarketId == disclosure.MarketId);
            bool follows = shown.Count == 0 || DisclosureOrder(shown[^1], disclosure) < 0;
            if (shown.Count == MaximumDisclosures || !follows || market is null
                || market.MerchantActorId != disclosure.MerchantActorId || !market.Commodity.Equals(disclosure.Definition))
            {
                throw new ArgumentException("Lot disclosures are bounded, one per seller and market, and name that market's commodity.");
            }
            shown.Add(disclosure);
        }
        Markets = bound.AsReadOnly();
        Quotes = records.AsReadOnly();
        Knowledge = learned.AsReadOnly();
        Disclosures = shown.AsReadOnly();
        ContentHash = ContentHash.Compute(WorldMarketCodec.Encode(this));
    }

    public ReadOnlyCollection<MarketBinding> Markets { get; }
    public ReadOnlyCollection<MarketQuoteRecord> Quotes { get; }
    public ReadOnlyCollection<MarketPriceObservation> Knowledge { get; }

    /// <summary>Each seller's latest explicit showing of one lot to each market, in (seller, market) order.</summary>
    public ReadOnlyCollection<MarketLotDisclosure> Disclosures { get; }
    public ContentHash ContentHash { get; }

    public MarketBinding? FindMarket(EntityId marketId) => Markets.FirstOrDefault(value => value.MarketId == marketId);

    public MarketQuoteRecord? FindQuote(EntityId quoteId) => Quotes.FirstOrDefault(value => value.QuoteId == quoteId);

    public MarketPriceObservation? FindObservation(EntityId observerActorId, EntityId marketId) =>
        Knowledge.FirstOrDefault(value => value.ObserverActorId == observerActorId && value.MarketId == marketId);

    /// <summary>Every dated observation one actor holds, in market order. Nothing else is disclosed.</summary>
    public IEnumerable<MarketPriceObservation> KnowledgeOf(EntityId observerActorId) =>
        Knowledge.Where(value => value.ObserverActorId == observerActorId);

    public MarketLotDisclosure? FindDisclosure(EntityId sellerActorId, EntityId marketId) =>
        Disclosures.FirstOrDefault(value => value.SellerActorId == sellerActorId && value.MarketId == marketId);

    /// <summary>Every showing addressed to one merchant's markets, live or not, in (seller, market) order.</summary>
    public IEnumerable<MarketLotDisclosure> DisclosuresTo(EntityId merchantActorId) =>
        Disclosures.Where(value => value.MerchantActorId == merchantActorId);

    /// <summary>
    /// Whether the counterparty of a buy quote explicitly showed its exact pinned lot to the issuing merchant,
    /// through this market or, for a bilateral quote, through any market of that merchant, and the showing
    /// is still live for a command applied after <paramref name="advancingSteps"/> steps.
    /// </summary>
    internal bool DisclosesTo(SpotQuote quote, EntityId? marketId, ulong advancingSteps) =>
        Disclosures.Any(value => (marketId is null || value.MarketId == marketId)
            && value.Authorizes(quote, advancingSteps));

    internal WorldMarketBook RecordQuote(MarketQuoteRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return new(Markets, Quotes.Append(record).OrderBy(value => value.QuoteId), Knowledge, Disclosures);
    }

    /// <summary>A newer showing by the same seller to the same market replaces its earlier one.</summary>
    internal WorldMarketBook Disclose(MarketLotDisclosure disclosure)
    {
        ArgumentNullException.ThrowIfNull(disclosure);
        MarketLotDisclosure? prior = FindDisclosure(disclosure.SellerActorId, disclosure.MarketId);
        if (prior is not null && prior.DisclosedSequence >= disclosure.DisclosedSequence)
        {
            throw new ArgumentException("A disclosure advances only through a later showing.");
        }
        List<MarketLotDisclosure> ordered = Disclosures.Where(value => !ReferenceEquals(value, prior)).Append(disclosure).ToList();
        ordered.Sort(DisclosureOrder);
        return new(Markets, Quotes, Knowledge, ordered);
    }

    /// <summary>The seller's own withdrawal of its showing to one market; quotes already issued keep their terms.</summary>
    internal WorldMarketBook Retract(MarketLotDisclosure disclosure)
    {
        ArgumentNullException.ThrowIfNull(disclosure);
        return new(Markets, Quotes, Knowledge, Disclosures.Where(value => !ReferenceEquals(value, disclosure)));
    }

    /// <summary>A newer in-person observation replaces the same actor's earlier memory of that market.</summary>
    internal WorldMarketBook Learn(MarketPriceObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        MarketPriceObservation? prior = FindObservation(observation.ObserverActorId, observation.MarketId);
        if (prior is not null && prior.ObservedSequence >= observation.ObservedSequence)
        {
            throw new ArgumentException("Knowledge advances only through a later observation.");
        }
        IEnumerable<MarketPriceObservation> retained = Knowledge.Where(value => !ReferenceEquals(value, prior));
        List<MarketPriceObservation> ordered = retained.Append(observation).ToList();
        ordered.Sort(KnowledgeOrder);
        return new(Markets, Quotes, ordered, Disclosures);
    }

    /// <summary>
    /// The successor of a coordinated cut. Markets, knowledge and disclosures survive whole, with their
    /// original causes; a quote record survives exactly while its quote is still open. Records of closed
    /// quotes stay in the archived closing root.
    /// </summary>
    internal WorldMarketBook ContinueAcrossCut(WorldPaidExchangeBook successorQuotes)
    {
        ArgumentNullException.ThrowIfNull(successorQuotes);
        return new(Markets, Quotes.Where(value => successorQuotes.FindOpen(value.QuoteId) is not null), Knowledge,
            Disclosures);
    }

    internal static int KnowledgeOrder(MarketPriceObservation left, MarketPriceObservation right)
    {
        int order = left.ObserverActorId.CompareTo(right.ObserverActorId);
        return order != 0 ? order : left.MarketId.CompareTo(right.MarketId);
    }

    internal static int DisclosureOrder(MarketLotDisclosure left, MarketLotDisclosure right)
    {
        int order = left.SellerActorId.CompareTo(right.SellerActorId);
        return order != 0 ? order : left.MarketId.CompareTo(right.MarketId);
    }
}

/// <summary>
/// Bounded market context of one coordinated closing boundary: the admitted bindings, the market basis
/// of every quote still open there and all live price knowledge. Its presence is the market capability.
/// </summary>
public sealed class WorldMarketContinuationContext
{
    private WorldMarketContinuationContext(WorldMarketBook carried)
    {
        Carried = carried;
        ContentHash = ContentHash.Compute(WorldMarketCodec.Encode(carried));
    }

    /// <summary>The closing owner as it continues: exact bindings, open-quote records and knowledge.</summary>
    public WorldMarketBook Carried { get; }
    public ContentHash ContentHash { get; }

    internal static WorldMarketContinuationContext Capture(WorldMarketBook closing, WorldPaidExchangeBook closingQuotes)
    {
        ArgumentNullException.ThrowIfNull(closing);
        return new(closing.ContinueAcrossCut(closingQuotes));
    }

    /// <summary>
    /// Bindings stay exact; every carried record stays exact; every carried observation stays exact or is
    /// replaced only by a later observation of the same actor and market; nothing else predates the boundary.
    /// </summary>
    internal void ValidateSuccessor(WorldMarketBook successor, ulong closingWorldSequence)
    {
        ArgumentNullException.ThrowIfNull(successor);
        if (!Carried.Markets.Select(value => value.ContentHash).SequenceEqual(successor.Markets.Select(value => value.ContentHash)))
        {
            throw new ArgumentException("A market successor retains its exact admitted bindings.");
        }
        foreach (MarketQuoteRecord record in Carried.Quotes)
        {
            MarketQuoteRecord? current = successor.FindQuote(record.QuoteId);
            if (current is null || !current.Encode().AsSpan().SequenceEqual(record.Encode()))
            {
                throw new ArgumentException("A carried market quote record remains exact after its boundary.");
            }
        }
        foreach (MarketPriceObservation observation in Carried.Knowledge)
        {
            MarketPriceObservation? current = successor.FindObservation(observation.ObserverActorId, observation.MarketId);
            if (current is null || current.ContentHash != observation.ContentHash && current.ObservedSequence <= closingWorldSequence)
            {
                throw new ArgumentException("Carried knowledge is never forgotten and changes only by a later observation.");
            }
        }
        foreach (MarketPriceObservation observation in successor.Knowledge)
        {
            if (Carried.FindObservation(observation.ObserverActorId, observation.MarketId) is null
                && observation.ObservedSequence <= closingWorldSequence)
            {
                throw new ArgumentException("New knowledge must follow its closing boundary.");
            }
        }
        // A carried showing stays exact until its seller withdraws it or replaces it with a later showing.
        foreach (MarketLotDisclosure disclosure in successor.Disclosures)
        {
            MarketLotDisclosure? carried = Carried.FindDisclosure(disclosure.SellerActorId, disclosure.MarketId);
            if (carried?.ContentHash != disclosure.ContentHash && disclosure.DisclosedSequence <= closingWorldSequence)
            {
                throw new ArgumentException("A disclosure changes after its boundary only through a later showing.");
            }
        }
    }
}
