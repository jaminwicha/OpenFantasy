using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldExchange;

/// <summary>
/// Two independently eligible, funded human buyers accept one issuer's quotes in one privileged trusted-host
/// batch of the real SQLite owner (not a network authentication claim). A batch orders its actions canonically
/// by actor identity, so submission order never chooses the winner: the canonically first eligible acceptance
/// fills and supersedes its sibling. Separate fresh admissions place either buyer first in that order.
/// </summary>
public sealed partial class PaidWorldExchangeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Two_eligible_humans_accept_one_issuers_quotes_in_one_batch_and_only_the_first_fills(
        bool merchantSortsFirst, bool merchantSubmitsFirst)
    {
        var fixture = new PaidTradeWorldFixture(merchantSortsFirst: merchantSortsFirst);
        var journey = fixture.Journey;
        using var files = new StorageFiles();
        SqliteWorldAuthority authority = fixture.Create(files);
        try
        {
            var state = EarnReward(journey, new CareWorldSession(authority));
            EntityId player = fixture.Player, helper = fixture.Helper, merchant = fixture.Merchant;
            CurrencyLedger genesis = fixture.Genesis.Progression!.Currency;
            EntityId wallet = genesis.GetActorAccount(merchant).AccountId;
            Assert.Equal(0UL, genesis.GetAccount(wallet).Balance);
            Assert.Equal(0UL, state.Progression!.Currency.GetAccount(wallet).Balance);

            // The player's earned quest funds pay an actual trade fee into the third person's empty wallet.
            var (funding, fundingQuote) = IssueCommand(state, helper, player, UnconstrainedLot(state, helper, fixture.OrdinaryLot),
                1, wallet, 1);
            state = Submit(authority, funding, WorldActionRejection.None);
            state = Submit(authority, AcceptCommand(state, player, fundingQuote, helper, 1, 1), WorldActionRejection.None);
            CurrencyLedger funded = state.Progression!.Currency;
            Assert.Equal(1UL, funded.GetAccount(wallet).Balance);
            Assert.Equal(genesis.AdmittedSupply, funded.AdmittedSupply);
            Assert.Equal(funded.AdmittedSupply, funded.Accounts.Aggregate(0UL, (total, value) => checked(total + value.Balance)));

            // One issuer quotes each buyer separately, on distinct divisible ordinary lots.
            var (toPlayerIssue, toPlayer) = FeelessQuote(state, helper, player, state.Inventory.Custody.GetLot(fixture.OrdinaryLot),
                issuerBuys: false);
            state = Submit(authority, toPlayerIssue, WorldActionRejection.None);
            var (toMerchantIssue, toMerchant) = FeelessQuote(state, helper, merchant,
                UnconstrainedLot(state, helper, fixture.OrdinaryLot), issuerBuys: false);
            state = Submit(authority, toMerchantIssue, WorldActionRejection.None);
            Assert.NotEqual(toPlayer.LotId, toMerchant.LotId);

            WorldSnapshot before = state;
            WorldActionCommand playerAccepts = AcceptCommand(before, player, toPlayer, helper, 1, 1);
            WorldActionCommand merchantAccepts = AcceptCommand(before, merchant, toMerchant, helper, 1, 1);
            foreach (SpotQuote quote in new[] { toPlayer, toMerchant })
            {
                AssertOtherwiseEligible(before, quote);
                AssertRoomFor(before, quote);
            }
            AssertEachSettlesAlone(authority, journey, before, playerAccepts, merchantAccepts);

            // The canonical batch is the same whichever acceptance the host received first.
            WorldActionCommand[] submitted = merchantSubmitsFirst ? [merchantAccepts, playerAccepts] : [playerAccepts, merchantAccepts];
            WorldStepBatch batch = journey.Batch(before, WorldBatchKind.AdvancingStep, submitted);
            (EntityId winnerId, EntityId loserId) = merchantSortsFirst ? (merchant, player) : (player, merchant);
            (WorldActionCommand winner, WorldActionCommand loser) = merchantSortsFirst
                ? (merchantAccepts, playerAccepts) : (playerAccepts, merchantAccepts);
            (SpotQuote won, SpotQuote lost) = merchantSortsFirst ? (toMerchant, toPlayer) : (toPlayer, toMerchant);
            Assert.Equal(new[] { winner.CommandId, loser.CommandId }, batch.Actions.Select(value => value.CommandId));
            WorldStepDecision decision = authority.Commit(batch);
            Assert.Equal(new[] { WorldActionRejection.None, WorldActionRejection.Unavailable }, RejectionsOf(decision, [winner, loser]));
            state = authority.ReadStateForHost();

            // Exactly the first eligible acceptance fills; its transaction closes the sibling as superseded.
            WorldPaidExchangeBook book = state.Coordination!.PaidExchange!;
            WorldPaidQuoteOutcome fill = Assert.Single(book.Fills, value => value.WorldSequence == state.Revision);
            Assert.Equal((won.QuoteId, winner.CommandId), (fill.Quote.QuoteId, fill.Settlement!.Acceptance.AcceptanceId));
            WorldPaidQuoteOutcome sibling = book.FindOutcome(lost.QuoteId)!;
            Assert.Equal((WorldPaidQuoteOutcomeKind.Superseded, winner.CommandId, won.QuoteId, state.Revision),
                (sibling.Kind, sibling.Supersession!.AcceptanceId, sibling.Supersession.FilledQuoteId, sibling.WorldSequence));
            Assert.Empty(book.Open);
            Assert.Equal(before.Coordination!.PaidExchange!.GetIssuerRevision(helper) + 1, book.GetIssuerRevision(helper));

            // One payment and one progression revision; no actor statistic, HP, MP or XP changes.
            CurrencyLedger paid = before.Progression!.Currency, after = state.Progression!.Currency;
            Assert.Equal((paid.Revision + 1, paid.AdmittedSupply), (after.Revision, after.AdmittedSupply));
            Assert.Equal(paid.GetActorAccount(winnerId).Balance - 1, after.GetActorAccount(winnerId).Balance);
            Assert.Equal(paid.GetActorAccount(helper).Balance + 1, after.GetActorAccount(helper).Balance);
            Assert.Equal(paid.GetActorAccount(loserId).Balance, after.GetActorAccount(loserId).Balance);
            Assert.Equal(before.Progression.Revision + 1, state.Progression.Revision);
            Assert.Equal(ActorsOf(before), ActorsOf(state));

            // Only the winner's split child exists; the loser's stock, pack and child identity are untouched.
            Assert.Equal(state.GetResident(winnerId).InventoryCustodianId,
                state.Inventory.Custody.GetLot(WorldPaidQuote.SplitChildOf(winner.CommandId)).CustodianId);
            EntityId unused = WorldPaidQuote.SplitChildOf(loser.CommandId);
            Assert.DoesNotContain(state.Inventory.Custody.Lots, value => value.LotId == unused);
            Assert.False(authority.IsIdentityOccupiedForHost(unused));
            CountedSourceLot unsold = before.Inventory.Custody.GetLot(lost.LotId);
            Assert.Equal((unsold.Revision, unsold.Quantity),
                (state.Inventory.Custody.GetLot(lost.LotId).Revision, state.Inventory.Custody.GetLot(lost.LotId).Quantity));
            EntityId loserPack = state.GetResident(loserId).InventoryCustodianId;
            Assert.Equal((RawUnits(before, loserPack), ProducedUnits(before, loserPack)),
                (RawUnits(state, loserPack), ProducedUnits(state, loserPack)));
            Assert.All(submitted, command => Assert.True(authority.IsIdentityOccupiedForHost(command.CommandId)));

            byte[][] receipts = submitted.Select(command => WorldReceiptCodecV1.Encode(
                decision.Receipts.Single(value => value.CommandId == command.CommandId))).ToArray();
            ContentHash head = state.ContentHash;
            authority.Dispose();
            authority = fixture.Reopen(files);
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);
            // Each exact retry returns its own original receipt without another transfer.
            for (int index = 0; index < submitted.Length; index++)
            {
                Assert.Equal(receipts[index], WorldReceiptCodecV1.Encode(
                    authority.SubmitPaidExchangeCommand(submitted[index], Delivery(submitted[index]), NextId())));
            }
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);
            Assert.False(authority.IsIdentityOccupiedForHost(unused));
        }
        finally { authority.Dispose(); }
    }
}
