using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldExchange;

/// <summary>
/// Paid spot exchange as actual complete-world actions of the explicit profile-32 world: earned quest
/// funding, quote issuance and acceptance through the real step processor and SQLite owner, exact
/// conservation, refusals that preserve every owner, retry, reopening and old-profile refusal.
/// </summary>
public sealed partial class PaidWorldExchangeTests
{
    private ulong _identity = 0x7000;

    [Fact]
    public void Earned_funds_buy_a_partial_raw_lot_and_survive_retry_and_reopen()
    {
        var ordinary = new OrdinaryCareWorldFixture();
        WorldSnapshot genesis = WorldPaidExchangeAdmission.CreateFresh(ordinary.Genesis);
        Assert.Equal(32, genesis.ProfileVersion);
        var journey = new CareRewardJourney(false, genesis);
        using var files = new StorageFiles();
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        try
        {
            var session = new CareWorldSession(authority);
            WorldSnapshot state = EarnReward(journey, session);
            EntityId player = journey.Player, helper = journey.Helper;
            CurrencyLedger before = state.Progression!.Currency;
            ulong buyerBefore = before.GetActorAccount(player).Balance;
            Assert.True(buyerBefore >= 4, "The quest reward, not the test, funds the purchase.");

            CountedSourceLot lot = SellableRawLot(state, helper);
            CurrencyAccount reserve = before.Accounts.First(value => value.Kind == CurrencyAccountKind.Reserve);
            (WorldActionCommand issue, SpotQuote quote) = IssueCommand(state, helper, player, lot, 3, reserve.AccountId, 2);
            Assert.Equal(WorldActionRejection.None, authority.SubmitPaidExchangeCommand(issue,
                Delivery(issue), NextId()).Rejection);
            state = authority.ReadStateForHost();
            Assert.Single(state.Coordination!.PaidExchange!.Open);
            Assert.Equal(before.Revision, state.Progression!.Currency.Revision); // Issuance moves no money.

            // Stale restated totals are refused without any owner change except the recorded step.
            WorldActionCommand stale = AcceptCommand(state, player, quote, helper, 1, payment: 2);
            ContentHash inventoryBefore = state.Inventory.ContentHash;
            Assert.Equal(WorldActionRejection.RevisionConflict,
                authority.SubmitPaidExchangeCommand(stale, Delivery(stale), NextId()).Rejection);
            state = authority.ReadStateForHost();
            Assert.Equal(inventoryBefore, state.Inventory.ContentHash);
            Assert.Equal(before.Revision, state.Progression!.Currency.Revision);

            ulong progressionBefore = state.Progression.Revision;
            WorldActionCommand accept = AcceptCommand(state, player, quote, helper, 1, payment: 3);
            WorldActionReceipt settled = authority.SubmitPaidExchangeCommand(accept, Delivery(accept), NextId());
            Assert.Equal(WorldActionRejection.None, settled.Rejection);
            state = authority.ReadStateForHost();
            CurrencyLedger after = state.Progression!.Currency;
            Assert.Equal(before.AdmittedSupply, after.AdmittedSupply);
            Assert.Equal(buyerBefore - 4, after.GetActorAccount(player).Balance);
            Assert.Equal(before.GetActorAccount(helper).Balance + 3, after.GetActorAccount(helper).Balance);
            Assert.Equal(reserve.Balance + 1, after.GetAccount(reserve.AccountId).Balance);
            Assert.Equal(before.Revision + 1, after.Revision);
            Assert.Equal(progressionBefore + 1, state.Progression.Revision);
            // Currency-only trade invents no actor resource batch.
            Assert.DoesNotContain(state.ResourceHistory!.History.Batches, value => value.CommandId == accept.CommandId);
            EntityId child = WorldPaidQuote.SplitChildOf(accept.CommandId);
            CountedSourceLot moved = state.Inventory.Custody.GetLot(child);
            Assert.Equal(state.GetResident(player).InventoryCustodianId, moved.CustodianId);
            Assert.Equal(1UL, moved.Quantity);
            Assert.Equal(lot.Source.SourceId, moved.Source.SourceId);
            Assert.Equal(lot.Revision + 1, state.Inventory.Custody.GetLot(lot.LotId).Revision);
            WorldPaidQuoteOutcome outcome = Assert.Single(state.Coordination!.PaidExchange!.Outcomes);
            Assert.Equal(WorldPaidQuoteOutcomeKind.Filled, outcome.Kind);
            Assert.Equal(1UL, state.Coordination.PaidExchange.GetIssuerRevision(helper));

            // An exact durable retry returns the original receipt and transfers nothing again.
            ContentHash settledHash = state.ContentHash;
            WorldActionReceipt retried = authority.SubmitPaidExchangeCommand(accept, Delivery(accept), NextId());
            Assert.Equal(settled.ResultId, retried.ResultId);
            Assert.Equal(settledHash, authority.ReadStateForHost().ContentHash);

            // A second acceptance of the single-fill quote finds nothing open.
            WorldActionCommand again = AcceptCommand(authority.ReadStateForHost(), player, quote, helper, 1, payment: 3);
            Assert.Equal(WorldActionRejection.Unavailable,
                authority.SubmitPaidExchangeCommand(again, Delivery(again), NextId()).Rejection);
            ContentHash head = authority.ReadStateForHost().ContentHash;

            authority.Dispose();
            authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);
            Assert.True(authority.IsIdentityOccupiedForHost(quote.QuoteId));
            Assert.True(authority.IsIdentityOccupiedForHost(child));
            Assert.Equal(settled.ResultId, authority.SubmitPaidExchangeCommand(accept, Delivery(accept), NextId()).ResultId);
        }
        finally
        {
            authority.Dispose();
        }
    }

    [Fact]
    public void Reachable_unfunded_and_expired_quotes_change_no_owner()
    {
        var ordinary = new OrdinaryCareWorldFixture();
        WorldSnapshot genesis = WorldPaidExchangeAdmission.CreateFresh(ordinary.Genesis);
        var journey = new CareRewardJourney(false, genesis);
        var session = new CareWorldSession(genesis);
        WorldSnapshot state = session.ReadStateForHost();
        EntityId player = journey.Player, helper = journey.Helper;
        CountedSourceLot lot = SellableRawLot(state, helper);
        CurrencyAccount reserve = state.Progression!.Currency.Accounts.First(value => value.Kind == CurrencyAccountKind.Reserve);
        (WorldActionCommand issue, SpotQuote quote) = IssueCommand(state, helper, player, lot, 5, reserve.AccountId, 1,
            lifetime: 3);
        state = Commit(session, state, issue, WorldActionRejection.None);

        // Establish the actual refusal boundary instead of assuming this source layout is distant.
        Assert.True(WorldActorAvailability.CanAct(state, player));
        Assert.True(WorldActorAvailability.CanAct(state, helper));
        Assert.Equal(SpatialInteraction.Reachable, state.Space.QueryInteraction(player, helper));
        Assert.Equal(SpatialInteraction.Reachable, state.Space.QueryInteraction(helper, player));
        Assert.Equal(0UL, state.Progression!.Currency.GetActorAccount(player).Balance);
        WorldSnapshot distant = Commit(session, state, AcceptCommand(state, player, quote, helper, 1, payment: 5),
            WorldActionRejection.Unavailable);
        AssertOwnersUnchanged(state, distant);
        state = distant;

        // The fixed-world boundary closes the due quote; a later acceptance finds nothing.
        for (int step = 0; state.Coordination!.PaidExchange!.Open.Count != 0; step++)
        {
            Assert.True(step < 8);
            state = session.Commit(journey.Batch(state, WorldBatchKind.AdvancingStep)).Candidate;
        }
        Assert.Equal(WorldPaidQuoteOutcomeKind.Expired, Assert.Single(state.Coordination!.PaidExchange!.Outcomes).Kind);
        Assert.Equal(quote.ExpiresAtStep, state.AdvancingSteps);
        state = Commit(session, state, AcceptCommand(state, player, quote, helper, 1, payment: 5), WorldActionRejection.Unavailable);

        // Beside the issuer, the unearned player still holds nothing, so payment cannot be funded.
        journey.Approach(session);
        state = session.ReadStateForHost();
        lot = SellableRawLot(state, helper);
        (issue, quote) = IssueCommand(state, helper, player, lot, 5, reserve.AccountId, 1);
        state = Commit(session, state, issue, WorldActionRejection.None);
        Assert.Equal(0UL, state.Progression!.Currency.GetActorAccount(player).Balance);
        WorldSnapshot unfunded = Commit(session, state, AcceptCommand(state, player, quote, helper, 1, payment: 5),
            WorldActionRejection.Unavailable);
        AssertOwnersUnchanged(state, unfunded);
    }

    [Fact]
    public void Paid_intents_and_bytes_are_refused_outside_the_paid_profile()
    {
        var ordinary = new OrdinaryCareWorldFixture();
        WorldSnapshot proposals = WorldMaterialProposalAdmission.CreateFresh(ordinary.Genesis);
        Assert.Equal(30, proposals.ProfileVersion);
        CountedSourceLot lot = SellableRawLot(proposals, new CareRewardJourney(false, proposals).Helper);
        var journey = new CareRewardJourney(false, proposals);
        CurrencyAccount reserve = proposals.Progression!.Currency.Accounts.First(value => value.Kind == CurrencyAccountKind.Reserve);
        (WorldActionCommand issue, _) = IssueCommand(proposals, journey.Helper, journey.Player, lot, 1, reserve.AccountId, 1);
        Assert.Throws<ArgumentException>(() => new WorldStepBatch(NextId(), proposals.WorldId, proposals.BranchId, 0, 0,
            WorldBatchKind.AdvancingStep, [], [issue], 30));

        // Canonical paid bytes: exact roundtrip, and a trailing byte is refused.
        WorldSnapshot paid = WorldPaidExchangeAdmission.CreateFresh(ordinary.Genesis);
        byte[] bytes = WorldSnapshotCodec.Encode(paid);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode([.. bytes, 0]));
        byte[] command = WorldActionCodec.Encode(issue);
        Assert.Equal(command, WorldActionCodec.Encode(WorldActionCodec.Decode(command)));
        Assert.ThrowsAny<FormatException>(() => WorldActionCodec.Decode([.. command, 0]));
        Assert.NotEqual(paid.AdmissionVersion, proposals.AdmissionVersion);
    }

    private WorldSnapshot EarnReward(CareRewardJourney journey, CareWorldSession session)
    {
        journey.Approach(session);
        WorldActionCommand reward = journey.PrepareReward(session);
        WorldSnapshot state = session.ReadStateForHost();
        state = CareRewardJourney.Commit(session, journey.Batch(state, WorldBatchKind.HeldInteraction, [reward])).Candidate;
        state = CareRewardJourney.Commit(session, journey.Batch(state, WorldBatchKind.HeldInteraction,
            [journey.Command(state, journey.Player, new CloseDialogueIntent(state.Dialogues.Single().DialogueId))])).Candidate;
        return state;
    }

    private static CountedSourceLot SellableRawLot(WorldSnapshot state, EntityId seller)
    {
        EntityId bag = state.GetResident(seller).InventoryCustodianId;
        return state.Inventory.Custody.Lots.First(value => value.CustodianId == bag && value.Quantity > 1
            && value.ReservedBy is null && value.Protection == LotProtection.None);
    }

    private (WorldActionCommand Command, SpotQuote Quote) IssueCommand(WorldSnapshot state, EntityId issuer,
        EntityId counterparty, CountedSourceLot lot, ulong price, EntityId feePayee, ulong allowance, ulong lifetime = 32)
    {
        var control = state.Inventory.Controls.Single(value => value.ActorId == issuer);
        CurrencyLedger ledger = state.Progression!.Currency;
        EntityId commandId = NextId();
        ulong issuedAt = state.AdvancingSteps + 1;
        var quote = new SpotQuote(WorldPaidQuote.DeriveIdentity(commandId), state.WorldId, state.BranchId, issuer,
            control.PrincipalId, control.SessionId, counterparty, SpotQuoteSide.IssuerSells, SpotLotKind.CountedSource,
            lot.LotId, lot.Definition, lot.Revision, lot.CustodianId, state.GetResident(counterparty).InventoryCustodianId,
            ledger.Currency.DefinitionId, ledger.Currency.Version, ledger.GetActorAccount(issuer).AccountId,
            ledger.GetActorAccount(counterparty).AccountId, price, [new SpotFee(feePayee, 1)], allowance,
            state.Coordination?.PaidExchange?.GetIssuerRevision(issuer) ?? 0, issuedAt, issuedAt + lifetime);
        return (new WorldActionCommand(commandId, control.PrincipalId, control.SessionId, issuer, state.WorldId,
            state.BranchId, state.Space.GetActor(issuer).Revision, new IssueSpotQuoteIntent(quote)), quote);
    }

    private WorldActionCommand AcceptCommand(WorldSnapshot state, EntityId counterparty, SpotQuote quote, EntityId issuer,
        ulong quantity, ulong payment)
    {
        var control = state.Inventory.Controls.Single(value => value.ActorId == counterparty);
        EntityId commandId = NextId();
        var acceptance = new SpotAcceptance(commandId, control.PrincipalId, control.SessionId, quote.QuoteId,
            quote.ContentHash, quantity, payment, quote.FeesFor(quantity));
        return new WorldActionCommand(commandId, control.PrincipalId, control.SessionId, counterparty, state.WorldId,
            state.BranchId, state.Space.GetActor(counterparty).Revision,
            new AcceptSpotQuoteIntent(acceptance, state.Space.GetActor(issuer).Revision));
    }

    private WorldSnapshot Commit(CareWorldSession session, WorldSnapshot state, WorldActionCommand command,
        WorldActionRejection expected)
    {
        WorldStepDecision decision = session.Commit(new WorldStepBatch(NextId(), state.WorldId, state.BranchId,
            state.Revision, state.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [command], state.ProfileVersion));
        Assert.Equal(expected, Assert.Single(decision.Receipts).Rejection);
        return decision.Candidate;
    }

    private static void AssertOwnersUnchanged(WorldSnapshot before, WorldSnapshot after)
    {
        Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
        Assert.Equal(before.Progression!.ContentHash, after.Progression!.ContentHash);
        Assert.Equal(before.Coordination!.PaidExchange!.ContentHash, after.Coordination!.PaidExchange!.ContentHash);
        Assert.Equal(before.Revision + 1, after.Revision);
    }

    private static TrustedCommandDelivery Delivery(WorldActionCommand command) => new(command.PrincipalId, command.OriginSessionId);

    private EntityId NextId() => new(0x9A1D_0000, ++_identity);
}
