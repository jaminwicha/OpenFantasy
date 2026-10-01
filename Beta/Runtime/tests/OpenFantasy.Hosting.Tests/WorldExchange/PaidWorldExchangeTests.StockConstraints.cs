using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldExchange;

/// <summary>
/// Constrained stock and combined recipient capacity through an actually admitted fresh genesis and the real
/// SQLite owner. Genuinely protected or reservation-held lots stay unsellable with their metadata intact while
/// an equal ordinary interval settles; a small human pack filled by earned raw and crafted purchases refuses
/// one more unit until a real sale frees space. Each refusal publishes only ordinary time, creates no child
/// and retries exactly, before and after reopening.
/// </summary>
public sealed partial class PaidWorldExchangeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Protected_or_reserved_stock_cannot_settle_keeps_its_metadata_and_is_withdrawn_while_equal_stock_sells(
        bool reserved)
    {
        var fixture = new PaidTradeWorldFixture();
        var journey = fixture.Journey;
        using var files = new StorageFiles();
        SqliteWorldAuthority authority = fixture.Create(files);
        try
        {
            var state = EarnReward(journey, new CareWorldSession(authority));
            EntityId player = fixture.Player, helper = fixture.Helper;
            CountedSourceLot held = state.Inventory.Custody.GetLot(reserved ? fixture.ReservedLot : fixture.ProtectedLot);
            CountedSourceLot ordinary = state.Inventory.Custody.GetLot(fixture.OrdinaryLot);
            // Genuine admitted metadata on one fresh source; the ordinary interval differs only in it.
            Assert.Equal(reserved ? LotProtection.None : LotProtection.Favorite, held.Protection);
            Assert.Equal(reserved ? fixture.Reservation : (EntityId?)null, held.ReservedBy);
            Assert.Contains(fixture.Reservation, state.Inventory.ReservationIds);
            Assert.Equal((LotProtection.None, (EntityId?)null), (ordinary.Protection, ordinary.ReservedBy));
            Assert.Equal((ordinary.Source.SourceId, ordinary.Definition, ordinary.CustodianId, ordinary.Quantity),
                (held.Source.SourceId, held.Definition, held.CustodianId, held.Quantity));

            // Issuance pins exact current stock and reserves nothing, so both quotes open at market revision zero.
            var (heldIssue, heldQuote) = FeelessQuote(state, helper, player, held, issuerBuys: false);
            state = Submit(authority, heldIssue, WorldActionRejection.None);
            var (ordinaryIssue, ordinaryQuote) = FeelessQuote(state, helper, player, ordinary, issuerBuys: false);
            state = Submit(authority, ordinaryIssue, WorldActionRejection.None);
            WorldSnapshot before = state;
            WorldActionCommand refused = AcceptCommand(before, player, heldQuote, helper, 1, 1);
            WorldActionCommand contrast = AcceptCommand(before, player, ordinaryQuote, helper, 1, 1);
            foreach (SpotQuote quote in new[] { heldQuote, ordinaryQuote })
            {
                AssertOtherwiseEligible(before, quote);
                AssertRoomFor(before, quote);
            }

            // Under the same world semantics from the same root, only the stock metadata separates them.
            WorldActionRejection Alone(WorldActionCommand command) => Assert.Single(new WorldStepProcessor().Decide(before,
                journey.Batch(before, WorldBatchKind.AdvancingStep, [command]), authority.IsIdentityOccupiedForHost).Receipts).Rejection;
            Assert.Equal(WorldActionRejection.Unavailable, Alone(refused));
            Assert.Equal(WorldActionRejection.None, Alone(contrast));
            Assert.Equal(SpotExchangeRejection.ProtectedStock, ComponentReason(authority, before, heldIssue, heldQuote, refused));
            Assert.Equal(SpotExchangeRejection.None, ComponentReason(authority, before, ordinaryIssue, ordinaryQuote, contrast));
            Assert.Equal(before.ContentHash, authority.ReadStateForHost().ContentHash);

            WorldSnapshot passive = new WorldStepProcessor().Decide(before, journey.Batch(before, WorldBatchKind.AdvancingStep),
                authority.IsIdentityOccupiedForHost).Candidate;
            WorldActionReceipt receipt = authority.SubmitPaidExchangeCommand(refused, Delivery(refused), NextId());
            Assert.Equal(WorldActionRejection.Unavailable, receipt.Rejection);
            state = authority.ReadStateForHost();
            AssertOnlyTimeAdvanced(before, state, passive);
            AssertHeld(state, held);
            EntityId unused = WorldPaidQuote.SplitChildOf(refused.CommandId);
            Assert.DoesNotContain(state.Inventory.Custody.Lots, value => value.LotId == unused);
            Assert.False(authority.IsIdentityOccupiedForHost(unused));
            Assert.True(authority.IsIdentityOccupiedForHost(refused.CommandId));
            Assert.Equal(heldQuote.ContentHash, state.Coordination!.PaidExchange!.FindOpen(heldQuote.QuoteId)!.Quote.ContentHash);
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt),
                WorldReceiptCodecV1.Encode(authority.SubmitPaidExchangeCommand(refused, Delivery(refused), NextId())));
            Assert.Equal(state.ContentHash, authority.ReadStateForHost().ContentHash);

            // The issuer withdraws the unwanted quote: nothing moves or pays, and the metadata remains.
            before = state;
            WorldActionCommand withdraw = WithdrawCommand(state, helper, heldQuote.QuoteId);
            WorldActionReceipt withdrawn = authority.SubmitPaidExchangeCommand(withdraw, Delivery(withdraw), NextId());
            Assert.Equal(WorldActionRejection.None, withdrawn.Rejection);
            state = authority.ReadStateForHost();
            WorldPaidExchangeBook book = state.Coordination!.PaidExchange!;
            WorldPaidQuoteOutcome closed = book.FindOutcome(heldQuote.QuoteId)!;
            Assert.Equal((WorldPaidQuoteOutcomeKind.Withdrawn, heldQuote.ContentHash, withdraw.CommandId),
                (closed.Kind, closed.Quote.Quote.ContentHash, closed.Withdrawal!.CommandId));
            Assert.Equal(before.Inventory.ContentHash, state.Inventory.ContentHash);
            Assert.Equal(before.Production!.ContentHash, state.Production!.ContentHash);
            Assert.Equal(before.Progression!.ContentHash, state.Progression!.ContentHash);
            Assert.Empty(book.IssuerRevisions);
            Assert.Equal(ordinaryQuote.ContentHash, book.FindOpen(ordinaryQuote.QuoteId)!.Quote.ContentHash);
            AssertHeld(state, held);

            // The equal ordinary interval settles the same fixed terms and splits a real child to the player.
            CurrencyLedger ledger = state.Progression!.Currency;
            WorldActionCommand settle = AcceptCommand(state, player, ordinaryQuote, helper, 1, 1);
            state = Submit(authority, settle, WorldActionRejection.None);
            CountedSourceLot child = state.Inventory.Custody.GetLot(WorldPaidQuote.SplitChildOf(settle.CommandId));
            Assert.Equal((state.GetResident(player).InventoryCustodianId, 1UL, ordinary.Source.SourceId),
                (child.CustodianId, child.Quantity, child.Source.SourceId));
            Assert.Equal(WorldPaidQuoteOutcomeKind.Filled, state.Coordination!.PaidExchange!.FindOutcome(ordinaryQuote.QuoteId)!.Kind);
            Assert.Equal(1UL, state.Coordination.PaidExchange.GetIssuerRevision(helper));
            CurrencyLedger after = state.Progression!.Currency;
            Assert.Equal((ledger.Revision + 1, ledger.AdmittedSupply), (after.Revision, after.AdmittedSupply));
            Assert.Equal(ledger.GetActorAccount(player).Balance - 1, after.GetActorAccount(player).Balance);
            Assert.Equal(ledger.GetActorAccount(helper).Balance + 1, after.GetActorAccount(helper).Balance);
            AssertHeld(state, held);
            Assert.Contains(fixture.Reservation, state.Inventory.ReservationIds);

            ContentHash head = state.ContentHash;
            authority.Dispose();
            authority = fixture.Reopen(files);
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt),
                WorldReceiptCodecV1.Encode(authority.SubmitPaidExchangeCommand(refused, Delivery(refused), NextId())));
            Assert.Equal(WorldReceiptCodecV1.Encode(withdrawn),
                WorldReceiptCodecV1.Encode(authority.SubmitPaidExchangeCommand(withdraw, Delivery(withdraw), NextId())));
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);
            Assert.False(authority.IsIdentityOccupiedForHost(unused));
            Assert.True(authority.IsIdentityOccupiedForHost(child.LotId));
            AssertHeld(authority.ReadStateForHost(), held);
        }
        finally { authority.Dispose(); }
    }

    [Fact]
    public void A_small_human_pack_filled_by_raw_and_crafted_purchases_refuses_one_more_unit_until_a_sale_frees_space()
    {
        var fixture = new PaidTradeWorldFixture(merchantCapacity: 2);
        var journey = fixture.Journey;
        using var files = new StorageFiles();
        SqliteWorldAuthority authority = fixture.Create(files);
        try
        {
            var session = new CareWorldSession(authority);
            var state = EarnReward(journey, session);
            EntityId player = fixture.Player, helper = fixture.Helper, merchant = fixture.Merchant;
            EntityId pack = state.GetResident(merchant).InventoryCustodianId;
            // Fresh small human storage whose admitted capacity covers its admitted (empty) contents.
            Assert.Equal(2UL, state.Inventory.Custody.GetStorage(pack).UnitCapacity);
            Assert.Equal((0UL, 0UL), (RawUnits(state, pack), ProducedUnits(state, pack)));

            // Earned quest funds buy three berries and pay their actual fee into the merchant's empty wallet.
            EntityId wallet = state.Progression!.Currency.GetActorAccount(merchant).AccountId;
            Assert.Equal(0UL, state.Progression.Currency.GetAccount(wallet).Balance);
            CountedSourceLot berries = UnconstrainedLot(state, helper, fixture.OrdinaryLot);
            var (funding, fundingQuote) = IssueCommand(state, helper, player, berries, 1, wallet, 3);
            state = Submit(authority, funding, WorldActionRejection.None);
            WorldActionCommand bought = AcceptCommand(state, player, fundingQuote, helper, 3, 3);
            state = Submit(authority, bought, WorldActionRejection.None);
            Assert.Equal(3UL, state.Progression!.Currency.GetAccount(wallet).Balance);
            CountedSourceLot playerStock = state.Inventory.Custody.GetLot(WorldPaidQuote.SplitChildOf(bought.CommandId));
            Assert.Equal((state.GetResident(player).InventoryCustodianId, 3UL), (playerStock.CustodianId, playerStock.Quantity));

            // Wren crafts a real potion from the remaining berries, then sells the merchant one raw unit and the potion.
            state = Commit(session, state, CraftPotion(journey, state, helper, state.Inventory.Custody.GetLot(berries.LotId)),
                WorldActionRejection.None);
            TransformedLot potion = Assert.Single(state.Production!.Lots);
            var (rawSale, rawQuote) = FeelessQuote(state, helper, merchant, state.Inventory.Custody.GetLot(fixture.OrdinaryLot),
                issuerBuys: false);
            state = Submit(authority, rawSale, WorldActionRejection.None);
            state = Submit(authority, AcceptCommand(state, merchant, rawQuote, helper, 1, 1), WorldActionRejection.None);
            var (potionSale, potionQuote) = ProducedSale(state, helper, merchant, ProducedLot(state, potion.LotId));
            state = Submit(authority, potionSale, WorldActionRejection.None);
            state = Submit(authority, AcceptCommand(state, merchant, potionQuote, helper, 1, 1), WorldActionRejection.None);
            Assert.Equal(pack, ProducedLot(state, potion.LotId).CustodianId);
            Assert.Equal((1UL, 1UL), (RawUnits(state, pack), ProducedUnits(state, pack)));

            // The player, who has never filled a quote, offers one more unit; every other condition holds and
            // raw contents alone would still admit it. Only the crafted unit in the same pack exhausts room.
            var (offer, quote) = FeelessQuote(state, player, merchant, playerStock, issuerBuys: false);
            state = Submit(authority, offer, WorldActionRejection.None);
            WorldSnapshot before = state;
            WorldActionCommand refused = AcceptCommand(before, merchant, quote, player, 1, 1);
            AssertOtherwiseEligible(before, quote);
            ulong capacity = before.Inventory.Custody.GetStorage(pack).UnitCapacity;
            Assert.True(RawUnits(before, pack) + 1 <= capacity, "Raw contents alone would still admit the unit.");
            Assert.True(RawUnits(before, pack) + ProducedUnits(before, pack) + 1 > capacity);
            Assert.Equal(SpotExchangeRejection.InsufficientCapacity, ComponentReason(authority, before, offer, quote, refused));

            WorldSnapshot passive = new WorldStepProcessor().Decide(before, journey.Batch(before, WorldBatchKind.AdvancingStep),
                authority.IsIdentityOccupiedForHost).Candidate;
            WorldActionReceipt receipt = authority.SubmitPaidExchangeCommand(refused, Delivery(refused), NextId());
            Assert.Equal(WorldActionRejection.Unavailable, receipt.Rejection);
            state = authority.ReadStateForHost();
            AssertOnlyTimeAdvanced(before, state, passive);
            EntityId unused = WorldPaidQuote.SplitChildOf(refused.CommandId);
            Assert.DoesNotContain(state.Inventory.Custody.Lots, value => value.LotId == unused);
            Assert.False(authority.IsIdentityOccupiedForHost(unused));
            Assert.True(authority.IsIdentityOccupiedForHost(refused.CommandId));
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt),
                WorldReceiptCodecV1.Encode(authority.SubmitPaidExchangeCommand(refused, Delivery(refused), NextId())));
            Assert.Equal(state.ContentHash, authority.ReadStateForHost().ContentHash);

            // An actual resale of the crafted potion frees one unit; the unchanged open quote then settles.
            var (resale, resaleQuote) = ProducedSale(state, merchant, helper, ProducedLot(state, potion.LotId));
            state = Submit(authority, resale, WorldActionRejection.None);
            state = Submit(authority, AcceptCommand(state, helper, resaleQuote, merchant, 1, 1), WorldActionRejection.None);
            Assert.Equal((1UL, 0UL), (RawUnits(state, pack), ProducedUnits(state, pack)));
            Assert.Equal(quote.ContentHash, state.Coordination!.PaidExchange!.FindOpen(quote.QuoteId)!.Quote.ContentHash);
            WorldActionCommand accepted = AcceptCommand(state, merchant, quote, player, 1, 1);
            state = Submit(authority, accepted, WorldActionRejection.None);
            Assert.Equal((capacity, 0UL), (RawUnits(state, pack), ProducedUnits(state, pack)));
            Assert.Equal(pack, state.Inventory.Custody.GetLot(WorldPaidQuote.SplitChildOf(accepted.CommandId)).CustodianId);
            Assert.Equal(1UL, state.Coordination!.PaidExchange!.GetIssuerRevision(player));
            Assert.False(authority.IsIdentityOccupiedForHost(unused));

            ContentHash head = state.ContentHash;
            authority.Dispose();
            authority = fixture.Reopen(files);
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt),
                WorldReceiptCodecV1.Encode(authority.SubmitPaidExchangeCommand(refused, Delivery(refused), NextId())));
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);
            Assert.False(authority.IsIdentityOccupiedForHost(unused));
        }
        finally { authority.Dispose(); }
    }

    /// <summary>
    /// Every acceptance condition except recipient room: the counterparty's own distinct control, ready living
    /// hands for both people, reciprocal reach, owned storage and accounts, funds, the issuer's current market
    /// revision and an exact divisible stock pin, so a fill would split a fresh child.
    /// </summary>
    private static void AssertOtherwiseEligible(WorldSnapshot state, SpotQuote quote)
    {
        EntityId issuer = quote.IssuerActorId, counterparty = quote.CounterpartyActorId;
        ActorControlBinding control = state.Inventory.Controls.Single(value => value.ActorId == counterparty);
        Assert.DoesNotContain(state.Inventory.Controls, value => value.ActorId != counterparty
            && (value.PrincipalId == control.PrincipalId || value.SessionId == control.SessionId));
        foreach (EntityId actor in new[] { issuer, counterparty })
        {
            Assert.True(WorldActorAvailability.CanAct(state, actor));
            Assert.True(state.Space.GetActor(actor).Condition.SupportsFunction(BodyFunction.Manipulation));
            Assert.True(state.Progression!.GetActor(actor).CurrentHp > 0);
        }
        Assert.Equal(SpatialInteraction.Reachable, state.Space.QueryInteraction(issuer, counterparty));
        Assert.Equal(SpatialInteraction.Reachable, state.Space.QueryInteraction(counterparty, issuer));
        CurrencyLedger ledger = state.Progression!.Currency;
        foreach ((EntityId owner, EntityId storage, EntityId account) in new[]
        {
            (quote.SellerActorId, quote.SellerCustodianId, quote.SellerAccountId),
            (quote.BuyerActorId, quote.BuyerCustodianId, quote.BuyerAccountId)
        })
        {
            Assert.Equal(owner, state.Inventory.Policies.Single(value => value.CustodianId == storage).OwnerActorId);
            Assert.Equal((EntityId?)owner, ledger.GetAccount(account).ActorId);
        }
        Assert.True(ledger.GetAccount(quote.BuyerAccountId).Balance >= quote.PaymentFor(1) + quote.FeesFor(1));
        Assert.Equal(quote.MarketRevision, state.Coordination!.PaidExchange!.GetIssuerRevision(issuer));
        CountedSourceLot lot = state.Inventory.Custody.GetLot(quote.LotId);
        Assert.Equal((quote.SellerCustodianId, quote.Definition, quote.ExpectedLotRevision),
            (lot.CustodianId, lot.Definition, lot.Revision));
        Assert.True(lot.Quantity > 1, "A one-unit fill must split a fresh child identity.");
    }

    /// <summary>The recipient pack's raw plus produced contents leave room for one more unit.</summary>
    private static void AssertRoomFor(WorldSnapshot state, SpotQuote quote) =>
        Assert.True(RawUnits(state, quote.BuyerCustodianId) + ProducedUnits(state, quote.BuyerCustodianId) + 1
            <= state.Inventory.Custody.GetStorage(quote.BuyerCustodianId).UnitCapacity);

    /// <summary>
    /// The public spot component's own reason for one acceptance, from a book holding only its quote at market
    /// revision zero. The component authorizes nothing in the world and its candidate is never published.
    /// </summary>
    private static SpotExchangeRejection ComponentReason(SqliteWorldAuthority authority, WorldSnapshot state,
        WorldActionCommand issue, SpotQuote quote, WorldActionCommand accept)
    {
        Assert.Equal(0UL, quote.MarketRevision);
        var book = new SpotQuoteBook(state.WorldId, state.BranchId, 1, [new SpotQuoteEntry(quote, Delivery(issue))], []);
        return new SpotExchangeAuthority().Accept(state.Inventory, state.Production!, state.Progression!, book,
            ((AcceptSpotQuoteIntent)accept.Intent).Acceptance, Delivery(accept), state.AdvancingSteps,
            authority.IsIdentityOccupiedForHost).Rejection;
    }

    /// <summary>A refused step changes no owner; only ordinary time and passive liquid progression advance.</summary>
    private static void AssertOnlyTimeAdvanced(WorldSnapshot before, WorldSnapshot after, WorldSnapshot passive)
    {
        AssertOwnersUnchanged(before, after);
        Assert.Equal(before.Coordination!.Sources.ContentHash, after.Coordination!.Sources.ContentHash);
        Assert.Equal(before.Coordination.Anchor?.ContentHash, after.Coordination.Anchor?.ContentHash);
        Assert.Equal(before.Coordination.ConsentedMaterial!.ContentHash, after.Coordination.ConsentedMaterial!.ContentHash);
        Assert.Equal(before.Coordination.Proposals!.ContentHash, after.Coordination.Proposals!.ContentHash);
        Assert.Equal(before.ResourceHistory!.ContentHash, after.ResourceHistory!.ContentHash);
        Assert.Equal(passive.Reservoirs!.ContentHash, after.Reservoirs!.ContentHash);
        Assert.Equal(passive.LiquidCells!.ContentHash, after.LiquidCells!.ContentHash);
        Assert.Equal((passive.Calendar, passive.AdvancingSteps), (after.Calendar, after.AdvancingSteps));
    }

    /// <summary>The constrained lot keeps its exact interval, revision, owner and protection or reservation.</summary>
    private static void AssertHeld(WorldSnapshot state, CountedSourceLot held)
    {
        CountedSourceLot current = state.Inventory.Custody.GetLot(held.LotId);
        Assert.Equal((held.CustodianId, held.Source, held.Revision, held.Protection, held.ReservedBy),
            (current.CustodianId, current.Source, current.Revision, current.Protection, current.ReservedBy));
    }

    private static CountedSourceLot UnconstrainedLot(WorldSnapshot state, EntityId owner, EntityId except)
    {
        EntityId storage = state.GetResident(owner).InventoryCustodianId;
        return state.Inventory.Custody.Lots.First(value => value.CustodianId == storage && value.Quantity > 1
            && value.ReservedBy is null && value.Protection == LotProtection.None && value.LotId != except);
    }

    private static TransformedLot ProducedLot(WorldSnapshot state, EntityId lotId) =>
        state.Production!.Lots.Single(value => value.LotId == lotId);

    private static ulong RawUnits(WorldSnapshot state, EntityId storage) => state.Inventory.Custody.Lots
        .Where(value => value.CustodianId == storage).Aggregate(0UL, (total, value) => checked(total + value.Quantity));

    private static ulong ProducedUnits(WorldSnapshot state, EntityId storage) => state.Production!.Lots
        .Where(value => value.CustodianId == storage).Aggregate(0UL, (total, value) => checked(total + value.Quantity));

    private static WorldActionCommand CraftPotion(CareRewardJourney journey, WorldSnapshot state, EntityId crafter,
        CountedSourceLot berries)
    {
        var recipe = state.Production!.Recipes.Single(value => value.PhysicalRecipe.Key == CareJourneyAdmission.RecipeKey);
        var input = Assert.Single(recipe.Inputs);
        Assert.Equal(recipe.ItemFor(input), berries.Definition);
        EntityId storage = state.GetResident(crafter).InventoryCustodianId;
        return journey.Command(state, crafter, new CraftIntent(new(recipe.DefinitionId, recipe.Version,
            state.Production.Revision, storage, state.Inventory.Custody.GetStorage(storage).Revision,
            [new(IngredientLotKind.Source, berries.LotId, berries.Revision, input.Count)])));
    }

    /// <summary>A feeless one-unit sale of a whole produced lot by any issuer to any counterparty.</summary>
    private (WorldActionCommand Command, SpotQuote Quote) ProducedSale(WorldSnapshot state, EntityId issuer,
        EntityId buyer, TransformedLot lot)
    {
        var control = state.Inventory.Controls.Single(value => value.ActorId == issuer);
        CurrencyLedger ledger = state.Progression!.Currency;
        EntityId commandId = NextId();
        ulong step = state.AdvancingSteps + 1;
        var quote = new SpotQuote(WorldPaidQuote.DeriveIdentity(commandId), state.WorldId, state.BranchId, issuer,
            control.PrincipalId, control.SessionId, buyer, SpotQuoteSide.IssuerSells, SpotLotKind.Produced, lot.LotId,
            lot.Definition, lot.Revision, lot.CustodianId, state.GetResident(buyer).InventoryCustodianId,
            ledger.Currency.DefinitionId, ledger.Currency.Version, ledger.GetActorAccount(issuer).AccountId,
            ledger.GetActorAccount(buyer).AccountId, 1, [], 1,
            state.Coordination!.PaidExchange!.GetIssuerRevision(issuer), step, step + 1024);
        return (new(commandId, control.PrincipalId, control.SessionId, issuer, state.WorldId, state.BranchId,
            state.Space.GetActor(issuer).Revision, new IssueSpotQuoteIntent(quote)), quote);
    }
}
