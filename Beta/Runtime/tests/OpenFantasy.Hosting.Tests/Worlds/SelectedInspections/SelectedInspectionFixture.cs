using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Tests.Worlds.CoPlayerTrade;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.SelectedInspections;

/// <summary>One actual market journey and coordinated cut shared by the read-only inspection contrasts.</summary>
public sealed class SelectedInspectionFixture : IDisposable
{
    private readonly StorageFiles _open = new();
    private readonly StorageFiles _history = new();
    internal CoPlayerTradeFixture Trade { get; } = new(ownerMarket: true);
    internal byte[] Genesis { get; }
    internal WorldSnapshot OpenHead { get; }
    internal WorldSnapshot HistoryHead { get; }
    internal EntityId QuoteId { get; }
    internal EntityId ProposalId { get; }
    internal EntityId LaterOpenQuoteId { get; }
    internal EntityId LaterOpenProposalId { get; }
    internal PaidQuoteView ClosedQuote { get; }
    internal MaterialProposalView ClosedProposal { get; }
    internal EntityId UnknownId => new(0x5e1ec7ed00000000, ulong.MaxValue);
    internal EntityId Owner => Trade.Owner;
    internal EntityId Grantee => Trade.Grantee;
    internal TrustedCommandDelivery Delivery => Trade.Control.Delivery(Grantee);

    public SelectedInspectionFixture()
    {
        Genesis = WorldSnapshotCodec.Encode(Trade.Genesis);
        using var source = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(source.Path, Trade.Genesis);
        var state = Trade.Prepare(authority, fund: true);
        var market = Trade.OwnerMarket!;
        var berries = state.Inventory.Custody.Lots.Single(value =>
            value.CustodianId == state.GetResident(Trade.Helper).InventoryCustodianId && value.Definition == market.Commodity);
        var (funding, purchase) = Trade.Quote(state, Trade.Helper, Owner, berries, price: 6);
        Trade.Submit(authority, funding);
        Trade.Submit(authority, Trade.Accept(authority.ReadStateForHost(), purchase));
        state = Trade.Ready(authority);

        var proposal = Trade.Offer(state, Trade.Helper, Owner, Trade.Lot(state, Trade.Helper));
        Trade.Submit(authority, proposal);
        ProposalId = WorldMaterialProposal.DeriveIdentity(proposal.CommandId);
        state = authority.ReadStateForHost();
        var stock = state.Inventory.Custody.Lots.Single(value => value.CustodianId == market.CustodianId
            && value.Definition == market.Commodity);
        var (issue, quote) = Trade.Quote(state, Owner, Trade.Helper, stock, market: market);
        Trade.Submit(authority, issue);
        QuoteId = quote.QuoteId;
        OpenHead = authority.ReadStateForHost();
        authority.BackupForHost(_open.Path);

        Trade.Submit(authority, Trade.Control.Commands.Command(OpenHead, Trade.Helper,
            new WithdrawMaterialProposalIntent(ProposalId)));
        Trade.Submit(authority, Trade.Control.Delegated(authority.ReadStateForHost(), new WithdrawSpotQuoteIntent(QuoteId)));
        ClosedQuote = Assert.IsType<PaidQuoteView>(authority.ReadSelectedPaidQuoteHistory(QuoteId, Owner,
            Trade.Control.Delivery(Owner), null));
        ClosedProposal = Assert.IsType<MaterialProposalView>(authority.ReadSelectedMaterialProposalHistory(ProposalId, Owner,
            Trade.Control.Delivery(Owner), null));
        Assert.Equal(market.MarketId, ClosedQuote.MarketId);

        var cut = Trade.Cut(authority);
        Assert.NotNull(cut.Publication.SealedMaterialCareSegment);
        Assert.Equal(1UL, authority.InspectForHost().Journal.MaterialCareArchiveCount);
        Assert.Null(authority.ReadStateForHost().Coordination!.PaidExchange!.FindOutcome(QuoteId));
        Assert.Null(authority.ReadStateForHost().Coordination!.Proposals!.FindOutcome(ProposalId));
        Reassign(authority, WorldControlCapabilities.PrivateHistory);

        // Both subjects stay open while a history-only delegate inspects the separate archived subjects.
        state = Trade.Ready(authority);
        var laterProposal = Trade.Offer(state, Trade.Helper, Owner, Trade.Lot(state, Trade.Helper));
        Trade.Submit(authority, laterProposal);
        LaterOpenProposalId = WorldMaterialProposal.DeriveIdentity(laterProposal.CommandId);
        state = authority.ReadStateForHost();
        var (laterIssue, laterQuote) = Trade.Quote(state, Trade.Helper, Owner, Trade.Lot(state, Trade.Helper));
        Trade.Submit(authority, laterIssue);
        LaterOpenQuoteId = laterQuote.QuoteId;
        HistoryHead = authority.ReadStateForHost();
        authority.BackupForHost(_history.Path);
    }

    internal WorldControlWitness Witness(WorldSnapshot state) =>
        Assert.IsType<WorldControlWitness>(state.EffectiveControl!.Find(Owner)!.Witness);

    internal WorldControlWitness Reassign(SqliteWorldAuthority authority, WorldControlCapabilities capabilities)
    {
        Trade.Control.Revoke(authority);
        Trade.Control.Event(authority, Owner, WorldControllerEventKind.Reconnected);
        Trade.Control.Grant(authority, capabilities);
        Trade.Control.LoseAndExpire(authority);
        return Witness(Trade.Control.Acquire(authority));
    }

    internal StorageFiles Copy(bool history)
    {
        var copy = new StorageFiles();
        File.Copy(history ? _history.Path : _open.Path, copy.Path);
        return copy;
    }

    public void Dispose()
    {
        _open.Dispose();
        _history.Dispose();
    }
}
