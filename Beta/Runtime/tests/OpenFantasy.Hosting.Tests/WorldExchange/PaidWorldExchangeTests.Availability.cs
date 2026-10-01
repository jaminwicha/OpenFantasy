using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldExchange;

/// <summary>
/// Settlement availability through actual battle, defeat, care and recovery in the real SQLite owner:
/// a handless animal, a slain animal and a recovering issuer cannot settle; whole-step refusals during
/// battle, defeat and pending care publish nothing; the same terms settle once both people are ready.
/// </summary>
public sealed partial class PaidWorldExchangeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_handless_then_slain_wolf_and_recovering_people_cannot_settle_until_ready(bool recoveringCaller)
    {
        var genesis = WorldPaidExchangeAdmission.CreateFresh(new OrdinaryCareWorldFixture().Genesis);
        var journey = new CareRewardJourney(false, genesis);
        using var files = new StorageFiles();
        SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        try
        {
            var session = new CareWorldSession(authority);
            var state = EarnReward(journey, session);
            EntityId player = journey.Player, helper = journey.Helper, wolf = journey.First.TargetActorId;
            // Earned quest funds pay an actual third-party fee into the wolf's admitted empty wallet.
            EntityId wolfWallet = state.Progression!.Currency.GetActorAccount(wolf).AccountId;
            Assert.Equal(0UL, state.Progression.Currency.GetAccount(wolfWallet).Balance);
            var (funding, fundingQuote) = IssueCommand(state, helper, player, SellableRawLot(state, helper), 1, wolfWallet, 1);
            state = Submit(authority, funding, WorldActionRejection.None);
            var bought = AcceptCommand(state, player, fundingQuote, helper, 1, 1);
            state = Submit(authority, bought, WorldActionRejection.None);
            Assert.Equal(1UL, state.Progression!.Currency.GetAccount(wolfWallet).Balance);
            CountedSourceLot owned = state.Inventory.Custody.GetLot(WorldPaidQuote.SplitChildOf(bought.CommandId));
            Assert.Equal((state.GetResident(player).InventoryCustodianId, 1UL), (owned.CustodianId, owned.Quantity));

            // The player offers its unit to the wolf, the wolf bids for it, and the player bids for helper stock.
            var (toWolf, sellToWolf) = FeelessQuote(state, player, wolf, owned, issuerBuys: false);
            state = Submit(authority, toWolf, WorldActionRejection.None);
            var (byWolf, wolfBuys) = FeelessQuote(state, wolf, player, owned, issuerBuys: true);
            state = Submit(authority, byWolf, WorldActionRejection.None);
            var (byPlayer, playerBuys) = FeelessQuote(state, player, helper, SellableRawLot(state, helper), issuerBuys: true);
            state = Submit(authority, byPlayer, WorldActionRejection.None);
            var (byHelper, helperSells) = FeelessQuote(state, helper, player, SellableRawLot(state, helper), issuerBuys: false);
            state = Submit(authority, byHelper, WorldActionRejection.None);

            // A living, ready, reachable, funded wolf with free storage lacks only hands.
            EntityId wolfBag = state.GetResident(wolf).InventoryCustodianId;
            Assert.True(WorldActorAvailability.CanAct(state, wolf));
            Assert.True(WorldActorAvailability.CanAct(state, player));
            Assert.Equal(SpatialInteraction.Reachable, state.Space.QueryInteraction(player, wolf));
            Assert.Equal(SpatialInteraction.Reachable, state.Space.QueryInteraction(wolf, player));
            Assert.False(state.Space.GetActor(wolf).Condition.SupportsFunction(BodyFunction.Manipulation));
            Assert.True(state.Space.GetActor(player).Condition.SupportsFunction(BodyFunction.Manipulation));
            Assert.DoesNotContain(state.Inventory.Custody.Lots, value => value.CustodianId == wolfBag);
            Assert.True(state.Inventory.Custody.GetStorage(wolfBag).UnitCapacity >= 1);
            var refused = new List<EntityId>();
            WorldSnapshot Refuse(WorldSnapshot current, WorldActionCommand command)
            {
                WorldSnapshot after = Submit(authority, command, WorldActionRejection.Unavailable);
                AssertOwnersUnchanged(current, after);
                refused.Add(command.CommandId);
                return after;
            }
            // The handless wolf can settle neither as the counterparty nor as the issuer.
            state = Refuse(state, AcceptCommand(state, wolf, sellToWolf, player, 1, 1));
            state = Refuse(state, AcceptCommand(state, player, wolfBuys, wolf, 1, 1));

            // Actual battle: an advancing step cannot be selected while the encounter owns time.
            EnterFirstBattle(authority, journey);
            WorldSnapshot fighting = authority.ReadStateForHost();
            Assert.NotNull(fighting.Pacing!.Encounter);
            var blocked = new List<EntityId>
            {
                AssertWholeStepRefused(authority, AcceptCommand(fighting, helper, playerBuys, player, 1, 1)),
                AssertWholeStepRefused(authority, AcceptCommand(fighting, player, wolfBuys, wolf, 1, 1))
            };
            state = journey.WinAndRelease(session);

            // Released exploration: the slain wolf and the player's actual recovery debt remain.
            Assert.Equal(EncounterLifecyclePhase.Exploration, state.EncounterLifecycle!.Phase);
            Assert.Equal(0, state.Progression!.GetActor(wolf).CurrentHp);
            Assert.True(state.EncounterLifecycle.GetDebt(player).RemainingDelay > 0);
            Assert.False(WorldActorAvailability.CanAct(state, player));
            Assert.True(WorldActorAvailability.CanAct(state, helper));
            Assert.Equal(SpatialInteraction.Reachable, state.Space.QueryInteraction(helper, player));
            Assert.Equal(SpatialInteraction.Reachable, state.Space.QueryInteraction(player, helper));
            // Separate actual journeys test each readiness gate at the same pre-step boundary.
            // A refused command still advances time, so it can retire this final recovery delay.
            if (recoveringCaller)
            {
                Assert.True(state.Progression!.GetActor(player).CurrentHp > 0);
                Assert.True(state.Progression.GetActor(helper).CurrentHp > 0);
                Assert.True(state.Progression.Currency.GetActorAccount(player).Balance >= 1);
                Assert.Equal(helperSells.ExpectedLotRevision, state.Inventory.Custody.GetLot(helperSells.LotId).Revision);
                state = Refuse(state, AcceptCommand(state, player, helperSells, helper, 1, 1));
            }
            else
            {
                // The ready helper cannot settle with the not-ready issuer.
                state = Refuse(state, AcceptCommand(state, helper, playerBuys, player, 1, 1));
            }
            // The slain wolf can neither settle as the counterparty nor withdraw its own quote.
            state = Refuse(state, AcceptCommand(state, wolf, sellToWolf, player, 1, 1));
            state = Refuse(state, WithdrawCommand(state, wolf, wolfBuys.QuoteId));
            for (int step = 0; !WorldActorAvailability.CanAct(state, player); step++)
            {
                Assert.True(step < 32, "Actual exploration steps must retire the retained recovery debt.");
                state = CareRewardJourney.Commit(session, journey.Batch(state, WorldBatchKind.AdvancingStep)).Candidate;
            }
            // The ready player cannot settle with the slain issuer, which still holds its funded bid.
            Assert.Equal(0, state.Progression!.GetActor(wolf).CurrentHp);
            Assert.Equal(1UL, state.Progression.Currency.GetAccount(wolfWallet).Balance);
            state = Refuse(state, AcceptCommand(state, player, wolfBuys, wolf, 1, 1));

            // Both people are ready: the same fixed terms settle once, and the issuer's sibling is superseded.
            Assert.True(WorldActorAvailability.CanAct(state, helper));
            CurrencyLedger ledger = state.Progression!.Currency;
            var recovered = AcceptCommand(state, helper, playerBuys, player, 1, 1);
            state = Submit(authority, recovered, WorldActionRejection.None);
            WorldPaidExchangeBook book = state.Coordination!.PaidExchange!;
            Assert.Equal(WorldPaidQuoteOutcomeKind.Filled, book.FindOutcome(playerBuys.QuoteId)!.Kind);
            WorldPaidQuoteOutcome sibling = book.FindOutcome(sellToWolf.QuoteId)!;
            Assert.Equal((WorldPaidQuoteOutcomeKind.Superseded, recovered.CommandId),
                (sibling.Kind, sibling.Supersession!.AcceptanceId));
            Assert.Equal(wolfBuys.ContentHash, book.FindOpen(wolfBuys.QuoteId)!.Quote.ContentHash);
            Assert.Equal((1UL, 0UL), (book.GetIssuerRevision(player), book.GetIssuerRevision(wolf)));
            Assert.Equal((ledger.Revision + 1, ledger.AdmittedSupply),
                (state.Progression!.Currency.Revision, state.Progression.Currency.AdmittedSupply));
            Assert.Equal(ledger.GetActorAccount(player).Balance - 1, state.Progression.Currency.GetActorAccount(player).Balance);
            Assert.Equal(ledger.GetActorAccount(helper).Balance + 1, state.Progression.Currency.GetActorAccount(helper).Balance);
            Assert.Equal(1UL, state.Progression.Currency.GetAccount(wolfWallet).Balance);
            Assert.Equal(state.GetResident(player).InventoryCustodianId,
                state.Inventory.Custody.GetLot(WorldPaidQuote.SplitChildOf(recovered.CommandId)).CustodianId);

            ContentHash head = state.ContentHash;
            authority.Dispose();
            authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);
            Assert.NotNull(authority.ReadStateForHost().Coordination!.PaidExchange!.FindOpen(wolfBuys.QuoteId));
            Assert.All(refused.Append(recovered.CommandId), value => Assert.True(authority.IsIdentityOccupiedForHost(value)));
            Assert.All(blocked, value => Assert.False(authority.IsIdentityOccupiedForHost(value)));
        }
        finally { authority.Dispose(); }
    }

    [Fact]
    public void Battle_defeat_and_pending_care_refuse_whole_steps_then_the_treated_issuer_settles_its_terms()
    {
        var genesis = WorldPaidExchangeAdmission.CreateFresh(new ConsentedWorldPublicationTests.DurableAccess().OrdinaryGenesis);
        var journey = new OrdinaryCareWorldFixture(genesis).Journey;
        using var files = new StorageFiles();
        SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        try
        {
            var session = new CareWorldSession(authority);
            var state = EarnReward(journey, session);
            EntityId player = journey.Player, helper = journey.Helper;
            // Pin helper stock other than the remedy, so the actual treatment cannot make the terms stale.
            var remedy = state.Care!.Remedies.Single().Item;
            EntityId helperBag = state.GetResident(helper).InventoryCustodianId;
            CountedSourceLot stock = state.Inventory.Custody.Lots.First(value => value.CustodianId == helperBag
                && value.Quantity > 1 && value.ReservedBy is null && value.Protection == LotProtection.None
                && value.Definition != remedy);
            var (issue, quote) = FeelessQuote(state, player, helper, stock, issuerBuys: true);
            state = Submit(authority, issue, WorldActionRejection.None);
            WorldSnapshot issued = state;
            WorldActionCommand Settle(WorldSnapshot live) => AcceptCommand(live, helper, quote, player, 1, 1);

            var blocked = new List<EntityId>();
            WorldSnapshot defeated = journey.PrepareDefeat(session, live => blocked.Add(AssertWholeStepRefused(authority, Settle(live))));
            Assert.Equal(EncounterLifecyclePhase.Outcome, defeated.EncounterLifecycle!.Phase);
            Assert.Equal(0, defeated.Progression!.GetActor(player).CurrentHp);
            blocked.Add(AssertWholeStepRefused(authority, Settle(defeated)));
            WorldSnapshot pending = CareRewardJourney.Commit(session, journey.Batch(defeated, WorldBatchKind.Care,
                [journey.Acknowledge(defeated)])).Candidate;
            Assert.NotNull(pending.Care!.Pending);
            blocked.Add(AssertWholeStepRefused(authority, Settle(pending)));
            WorldSnapshot treated = CareRewardJourney.Commit(session, journey.Batch(pending, WorldBatchKind.Care,
                [journey.Treat(pending)])).Candidate;
            Assert.True(treated.Progression!.GetActor(player).CurrentHp > 0);
            state = journey.WinAndRelease(session);
            for (int step = 0; !WorldActorAvailability.CanAct(state, player) || !WorldActorAvailability.CanAct(state, helper); step++)
            {
                Assert.True(step < 32, "Actual exploration steps must retire the retained encounter debts.");
                state = CareRewardJourney.Commit(session, journey.Batch(state, WorldBatchKind.AdvancingStep)).Candidate;
            }

            // Battle, defeat and care changed neither the quote book nor any balance.
            Assert.Equal(issued.Coordination!.PaidExchange!.ContentHash, state.Coordination!.PaidExchange!.ContentHash);
            CurrencyLedger ledger = state.Progression!.Currency;
            Assert.Equal(issued.Progression!.Currency.Revision, ledger.Revision);
            Assert.Equal(issued.Progression.Currency.Accounts.Select(value => (value.AccountId, value.Balance, value.Revision)),
                ledger.Accounts.Select(value => (value.AccountId, value.Balance, value.Revision)));
            Assert.Equal(stock.Revision, state.Inventory.Custody.GetLot(stock.LotId).Revision);

            var settle = Settle(state);
            state = Submit(authority, settle, WorldActionRejection.None);
            Assert.Equal(WorldPaidQuoteOutcomeKind.Filled, state.Coordination!.PaidExchange!.FindOutcome(quote.QuoteId)!.Kind);
            Assert.Equal(ledger.GetActorAccount(player).Balance - 1, state.Progression!.Currency.GetActorAccount(player).Balance);
            Assert.Equal(ledger.GetActorAccount(helper).Balance + 1, state.Progression.Currency.GetActorAccount(helper).Balance);
            Assert.Equal(ledger.AdmittedSupply, state.Progression.Currency.AdmittedSupply);

            ContentHash head = state.ContentHash;
            authority.Dispose();
            authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);
            Assert.Equal(3, blocked.Count);
            Assert.All(blocked, value => Assert.False(authority.IsIdentityOccupiedForHost(value)));
            Assert.True(authority.IsIdentityOccupiedForHost(settle.CommandId));
        }
        finally { authority.Dispose(); }
    }

    /// <summary>The trusted single-command submission cannot even select its step; nothing is published or reserved.</summary>
    private EntityId AssertWholeStepRefused(SqliteWorldAuthority authority, WorldActionCommand command)
    {
        ContentHash head = authority.ReadStateForHost().ContentHash;
        Assert.Throws<AuthorityDispatchException>(() => authority.SubmitPaidExchangeCommand(command, Delivery(command), NextId()));
        Assert.Equal(head, authority.ReadStateForHost().ContentHash);
        Assert.False(authority.IsIdentityOccupiedForHost(command.CommandId));
        return command.CommandId;
    }

    private static void EnterFirstBattle(SqliteWorldAuthority authority, CareRewardJourney journey)
    {
        WorldSnapshot state = authority.ReadStateForHost();
        EncounterScenarioDefinition scenario = journey.First;
        WorldActionCommand enter = journey.Command(state, journey.Player, new EnterEncounterIntent(scenario.DefinitionId,
            EncounterEpisodeIdentity.Derive(state.EncounterLifecycle!, scenario.DefinitionId),
            state.EncounterLifecycle!.EncounterCount, state.Space.GetActor(scenario.TargetActorId).Revision));
        Assert.Equal(WorldActionRejection.None, Assert.Single(authority.Commit(
            journey.Batch(state, WorldBatchKind.EncounterLifecycle, [enter])).Receipts).Rejection);
        for (int stage = 0; stage < 2; stage++)
        {
            authority.Commit(journey.Batch(authority.ReadStateForHost(), WorldBatchKind.EncounterLifecycle));
        }
        Assert.Equal(EncounterLifecyclePhase.Active, authority.ReadStateForHost().EncounterLifecycle!.Phase);
    }
}
