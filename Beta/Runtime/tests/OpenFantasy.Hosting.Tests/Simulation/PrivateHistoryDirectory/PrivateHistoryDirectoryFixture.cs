using OpenFantasy.Core;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Tests.Worlds.CoPlayerTrade;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.PrivateHistoryDirectory;

/// <summary>Actual same-step expiry of both kinds, followed by real closure pressure and an archived original genesis.</summary>
public sealed class PrivateHistoryDirectoryFixture : IDisposable
{
    private readonly StorageFiles _files = new();
    internal CoPlayerTradeFixture Trade { get; } = new();
    internal byte[] Genesis { get; }
    internal WorldSnapshot Head { get; }
    internal EntityId[] TiedSubjects { get; }
    internal EntityId[] AllSubjects { get; }
    internal ulong TiedSequence { get; }

    public PrivateHistoryDirectoryFixture()
    {
        Genesis = WorldSnapshotCodec.Encode(Trade.Genesis);
        using var authority = SqliteWorldAuthority.CreateNew(_files.Path, Trade.Genesis);
        var state = Trade.Prepare(authority, fund: false);
        ulong expires = state.AdvancingSteps + 24;
        List<EntityId> tied = [];
        for (int index = 0; index < WorldMaterialProposalBook.MaximumOpen; index++)
        {
            state = authority.ReadStateForHost();
            var original = Trade.Offer(state, Trade.Helper, Trade.Owner, Trade.Lot(state, Trade.Helper));
            var intent = Assert.IsType<ProposeMaterialIntent>(original.Intent);
            var command = Trade.Control.Commands.Command(state, Trade.Helper, new ProposeMaterialIntent(intent.Terms, expires));
            Trade.Submit(authority, command);
            tied.Add(WorldMaterialProposal.DeriveIdentity(command.CommandId));
        }
        for (int index = 0; index < 3; index++)
        {
            state = authority.ReadStateForHost();
            var (original, quote) = Trade.Quote(state, Trade.Helper, Trade.Owner, Trade.Lot(state, Trade.Helper));
            var timed = new SpotQuote(quote.QuoteId, quote.WorldId, quote.BranchId, quote.IssuerActorId,
                quote.IssuerPrincipalId, quote.IssuerSessionId, quote.CounterpartyActorId, quote.Side, quote.LotKind,
                quote.LotId, quote.Definition, quote.ExpectedLotRevision, quote.SellerCustodianId, quote.BuyerCustodianId,
                quote.CurrencyDefinitionId, quote.CurrencyVersion, quote.SellerAccountId, quote.BuyerAccountId,
                quote.UnitPrice, quote.Fees, quote.QuantityAllowance, quote.MarketRevision, quote.IssuedAtStep, expires);
            var command = new WorldActionCommand(original.CommandId, original.PrincipalId, original.OriginSessionId,
                original.ActorId, original.WorldId, original.BranchId, original.ExpectedActorRevision,
                new IssueSpotQuoteIntent(timed));
            Trade.Submit(authority, command);
            tied.Add(timed.QuoteId);
        }
        state = authority.ReadStateForHost();
        Assert.True(state.AdvancingSteps < expires);
        Assert.Empty(authority.ReadSelectedPrivateHistory(Trade.Owner, Trade.Control.Delivery(Trade.Owner), null, new()).Entries);
        while (state.AdvancingSteps < expires)
            state = Trade.Control.Journey.Commit(authority, WorldBatchKind.AdvancingStep).Candidate;
        TiedSequence = state.Revision;
        TiedSubjects = tied.ToArray();
        Assert.Equal(19, state.Coordination!.Proposals!.Outcomes.Count + state.Coordination.PaidExchange!.Outcomes.Count);
        Assert.All(state.Coordination.Proposals.Outcomes, value => Assert.Equal(TiedSequence, value.WorldSequence));
        Assert.All(state.Coordination.PaidExchange.Outcomes, value => Assert.Equal(TiedSequence, value.WorldSequence));
        var cut = Trade.Cut(authority);
        Assert.NotNull(cut.Publication.SealedMaterialCareSegment);
        Assert.Equal(1UL, authority.InspectForHost().Journal.MaterialCareArchiveCount);
        Head = authority.ReadStateForHost();
        AllSubjects = cut.Closing.Coordination!.Proposals!.Outcomes.Select(value => value.Proposal.ProposalId)
            .Concat(cut.Closing.Coordination.PaidExchange!.Outcomes.Select(value => value.Quote.QuoteId))
            .Concat(Head.Coordination!.Proposals!.Outcomes.Select(value => value.Proposal.ProposalId))
            .Concat(Head.Coordination.PaidExchange!.Outcomes.Select(value => value.Quote.QuoteId)).ToArray();
        Assert.Equal(AllSubjects.Length, AllSubjects.Distinct().Count());
    }

    internal StorageFiles Copy()
    {
        var copy = new StorageFiles();
        File.Copy(_files.Path, copy.Path);
        return copy;
    }

    internal EntityId Append(SqliteWorldAuthority authority)
    {
        var state = Trade.Ready(authority);
        var issue = Trade.Offer(state, Trade.Helper, Trade.Owner, Trade.Lot(state, Trade.Helper));
        Trade.Submit(authority, issue);
        EntityId id = WorldMaterialProposal.DeriveIdentity(issue.CommandId);
        Trade.Submit(authority, Trade.Control.Commands.Command(authority.ReadStateForHost(), Trade.Helper,
            new WithdrawMaterialProposalIntent(id)));
        return id;
    }

    public void Dispose() => _files.Dispose();
}
