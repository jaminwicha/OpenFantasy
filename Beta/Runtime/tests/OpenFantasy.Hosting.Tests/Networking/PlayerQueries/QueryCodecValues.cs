using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldExchange.Scheduled;

namespace OpenFantasy.Hosting.Tests.Networking.PlayerQueries;

/// <summary>Immutable values captured after genuine paid and proposal commands in the retained admitted market world.</summary>
public sealed class QueryCodecValues
{
    public QueryCodecValues()
    {
        using var world = new ScheduledTradingWorld();
        var purchase = world.BuyOneFromWrenDirectly();
        var proposal = world.Propose(world.Wren, world.Player, world.Fixture.Paid.OrdinaryLot, 1);
        var view = new LocalWorldView(world.State, world.Player);
        WorldId = view.WorldId;
        BranchId = view.BranchId;
        ActorId = view.PlayerActorId;
        Paid = new(WorldId, BranchId, ActorId, PlayerQueryKind.InspectPaidQuote, purchase.Quote.QuoteId,
            paidQuote: view.PaidQuotes.Single(value => value.QuoteId == purchase.Quote.QuoteId));
        Proposal = new(WorldId, BranchId, ActorId, PlayerQueryKind.InspectMaterialProposal, proposal.ProposalId,
            materialProposal: view.MaterialProposals!.Offers.Single(value => value.ProposalId == proposal.ProposalId));
        Inquiry = new(world.Fixture.WrenMarket.MarketId, world.Berries, LocalTradeDirection.Buy, 1);
        Delivery = world.DeliveryOf(world.Player);
        var wren = world.DeliveryOf(world.Wren);
        var issue = world.Service.PrepareOffer(wren, world.Fixture.WrenMarket.MarketId, world.Player,
            SpotQuoteSide.IssuerSells, world.Fixture.Paid.OrdinaryLot, 1, 32, world.Fixture.NextId())!;
        Assert.Equal(WorldActionRejection.None, world.Service.Submit(issue, wren, world.Fixture.NextId()).Rejection);
        var quoteId = ((IssueMarketQuoteIntent)issue.Intent).Quote.QuoteId;
        OpenPaid = new(WorldId, BranchId, ActorId, PlayerQueryKind.InspectPaidQuote, quoteId,
            paidQuote: new LocalWorldView(world.State, world.Player).PaidQuotes.Single(value => value.QuoteId == quoteId));
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public EntityId ActorId { get; }
    public PlayerQueryResult Paid { get; }
    public PlayerQueryResult OpenPaid { get; }
    public PlayerQueryResult Proposal { get; }
    public LocalTradeInquiry Inquiry { get; }
    public TrustedCommandDelivery Delivery { get; }
}
