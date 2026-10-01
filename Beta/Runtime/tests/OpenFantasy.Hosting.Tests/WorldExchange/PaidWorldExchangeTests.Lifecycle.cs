using OpenFantasy.Core;
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
/// Quote lifecycle through the actual paid world and SQLite owner: a fill supersedes its issuer's other
/// quotes in the same transaction, the issuer's withdrawal releases a shared slot, and a real automatic
/// cut and reopening keep every carried, withdrawn and superseded identity and term exact.
/// </summary>
public sealed partial class PaidWorldExchangeTests
{
    [Fact]
    public void Filling_one_of_sixteen_quotes_supersedes_the_rest_and_frees_the_shared_book()
    {
        var genesis = WorldPaidExchangeAdmission.CreateFresh(new OrdinaryCareWorldFixture().Genesis);
        var journey = new CareRewardJourney(false, genesis);
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var state = EarnReward(journey, new CareWorldSession(authority));
        EntityId player = journey.Player, helper = journey.Helper;
        var issued = new List<SpotQuote>();
        for (int index = 0; index < WorldPaidExchangeBook.MaximumOpen; index++)
        {
            var (issue, quote) = FeelessQuote(state, helper, player, SellableRawLot(state, helper), issuerBuys: false);
            state = Submit(authority, issue, WorldActionRejection.None);
            issued.Add(quote);
        }
        Assert.False(state.Coordination!.PaidExchange!.CanIssue);
        // The bound is shared by the whole world, so another actor is refused too.
        var (blocked, _) = FeelessQuote(state, player, helper, SellableRawLot(state, helper), issuerBuys: true);
        var before = state;
        state = Submit(authority, blocked, WorldActionRejection.Unavailable);
        AssertOwnersUnchanged(before, state);

        before = state;
        SpotQuote target = issued[5];
        var accept = AcceptCommand(state, player, target, helper, 1, 1);
        state = Submit(authority, accept, WorldActionRejection.None);
        var book = state.Coordination!.PaidExchange!;
        Assert.Empty(book.Open);
        Assert.Equal(1UL, book.GetIssuerRevision(helper));
        WorldPaidQuoteOutcome fill = Assert.Single(book.Fills);
        WorldPaidQuoteOutcome[] superseded = book.Outcomes.Where(value => value.Kind == WorldPaidQuoteOutcomeKind.Superseded).ToArray();
        Assert.Equal(WorldPaidExchangeBook.MaximumOpen, book.Outcomes.Count);
        Assert.Equal(WorldPaidExchangeBook.MaximumOpen - 1, superseded.Length);
        // Exact terms, the actual superseding acceptance and the same published step.
        Assert.Equal(before.Coordination!.PaidExchange!.Open.Where(value => value.QuoteId != target.QuoteId)
            .Select(value => value.ContentHash), superseded.Select(value => value.Quote.ContentHash));
        Assert.All(superseded, value =>
        {
            Assert.Equal(fill.WorldSequence, value.WorldSequence);
            Assert.Equal(accept.CommandId, value.Supersession!.AcceptanceId);
            Assert.Equal(target.QuoteId, value.Supersession.FilledQuoteId);
        });
        // Siblings close without money, goods, actor or extra revision effects: only the one fill paid.
        CurrencyLedger paidBefore = before.Progression!.Currency, paidAfter = state.Progression!.Currency;
        Assert.Equal(paidBefore.Revision + 1, paidAfter.Revision);
        Assert.Equal(before.Progression.Revision + 1, state.Progression.Revision);
        Assert.Equal(paidBefore.AdmittedSupply, paidAfter.AdmittedSupply);
        Assert.Equal(paidBefore.GetActorAccount(player).Balance - 1, paidAfter.GetActorAccount(player).Balance);
        Assert.Equal(paidBefore.GetActorAccount(helper).Balance + 1, paidAfter.GetActorAccount(helper).Balance);
        Assert.Equal(ActorsOf(before), ActorsOf(state));
        Assert.DoesNotContain(state.ResourceHistory!.History.Batches, value => value.CommandId == accept.CommandId);
        Assert.All(issued, value => Assert.True(authority.IsIdentityOccupiedForHost(value.QuoteId)));

        // Capacity is released for another actor at once, and the issuer quotes again at its new revision.
        var (fresh, _) = FeelessQuote(state, player, helper, SellableRawLot(state, helper), issuerBuys: true);
        state = Submit(authority, fresh, WorldActionRejection.None);
        var (again, renewed) = FeelessQuote(state, helper, player, SellableRawLot(state, helper), issuerBuys: false);
        state = Submit(authority, again, WorldActionRejection.None);
        Assert.Equal(1UL, renewed.MarketRevision);
        Assert.Equal(2, state.Coordination!.PaidExchange!.Open.Count);
    }

    [Fact]
    public void An_issuer_withdraws_a_stale_stock_quote_freeing_a_slot_with_exact_retry()
    {
        var genesis = WorldPaidExchangeAdmission.CreateFresh(new OrdinaryCareWorldFixture().Genesis);
        var journey = new CareRewardJourney(false, genesis);
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var session = new CareWorldSession(authority);
        var state = EarnReward(journey, session);
        EntityId player = journey.Player, helper = journey.Helper;
        var lot = SellableRawLot(state, helper);
        var issued = new List<SpotQuote>();
        for (int index = 0; index < WorldPaidExchangeBook.MaximumOpen; index++)
        {
            var (issue, quote) = FeelessQuote(state, helper, player, lot, issuerBuys: false);
            state = Submit(authority, issue, WorldActionRejection.None);
            issued.Add(quote);
        }
        // An actual saved gift spends part of the pinned lot, so every quote on it is stale but stays open.
        var terms = new ConsentedMaterialTerms(state.WorldId, state.BranchId, helper, player,
            ConsentedMaterialLotKind.CountedSource, lot.LotId, lot.Definition, lot.CustodianId,
            state.GetResident(player).InventoryCustodianId, 1, lot.Revision);
        state = Commit(session, state, journey.Command(state, helper, new ProposeMaterialIntent(terms, state.AdvancingSteps + 100)),
            WorldActionRejection.None);
        state = Commit(session, state, journey.Command(state, player, AcceptMaterialProposalIntent.Approving(
            Assert.Single(state.Coordination!.Proposals!.Open), state.Space.GetActor(helper).Revision)), WorldActionRejection.None);
        Assert.Equal(lot.Revision + 1, state.Inventory.Custody.GetLot(lot.LotId).Revision);
        SpotQuote stale = issued[0];
        var before = state;
        state = Submit(authority, AcceptCommand(state, player, stale, helper, 1, 1), WorldActionRejection.RevisionConflict);
        AssertOwnersUnchanged(before, state);
        var (blocked, _) = FeelessQuote(state, player, helper, SellableRawLot(state, helper), issuerBuys: true);
        state = Submit(authority, blocked, WorldActionRejection.Unavailable);

        // Another actor cannot withdraw the quote and learns only the ordinary refusal.
        before = state;
        state = Submit(authority, WithdrawCommand(state, player, stale.QuoteId), WorldActionRejection.Unavailable);
        AssertOwnersUnchanged(before, state);
        // The issuer's command under another delivery is refused before anything is disclosed or published.
        var withdraw = WithdrawCommand(state, helper, stale.QuoteId);
        var playerControl = state.Inventory.Controls.Single(value => value.ActorId == player);
        Assert.Throws<AuthorityDispatchException>(() => authority.SubmitPaidExchangeCommand(withdraw,
            new TrustedCommandDelivery(playerControl.PrincipalId, playerControl.SessionId), NextId()));
        Assert.Equal(state.ContentHash, authority.ReadStateForHost().ContentHash);

        before = state;
        WorldActionReceipt receipt = authority.SubmitPaidExchangeCommand(withdraw, Delivery(withdraw), NextId());
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
        state = authority.ReadStateForHost();
        WorldPaidQuoteOutcome withdrawn = Assert.Single(state.Coordination!.PaidExchange!.Outcomes);
        Assert.Equal((WorldPaidQuoteOutcomeKind.Withdrawn, stale.ContentHash, state.Revision),
            (withdrawn.Kind, withdrawn.Quote.Quote.ContentHash, withdrawn.WorldSequence));
        Assert.Equal((withdraw.CommandId, withdraw.PrincipalId, withdraw.OriginSessionId, 0U),
            (withdrawn.Withdrawal!.CommandId, withdrawn.Withdrawal.Delivery.PrincipalId,
             withdrawn.Withdrawal.Delivery.SessionId, withdrawn.Withdrawal.ActionIndex));
        // Withdrawal pays nothing, moves nothing, advances no market revision and uses no work.
        Assert.Equal(before.Inventory.ContentHash, state.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, state.Production!.ContentHash);
        Assert.Equal(before.Progression!.ContentHash, state.Progression!.ContentHash);
        Assert.Empty(state.Coordination.PaidExchange.IssuerRevisions);
        Assert.True(state.IdentityIndex.Contains(withdraw.CommandId));

        ContentHash head = state.ContentHash;
        Assert.Equal(receipt.ResultId, authority.SubmitPaidExchangeCommand(withdraw, Delivery(withdraw), NextId()).ResultId);
        Assert.Equal(head, authority.ReadStateForHost().ContentHash);
        // A second withdrawal of the closed quote finds nothing open.
        state = Submit(authority, WithdrawCommand(state, helper, stale.QuoteId), WorldActionRejection.Unavailable);
        // The released slot serves another actor.
        var (fresh, _) = FeelessQuote(state, player, helper, SellableRawLot(state, helper), issuerBuys: true);
        state = Submit(authority, fresh, WorldActionRejection.None);
        Assert.Equal(WorldPaidExchangeBook.MaximumOpen, state.Coordination!.PaidExchange!.Open.Count);
    }

    [Fact]
    public void A_real_cut_and_reopening_keep_superseded_withdrawn_and_carried_quotes_exact()
    {
        var genesis = WorldPaidExchangeAdmission.CreateFresh(new OrdinaryCareWorldFixture().Genesis);
        var journey = new CareRewardJourney(false, genesis);
        using var files = new StorageFiles();
        SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        try
        {
            var session = new CareWorldSession(authority);
            var state = EarnReward(journey, session);
            EntityId player = journey.Player, helper = journey.Helper;
            var fills = new List<WorldActionCommand>();
            void FillRound()
            {
                var round = new List<SpotQuote>();
                for (int index = 0; index < WorldPaidExchangeBook.MaximumOpen; index++)
                {
                    var (issue, quote) = FeelessQuote(state, helper, player, SellableRawLot(state, helper), issuerBuys: false);
                    state = Submit(authority, issue, WorldActionRejection.None);
                    round.Add(quote);
                }
                var accept = AcceptCommand(state, player, round[0], helper, 1, 1);
                state = Submit(authority, accept, WorldActionRejection.None);
                fills.Add(accept);
            }
            FillRound();
            FillRound();
            var (cancelled, cancelledQuote) = FeelessQuote(state, helper, player, SellableRawLot(state, helper), issuerBuys: false);
            state = Submit(authority, cancelled, WorldActionRejection.None);
            var withdraw = WithdrawCommand(state, helper, cancelledQuote.QuoteId);
            WorldActionReceipt withdrawn = authority.SubmitPaidExchangeCommand(withdraw, Delivery(withdraw), NextId());
            Assert.Equal(WorldActionRejection.None, withdrawn.Rejection);
            state = authority.ReadStateForHost();
            FillRound();
            Assert.Null(state.Coordination!.Anchor);
            Assert.Equal(49, state.Coordination.PaidExchange!.Outcomes.Count);

            // Issue until the actual automatic boundary: every open quote keeps one reserved outcome slot.
            WorldSnapshot? closing = null;
            for (int index = 0; closing is null; index++)
            {
                Assert.True(index < WorldPaidExchangeBook.MaximumOpen);
                var (issue, _) = FeelessQuote(state, helper, player, SellableRawLot(state, helper), issuerBuys: false);
                WorldStepDecision decision = session.Commit(journey.Batch(state, WorldBatchKind.AdvancingStep, [issue]));
                Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
                if (decision.SealedMaterialCareSegment is { } sealedSegment)
                {
                    Assert.Equal(state.ContentHash, sealedSegment.Reference.BoundaryHash);
                    closing = state;
                }
                state = decision.Candidate;
            }
            WorldSnapshot closingRoot = closing!;
            WorldPaidExchangeBook closed = closingRoot.Coordination!.PaidExchange!;
            Assert.Equal((3, 45, 1), (closed.Fills.Count(),
                closed.Outcomes.Count(value => value.Kind == WorldPaidQuoteOutcomeKind.Superseded),
                closed.Outcomes.Count(value => value.Kind == WorldPaidQuoteOutcomeKind.Withdrawn)));
            Assert.Equal(14, closed.Open.Count);
            WorldPaidExchangeBook carried = state.Coordination!.PaidExchange!;
            Assert.NotNull(state.Coordination.Anchor);
            Assert.Empty(carried.Outcomes);
            Assert.All(closed.Open, value => Assert.Equal(value.ContentHash, carried.FindOpen(value.QuoteId)!.ContentHash));
            Assert.Equal(1UL, authority.InspectForHost().Journal.MaterialCareArchiveCount);

            ContentHash head = state.ContentHash;
            authority.Dispose();
            authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
            session = new CareWorldSession(authority);
            state = authority.ReadStateForHost();
            Assert.Equal(head, state.ContentHash);
            var archived = closed.Outcomes.Where(value => value.Kind != WorldPaidQuoteOutcomeKind.Filled).Select(value => value.Quote.QuoteId)
                .Concat(fills.Select(value => value.CommandId)).Append(withdraw.CommandId).ToArray();
            Assert.All(archived, value => Assert.True(authority.IsIdentityOccupiedForHost(value)));
            Assert.Equal(WorldReceiptCodecV1.Encode(withdrawn),
                WorldReceiptCodecV1.Encode(authority.SubmitPaidExchangeCommand(withdraw, Delivery(withdraw), NextId())));
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);

            // After the boundary the issuer cancels one carried quote, then one fill supersedes the rest.
            WorldPaidQuote[] open = carried.Open.ToArray();
            WorldPaidQuote cancelledCarried = closed.Open[0], boughtCarried = closed.Open[1];
            var lateWithdraw = WithdrawCommand(state, helper, cancelledCarried.QuoteId);
            state = Submit(authority, lateWithdraw, WorldActionRejection.None);
            var lateAccept = AcceptCommand(state, player, boughtCarried.Quote, helper, 1, 1);
            state = Submit(authority, lateAccept, WorldActionRejection.None);
            WorldPaidExchangeBook successor = state.Coordination!.PaidExchange!;
            Assert.Empty(successor.Open);
            Assert.Equal(open.Length, successor.Outcomes.Count);
            Assert.All(open, value => Assert.Equal(value.ContentHash, successor.FindOutcome(value.QuoteId)!.Quote.ContentHash));
            Assert.Equal(lateWithdraw.CommandId, successor.FindOutcome(cancelledCarried.QuoteId)!.Withdrawal!.CommandId);
            Assert.All(successor.Outcomes.Where(value => value.Kind == WorldPaidQuoteOutcomeKind.Superseded), value =>
            {
                Assert.Equal(lateAccept.CommandId, value.Supersession!.AcceptanceId);
                Assert.True(value.WorldSequence > closingRoot.Revision);
            });
            Assert.Equal(4UL, successor.GetIssuerRevision(helper));

            ContentHash final = state.ContentHash;
            authority.Dispose();
            authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
            Assert.Equal(final, authority.ReadStateForHost().ContentHash);
        }
        finally { authority.Dispose(); }
    }

    private (WorldActionCommand Command, SpotQuote Quote) FeelessQuote(WorldSnapshot state, EntityId issuer, EntityId other,
        CountedSourceLot lot, bool issuerBuys)
    {
        EntityId seller = issuerBuys ? other : issuer, buyer = issuerBuys ? issuer : other;
        var control = state.Inventory.Controls.Single(value => value.ActorId == issuer);
        CurrencyLedger ledger = state.Progression!.Currency;
        EntityId commandId = NextId();
        ulong step = state.AdvancingSteps + 1;
        var quote = new SpotQuote(WorldPaidQuote.DeriveIdentity(commandId), state.WorldId, state.BranchId, issuer,
            control.PrincipalId, control.SessionId, other, issuerBuys ? SpotQuoteSide.IssuerBuys : SpotQuoteSide.IssuerSells,
            SpotLotKind.CountedSource, lot.LotId, lot.Definition, lot.Revision, lot.CustodianId,
            state.GetResident(buyer).InventoryCustodianId, ledger.Currency.DefinitionId, ledger.Currency.Version,
            ledger.GetActorAccount(seller).AccountId, ledger.GetActorAccount(buyer).AccountId, 1, [], 1,
            state.Coordination!.PaidExchange!.GetIssuerRevision(issuer), step, step + 1024);
        return (new WorldActionCommand(commandId, control.PrincipalId, control.SessionId, issuer, state.WorldId,
            state.BranchId, state.Space.GetActor(issuer).Revision, new IssueSpotQuoteIntent(quote)), quote);
    }

    private WorldActionCommand WithdrawCommand(WorldSnapshot state, EntityId actor, EntityId quoteId)
    {
        var control = state.Inventory.Controls.Single(value => value.ActorId == actor);
        return new WorldActionCommand(NextId(), control.PrincipalId, control.SessionId, actor, state.WorldId,
            state.BranchId, state.Space.GetActor(actor).Revision, new WithdrawSpotQuoteIntent(quoteId));
    }

    private WorldSnapshot Submit(SqliteWorldAuthority authority, WorldActionCommand command, WorldActionRejection expected)
    {
        Assert.Equal(expected, authority.SubmitPaidExchangeCommand(command, Delivery(command), NextId()).Rejection);
        WorldSnapshot state = authority.ReadStateForHost();
        WorldPaidExchangeBook book = state.Coordination!.PaidExchange!;
        // After every published step the cut reservation still holds one outcome slot per open quote.
        Assert.InRange(book.Outcomes.Count + book.Open.Count, 0, WorldPaidExchangeBook.MaximumOutcomes - 1);
        return state;
    }

    private static object[] ActorsOf(WorldSnapshot state) => state.Progression!.Actors.Select(value =>
        (object)(value.ActorId, value.Revision, value.Level, value.Experience, value.CurrentHp, value.CurrentMp, value.Stats)).ToArray();
}
