using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldExchange.Markets;
using OpenFantasy.Hosting.Tests.WorldExchange.Scheduled;
using OpenFantasy.Hosting.Tests.WorldStorage;

namespace OpenFantasy.Hosting.Tests.HistoryQueries;

/// <summary>One real market journey supplies independent publishing roots and disposable replay copies.</summary>
public sealed class ArchivedQuoteReplayFixture : IDisposable
{
    private readonly StorageFiles _archived = new();
    private readonly Dictionary<WorldPaidQuoteOutcomeKind, ExpectedQuote> _expected = [];

    public ArchivedQuoteReplayFixture()
    {
        using var world = new ScheduledTradingWorld();
        Player = world.Player;
        Issuer = world.Wren;
        Outsider = world.Trader;
        Market = world.Fixture.WrenMarket.MarketId;

        SpotQuote sibling = IssueBilateral(world, 32);
        var purchase = world.BuyOneFromWrenDirectly();
        // The oracle is the actual root immediately after the accepted fill, before any archive read.
        Capture(world.State, purchase.Quote.QuoteId, WorldPaidQuoteOutcomeKind.Filled);
        Capture(world.State, sibling.QuoteId, WorldPaidQuoteOutcomeKind.Superseded);

        SpotQuote withdrawn = IssueBilateral(world, 32);
        Withdraw(world, withdrawn);
        Capture(world.State, withdrawn.QuoteId, WorldPaidQuoteOutcomeKind.Withdrawn);

        SpotQuote expired = IssueBilateral(world, 1);
        Advance(world);
        Capture(world.State, expired.QuoteId, WorldPaidQuoteOutcomeKind.Expired);

        world.CutArchive();
        ArchiveHead = world.State;
        Assert.True(world.Authority.InspectForHost().Journal.MaterialCareArchiveCount > 0);
        foreach (ExpectedQuote expected in _expected.Values)
            Assert.Null(ArchiveHead.Coordination!.PaidExchange!.FindOutcome(expected.Outcome.Quote.QuoteId));
        Assert.Null(ArchiveHead.Coordination!.Markets!.FindQuote(purchase.Quote.QuoteId));

        // A terminal outcome and an open quote in the live segment must not select archived results.
        SpotQuote live = IssueBilateral(world, 32);
        Withdraw(world, live);
        LiveOutcomeId = live.QuoteId;
        OpenQuoteId = IssueBilateral(world, 32).QuoteId;

        // This later genuine refusal owns a command receipt and permanent identities after the archive.
        WorldSnapshot beforeRefusal = world.State;
        WorldActionCommand command = world.Fixture.Command(beforeRefusal, Issuer, new WithdrawSpotQuoteIntent(OpenQuoteId));
        var stale = new WorldActionCommand(command.CommandId, command.PrincipalId, command.OriginSessionId,
            command.ActorId, command.WorldId, command.BranchId, command.ExpectedActorRevision + 1, command.Intent);
        WorldActionReceipt refusal = world.Authority.SubmitPaidExchangeCommand(stale,
            LocalMarketWorldFixture.Delivery(stale), world.Fixture.NextId());
        Assert.Equal(WorldActionRejection.RevisionConflict, refusal.Rejection);
        Head = world.State;
        Assert.True(Head.Revision > ArchiveHead.Revision);
        Assert.NotNull(Head.Coordination!.PaidExchange!.FindOpen(OpenQuoteId));
        Assert.NotNull(Head.Coordination.PaidExchange.FindOutcome(LiveOutcomeId));
        UnknownId = world.Fixture.NextId();
        Assert.False(world.Authority.IsIdentityOccupiedForHost(UnknownId));
        world.Authority.BackupForHost(_archived.Path);
    }

    internal EntityId Player { get; }
    internal EntityId Issuer { get; }
    internal EntityId Outsider { get; }
    internal EntityId Market { get; }
    internal EntityId UnknownId { get; }
    internal EntityId LiveOutcomeId { get; }
    internal EntityId OpenQuoteId { get; }
    internal WorldSnapshot ArchiveHead { get; }
    internal WorldSnapshot Head { get; }

    internal ExpectedQuote Expected(WorldPaidQuoteOutcomeKind kind) => _expected[kind];

    internal TrustedCommandDelivery Delivery(EntityId actor) => LocalMarketWorldFixture.DeliveryOf(Head, actor);

    internal StorageFiles Copy()
    {
        var files = new StorageFiles();
        File.Copy(_archived.Path, files.Path);
        return files;
    }

    private void Capture(WorldSnapshot state, EntityId quoteId, WorldPaidQuoteOutcomeKind kind)
    {
        WorldPaidQuoteOutcome outcome = Assert.IsType<WorldPaidQuoteOutcome>(state.Coordination!.PaidExchange!.FindOutcome(quoteId));
        Assert.Equal(kind, outcome.Kind);
        Assert.Equal(state.Revision, outcome.WorldSequence);
        _expected.Add(kind, new ExpectedQuote(state, outcome));
    }

    private static SpotQuote IssueBilateral(ScheduledTradingWorld world, ulong lifetime)
    {
        WorldSnapshot state = world.State;
        var lot = state.Inventory.Custody.GetLot(world.Fixture.Paid.OrdinaryLot);
        var ledger = state.Progression!.Currency;
        TrustedCommandDelivery delivery = world.DeliveryOf(world.Wren);
        EntityId commandId = world.Fixture.NextId();
        ulong issuedAt = state.AdvancingSteps + 1;
        var quote = new SpotQuote(WorldPaidQuote.DeriveIdentity(commandId), state.WorldId, state.BranchId,
            world.Wren, delivery.PrincipalId, delivery.SessionId, world.Player, SpotQuoteSide.IssuerSells,
            SpotLotKind.CountedSource, lot.LotId, lot.Definition, lot.Revision, lot.CustodianId,
            state.GetResident(world.Player).InventoryCustodianId, ledger.Currency.DefinitionId, ledger.Currency.Version,
            ledger.GetActorAccount(world.Wren).AccountId, ledger.GetActorAccount(world.Player).AccountId, 1, [], 1,
            state.Coordination!.PaidExchange!.GetIssuerRevision(world.Wren), issuedAt, issuedAt + lifetime);
        var command = new WorldActionCommand(commandId, delivery.PrincipalId, delivery.SessionId, world.Wren,
            state.WorldId, state.BranchId, state.Space.GetActor(world.Wren).Revision, new IssueSpotQuoteIntent(quote));
        Assert.Equal(WorldActionRejection.None,
            world.Authority.SubmitPaidExchangeCommand(command, delivery, world.Fixture.NextId()).Rejection);
        return quote;
    }

    private static void Withdraw(ScheduledTradingWorld world, SpotQuote quote)
    {
        WorldActionCommand command = world.Fixture.Command(world.State, world.Wren, new WithdrawSpotQuoteIntent(quote.QuoteId));
        Assert.Equal(WorldActionRejection.None, world.Authority.SubmitPaidExchangeCommand(command,
            LocalMarketWorldFixture.Delivery(command), world.Fixture.NextId()).Rejection);
    }

    private static void Advance(ScheduledTradingWorld world)
    {
        WorldSnapshot state = world.State;
        world.Authority.Commit(new WorldStepBatch(world.Fixture.NextId(), state.WorldId, state.BranchId,
            state.Revision, state.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [], state.ProfileVersion));
    }

    public void Dispose() => _archived.Dispose();

    internal sealed record ExpectedQuote(WorldSnapshot State, WorldPaidQuoteOutcome Outcome);
}
