using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.WorldExchange.Scheduled;

namespace OpenFantasy.Hosting.Tests.Networking.PlayerQueries;

public sealed class PlayerTradeQueryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inquiry_yields_real_merchant_terms_and_acceptance_remains_a_separate_durable_action(bool sockets)
    {
        using var world = new ScheduledTradingWorld();
        world.WalkWrenToWork();
        world.WalkBesideWork(world.Player);
        var time = new ManualSessionTimeProvider();
        await using var host = world.Open(time, ScheduledTradingWorld.Merchants(world.Wren));
        await using var connection = await QuerySessionConnection.Open(host, world.Player, world.DeliveryOf(world.Player), sockets);
        var inquiry = new LocalTradeInquiry(world.Fixture.WrenMarket.MarketId, world.Berries, LocalTradeDirection.Buy, 1);
        var before = world.State;
        var waiting = await connection.Query(new(PlayerQueryKind.SubmitTradeInquiry, inquiry));
        Assert.Equal(LocalTradeInquiryState.Waiting, waiting.Inquiry!.State);
        Assert.Equal(before.ContentHash, world.State.ContentHash);
        Assert.Empty(connection.Channel.Player.ReadView().PaidQuotes);

        await connection.Send(PlayerGameplayCodec.Readiness(connection.NextId(), true));
        await connection.Ping();
        await SessionFixture.Tick(time);
        var answered = (await connection.Query(new(PlayerQueryKind.ReadTradeInquiry))).Inquiry!;
        Assert.Equal(LocalTradeInquiryState.Offered, answered.State);
        Assert.Contains(world.RecordedBatch(world.State.Revision).Actions,
            action => action.ActorId == world.Wren && action.Intent is IssueMarketQuoteIntent);
        var quote = (await connection.Query(new(PlayerQueryKind.InspectPaidQuote, subjectId: answered.QuoteId))).PaidQuote!;
        Assert.Equal((world.Wren, world.Player, PaidQuoteStatus.Open, world.Fixture.WrenMarket.MarketId),
            (quote.IssuerActorId, quote.CounterpartyActorId, quote.Status, quote.MarketId!.Value));
        Assert.Equal(inquiry.Commodity, quote.Definition);
        ScheduledTradingWorld.AssertCarriesNoDelivery(quote, world.DeliveryOf(world.Wren), world.DeliveryOf(world.Trader));

        var acceptance = world.PlayerAcceptance(quote, 1);
        ulong commandRequest = connection.NextId();
        await connection.Send(PlayerGameplayCodec.Command(commandRequest, acceptance));
        // The action awaits a real world cutoff; this query must still complete through the receive loop.
        Assert.Equal(answered, (await connection.Query(new(PlayerQueryKind.ReadTradeInquiry))).Inquiry);
        Assert.NotNull(world.State.Coordination!.PaidExchange!.FindOpen(quote.QuoteId));
        await SessionFixture.Tick(time);
        var message = await connection.Receive();
        Assert.Equal((PlayerMessageKind.Receipt, commandRequest), (message.Kind, message.RequestId));
        var receipt = WorldReceiptCodecV1.Decode(message.Payload);
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
        var filled = (await connection.Query(new(PlayerQueryKind.InspectPaidQuote, subjectId: quote.QuoteId))).PaidQuote!;
        Assert.Equal((PaidQuoteStatus.Filled, quote.QuoteHash, 1UL), (filled.Status, filled.QuoteHash, filled.FilledQuantity!.Value));
        Assert.Equal(world.State.Coordination.PaidExchange.FindOutcome(quote.QuoteId)!.WorldSequence, filled.ClosedAtSequence);
    }

    [Fact]
    public async Task Inquiry_cancel_conflict_and_reconnect_keep_ephemeral_state_separate_from_world_facts()
    {
        using var world = new ScheduledTradingWorld();
        var time = new ManualSessionTimeProvider();
        await using var host = world.Open(time, ScheduledTradingWorld.Merchants(world.Wren));
        var inquiry = new LocalTradeInquiry(world.Fixture.WrenMarket.MarketId, world.Berries, LocalTradeDirection.Buy, 1);
        var before = world.State;
        await using (var connection = await QuerySessionConnection.Open(host, world.Player, world.DeliveryOf(world.Player)))
        {
            Assert.Null((await connection.Query(new(PlayerQueryKind.ReadTradeInquiry))).Inquiry);
            Assert.Null((await connection.Query(new(PlayerQueryKind.CancelTradeInquiry))).Inquiry);
            var waiting = (await connection.Query(new(PlayerQueryKind.SubmitTradeInquiry, inquiry))).Inquiry;
            Assert.Equal(waiting, (await connection.Query(new(PlayerQueryKind.SubmitTradeInquiry, inquiry))).Inquiry);
            var conflicting = new LocalTradeInquiry(inquiry.MarketId, inquiry.Commodity, inquiry.Direction, 2);
            ulong request = connection.NextId();
            await connection.SendQuery(request, new(PlayerQueryKind.SubmitTradeInquiry, conflicting));
            var refusal = await connection.Receive();
            Assert.Equal(request, refusal.RequestId);
            Assert.Equal(PlayerRequestRefusal.NotAdmitted, PlayerGameplayCodec.ReadRefusal(refusal));
            var cancelled = (await connection.Query(new(PlayerQueryKind.CancelTradeInquiry))).Inquiry;
            Assert.Equal(LocalTradeInquiryState.Cancelled, cancelled!.State);
            Assert.Equal(cancelled, (await connection.Query(new(PlayerQueryKind.ReadTradeInquiry))).Inquiry);
            await connection.Query(new(PlayerQueryKind.SubmitTradeInquiry, inquiry));
        }
        await using var reopened = await QuerySessionConnection.Open(host, world.Player, world.DeliveryOf(world.Player), sockets: true);
        Assert.Null((await reopened.Query(new(PlayerQueryKind.ReadTradeInquiry))).Inquiry);
        Assert.Empty(reopened.Channel.Player.ReadView().PaidQuotes);
        Assert.Equal(before.ContentHash, world.State.ContentHash);
        Assert.Equal(LocalWorldRunState.Paused, reopened.Channel.Player.ReadRunState());
    }

    [Fact]
    public async Task Archived_paid_quote_is_exact_for_participant_and_absent_for_authenticated_outsider_over_sockets()
    {
        using var world = new ScheduledTradingWorld();
        var purchase = world.BuyOneFromWrenDirectly();
        world.Fixture.Gift(world.Authority, world.Wren, world.Trader, world.Fixture.Paid.OrdinaryLot, 1);
        world.WalkWrenToWork();
        world.WalkBesideWork(world.Trader);
        var time = new ManualSessionTimeProvider();
        EntityId foreignQuote;
        var trading = new LocalTradingConfiguration([new LocalMerchantPreference(world.Wren)],
            [new LocalShopperPreference(world.Trader, world.Fixture.WrenMarket.MarketId, desiredUnits: 0)]);
        await using (var preparation = world.Open(time, trading))
        {
            preparation.SetReady(true);
            await SessionFixture.Tick(time); // The shopper shows its own gifted stock.
            await SessionFixture.Tick(time); // Wren issues her actual bid to that shopper.
            foreignQuote = Assert.Single(world.State.Coordination!.PaidExchange!.Open,
                quote => quote.Quote.CounterpartyActorId == world.Trader).QuoteId;
            await SessionFixture.Tick(time); // The shopper accepts separately under its own controller.
            Assert.Equal(WorldPaidQuoteOutcomeKind.Filled,
                world.State.Coordination!.PaidExchange!.FindOutcome(foreignQuote)!.Kind);
        }
        world.Reopen();
        world.CutArchive();
        Assert.Null(world.State.Coordination!.PaidExchange!.FindOutcome(purchase.Quote.QuoteId));
        Assert.Null(world.State.Coordination.PaidExchange.FindOutcome(foreignQuote));
        world.Reopen();
        var before = world.State;
        var journal = world.Authority.InspectForHost().Journal;
        await using var host = world.Open(new ManualSessionTimeProvider(), null);
        await using var participant = await QuerySessionConnection.Open(host, world.Player, world.DeliveryOf(world.Player), sockets: true);
        var request = new PlayerQuery(PlayerQueryKind.InspectPaidQuote, subjectId: purchase.Quote.QuoteId);
        var found = (await participant.Query(request)).PaidQuote!;
        Assert.Equal((PaidQuoteStatus.Filled, purchase.Quote.ContentHash, world.Fixture.WrenMarket.MarketId),
            (found.Status, found.QuoteHash, found.MarketId!.Value));
        Assert.Null(found.ObservedIssuerRevision);
        // This real player is a participant in its purchase but an outsider to the two NPCs' sale.
        Assert.Null((await participant.Query(new(PlayerQueryKind.InspectPaidQuote, subjectId: foreignQuote))).PaidQuote);
        Assert.Null((await participant.Query(new(PlayerQueryKind.InspectPaidQuote, subjectId: new EntityId(0x5155455259, 999)))).PaidQuote);
        Assert.DoesNotContain(participant.Channel.Player.ReadView().PaidQuotes, quote => quote.QuoteId == found.QuoteId);
        Assert.Equal(before.ContentHash, world.State.ContentHash);
        Assert.Equal(journal, world.Authority.InspectForHost().Journal);
    }
}
