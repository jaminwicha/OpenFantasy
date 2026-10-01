using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Tests.Content.Trading;

/// <summary>Native configuration through actual session cutoffs: timing, earned funding and the finite shared supply.</summary>
public sealed class NativeMarketSessionTests
{
    [Fact]
    public async Task A_paused_session_neither_answers_nor_ages_a_request_and_resuming_answers_without_deadlock()
    {
        await using var world = new NativeMarketTestWorld(ready: false);
        ulong steps = world.State.AdvancingSteps;
        LocalTradeInquiryView asked = await world.Client.AskAsync(new LocalTradeInquiry(world.Stall.MarketId,
            world.Stall.Commodity, LocalTradeDirection.Buy, 2));
        Assert.Equal(LocalTradeInquiryState.Waiting, asked.State);
        Assert.Equal(steps + NativeTradingPolicy.InquiryLifetimeSteps, asked.ExpiresAtStep);
        for (int tick = 0; tick < 3; tick++) await world.Tick();
        Assert.Equal(LocalTradeInquiryState.Waiting, world.Session.ReadTradeInquiry()!.State);
        Assert.Equal(steps, world.State.AdvancingSteps);
        Assert.Empty(world.View.PaidQuotes);

        // Readiness, not panel visibility, lets the merchant answer: the very next advancing cutoff does.
        world.Session.SetReady(true);
        await world.Tick();
        LocalTradeInquiryView answered = world.Session.ReadTradeInquiry()!;
        Assert.Equal(LocalTradeInquiryState.Offered, answered.State);
        PaidQuoteView offer = Assert.Single(world.View.PaidQuotes);
        Assert.Equal(answered.QuoteId, offer.QuoteId);
        Assert.Equal(SpotQuoteSide.IssuerSells, offer.Side);
        Assert.Equal(NativeTradingPolicy.OfferWindowSteps, offer.ExpiresAtStep - offer.IssuedAtStep);
        Assert.Equal(world.Stall.Policy.Ask(6, 2), offer.UnitPrice);
        Assert.NotNull(offer.ObservedIssuerRevision);

        // A reading pause holds the offer's window exactly.
        world.Session.SetReady(false);
        ulong reading = world.State.AdvancingSteps;
        for (int tick = 0; tick < 3; tick++) await world.Tick();
        Assert.Equal(reading, world.State.AdvancingSteps);
        Assert.Equal(PaidQuoteStatus.Open, Assert.Single(world.View.PaidQuotes).Status);
    }

    [Fact]
    public async Task Only_the_earned_reward_funds_a_purchase_and_an_unfunded_confirmation_is_refused()
    {
        await using var world = new NativeMarketTestWorld();
        Assert.Equal(LocalTradeInquiryState.Offered, (await world.Ask(LocalTradeDirection.Buy, 2)).State);
        PaidQuoteView offer = Assert.Single(world.View.PaidQuotes);
        var draft = new FarmMarketDraft();
        draft.Begin(world.View, world.Stall.MarketId, LocalTradeDirection.Buy);
        Assert.True(draft.SetQuantity(2, world.StallView.MaximumUnitsPerOffer));
        Assert.True(draft.CaptureReview(world.View, world.StallView, offer));
        FarmActionOffer unfunded = world.Offers(draft)[FarmSemanticAction.ConfirmTrade];
        Assert.False(unfunded.CanExecute);
        Assert.Contains("You need", unfunded.BlockingReason);
        // The world independently refuses the same exact acceptance; no balance was injected to make it pass.
        EntityId forced = NativeMarketTestWorld.NewId();
        WorldActionReceipt refused = await world.Submit(world.PlayerCommand(
            offer.PrepareAcceptance(forced, world.PlayerDelivery, 2), forced));
        Assert.NotEqual(WorldActionRejection.None, refused.Rejection);
        Assert.Equal((0UL, 0UL, 6UL), (world.Balance(world.Player), world.Berries(world.Player), world.Berries(world.Wren)));

        await world.EarnReward();
        ulong reward = world.State.Quests!.Definitions.Single().Reward.CurrencyAmount;
        Assert.Equal(reward, world.Balance(world.Player));
        ulong wrenBefore = world.Balance(world.Wren);
        PaidQuoteView current = world.View.PaidQuotes.Single(value => value.QuoteId == offer.QuoteId);
        Assert.True(draft.CaptureReview(world.View, world.StallView, current));
        FarmActionOffer confirm = world.Offers(draft)[FarmSemanticAction.ConfirmTrade];
        Assert.True(confirm.CanExecute, confirm.BlockingReason);
        var accept = Assert.IsType<AcceptSpotQuoteIntent>(confirm.Intent);
        Assert.Equal(confirm.CommandId, accept.Acceptance.AcceptanceId);
        Assert.Equal((offer.QuoteId, offer.QuoteHash, 2UL, offer.UnitPrice * 2),
            (accept.Acceptance.QuoteId, accept.Acceptance.QuoteHash, accept.Acceptance.Quantity, accept.Acceptance.Payment));
        Assert.Throws<InvalidOperationException>(() => confirm.ToCommand(NativeMarketTestWorld.NewId()));
        Assert.Equal(WorldActionRejection.None, (await world.Submit(confirm)).Rejection);
        Assert.Equal(reward - offer.TotalFor(2), world.Balance(world.Player));
        Assert.Equal(wrenBefore + offer.TotalFor(2), world.Balance(world.Wren));
        Assert.Equal((2UL, 4UL), (world.Berries(world.Player), world.Berries(world.Wren)));
        // The filled offer can never be confirmed again from the same review.
        FarmActionOffer repeated = world.Offers(draft)[FarmSemanticAction.ConfirmTrade];
        Assert.False(repeated.CanExecute);
        Assert.Contains("filled", repeated.BlockingReason);
    }

    [Fact]
    public async Task Wrens_berries_are_one_finite_supply_for_trade_and_medicine()
    {
        await using var world = new NativeMarketTestWorld();
        WorldSnapshot state = world.State;
        CraftRecipeDefinition recipe = state.Production!.Recipes.Single(value => value.PhysicalRecipe.Key == CareJourneyAdmission.RecipeKey);
        var input = Assert.Single(recipe.Inputs);
        EntityId bag = state.GetResident(world.Wren).InventoryCustodianId;
        var lot = state.Inventory.Custody.Lots.Single(value => value.CustodianId == bag && value.Definition == recipe.ItemFor(input));
        TrustedCommandDelivery wren = NativeMarketTestWorld.DeliveryOf(state, world.Wren);
        var craft = new WorldActionCommand(NativeMarketTestWorld.NewId(), wren.PrincipalId, wren.SessionId, world.Wren,
            state.WorldId, state.BranchId, state.Space.GetActor(world.Wren).Revision,
            new CraftIntent(new(recipe.DefinitionId, recipe.Version, state.Production.Revision, bag,
                state.Inventory.Custody.GetStorage(bag).Revision, [new(IngredientLotKind.Source, lot.LotId, lot.Revision, input.Count)])));
        // Wren's own ordinary craft, published by another authority caller, consumes her actual berries.
        WorldStepDecision crafted = world.Authority.Commit(new WorldStepBatch(NativeMarketTestWorld.NewId(), state.WorldId,
            state.BranchId, state.Revision, state.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [craft], state.ProfileVersion));
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(crafted.Receipts).Status);
        ulong left = world.Stall.CountUsableStock(world.State.Inventory, world.State.Production!.Lots);
        Assert.Equal(CareJourneyAdmission.InitialBerryQuantity - input.Count, left);

        Assert.Equal(LocalTradeInquiryState.Declined, (await world.Ask(LocalTradeDirection.Buy, left + 1)).State);
        Assert.Empty(world.View.PaidQuotes);
        LocalTradeInquiryView offered = await world.Ask(LocalTradeDirection.Buy, left);
        Assert.Equal(LocalTradeInquiryState.Offered, offered.State);
        PaidQuoteView offer = Assert.Single(world.View.PaidQuotes);
        Assert.Equal(world.Stall.Policy.Ask(left, left), offer.UnitPrice);
        // The decline and the offer state terms only: no stock count or merchant balance is projected.
        Assert.DoesNotContain(typeof(LocalMarketStallView).GetProperties(), property => property.Name.Contains("Stock")
            || property.Name.Contains("Balance") || property.Name.Contains("Target") || property.Name.Contains("Principal")
            || property.Name.Contains("Session") || property.Name.Contains("Account"));
        string exposed = System.Text.Json.JsonSerializer.Serialize(world.View);
        Assert.DoesNotContain(wren.PrincipalId.ToString(), exposed);
        Assert.DoesNotContain(wren.SessionId.ToString(), exposed);
    }

    [Fact]
    public async Task A_stale_offer_closes_by_its_world_window_and_nothing_requotes_or_confirms_it()
    {
        await using var world = new NativeMarketTestWorld();
        Assert.Equal(LocalTradeInquiryState.Offered, (await world.Ask(LocalTradeDirection.Buy, 1)).State);
        PaidQuoteView offer = Assert.Single(world.View.PaidQuotes);
        var draft = new FarmMarketDraft();
        draft.Begin(world.View, world.Stall.MarketId, LocalTradeDirection.Buy);
        Assert.True(draft.CaptureReview(world.View, world.StallView, offer));
        // Before the window closes, the same request is refused by the one-standing-offer rule, never requoted.
        Assert.Contains("standing offer", MarketActionOffers.PrepareRequest(world.View, world.StallView, draft,
            world.Session.ReadTradeInquiry()).BlockingReason);
        while (world.State.AdvancingSteps < offer.ExpiresAtStep) await world.Tick();
        PaidQuoteView closed = Assert.Single(world.View.PaidQuotes);
        Assert.Equal(PaidQuoteStatus.Expired, closed.Status);
        FarmActionOffer confirm = world.Offers(draft)[FarmSemanticAction.ConfirmTrade];
        Assert.False(confirm.CanExecute);
        Assert.Contains("expired", confirm.BlockingReason);
        Assert.Equal(LocalTradeInquiryState.Offered, world.Session.ReadTradeInquiry()!.State);
        Assert.Equal(offer.QuoteId, world.Session.ReadTradeInquiry()!.QuoteId);
        Assert.Equal(0UL, world.Berries(world.Player));
    }
}
