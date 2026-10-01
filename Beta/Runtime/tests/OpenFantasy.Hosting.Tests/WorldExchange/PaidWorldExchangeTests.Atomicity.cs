using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
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
/// Competing commands in one privileged trusted-host batch of the real SQLite owner (not a network
/// authentication claim): two acceptances against one issuer, a saved gift and a paid fill on one lot, a
/// held quest reward beside a fill, and a seller collecting its own fee, each with exact reopening.
/// A batch admits one action per actor, so each competition uses distinct actual residents.
/// </summary>
public sealed partial class PaidWorldExchangeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Two_acceptances_against_one_issuer_in_one_batch_fill_at_most_once(bool wolfFirst)
    {
        var genesis = WorldPaidExchangeAdmission.CreateFresh(new OrdinaryCareWorldFixture().Genesis);
        var journey = new CareRewardJourney(false, genesis);
        using var files = new StorageFiles();
        SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        try
        {
            var state = EarnReward(journey, new CareWorldSession(authority));
            EntityId player = journey.Player, helper = journey.Helper, wolf = journey.First.TargetActorId;
            var (toPlayerIssue, toPlayer) = FeelessQuote(state, helper, player, SellableRawLot(state, helper), issuerBuys: false);
            state = Submit(authority, toPlayerIssue, WorldActionRejection.None);
            var (againIssue, again) = FeelessQuote(state, helper, player, SellableRawLot(state, helper), issuerBuys: false);
            state = Submit(authority, againIssue, WorldActionRejection.None);
            var (toWolfIssue, toWolf) = FeelessQuote(state, helper, wolf, SellableRawLot(state, helper), issuerBuys: false);
            state = Submit(authority, toWolfIssue, WorldActionRejection.None);

            WorldSnapshot before = state;
            WorldActionCommand playerAccepts = AcceptCommand(before, player, toPlayer, helper, 1, 1);
            WorldActionCommand playerAgain = AcceptCommand(before, player, again, helper, 1, 1);
            WorldActionCommand wolfAccepts = AcceptCommand(before, wolf, toWolf, helper, 1, 1);
            AssertEachSettlesAlone(authority, journey, before, playerAccepts, playerAgain);
            // One counterparty cannot submit two acceptances in one step: the batch itself is refused.
            Assert.Throws<ArgumentException>(() => journey.Batch(before, WorldBatchKind.AdvancingStep, [playerAccepts, playerAgain]));
            // The only other resident counterparty is a ready, reachable wolf without manipulation.
            Assert.True(WorldActorAvailability.CanAct(before, wolf));
            Assert.False(before.Space.GetActor(wolf).Condition.SupportsFunction(BodyFunction.Manipulation));

            WorldActionCommand[] order = wolfFirst ? [wolfAccepts, playerAccepts] : [playerAccepts, wolfAccepts];
            WorldStepDecision decision = authority.Commit(journey.Batch(before, WorldBatchKind.AdvancingStep, order));
            Assert.Equal(new[] { WorldActionRejection.None, WorldActionRejection.Unavailable },
                RejectionsOf(decision, [playerAccepts, wolfAccepts]));
            state = authority.ReadStateForHost();
            WorldPaidExchangeBook book = state.Coordination!.PaidExchange!;
            WorldPaidQuoteOutcome fill = Assert.Single(book.Fills);
            Assert.Equal((toPlayer.QuoteId, playerAccepts.CommandId), (fill.Quote.QuoteId, fill.Settlement!.Acceptance.AcceptanceId));
            // The one fill closes both siblings in its own transaction, whichever batch order was used.
            foreach (SpotQuote sibling in new[] { again, toWolf })
            {
                WorldPaidQuoteOutcome closed = book.FindOutcome(sibling.QuoteId)!;
                Assert.Equal((WorldPaidQuoteOutcomeKind.Superseded, playerAccepts.CommandId, state.Revision),
                    (closed.Kind, closed.Supersession!.AcceptanceId, closed.WorldSequence));
            }
            Assert.Empty(book.Open);
            Assert.Equal(1UL, book.GetIssuerRevision(helper));
            CurrencyLedger paid = before.Progression!.Currency, after = state.Progression!.Currency;
            Assert.Equal((paid.Revision + 1, paid.AdmittedSupply), (after.Revision, after.AdmittedSupply));
            Assert.Equal(paid.GetActorAccount(player).Balance - 1, after.GetActorAccount(player).Balance);
            Assert.Equal(paid.GetActorAccount(helper).Balance + 1, after.GetActorAccount(helper).Balance);
            Assert.Equal(paid.GetActorAccount(wolf).Balance, after.GetActorAccount(wolf).Balance);
            Assert.Equal(before.Progression.Revision + 1, state.Progression.Revision);
            Assert.Equal(ActorsOf(before), ActorsOf(state));
            // Only the winning acceptance names a moved child; the refused one created and reserved nothing.
            Assert.Equal(state.GetResident(player).InventoryCustodianId,
                state.Inventory.Custody.GetLot(WorldPaidQuote.SplitChildOf(playerAccepts.CommandId)).CustodianId);
            EntityId unused = WorldPaidQuote.SplitChildOf(wolfAccepts.CommandId);
            Assert.DoesNotContain(state.Inventory.Custody.Lots, value => value.LotId == unused
                || value.CustodianId == state.GetResident(wolf).InventoryCustodianId);
            Assert.False(authority.IsIdentityOccupiedForHost(unused));
            Assert.All(order, command => Assert.True(authority.IsIdentityOccupiedForHost(command.CommandId)));
            Assert.False(authority.IsIdentityOccupiedForHost(playerAgain.CommandId));

            byte[][] receipts = order.Select(command => WorldReceiptCodecV1.Encode(
                decision.Receipts.Single(value => value.CommandId == command.CommandId))).ToArray();
            ContentHash head = state.ContentHash;
            authority.Dispose();
            authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);
            // Each exact retry returns its own original receipt without another transfer.
            for (int index = 0; index < order.Length; index++)
            {
                Assert.Equal(receipts[index], WorldReceiptCodecV1.Encode(
                    authority.SubmitPaidExchangeCommand(order[index], Delivery(order[index]), NextId())));
            }
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);
        }
        finally { authority.Dispose(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_saved_gift_and_a_paid_fill_on_one_lot_in_one_batch_block_each_other_without_effect(bool paidFirst)
    {
        var genesis = WorldPaidExchangeAdmission.CreateFresh(new OrdinaryCareWorldFixture().Genesis);
        var journey = new CareRewardJourney(false, genesis);
        using var files = new StorageFiles();
        SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        try
        {
            var state = EarnReward(journey, new CareWorldSession(authority));
            EntityId player = journey.Player, helper = journey.Helper;
            // The player first buys one actual unit, which both competing settlements then name.
            var (sale, saleQuote) = FeelessQuote(state, helper, player, SellableRawLot(state, helper), issuerBuys: false);
            state = Submit(authority, sale, WorldActionRejection.None);
            var bought = AcceptCommand(state, player, saleQuote, helper, 1, 1);
            state = Submit(authority, bought, WorldActionRejection.None);
            CountedSourceLot lot = state.Inventory.Custody.GetLot(WorldPaidQuote.SplitChildOf(bought.CommandId));
            var terms = new ConsentedMaterialTerms(state.WorldId, state.BranchId, player, helper,
                ConsentedMaterialLotKind.CountedSource, lot.LotId, lot.Definition, lot.CustodianId,
                state.GetResident(helper).InventoryCustodianId, 1, lot.Revision);
            var propose = journey.Command(state, player, new ProposeMaterialIntent(terms, state.AdvancingSteps + 100));
            Assert.Equal(WorldActionRejection.None, authority.SubmitMaterialProposalCommand(propose, Delivery(propose), NextId()).Rejection);
            state = authority.ReadStateForHost();
            var gift = Assert.Single(state.Coordination!.Proposals!.Open);
            // The gift's recipient also bids for the same unit from its own earned trade proceeds.
            var (bid, quote) = FeelessQuote(state, helper, player, lot, issuerBuys: true);
            state = Submit(authority, bid, WorldActionRejection.None);

            WorldSnapshot before = state;
            WorldActionCommand acceptGift = journey.Command(before, helper,
                AcceptMaterialProposalIntent.Approving(gift, before.Space.GetActor(player).Revision));
            WorldActionCommand acceptPaid = AcceptCommand(before, player, quote, helper, 1, 1);
            AssertEachSettlesAlone(authority, journey, before, acceptGift, acceptPaid);

            // Each acceptor is the other settlement's absent party, so the sender and issuer rules refuse both.
            WorldActionCommand[] order = paidFirst ? [acceptPaid, acceptGift] : [acceptGift, acceptPaid];
            WorldStepDecision decision = authority.Commit(journey.Batch(before, WorldBatchKind.AdvancingStep, order));
            WorldActionRejection[] reasons = RejectionsOf(decision, [acceptGift, acceptPaid]);
            Assert.Equal(WorldActionRejection.Unavailable, reasons[0]);
            Assert.Equal(WorldActionRejection.Unavailable, reasons[1]);
            state = authority.ReadStateForHost();
            AssertOwnersUnchanged(before, state);
            Assert.Equal(before.Coordination!.Proposals!.ContentHash, state.Coordination!.Proposals!.ContentHash);
            Assert.Equal((lot.Revision, lot.CustodianId), (state.Inventory.Custody.GetLot(lot.LotId).Revision,
                state.Inventory.Custody.GetLot(lot.LotId).CustodianId));
            Assert.All(order, command => Assert.True(authority.IsIdentityOccupiedForHost(command.CommandId)));
            Assert.False(authority.IsIdentityOccupiedForHost(WorldPaidQuote.SplitChildOf(acceptPaid.CommandId)));

            // Alone in the next step the gift settles; the bid's pinned unit has then left the seller.
            WorldActionCommand later = journey.Command(state, helper,
                AcceptMaterialProposalIntent.Approving(gift, state.Space.GetActor(player).Revision));
            Assert.Equal(WorldActionRejection.None, authority.SubmitMaterialProposalCommand(later, Delivery(later), NextId()).Rejection);
            state = authority.ReadStateForHost();
            Assert.Equal(WorldMaterialProposalOutcomeKind.Accepted, state.Coordination!.Proposals!.FindOutcome(gift.ProposalId)!.Kind);
            Assert.Equal(state.GetResident(helper).InventoryCustodianId, state.Inventory.Custody.GetLot(lot.LotId).CustodianId);
            Assert.Equal(quote.ContentHash, state.Coordination.PaidExchange!.FindOpen(quote.QuoteId)!.Quote.ContentHash);

            ContentHash head = state.ContentHash;
            authority.Dispose();
            authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);
        }
        finally { authority.Dispose(); }
    }

    [Fact]
    public void A_held_quest_reward_cannot_share_a_batch_with_a_fill_but_funds_it_in_causal_order()
    {
        var genesis = WorldPaidExchangeAdmission.CreateFresh(new OrdinaryCareWorldFixture().Genesis);
        var journey = new CareRewardJourney(false, genesis);
        using var files = new StorageFiles();
        SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        try
        {
            var session = new CareWorldSession(authority);
            journey.Approach(session);
            EntityId player = journey.Player, helper = journey.Helper;
            var state = authority.ReadStateForHost();
            var (issue, quote) = FeelessQuote(state, helper, player, SellableRawLot(state, helper), issuerBuys: false);
            state = Submit(authority, issue, WorldActionRejection.None);
            Assert.Equal(0UL, state.Progression!.Currency.GetActorAccount(player).Balance);

            WorldActionCommand reward = journey.PrepareReward(session);
            state = authority.ReadStateForHost();
            Assert.True(state.HasReadingHold);
            WorldActionCommand accept = AcceptCommand(state, player, quote, helper, 1, 1);
            // A reward needs the giver's reading hold; a paid acceptance exists only in an advancing unheld step.
            Assert.Throws<ArgumentException>(() => journey.Batch(state, WorldBatchKind.HeldInteraction, [reward, accept]));
            // Alone, the trusted submission selects an advancing step, which the open hold refuses whole.
            AssertWholeStepRefused(authority, accept);

            state = CareRewardJourney.Commit(session, journey.Batch(state, WorldBatchKind.HeldInteraction, [reward])).Candidate;
            CurrencyLedger rewarded = state.Progression!.Currency;
            Assert.True(rewarded.GetActorAccount(player).Balance >= 1, "The quest reward, not the test, funds the purchase.");
            Assert.Equal(quote.ContentHash, state.Coordination!.PaidExchange!.FindOpen(quote.QuoteId)!.Quote.ContentHash);
            state = CareRewardJourney.Commit(session, journey.Batch(state, WorldBatchKind.HeldInteraction,
                [journey.Command(state, player, new CloseDialogueIntent(state.Dialogues.Single().DialogueId))])).Candidate;
            ulong progressionBefore = state.Progression!.Revision;
            WorldActionCommand settle = AcceptCommand(state, player, quote, helper, 1, 1);
            state = Submit(authority, settle, WorldActionRejection.None);
            // The same player account was credited by the reward and then debited by the later fill.
            CurrencyLedger after = state.Progression!.Currency;
            Assert.Equal((rewarded.Revision + 1, rewarded.AdmittedSupply), (after.Revision, after.AdmittedSupply));
            Assert.Equal(rewarded.GetActorAccount(player).Balance - 1, after.GetActorAccount(player).Balance);
            Assert.Equal(rewarded.GetActorAccount(player).Revision + 1, after.GetActorAccount(player).Revision);
            Assert.Equal(progressionBefore + 1, state.Progression.Revision);
            Assert.Contains(state.ResourceHistory!.History.Batches, value => value.CommandId == reward.CommandId);
            Assert.DoesNotContain(state.ResourceHistory.History.Batches, value => value.CommandId == settle.CommandId);

            ContentHash head = state.ContentHash;
            authority.Dispose();
            authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);
            Assert.False(authority.IsIdentityOccupiedForHost(accept.CommandId));
        }
        finally { authority.Dispose(); }
    }

    [Fact]
    public void A_seller_collecting_its_own_fee_is_credited_once_through_hosted_replay()
    {
        var genesis = WorldPaidExchangeAdmission.CreateFresh(new OrdinaryCareWorldFixture().Genesis);
        var journey = new CareRewardJourney(false, genesis);
        using var files = new StorageFiles();
        SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        try
        {
            var state = EarnReward(journey, new CareWorldSession(authority));
            EntityId player = journey.Player, helper = journey.Helper;
            CurrencyLedger before = state.Progression!.Currency;
            EntityId buyerAccount = before.GetActorAccount(player).AccountId, sellerAccount = before.GetActorAccount(helper).AccountId;
            Assert.True(before.GetAccount(buyerAccount).Balance >= 3, "The quest reward, not the test, funds the purchase.");
            var (issue, quote) = IssueCommand(state, helper, player, SellableRawLot(state, helper), 2, sellerAccount, 1);
            Assert.Equal(sellerAccount, Assert.Single(quote.Fees).PayeeAccountId);
            state = Submit(authority, issue, WorldActionRejection.None);
            WorldActionCommand accept = AcceptCommand(state, player, quote, helper, 1, payment: 2);
            Assert.Equal(1UL, ((AcceptSpotQuoteIntent)accept.Intent).Acceptance.Fees);
            ulong progressionBefore = state.Progression!.Revision;
            WorldActionReceipt receipt = authority.SubmitPaidExchangeCommand(accept, Delivery(accept), NextId());
            Assert.Equal(WorldActionRejection.None, receipt.Rejection);
            state = authority.ReadStateForHost();

            CurrencyLedger after = state.Progression!.Currency;
            Assert.Equal((before.Revision + 1, before.AdmittedSupply), (after.Revision, after.AdmittedSupply));
            Assert.Equal(before.GetAccount(buyerAccount).Balance - 3, after.GetAccount(buyerAccount).Balance);
            Assert.Equal(before.GetAccount(sellerAccount).Balance + 3, after.GetAccount(sellerAccount).Balance);
            // Price and fee are aggregated: each touched account advances exactly once.
            Assert.Equal(before.GetAccount(buyerAccount).Revision + 1, after.GetAccount(buyerAccount).Revision);
            Assert.Equal(before.GetAccount(sellerAccount).Revision + 1, after.GetAccount(sellerAccount).Revision);
            Assert.Equal(before.Accounts.Where(value => value.AccountId != buyerAccount && value.AccountId != sellerAccount)
                    .Select(value => (value.AccountId, value.Balance, value.Revision)),
                after.Accounts.Where(value => value.AccountId != buyerAccount && value.AccountId != sellerAccount)
                    .Select(value => (value.AccountId, value.Balance, value.Revision)));
            Assert.Equal(progressionBefore + 1, state.Progression.Revision);
            WorldPaidQuoteOutcome fill = Assert.Single(state.Coordination!.PaidExchange!.Fills);
            Assert.Equal(quote.ContentHash, fill.Quote.Quote.ContentHash);

            // Durable replay reproduces the exact ledger; a retry returns the original receipt only.
            ContentHash head = state.ContentHash;
            authority.Dispose();
            authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt),
                WorldReceiptCodecV1.Encode(authority.SubmitPaidExchangeCommand(accept, Delivery(accept), NextId())));
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);
        }
        finally { authority.Dispose(); }
    }

    /// <summary>Each command settles when decided alone from the same exact root; the decision is never published.</summary>
    private static void AssertEachSettlesAlone(SqliteWorldAuthority authority, CareRewardJourney journey, WorldSnapshot root,
        params WorldActionCommand[] commands)
    {
        foreach (WorldActionCommand command in commands)
        {
            WorldStepDecision alone = new WorldStepProcessor().Decide(root,
                journey.Batch(root, WorldBatchKind.AdvancingStep, [command]), authority.IsIdentityOccupiedForHost);
            Assert.Equal(WorldActionRejection.None, Assert.Single(alone.Receipts).Rejection);
        }
        Assert.Equal(root.ContentHash, authority.ReadStateForHost().ContentHash);
    }

    private static WorldActionRejection[] RejectionsOf(WorldStepDecision decision, IEnumerable<WorldActionCommand> commands) =>
        commands.Select(command => decision.Receipts.Single(value => value.CommandId == command.CommandId).Rejection).ToArray();
}
