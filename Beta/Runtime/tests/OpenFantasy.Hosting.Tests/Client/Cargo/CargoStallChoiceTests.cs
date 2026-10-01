using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.NativeCargo;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Tests.Client.Cargo;

/// <summary>
/// Explicit stall choice: opening Offers while no stall is in reach only looks at a stall and never pins it, an explicit
/// choice is never downgraded or retargeted, and an out-of-reach pinned stall explains the Offers → Stall route. A world
/// with a single stall keeps choosing that stall on opening, as before.
/// </summary>
public sealed class CargoStallChoiceTests
{
    [Fact]
    public void Opening_offers_at_the_farm_looks_without_pinning_and_explicit_choices_are_never_retargeted()
    {
        WorldSnapshot genesis = CargoTestWorld.Genesis;
        EntityId player = CargoTestWorld.Player(genesis);
        CargoCircuitMarkets markets = CargoTestWorld.Markets(genesis);
        var view = new LocalWorldView(genesis, player);
        Assert.All(view.Markets!.Stalls, stall => Assert.False(stall.IsAvailableInPerson));
        LocalMarketStallView fallback = MarketActionOffers.ContextStall(view, null)!;
        Assert.False(MarketActionOffers.PinsOnOpening(view, fallback, null));
        var draft = new FarmMarketDraft();
        draft.Look(view, fallback.MarketId);
        Assert.Equal((fallback.MarketId, false, (EntityId?)null), (draft.MarketId!.Value, draft.IsExplicit, draft.ChosenMarketId));
        draft.ReleaseLook();
        Assert.Null(draft.MarketId);

        // Without a choice the wheel addresses no pinned stall; its blocker explains how to choose one.
        FarmActionOffers offers = Offers(genesis, player);
        FarmActionOffer unpinned = offers.Create(view, null, null, draft)[FarmSemanticAction.TradeBuy];
        Assert.False(unpinned.CanExecute);
        Assert.Contains("under Stall", unpinned.BlockingReason, StringComparison.Ordinal);

        // An explicit selector choice stays addressed: looking elsewhere and closing never downgrade or retarget it.
        LocalMarketStallView marla = view.Markets.Stalls.Single(stall => stall.MarketId == markets.Origin.MarketId);
        LocalMarketStallView kegga = view.Markets.Stalls.Single(stall => stall.MarketId == markets.Destination.MarketId);
        draft.Begin(view, marla.MarketId, null);
        draft.Look(view, kegga.MarketId);
        draft.ReleaseLook();
        Assert.Equal((marla.MarketId, true), (draft.ChosenMarketId!.Value, draft.IsExplicit));
        Assert.True(MarketActionOffers.PinsOnOpening(view, marla, draft.ChosenMarketId));
        Assert.Equal(marla.MarketId, MarketActionOffers.ContextStall(view, draft.ChosenMarketId)!.MarketId);
        FarmActionOffer pinned = offers.Create(view, null, null, draft)[FarmSemanticAction.TradeBuy];
        Assert.Equal(marla.MerchantActorId, pinned.TargetId);
        Assert.Contains("Walk to her stall, or open Offers and choose another under Stall", pinned.BlockingReason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_single_stall_world_still_chooses_its_only_stall_on_opening()
    {
        WorldSnapshot genesis = NativeTradingWorld.CreateEmbedded("cargo-single-stall", "Traveler");
        EntityId player = CargoTestWorld.Player(genesis);
        var view = new LocalWorldView(genesis, player);
        LocalMarketStallView only = Assert.Single(view.Markets!.Stalls);
        Assert.True(MarketActionOffers.PinsOnOpening(view, only, null));
        FarmActionOffer buy = Offers(genesis, player).Create(view, null, null, new FarmMarketDraft())[FarmSemanticAction.TradeBuy];
        if (!only.IsAvailableInPerson)
            Assert.Equal($"{FarmMarketTerms.Merchant(view, only)} must be within reach and able to trade in person.", buy.BlockingReason);
    }

    private static FarmActionOffers Offers(WorldSnapshot world, EntityId player)
    {
        ActorControlBinding control = world.Inventory.Controls.Single(value => value.ActorId == player);
        EntityId wren = world.Quests!.Definitions.Select(value => value.GiverActorId).Distinct().Single();
        return new FarmActionOffers(new TrustedCommandDelivery(control.PrincipalId, control.SessionId), wren,
            world.Quests.Definitions[0].Item.DefinitionId, null);
    }
}
