using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldExchange;

public sealed partial class PaidWorldExchangeTests
{
    [Fact]
    public void Independent_delivery_is_required_before_issuance_acceptance_and_retry_disclosure()
    {
        var genesis = WorldPaidExchangeAdmission.CreateFresh(new OrdinaryCareWorldFixture().Genesis);
        var journey = new CareRewardJourney(false, genesis);
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var session = new CareWorldSession(authority);
        var state = EarnReward(journey, session);
        var reserve = state.Progression!.Currency.Accounts.Single(value => value.Kind == CurrencyAccountKind.Reserve);
        var (issue, quote) = IssueCommand(state, journey.Helper, journey.Player,
            SellableRawLot(state, journey.Helper), 1, reserve.AccountId, 1);
        var playerControl = state.Inventory.Controls.Single(value => value.ActorId == journey.Player);
        var playerDelivery = new TrustedCommandDelivery(playerControl.PrincipalId, playerControl.SessionId);
        var wrongIssuerSession = new TrustedCommandDelivery(issue.PrincipalId, playerControl.SessionId);
        foreach (var wrong in new[] { playerDelivery, wrongIssuerSession })
        {
            Assert.Throws<AuthorityDispatchException>(() => authority.SubmitPaidExchangeCommand(issue, wrong, NextId()));
            Assert.Equal(state.ContentHash, authority.ReadStateForHost().ContentHash);
        }
        Assert.Equal(WorldActionRejection.None, authority.SubmitPaidExchangeCommand(issue, Delivery(issue), NextId()).Rejection);
        state = authority.ReadStateForHost();
        var accept = AcceptCommand(state, journey.Player, quote, journey.Helper, 1, 1);
        Assert.Throws<AuthorityDispatchException>(() => authority.SubmitPaidExchangeCommand(accept, Delivery(issue), NextId()));
        Assert.Equal(state.ContentHash, authority.ReadStateForHost().ContentHash);
        var receipt = authority.SubmitPaidExchangeCommand(accept, playerDelivery, NextId());
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
        var after = authority.ReadStateForHost().ContentHash;
        Assert.Throws<AuthorityDispatchException>(() => authority.SubmitPaidExchangeCommand(accept, Delivery(issue), NextId()));
        var changed = new WorldActionCommand(accept.CommandId, accept.PrincipalId, accept.OriginSessionId,
            accept.ActorId, accept.WorldId, accept.BranchId, accept.ExpectedActorRevision,
            new AcceptSpotQuoteIntent(((AcceptSpotQuoteIntent)accept.Intent).Acceptance,
                ((AcceptSpotQuoteIntent)accept.Intent).ExpectedIssuerRevision + 1));
        Assert.Throws<AuthorityDispatchException>(() => authority.SubmitPaidExchangeCommand(changed, playerDelivery, NextId()));
        Assert.Equal(receipt.ResultId, authority.SubmitPaidExchangeCommand(accept, playerDelivery, NextId()).ResultId);
        Assert.Equal(after, authority.ReadStateForHost().ContentHash);
    }

    [Fact]
    public void A_saved_gift_can_spend_quoted_stock_but_cannot_authorize_a_stale_paid_transfer()
    {
        var genesis = WorldPaidExchangeAdmission.CreateFresh(new OrdinaryCareWorldFixture().Genesis);
        var journey = new CareRewardJourney(false, genesis);
        var session = new CareWorldSession(genesis);
        var state = EarnReward(journey, session);
        var lot = SellableRawLot(state, journey.Helper);
        var reserve = state.Progression!.Currency.Accounts.Single(value => value.Kind == CurrencyAccountKind.Reserve);
        var (issue, quote) = IssueCommand(state, journey.Helper, journey.Player, lot, 1, reserve.AccountId, 1);
        state = Commit(session, state, issue, WorldActionRejection.None);
        var terms = new ConsentedMaterialTerms(state.WorldId, state.BranchId, journey.Helper, journey.Player,
            ConsentedMaterialLotKind.CountedSource, lot.LotId, lot.Definition, lot.CustodianId,
            state.GetResident(journey.Player).InventoryCustodianId, 1, lot.Revision);
        state = Commit(session, state, journey.Command(state, journey.Helper,
            new ProposeMaterialIntent(terms, state.AdvancingSteps + 100)), WorldActionRejection.None);
        var gift = Assert.Single(state.Coordination!.Proposals!.Open);
        state = Commit(session, state, journey.Command(state, journey.Player,
            AcceptMaterialProposalIntent.Approving(gift, state.Space.GetActor(journey.Helper).Revision)), WorldActionRejection.None);
        Assert.Equal(lot.Revision + 1, state.Inventory.Custody.GetLot(lot.LotId).Revision);
        Assert.Equal(0UL, state.Coordination!.PaidExchange!.GetIssuerRevision(journey.Helper));
        var before = state;
        var accept = AcceptCommand(state, journey.Player, quote, journey.Helper, 1, 1);
        state = Commit(session, state, accept, WorldActionRejection.RevisionConflict);
        AssertOwnersUnchanged(before, state);
        Assert.Single(state.Coordination!.Proposals!.Outcomes);
        Assert.Empty(state.Coordination.PaidExchange!.Fills);
    }

    [Fact]
    public void Complete_admission_refuses_missing_payment_causes_and_repriced_historical_fills()
    {
        var genesis = WorldPaidExchangeAdmission.CreateFresh(new OrdinaryCareWorldFixture().Genesis);
        var journey = new CareRewardJourney(false, genesis);
        var session = new CareWorldSession(genesis);
        var state = EarnReward(journey, session);
        var reserve = state.Progression!.Currency.Accounts.Single(value => value.Kind == CurrencyAccountKind.Reserve);
        for (int trade = 0; trade < 2; trade++)
        {
            var (issue, quote) = IssueCommand(state, journey.Helper, journey.Player,
                SellableRawLot(state, journey.Helper), 1, reserve.AccountId, 1);
            state = Commit(session, state, issue, WorldActionRejection.None);
            state = Commit(session, state, AcceptCommand(state, journey.Player, quote, journey.Helper, 1, 1), WorldActionRejection.None);
        }
        var book = state.Coordination!.PaidExchange!;
        Assert.Equal(2UL, book.GetIssuerRevision(journey.Helper));
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(ReplaceBook(state, WorldPaidExchangeBook.Empty)));
        var last = book.Fills.Last();
        Assert.Equal(1UL, last.Quote.Quote.MarketRevision);
        var repriced = CloneQuote(last.Quote.Quote, last.Quote.QuoteId, marketRevision: 0);
        var original = last.Settlement!;
        var acceptance = original.Acceptance;
        var forgedAcceptance = new SpotAcceptance(acceptance.AcceptanceId, acceptance.PrincipalId,
            acceptance.OriginSessionId, acceptance.QuoteId, repriced.ContentHash,
            acceptance.Quantity, acceptance.Payment, acceptance.Fees);
        var forged = new WorldPaidQuoteOutcome(new WorldPaidQuote(last.Quote.CreationCommandId, repriced,
            last.Quote.CreatedSequence), WorldPaidQuoteOutcomeKind.Filled, last.WorldSequence,
            new WorldPaidSettlement(original.ActionIndex, forgedAcceptance, original.MovedLotId,
                original.RightsOwnerId, original.OutputIndex, original.MovedStart, original.MovedEnd));
        // Local shape and final issuer count still agree. Only the complete causal prefix exposes this lie.
        var counterfeit = new WorldPaidExchangeBook(book.Open, [book.Outcomes[0], forged], book.IssuerRevisions);
        var error = Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(ReplaceBook(state, counterfeit)));
        Assert.Contains("market revision at its own causal boundary", error.ToString());
    }
}
