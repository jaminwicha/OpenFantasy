using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.CoPlayerTrade;

public sealed class CoPlayerTradeJournalTests
{
    [Fact]
    public void Actual_delegated_gifts_and_paid_fills_keep_true_operators_after_revocation_archive_copy_and_full_replay()
    {
        var fixture = new CoPlayerTradeFixture();
        using var files = new StorageFiles();
        using var copy = new StorageFiles();
        var genesis = WorldSnapshotCodec.Encode(fixture.Genesis);
        WorldSnapshot final;
        WorldSnapshot closing;
        WorldActionCommand delegatedGift;
        WorldActionCommand delegatedGiftOffer;
        WorldActionCommand delegatedPurchase;
        WorldActionCommand delegatedSale;
        WorldMaterialProposalOutcome giftOutcome;
        WorldPaidQuoteOutcome purchaseOutcome;
        WorldPaidQuoteOutcome saleOutcome;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            var state = fixture.Prepare(authority, fund: true);
            var witness = Assert.IsType<WorldControlWitness>(state.EffectiveControl!.Find(fixture.Owner)!.Witness);
            Assert.False(state.Inventory.CanControl(fixture.Control.Delivery(fixture.Grantee), fixture.Owner));
            var money = state.Progression!.Currency;
            ulong supply = state.Inventory.AdmittedUnitCapacity;
            var grant = fixture.Offer(state, fixture.Helper, fixture.Owner, fixture.Lot(state, fixture.Helper));
            fixture.Submit(authority, grant);
            state = authority.ReadStateForHost();
            var offered = Assert.Single(state.Coordination!.Proposals!.Open);
            delegatedGift = fixture.Accept(state, offered);
            fixture.Submit(authority, delegatedGift);
            state = authority.ReadStateForHost();
            giftOutcome = Assert.Single(state.Coordination!.Proposals!.Outcomes);
            Assert.Equal(fixture.Owner, giftOutcome.Proposal.Terms.RecipientActorId);
            Assert.Equal(fixture.Control.Delivery(fixture.Grantee), giftOutcome.Settlement!.RecipientDelivery);
            Assert.Equal(witness, giftOutcome.Settlement.ControlWitness);
            Assert.Equal(state.GetResident(fixture.Owner).InventoryCustodianId,
                state.Inventory.Custody.GetLot(giftOutcome.Settlement.MovedLotId).CustodianId);
            Assert.False(new ConsentedMaterialApproval(offered.Grant, offered.SenderDelivery,
                giftOutcome.Settlement.Acceptance, giftOutcome.Settlement.RecipientDelivery).IsApprovedByBothParticipants(state.Inventory));

            state = fixture.Ready(authority);
            delegatedGiftOffer = fixture.Offer(state, fixture.Owner, fixture.Helper,
                state.Inventory.Custody.GetLot(giftOutcome.Settlement.MovedLotId));
            fixture.Submit(authority, delegatedGiftOffer);
            state = authority.ReadStateForHost();
            var returnGift = Assert.Single(state.Coordination!.Proposals!.Open);
            fixture.Submit(authority, fixture.Accept(state, returnGift));
            state = authority.ReadStateForHost();
            var returned = state.Coordination!.Proposals!.FindOutcome(returnGift.ProposalId)!;
            Assert.Equal(witness, returned.Proposal.ControlWitness);
            Assert.Equal(fixture.Control.Delivery(fixture.Grantee), returned.Proposal.SenderDelivery);
            Assert.Equal(state.GetResident(fixture.Helper).InventoryCustodianId,
                state.Inventory.Custody.GetLot(returned.Settlement!.MovedLotId).CustodianId);

            state = fixture.Ready(authority);
            var (issue, quote) = fixture.Quote(state, fixture.Helper, fixture.Owner, fixture.Lot(state, fixture.Helper));
            fixture.Submit(authority, issue);
            state = authority.ReadStateForHost();
            delegatedPurchase = fixture.Accept(state, quote);
            fixture.Submit(authority, delegatedPurchase);
            state = authority.ReadStateForHost();
            purchaseOutcome = Assert.Single(state.Coordination!.PaidExchange!.Outcomes);
            Assert.Equal(witness, purchaseOutcome.Settlement!.ControlWitness);
            Assert.Equal(fixture.Control.Delivery(fixture.Grantee), purchaseOutcome.Settlement.CounterpartyDelivery);
            Assert.Equal(money.GetActorAccount(fixture.Owner).Balance - 1, state.Progression!.Currency.GetActorAccount(fixture.Owner).Balance);
            Assert.Equal(money.GetActorAccount(fixture.Helper).Balance + 1, state.Progression.Currency.GetActorAccount(fixture.Helper).Balance);

            state = fixture.Ready(authority);
            var purchased = state.Inventory.Custody.GetLot(purchaseOutcome.Settlement.MovedLotId);
            (delegatedSale, quote) = fixture.Quote(state, fixture.Owner, fixture.Helper, purchased);
            fixture.Submit(authority, delegatedSale);
            fixture.Submit(authority, fixture.Accept(authority.ReadStateForHost(), quote));
            state = authority.ReadStateForHost();
            saleOutcome = state.Coordination!.PaidExchange!.FindOutcome(quote.QuoteId)!;
            Assert.Equal(witness, saleOutcome.Quote.ControlWitness);
            Assert.Equal(fixture.Control.Delivery(fixture.Grantee), saleOutcome.Quote.IssuerDelivery);
            Assert.Equal(money.GetActorAccount(fixture.Owner).Balance, state.Progression!.Currency.GetActorAccount(fixture.Owner).Balance);
            Assert.Equal(money.GetActorAccount(fixture.Helper).Balance, state.Progression.Currency.GetActorAccount(fixture.Helper).Balance);
            Assert.Equal(money.AdmittedSupply, state.Progression.Currency.AdmittedSupply);
            Assert.Equal(supply, state.Inventory.AdmittedUnitCapacity);
            Assert.Equal(state.GetResident(fixture.Helper).InventoryCustodianId,
                state.Inventory.Custody.GetLot(saleOutcome.Settlement!.MovedLotId).CustodianId);

            fixture.Control.Revoke(authority);
            state = authority.ReadStateForHost();
            var revision = state.Revision;
            var retry = fixture.Submit(authority, delegatedPurchase);
            Assert.Equal(purchaseOutcome.WorldSequence, retry.CommitSequence);
            Assert.Equal(revision, authority.ReadStateForHost().Revision);
            Assert.Throws<AuthorityDispatchException>(() => authority.FindReceipt(delegatedPurchase,
                fixture.Control.Delivery(fixture.Owner)));
            Assert.Throws<AuthorityDispatchException>(() => authority.ReadRecordedProposalForController(
                offered.ProposalId, fixture.Owner, fixture.Control.Delivery(fixture.Grantee), (_, _) => true));
            Assert.Throws<AuthorityDispatchException>(() => authority.ReadRecordedPaidQuoteForController(
                quote.QuoteId, fixture.Owner, fixture.Control.Delivery(fixture.Grantee), (_, _) => true));
            var cut = fixture.Cut(authority);
            closing = cut.Closing;
            var archive = Assert.IsType<WorldMaterialCareArchiveSegment>(cut.Publication.SealedMaterialCareSegment);
            Assert.Equal(WorldSnapshotCodec.Encode(closing), archive.Encode());
            Assert.Equal(1UL, authority.InspectForHost().Journal.MaterialCareArchiveCount);
            Assert.Equal(giftOutcome.Encode(), closing.Coordination!.Proposals!.FindOutcome(offered.ProposalId)!.Encode());
            Assert.Equal(purchaseOutcome.Encode(), closing.Coordination.PaidExchange!.FindOutcome(purchaseOutcome.Quote.QuoteId)!.Encode());
            final = authority.ReadStateForHost();
            authority.BackupForHost(copy.Path);
        }
        using (var reopened = SqliteWorldAuthority.OpenExisting(copy.Path, genesis))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
            Assert.Equal(WorldSnapshotCodec.Encode(closing), reopened.ReadMaterialCareArchiveForHost(0).Segment.Encode());
            Assert.Equal(giftOutcome.Encode(), reopened.ReadRecordedProposalForController(giftOutcome.Proposal.ProposalId,
                fixture.Owner, fixture.Control.Delivery(fixture.Owner), (_, outcome) => Assert.IsType<WorldMaterialProposalOutcome>(outcome).Encode()));
            Assert.Equal(purchaseOutcome.Encode(), reopened.ReadRecordedPaidQuoteForController(purchaseOutcome.Quote.QuoteId,
                fixture.Owner, fixture.Control.Delivery(fixture.Owner), (_, outcome) => Assert.IsType<WorldPaidQuoteOutcome>(outcome).Encode()));
            Assert.Equal(saleOutcome.Encode(), reopened.ReadRecordedPaidQuoteForController(saleOutcome.Quote.QuoteId,
                fixture.Helper, fixture.Control.Delivery(fixture.Helper), (_, outcome) => Assert.IsType<WorldPaidQuoteOutcome>(outcome).Encode()));
            foreach (var command in new[] { delegatedGift, delegatedGiftOffer, delegatedPurchase, delegatedSale })
            {
                Assert.True(reopened.IsIdentityOccupiedForHost(command.CommandId));
                Assert.NotNull(reopened.FindReceipt(command, fixture.Control.Delivery(fixture.Grantee)));
            }
        }
        using var database = WorldDatabase.Open(copy.Path, false);
        Assert.Equal(12, database.SchemaVersion);
        Assert.Equal(genesis, database.ReadGenesis());
    }

    [Fact]
    public void Regrant_cannot_resurrect_old_material_or_paid_offers_and_returning_owner_withdraws_with_its_own_attribution()
    {
        var fixture = new CoPlayerTradeFixture();
        using var files = new StorageFiles();
        var genesis = WorldSnapshotCodec.Encode(fixture.Genesis);
        WorldSnapshot final;
        WorldMaterialProposalOutcome withdrawn;
        WorldPaidQuoteOutcome paidWithdrawal;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            var state = fixture.Prepare(authority, fund: true);
            var (fundingIssue, fundingQuote) = fixture.Quote(state, fixture.Helper, fixture.Owner, fixture.Lot(state, fixture.Helper));
            fixture.Submit(authority, fundingIssue);
            fixture.Submit(authority, fixture.Accept(authority.ReadStateForHost(), fundingQuote));
            state = fixture.Ready(authority);
            Assert.True(state.Progression!.Currency.GetActorAccount(fixture.Helper).Balance > 0);
            var oldWitness = state.EffectiveControl!.Find(fixture.Owner)!.Witness;
            var offer = fixture.Offer(state, fixture.Owner, fixture.Helper, fixture.Lot(state, fixture.Owner));
            fixture.Submit(authority, offer);
            state = authority.ReadStateForHost();
            var material = Assert.Single(state.Coordination!.Proposals!.Open);
            var (issue, quote) = fixture.Quote(state, fixture.Owner, fixture.Helper, fixture.Lot(state, fixture.Owner));
            fixture.Submit(authority, issue);
            fixture.Control.Revoke(authority);
            fixture.Control.Event(authority, fixture.Owner, WorldControllerEventKind.Reconnected);
            fixture.Control.Grant(authority, WorldControlCapabilities.Inventory | WorldControlCapabilities.Trade);
            fixture.Control.LoseAndExpire(authority);
            state = fixture.Control.Acquire(authority);
            Assert.NotEqual(oldWitness, state.EffectiveControl!.Find(fixture.Owner)!.Witness);
            var inventory = state.Inventory.ContentHash;
            var production = state.Production!.ContentHash;
            var progression = state.Progression!.ContentHash;
            fixture.Submit(authority, fixture.Accept(state, material), WorldActionRejection.Unavailable);
            fixture.Submit(authority, fixture.Accept(authority.ReadStateForHost(), quote), WorldActionRejection.Unavailable);
            state = authority.ReadStateForHost();
            Assert.Equal(inventory, state.Inventory.ContentHash);
            Assert.Equal(production, state.Production!.ContentHash);
            Assert.Equal(progression, state.Progression!.ContentHash);
            Assert.Equal(material.Encode(), Assert.Single(state.Coordination!.Proposals!.Open).Encode());
            Assert.Equal(oldWitness, Assert.Single(state.Coordination.PaidExchange!.Open).ControlWitness);

            fixture.Control.Event(authority, fixture.Owner, WorldControllerEventKind.Reconnected);
            state = authority.ReadStateForHost();
            Assert.Equal(WorldControllerMode.Human, state.Controllers!.Get(fixture.Owner).Mode);
            var close = fixture.Control.Commands.Command(state, fixture.Owner, new WithdrawMaterialProposalIntent(material.ProposalId));
            fixture.Submit(authority, close);
            state = authority.ReadStateForHost();
            withdrawn = Assert.Single(state.Coordination!.Proposals!.Outcomes);
            Assert.Equal(fixture.Control.Delivery(fixture.Grantee), withdrawn.Proposal.SenderDelivery);
            Assert.Equal(oldWitness, withdrawn.Proposal.ControlWitness);
            Assert.Equal(fixture.Control.Delivery(fixture.Owner), withdrawn.Termination!.Delivery);
            Assert.Null(withdrawn.Termination.ControlWitness);
            Assert.Equal(close.CommandId, withdrawn.Termination.CommandId);
            var paidClose = fixture.Control.Commands.Command(state, fixture.Owner, new WithdrawSpotQuoteIntent(quote.QuoteId));
            fixture.Submit(authority, paidClose);
            final = authority.ReadStateForHost();
            paidWithdrawal = final.Coordination!.PaidExchange!.FindOutcome(quote.QuoteId)!;
            Assert.Equal(oldWitness, paidWithdrawal.Quote.ControlWitness);
            Assert.Equal(fixture.Control.Delivery(fixture.Owner), paidWithdrawal.Withdrawal!.Delivery);
            Assert.Null(paidWithdrawal.Withdrawal.ControlWitness);
            Assert.Equal(inventory, final.Inventory.ContentHash);
            Assert.Equal(progression, final.Progression!.ContentHash);
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, genesis);
        Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.Equal(withdrawn.Encode(), Assert.Single(reopened.ReadStateForHost().Coordination!.Proposals!.Outcomes).Encode());
        Assert.Equal(paidWithdrawal.Encode(), reopened.ReadStateForHost().Coordination!.PaidExchange!.FindOutcome(paidWithdrawal.Quote.QuoteId)!.Encode());
    }
}
