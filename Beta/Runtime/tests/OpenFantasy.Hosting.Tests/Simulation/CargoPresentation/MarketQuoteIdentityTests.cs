using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.WorldExchange.Scheduled;

namespace OpenFantasy.Hosting.Tests.Simulation.CargoPresentation;

/// <summary>
/// The participant quote view names its market from the canonical market quote record: live from the current book,
/// and after a real archive cut from the recorded root that published the outcome. The record's stock basis stays
/// private, and a non-participant learns nothing.
/// </summary>
public sealed class MarketQuoteIdentityTests
{
    [Fact]
    public async Task Live_and_archived_quotes_name_their_market_from_the_canonical_record()
    {
        using var world = new ScheduledTradingWorld();
        world.WalkWrenToWork();
        world.WalkBesideWork(world.Player);
        Assert.Equal(SpatialInteraction.Reachable, world.State.Space.QueryInteraction(world.Player, world.Wren));
        MarketBinding market = world.Fixture.WrenMarket;
        var time = new ManualSessionTimeProvider();
        LocalWorldSession session = world.Open(time, ScheduledTradingWorld.Merchants(world.Wren));
        PaidQuoteView offer;
        try
        {
            session.SetReady(true);
            LocalTradeInquiryView answered = await ScheduledTradingWorld.Ask(session, time,
                new LocalTradeInquiry(market.MarketId, world.Berries, LocalTradeDirection.Buy, 1));
            Assert.Equal(LocalTradeInquiryState.Offered, answered.State);
            offer = Assert.Single(session.ReadView().PaidQuotes);
            // Live: the record of this exact quote in the current market book names the market.
            Assert.Equal(market.MarketId, world.State.Coordination!.Markets!.FindQuote(offer.QuoteId)!.MarketId);
            Assert.Equal(market.MarketId, offer.MarketId);
            Assert.Equal(WorldActionRejection.None,
                (await ScheduledTradingWorld.Submit(session, time, world.PlayerAcceptance(offer, 1))).Rejection);
            PaidQuoteView filled = Assert.Single(session.ReadView().PaidQuotes);
            Assert.Equal((PaidQuoteStatus.Filled, (EntityId?)market.MarketId), (filled.Status, filled.MarketId));
        }
        finally
        {
            session.SetReady(false);
            await session.StopAsync();
        }

        // A real archive cut removes the outcome and its record from the live segment.
        world.Reopen();
        world.CutArchive();
        Assert.Null(world.State.Coordination!.PaidExchange!.FindOutcome(offer.QuoteId));
        Assert.Null(world.State.Coordination.Markets!.FindQuote(offer.QuoteId));
        ContentHash head = world.State.ContentHash;
        world.Reopen();
        var reopenedTime = new ManualSessionTimeProvider();
        LocalWorldSession reopened = world.Open(reopenedTime, null);
        try
        {
            Assert.DoesNotContain(reopened.ReadView().PaidQuotes, value => value.QuoteId == offer.QuoteId);
            PaidQuoteView archived = (await reopened.InspectPaidQuoteAsync(offer.QuoteId))!;
            Assert.Equal((PaidQuoteStatus.Filled, offer.QuoteHash, (EntityId?)market.MarketId),
                (archived.Status, archived.QuoteHash, archived.MarketId));
            Assert.Null(archived.ObservedIssuerRevision);
            Assert.Null(await reopened.InspectPaidQuoteAsync(new EntityId(0x4341_5247, 7)));
            Assert.Equal(head, world.State.ContentHash);
        }
        finally { await reopened.StopAsync(); }

        // The issuer reads the same archived market identity; a foreign delivery is refused before anything is read.
        world.Reopen();
        PaidQuoteView merchantView = world.Service.FindRecordedQuote(offer.QuoteId, world.Wren, world.DeliveryOf(world.Wren))!;
        Assert.Equal((true, (EntityId?)market.MarketId), (merchantView.ViewerIsIssuer, merchantView.MarketId));
        Assert.Throws<AuthorityDispatchException>(() => world.Service.FindRecordedQuote(offer.QuoteId, world.Wren,
            world.DeliveryOf(world.Player)));
        Assert.Equal(head, world.State.ContentHash);
    }

    [Fact]
    public void The_participant_view_exposes_the_market_but_never_its_pricing_basis()
    {
        string[] names = typeof(PaidQuoteView).GetProperties().Select(property => property.Name).ToArray();
        Assert.Contains("MarketId", names);
        Assert.DoesNotContain("StockBasis", names);
        Assert.DoesNotContain("PolicyVersion", names);
        Assert.DoesNotContain("IssuerPrincipalId", names);
        Assert.DoesNotContain("IssuerSessionId", names);
    }
}
