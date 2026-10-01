using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Observations;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.WorldExchange.Markets;

/// <summary>
/// The integer market rule as a pure contract: source-price anchoring, monotone scarcity, declared spread,
/// demand bounds, checked arithmetic at the representable extremes and the absence of any closed
/// buy/resell profit at unchanged other state, together with the canonical market owner format.
/// </summary>
public sealed class MarketPricePolicyTests
{
    private static readonly ContentHash SourceDigest = ContentHash.Compute(new byte[] { 1, 2, 3 });

    [Fact]
    public void Mid_price_equals_the_cited_source_at_the_stocking_target_and_never_rises_with_stock()
    {
        MarketPricePolicy policy = BerryStall();
        Assert.Equal(5UL, policy.MidPrice(policy.StockingTarget));
        ulong previous = ulong.MaxValue;
        for (ulong stock = 0; stock <= 40; stock++)
        {
            ulong mid = policy.MidPrice(stock);
            Assert.InRange(mid, policy.MinimumUnitPrice, policy.MaximumUnitPriceBound);
            Assert.True(mid <= previous, "Scarcity can only raise the price as stock falls.");
            previous = mid;
        }
        // Extreme stock counts saturate at the floor without overflow.
        Assert.Equal(policy.MinimumUnitPrice, policy.MidPrice(ulong.MaxValue));
        Assert.Equal(15UL, policy.MidPrice(0));
    }

    [Fact]
    public void Ask_and_bid_respect_stock_demand_lot_limits_and_the_declared_spread()
    {
        MarketPricePolicy policy = BerryStall();
        Assert.Null(policy.Ask(0, 1)); // Zero stock posts no ask.
        Assert.Null(policy.Ask(3, 4)); // An allowance beyond usable stock cannot be quoted.
        Assert.Null(policy.Ask(10, 5)); // The rule's lot limit bounds every fixed offer.
        Assert.Null(policy.Ask(10, 0));
        Assert.Null(policy.Bid(16, 1)); // At the holding limit demand is exhausted.
        Assert.Null(policy.Bid(15, 2));
        Assert.Null(policy.Bid(ulong.MaxValue, 1));
        Assert.Equal(16UL, policy.Ask(1, 1));
        Assert.Equal(13UL, policy.Ask(2, 1));
        Assert.Equal(11UL, policy.Bid(0, 1));
        for (ulong stock = 0; stock <= 16; stock++)
        {
            MarketPostedTerms posted = new(policy.Ask(stock, 1), policy.Bid(stock, 1), policy.UnitFee);
            if (posted.AskUnitPrice is { } ask && posted.BidUnitPrice is { } bid)
            {
                Assert.True(bid < ask, "A posted bid stays below the posted ask.");
            }
        }
        // A larger allowance is priced at the state it would produce, never more cheaply per unit.
        Assert.True(policy.Ask(6, 4) >= policy.Ask(6, 1));
        Assert.True(policy.Bid(6, 4) <= policy.Bid(6, 1));
    }

    /// <summary>
    /// Every closed sequence of buys and resales of the same number of units, in any split, at unchanged
    /// other state, costs the cycling actor at least the spread per unit, with or without a market due.
    /// </summary>
    [Theory]
    [InlineData(0UL)]
    [InlineData(2UL)]
    public void No_split_buy_and_resell_cycle_creates_money_at_unchanged_state(ulong unitFee)
    {
        MarketPricePolicy policy = BerryStall(unitFee);
        ulong[][] splits = [[1], [2], [3], [4], [1, 1], [1, 2], [2, 1], [1, 1, 1], [2, 2], [1, 3], [3, 1], [1, 1, 1, 1]];
        for (ulong start = 0; start <= 16; start++)
        {
            foreach (ulong[] buys in splits)
            {
                foreach (ulong[] sells in splits)
                {
                    if (buys.Sum(value => (long)value) != sells.Sum(value => (long)value))
                    {
                        continue;
                    }
                    (bool complete, long net) = Cycle(policy, start, buys, sells);
                    if (complete)
                    {
                        long units = buys.Sum(value => (long)value);
                        long spread = (long)(policy.AskMarkup + policy.BidMarkdown);
                        Assert.True(net <= -units * spread, $"Cycle from {start} must lose at least the spread per unit; net {net}.");
                    }
                }
            }
        }
    }

    /// <summary>
    /// The reviewed counterexample: a constant mid price of five, one unit of markup and markdown and a
    /// three-unit due to a third actor. Pricing the merchant's bid without its own due posted ask 6 and bid 4:
    /// buying and reselling one unit left the trader -5, the payee +6 and the merchant -1, so the trader and
    /// payee together gained one unit while currency stayed conserved. The repaired rule charges the due
    /// inside the merchant's all-in bid.
    /// </summary>
    [Fact]
    public void A_market_due_paid_to_a_third_actor_cannot_fund_a_cooperating_buy_and_resell()
    {
        MarketPricePolicy policy = Constant(fee: 3);
        Assert.Equal(6UL, policy.Ask(4, 1));
        // Reproduction of the earlier bid, mid minus markdown, with the due paid on top by the merchant.
        ulong earlierBid = policy.MidPrice(4) - policy.BidMarkdown;
        Assert.Equal(4UL, earlierBid);
        long earlierTrader = -(long)(6 + 3) + (long)earlierBid, earlierPayee = 3 + 3, earlierMerchant = 6 - (long)earlierBid - 3;
        Assert.Equal((-5L, 6L, -1L), (earlierTrader, earlierPayee, earlierMerchant));
        Assert.Equal(1L, earlierTrader + earlierPayee);

        // The repaired bid: the merchant's all-in cost is the mid less its markdown, and the seller receives that less the due.
        Assert.Equal(4UL, policy.AllInBid(4, 1));
        Assert.Equal(1UL, policy.Bid(4, 1));
        (long trader, long payee, long merchant) = CycleBalances(policy, 5, [1], [1]);
        Assert.Equal((-8L, 6L, 2L), (trader, payee, merchant));
        Assert.Equal(-(long)(policy.AskMarkup + policy.BidMarkdown), trader + payee);
        // A due as large as the all-in bid leaves no positive seller price, so the market simply posts no bid.
        Assert.Null(Constant(fee: 4).Bid(4, 1));
        Assert.Equal(6UL, Constant(fee: 4).Ask(4, 1));
    }

    /// <summary>
    /// Every closed sequence of buys and resales at unchanged other state, in any split and any interleaving,
    /// pays the merchant at least its declared spread per unit, so every coalition of traders and the due's
    /// payee loses at least that much. Currency is conserved, so the merchant's gain is their loss.
    /// </summary>
    [Theory]
    [InlineData(0UL, false)]
    [InlineData(2UL, false)]
    [InlineData(3UL, false)]
    [InlineData(0UL, true)]
    [InlineData(3UL, true)]
    public void No_split_or_order_of_a_closed_cycle_profits_traders_together_with_the_payee(ulong unitFee, bool constant)
    {
        MarketPricePolicy policy = constant ? Constant(unitFee) : BerryStall(unitFee);
        long spread = (long)(policy.AskMarkup + policy.BidMarkdown);
        int checkedCycles = 0;
        foreach (long[] sequence in ClosedSequences(4, 4))
        {
            for (ulong start = 0; start <= 16; start++)
            {
                if (Interleaved(policy, start, sequence) is not { } result)
                {
                    continue;
                }
                checkedCycles++;
                Assert.True(result.Merchant >= spread * result.Units,
                    $"From stock {start}, [{string.Join(',', sequence)}] gave the merchant {result.Merchant} for {result.Units} units.");
                Assert.Equal(0L, result.Merchant + result.Traders + result.Payee);
            }
        }
        Assert.True(checkedCycles > 100, "The grid must exercise many real closed cycles.");
    }

    [Fact]
    public void Stock_beyond_the_rules_admitted_domain_posts_no_terms_instead_of_an_unrecordable_basis()
    {
        MarketPricePolicy policy = BerryStall();
        Assert.NotNull(policy.Ask(MarketPricePolicy.MaximumStock, 1));
        Assert.Null(policy.Ask(MarketPricePolicy.MaximumStock + 1, 1));
        Assert.Null(policy.Ask(ulong.MaxValue, MarketPricePolicy.MaximumLotLimit));
        Assert.Null(policy.Bid(MarketPricePolicy.MaximumStock + 1, 1));
        // The quote record cannot hold such a basis, so the world must refuse before reaching it.
        Assert.Throws<ArgumentException>(() => new MarketQuoteRecord(Id(50), Id(10), policy.Version, MarketPricePolicy.MaximumStock + 1));
    }

    [Fact]
    public void A_lot_disclosure_authorizes_only_its_merchants_exact_bounded_buy_quote_and_has_canonical_bytes()
    {
        MarketBinding stall = Binding(Id(10), Id(1), Id(21), Id(31));
        var disclosure = new MarketLotDisclosure(Id(3), stall.MarketId, stall.MerchantActorId, SpotLotKind.CountedSource,
            Id(80), stall.Commodity, 4, 2, Id(23), Id(33), 9, 6, 20, Id(41), 0);
        SpotQuote quote = BuyQuote(stall, disclosure, allowance: 2, revision: 4, expires: 20);
        Assert.True(disclosure.Authorizes(quote, 6));
        Assert.True(disclosure.Authorizes(quote, 19));
        Assert.False(disclosure.Authorizes(quote, 20)); // Expired.
        Assert.False(disclosure.Authorizes(BuyQuote(stall, disclosure, allowance: 3, revision: 4, expires: 20), 6));
        Assert.False(disclosure.Authorizes(BuyQuote(stall, disclosure, allowance: 1, revision: 5, expires: 20), 6));
        Assert.False(disclosure.Authorizes(BuyQuote(stall, disclosure, allowance: 1, revision: 4, expires: 21), 6));
        Assert.False(disclosure.Authorizes(BuyQuote(stall, disclosure, allowance: 1, revision: 4, expires: 20, issuer: Id(2)), 6));

        var book = new WorldMarketBook([stall], [], [], [disclosure]);
        byte[] bytes = WorldMarketCodec.Encode(book);
        Assert.Equal(bytes, WorldMarketCodec.Encode(WorldMarketCodec.Decode(bytes)));
        Assert.Equal(disclosure.ContentHash, WorldMarketCodec.Decode(bytes).FindDisclosure(Id(3), stall.MarketId)!.ContentHash);
        Assert.Throws<FormatException>(() => WorldMarketCodec.Decode([.. bytes, 0]));
        // One showing per seller and market, in order, naming that market's own merchant and commodity.
        Assert.Throws<ArgumentException>(() => new WorldMarketBook([stall], [], [], [disclosure, disclosure]));
        Assert.Throws<ArgumentException>(() => new WorldMarketBook([stall], [], [], [new MarketLotDisclosure(Id(3),
            stall.MarketId, Id(2), SpotLotKind.CountedSource, Id(80), stall.Commodity, 4, 2, Id(23), Id(33), 9, 6, 20, Id(41), 0)]));
        // A seller cannot show goods to itself, and a showing has a forward window.
        Assert.Throws<ArgumentException>(() => new MarketLotDisclosure(Id(1), stall.MarketId, Id(1), SpotLotKind.CountedSource,
            Id(80), stall.Commodity, 4, 2, Id(23), Id(33), 9, 6, 20, Id(41), 0));
        Assert.Throws<ArgumentException>(() => new MarketLotDisclosure(Id(3), stall.MarketId, stall.MerchantActorId,
            SpotLotKind.CountedSource, Id(80), stall.Commodity, 4, 2, Id(23), Id(33), 9, 6, 6, Id(41), 0));
    }

    [Fact]
    public void Disclosure_commands_have_exact_canonical_action_frames()
    {
        MarketBinding stall = Binding(Id(10), Id(1), Id(21), Id(31));
        foreach (WorldActionIntent intent in new WorldActionIntent[]
        {
            new DiscloseMarketLotIntent(stall.MarketId, SpotLotKind.Produced, Id(80), stall.Commodity, 3, 2, Id(33), 40),
            new WithdrawMarketLotDisclosureIntent(stall.MarketId)
        })
        {
            var command = new WorldActionCommand(Id(90), Id(91), Id(92), Id(3), Id(93), Id(94), 7, intent);
            byte[] bytes = WorldActionCodec.Encode(command);
            Assert.Equal(bytes, WorldActionCodec.Encode(WorldActionCodec.Decode(bytes)));
            Assert.Equal(intent, WorldActionCodec.Decode(bytes).Intent);
            Assert.ThrowsAny<FormatException>(() => WorldActionCodec.Decode([.. bytes, 0]));
        }
    }

    [Fact]
    public void Policy_construction_refuses_unbounded_or_inconsistent_rules()
    {
        var source = new MarketSourcePrice("berries", 5, SourceDigest);
        Assert.Throws<ArgumentException>(() => Policy(source, target: 0));
        Assert.Throws<ArgumentException>(() => Policy(source, holding: 7));
        Assert.Throws<ArgumentException>(() => Policy(source, minimum: 6));
        Assert.Throws<ArgumentException>(() => Policy(source, maximum: 4));
        Assert.Throws<ArgumentException>(() => Policy(source, maximum: MarketPricePolicy.MaximumUnitPrice + 1));
        Assert.Throws<ArgumentException>(() => Policy(source, markup: 0, markdown: 0));
        Assert.Throws<ArgumentException>(() => Policy(source, fee: 1));
        Assert.Throws<ArgumentException>(() => Policy(source, lot: MarketPricePolicy.MaximumLotLimit + 1));
        Assert.Throws<ArgumentException>(() => Policy(source, lifetime: MarketPricePolicy.MaximumQuoteLifetimeSteps + 1));
        Assert.Throws<ArgumentException>(() => new MarketSourcePrice("", 5, SourceDigest));
        Assert.Throws<ArgumentException>(() => new MarketSourcePrice("rations", 0, SourceDigest));
        Assert.Throws<ArgumentException>(() => new MarketSourcePrice("ra tions", 5, SourceDigest));

        // The largest admitted rule still prices its whole allowance exactly within checked arithmetic.
        var largest = new MarketPricePolicy(new MarketSourcePrice("bulk", MarketPricePolicy.MaximumUnitPrice, SourceDigest),
            MarketPricePolicy.MaximumStock, MarketPricePolicy.MaximumStock, MarketPricePolicy.MaximumStock,
            1, MarketPricePolicy.MaximumUnitPrice, MarketPricePolicy.MaximumUnitPrice, MarketPricePolicy.MaximumUnitPrice,
            MarketPricePolicy.MaximumUnitPrice, Id(90), MarketPricePolicy.MaximumLotLimit, 1);
        ulong ask = largest.Ask(MarketPricePolicy.MaximumStock, MarketPricePolicy.MaximumLotLimit)!.Value;
        Assert.Equal(checked(largest.MidPrice(MarketPricePolicy.MaximumStock - MarketPricePolicy.MaximumLotLimit)
            + MarketPricePolicy.MaximumUnitPrice), ask);
        _ = checked((ask + largest.UnitFee) * MarketPricePolicy.MaximumLotLimit);
    }

    [Fact]
    public void Pricing_is_a_pure_function_of_its_rule_and_stock()
    {
        MarketPricePolicy first = BerryStall(), second = BerryStall();
        Assert.Equal(first.Version, second.Version);
        ulong?[] forward = Enumerable.Range(0, 17).Select(value => first.Ask((ulong)value, 1)).ToArray();
        ulong?[] reversed = Enumerable.Range(0, 17).Reverse().Select(value => second.Ask((ulong)value, 1)).Reverse().ToArray();
        Assert.Equal(forward, reversed);
        // Any parameter change is a different versioned rule.
        Assert.NotEqual(first.Version, BerryStall(1).Version);
        Assert.NotEqual(first.Version, Policy(new MarketSourcePrice("berries", 5, SourceDigest), markup: 2).Version);
    }

    [Fact]
    public void Market_owner_bytes_are_canonical_and_refuse_permutation_and_trailing_data()
    {
        MarketBinding stall = Binding(Id(10), Id(1), Id(21), Id(31)), counter = Binding(Id(11), Id(2), Id(22), Id(32));
        var observation = new MarketPriceObservation(Id(3), stall.MarketId, stall.MerchantActorId, stall.Commodity,
            stall.Policy.Version, 13, 9, 0, 7, 5, Id(40), 0);
        var record = new MarketQuoteRecord(Id(50), stall.MarketId, stall.Policy.Version, 2);
        var book = new WorldMarketBook([stall, counter], [record], [observation]);
        byte[] bytes = WorldMarketCodec.Encode(book);
        Assert.Equal(bytes, WorldMarketCodec.Encode(WorldMarketCodec.Decode(bytes)));
        Assert.Throws<FormatException>(() => WorldMarketCodec.Decode([.. bytes, 0]));
        Assert.Throws<ArgumentException>(() => new WorldMarketBook([counter, stall], [], []));
        Assert.Throws<ArgumentException>(() => new WorldMarketBook([stall], [new MarketQuoteRecord(Id(50), Id(99),
            stall.Policy.Version, 2)], []));
        // Knowledge pins the market's actual merchant, commodity and rule version.
        Assert.Throws<ArgumentException>(() => new WorldMarketBook([stall], [], [new MarketPriceObservation(Id(3),
            stall.MarketId, Id(4), stall.Commodity, stall.Policy.Version, 13, 9, 0, 7, 5, Id(40), 0)]));
        Assert.Throws<ArgumentException>(() => new MarketPriceObservation(Id(3), stall.MarketId, stall.MerchantActorId,
            stall.Commodity, stall.Policy.Version, 9, 9, 0, 7, 5, Id(40), 0));
        Assert.Throws<ArgumentException>(() => new MarketPriceObservation(Id(1), stall.MarketId, stall.MerchantActorId,
            stall.Commodity, stall.Policy.Version, 13, 9, 0, 7, 5, Id(40), 0));
        Assert.Equal(2UL, observation.AgeAt(7));
        Assert.Equal(0UL, observation.AgeAt(1));
    }

    private static (bool Complete, long Net) Cycle(MarketPricePolicy policy, ulong stock, ulong[] buys, ulong[] sells)
    {
        long net = 0;
        foreach (ulong quantity in buys)
        {
            if (policy.Ask(stock, quantity) is not { } ask)
            {
                return (false, 0);
            }
            net -= (long)((ask + policy.UnitFee) * quantity);
            stock -= quantity;
        }
        foreach (ulong quantity in sells)
        {
            if (policy.Bid(stock, quantity) is not { } bid)
            {
                return (false, 0);
            }
            // The merchant pays its own market due as buyer; the cycling seller receives only the bid.
            net += (long)(bid * quantity);
            stock += quantity;
        }
        return (true, net);
    }

    /// <summary>The balance changes of one cycling trader, the due's payee and the merchant over one closed cycle.</summary>
    private static (long Trader, long Payee, long Merchant) CycleBalances(MarketPricePolicy policy, ulong stock, ulong[] buys, ulong[] sells)
    {
        long trader = 0, payee = 0, merchant = 0;
        foreach (ulong quantity in buys)
        {
            ulong ask = policy.Ask(stock, quantity)!.Value;
            trader -= (long)((ask + policy.UnitFee) * quantity);
            merchant += (long)(ask * quantity);
            payee += (long)(policy.UnitFee * quantity);
            stock -= quantity;
        }
        foreach (ulong quantity in sells)
        {
            ulong bid = policy.Bid(stock, quantity)!.Value;
            trader += (long)(bid * quantity);
            merchant -= (long)((bid + policy.UnitFee) * quantity);
            payee += (long)(policy.UnitFee * quantity);
            stock += quantity;
        }
        return (trader, payee, merchant);
    }

    /// <summary>Every sequence of nonzero signed fills (negative buys from the merchant) of bounded length that nets to zero.</summary>
    private static IEnumerable<long[]> ClosedSequences(int maximumLength, long maximumQuantity)
    {
        IEnumerable<long[]> Extend(long[] prefix)
        {
            if (prefix.Length > 0 && prefix.Sum() == 0)
            {
                yield return prefix;
            }
            if (prefix.Length == maximumLength)
            {
                yield break;
            }
            for (long quantity = -maximumQuantity; quantity <= maximumQuantity; quantity++)
            {
                if (quantity == 0)
                {
                    continue;
                }
                foreach (long[] longer in Extend([.. prefix, quantity]))
                {
                    yield return longer;
                }
            }
        }
        return Extend([]);
    }

    /// <summary>
    /// Applies one interleaved sequence of fills at their posted terms: a negative entry is a purchase from the
    /// merchant at its ask, a positive one a sale to it at its bid. Null when the rule posts no such quote.
    /// </summary>
    private static (long Merchant, long Traders, long Payee, long Units)? Interleaved(MarketPricePolicy policy, ulong stock, long[] fills)
    {
        long merchant = 0, traders = 0, payee = 0, units = 0;
        foreach (long fill in fills)
        {
            ulong quantity = (ulong)Math.Abs(fill);
            ulong? price = fill < 0 ? policy.Ask(stock, quantity) : policy.Bid(stock, quantity);
            if (price is not { } unit)
            {
                return null;
            }
            long payment = (long)(unit * quantity), due = (long)(policy.UnitFee * quantity);
            if (fill < 0)
            {
                traders -= payment + due;
                merchant += payment;
                units += (long)quantity;
                stock -= quantity;
            }
            else
            {
                merchant -= payment + due;
                traders += payment;
                stock += quantity;
            }
            payee += due;
        }
        return (merchant, traders, payee, units);
    }

    private static MarketPricePolicy Constant(ulong fee) =>
        new(new MarketSourcePrice("berries", 5, SourceDigest), 8, 16, 4, 5, 5, 1, 1, fee, fee == 0 ? null : Id(90), 4, 64);

    private static SpotQuote BuyQuote(MarketBinding market, MarketLotDisclosure shown, ulong allowance, ulong revision,
        ulong expires, EntityId? issuer = null) =>
        new(Id(95), Id(93), Id(94), issuer ?? market.MerchantActorId, Id(96), Id(97), shown.SellerActorId,
            SpotQuoteSide.IssuerBuys, shown.LotKind, shown.LotId, shown.Definition, revision, shown.SellerCustodianId,
            market.CustodianId, Id(98), ContentHash.Compute(new byte[] { 9 }), shown.SellerAccountId, market.AccountId, 3, [],
            allowance, 0, 7, expires);

    private static MarketPricePolicy BerryStall(ulong unitFee = 0) =>
        Policy(new MarketSourcePrice("berries", 5, SourceDigest), fee: unitFee, payee: unitFee == 0 ? null : Id(90));

    private static MarketPricePolicy Policy(MarketSourcePrice source, ulong target = 8, ulong holding = 16,
        ulong minimum = 1, ulong maximum = 20, ulong markup = 1, ulong markdown = 1, ulong fee = 0, EntityId? payee = null,
        ulong lot = 4, ulong lifetime = 64) =>
        new(source, target, holding, 4, minimum, maximum, markup, markdown, fee, payee, lot, lifetime);

    private static MarketBinding Binding(EntityId market, EntityId merchant, EntityId storage, EntityId account) =>
        new(market, merchant, storage, account, new CountedItemDefinition(Id(60), ContentHash.Compute(new byte[] { 6 })),
            Id(70), BerryStall());

    private static EntityId Id(ulong value) => new(0x3A4B_0000, value);
}
