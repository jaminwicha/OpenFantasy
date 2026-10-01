using OpenFantasy.Client;
using OpenFantasy.Client.Cargo;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.NativeCargo;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;

namespace OpenFantasy.Hosting.Tests.Client.Cargo;

/// <summary>
/// Stall identity is the canonical market identity. A same-merchant, same-goods twin stall, a bilateral quote and a
/// stale or unknown choice are the counterexamples that the old issuer-plus-commodity association could not separate.
/// </summary>
public sealed class CargoStallIdentityTests
{
    [Fact]
    public void A_quote_belongs_only_to_the_stall_its_canonical_market_record_names()
    {
        WorldSnapshot genesis = CargoTestWorld.Genesis;
        EntityId player = CargoTestWorld.Player(genesis);
        MarketBinding marla = CargoTestWorld.Markets(genesis).Origin;
        SpotQuote quote = MarlaSellOffer(genesis, marla, player);
        // A second stall of the same merchant for the same goods, over another store: issuer and commodity agree.
        var twin = new MarketBinding(CargoTestWorld.NewId(), marla.MerchantActorId, CargoTestWorld.NewId(), marla.AccountId,
            marla.Commodity, marla.HomePlaceId, marla.Policy);
        var realStall = new LocalMarketStallView(genesis, marla, player);
        var twinStall = new LocalMarketStallView(genesis, twin, player);
        var real = new PaidQuoteView(quote, player, PaidQuoteStatus.Open, null, null, 1, marla.MarketId);
        var twins = new PaidQuoteView(quote, player, PaidQuoteStatus.Open, null, null, 1, twin.MarketId);
        var bilateral = new PaidQuoteView(quote, player, PaidQuoteStatus.Open, null, null, 1);
        Assert.Equal((real.IssuerActorId, real.Definition), (twins.IssuerActorId, twins.Definition));
        Assert.True(MarketActionOffers.BelongsTo(realStall, real));
        Assert.False(MarketActionOffers.BelongsTo(twinStall, real));
        Assert.True(MarketActionOffers.BelongsTo(twinStall, twins));
        Assert.False(MarketActionOffers.BelongsTo(realStall, twins));
        Assert.False(MarketActionOffers.BelongsTo(realStall, bilateral));
        Assert.False(MarketActionOffers.BelongsTo(twinStall, bilateral));

        // A review captures only an offer of the addressed stall; the twin's identical terms are refused.
        var view = new LocalWorldView(genesis, player);
        var draft = new FarmMarketDraft();
        draft.Begin(view, marla.MarketId, LocalTradeDirection.Buy);
        Assert.False(draft.CaptureReview(view, realStall, twins));
        Assert.False(draft.CaptureReview(view, realStall, bilateral));
        Assert.True(draft.CaptureReview(view, realStall, real));
    }

    [Fact]
    public void An_explicit_stall_choice_is_kept_by_identity_and_never_retargeted()
    {
        WorldSnapshot genesis = CargoTestWorld.Genesis;
        CargoCircuitMarkets markets = CargoTestWorld.Markets(genesis);
        var view = new LocalWorldView(genesis, CargoTestWorld.Player(genesis));
        Assert.Equal(2, view.Markets!.Stalls.Count);
        Assert.All(view.Markets.Stalls, stall => Assert.False(stall.IsAvailableInPerson));
        Assert.Equal(markets.Destination.MarketId, MarketActionOffers.ContextStall(view, markets.Destination.MarketId)!.MarketId);
        Assert.Equal(markets.Origin.MarketId, MarketActionOffers.ContextStall(view, markets.Origin.MarketId)!.MarketId);
        // A choice that names no stall in this world addresses nothing; it never falls back to another stall.
        Assert.Null(MarketActionOffers.ContextStall(view, CargoTestWorld.NewId()));
        Assert.Equal(view.Markets.Stalls[0].MarketId, MarketActionOffers.ContextStall(view, null)!.MarketId);
        // Wheel offers name the explicitly chosen stall even while it is out of reach.
        var draft = new FarmMarketDraft();
        draft.Begin(view, markets.Destination.MarketId, null);
        var offers = new Dictionary<FarmSemanticAction, FarmActionOffer>();
        ActorControlBinding control = genesis.Inventory.Controls.Single(value => value.ActorId == view.PlayerActorId);
        MarketActionOffers.Add(view, FarmActionScope.From(new TrustedCommandDelivery(control.PrincipalId, control.SessionId), view),
            draft, offers);
        Assert.Equal(markets.Destination.MerchantActorId, offers[FarmSemanticAction.TradeBuy].TargetId);
        Assert.False(offers[FarmSemanticAction.TradeBuy].CanExecute);
    }

    [Fact]
    public void A_pointer_pick_maps_back_through_the_projection_to_the_one_stall_drawn_there()
    {
        WorldSnapshot genesis = CargoTestWorld.Genesis;
        MarketBinding marla = CargoTestWorld.Markets(genesis).Origin;
        var view = new LocalWorldView(genesis, CargoTestWorld.Player(genesis));
        var projection = new CargoViewProjection(LocalRegionObserver.Project(genesis, marla.MerchantActorId)!);
        SubunitPosition feet = genesis.Space.GetActor(marla.MerchantActorId).Feet;
        CargoCanvasPoint drawn = projection.Project(feet);
        Assert.Equal(marla.MarketId, CargoStallPick.Pick(projection, view, drawn));
        Assert.Equal(marla.MarketId, CargoStallPick.Pick(projection, view, new CargoCanvasPoint(drawn.X + 30, drawn.Y)));
        Assert.Null(CargoStallPick.Pick(projection, view, new CargoCanvasPoint(drawn.X + 96, drawn.Y)));
        Assert.Null(CargoStallPick.Pick(projection, view, new CargoCanvasPoint(-1_000_000, -1_000_000)));
    }

    [Fact]
    public void Merchants_compose_their_own_source_appearances_in_the_original_layer_order()
    {
        IReadOnlyList<NativeCargoStall> stalls = NativeCargoWorld.Stalls(CargoTestWorld.Genesis);
        NativeCargoStall marla = stalls.Single(stall => stall.MerchantSourceId == CargoCircuitSources.OriginMerchantId);
        NativeCargoStall kegga = stalls.Single(stall => stall.MerchantSourceId == CargoCircuitSources.DestinationMerchantId);
        Assert.Equal(new NativeCargoAppearance(1, 4, 0, 3, 4, 10, 0, 2), marla.Appearance);
        Assert.Equal(new NativeCargoAppearance(2, 3, 8, 12, 2, 7, 15, 3), kegga.Appearance);
        Assert.Equal(("Brasshollow", "Gearford"), (marla.PlaceName, kegga.PlaceName));
        Assert.Equal(new CargoPaperdollStep[]
        {
            new("body", "skins", 4), new("outfit_04_a", "outfitColors", 10), new("outfit_04_b", "outfitColors", 0),
            new("hair_00", "hairColors", 3), new("race_01", "skins", 4), new("acc_02", "outfitColors", 0)
        }, CargoPaperdollPlan.Steps(marla.Appearance));
        // A dwarf's race layer (the beard) takes the hair ramp, as in the predecessor compositor.
        Assert.Equal(new CargoPaperdollStep[]
        {
            new("body", "skins", 3), new("outfit_02_a", "outfitColors", 7), new("outfit_02_b", "outfitColors", 15),
            new("hair_08", "hairColors", 12), new("race_02", "hairColors", 12), new("acc_03", "outfitColors", 15)
        }, CargoPaperdollPlan.Steps(kegga.Appearance));
        // Wren's retained farm appearance is neither merchant's: no merchant is a Wren clone.
        var wren = new NativeCargoAppearance(0, 5, 5, 13, 8, 10, 10, 3);
        Assert.DoesNotContain(wren, stalls.Select(stall => stall.Appearance));
    }

    /// <summary>Marla's own controller prepares a rule-priced two-unit offer from her actual shelf lot.</summary>
    private static SpotQuote MarlaSellOffer(WorldSnapshot genesis, MarketBinding marla, EntityId player)
    {
        ActorControlBinding control = genesis.Inventory.Controls.Single(value => value.ActorId == marla.MerchantActorId);
        var delivery = new TrustedCommandDelivery(control.PrincipalId, control.SessionId);
        CountedSourceLot shelf = genesis.Inventory.Custody.Lots.Single(lot => lot.CustodianId == marla.CustodianId
            && lot.Definition.Equals(marla.Commodity));
        WorldActionCommand command = MarketTradingService.PrepareOffer(genesis, delivery, marla.MarketId, player,
            SpotQuoteSide.IssuerSells, shelf.LotId, 2, 100, CargoTestWorld.NewId())!;
        return ((IssueMarketQuoteIntent)command.Intent).Quote;
    }
}
