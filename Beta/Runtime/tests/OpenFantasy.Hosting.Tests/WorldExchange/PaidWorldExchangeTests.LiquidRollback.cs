using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldExchange;

public sealed partial class PaidWorldExchangeTests
{
    [Fact]
    public void A_locally_valid_paid_water_transfer_rolls_back_when_it_would_detach_live_liquid_placement()
    {
        WorldSnapshot genesis = WorldPaidExchangeAdmission.CreateFresh(new OrdinaryCareWorldFixture().Genesis);
        var journey = new CareRewardJourney(false, genesis);
        using var files = new StorageFiles();
        SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        try
        {
            WorldSnapshot state = EarnReward(journey, new CareWorldSession(authority));
            EntityId player = journey.Player, helper = journey.Helper;
            var reservoir = Assert.Single(state.Reservoirs!.Geometries);
            Assert.Equal(helper, state.Inventory.Policies.Single(value => value.CustodianId == reservoir.CustodianId).OwnerActorId);
            Assert.NotEqual(state.GetResident(helper).InventoryCustodianId, reservoir.CustodianId);
            CountedSourceLot water = Assert.Single(state.Inventory.Custody.Lots, value => value.CustodianId == reservoir.CustodianId);
            Assert.True(water.Quantity > 1);
            Assert.Equal(LotProtection.None, water.Protection);
            Assert.Null(water.ReservedBy);
            Assert.False(state.Production!.IsContinued);

            var (issue, quote) = IssueCommand(state, helper, player, water, 1,
                state.Progression!.Currency.GetActorAccount(helper).AccountId, 1);
            state = Submit(authority, issue, WorldActionRejection.None);
            WorldSnapshot before = state;
            var accept = AcceptCommand(before, player, quote, helper, 1, 1);
            var acceptance = ((AcceptSpotQuoteIntent)accept.Intent).Acceptance;

            // The public component can move and pay these actual owners. Its diagnostic candidate
            // has not been published, and it deliberately cannot authorize the world's fluid state.
            var book = new SpotQuoteBook(before.WorldId, before.BranchId, 1,
                [new SpotQuoteEntry(quote, Delivery(issue))], []);
            SpotExchangePlan local = new SpotExchangeAuthority().Accept(before.Inventory, before.Production!,
                before.Progression!, book, acceptance, Delivery(accept), before.AdvancingSteps,
                authority.IsIdentityOccupiedForHost);
            Assert.Equal(SpotExchangeOutcome.Applied, local.Outcome);
            Assert.Equal(SpotExchangeRejection.None, local.Rejection);
            EntityId child = WorldPaidQuote.SplitChildOf(accept.CommandId);
            Assert.Equal(state.GetResident(player).InventoryCustodianId, local.CandidateInventory.Custody.GetLot(child).CustodianId);
            Assert.NotEqual(before.Progression!.Currency.Revision, local.CandidateProgression.Currency.Revision);

            // Reservoir quantity and physical volume still fit. The later detailed placement join
            // rejects detaching that unit from cells which still own its exact source interval.
            var capacity = before.Reservoirs!.Capacity.PlanReconcile(local.CandidateInventory, local.CandidateProduction,
                TestContext.Current.CancellationToken);
            var reservoirs = before.Reservoirs.PlanApply(capacity).ApplyTo(before.Reservoirs);
            ArgumentException detached = Assert.Throws<ArgumentException>(() => before.LiquidCells!.Rebind(reservoirs));
            Assert.Contains("Detailed placements must exhaust exactly", detached.Message);
            Assert.Equal(before.ContentHash, authority.ReadStateForHost().ContentHash);
            Assert.False(authority.IsIdentityOccupiedForHost(child));

            WorldSnapshot passive = new WorldStepProcessor().Decide(before,
                journey.Batch(before, WorldBatchKind.AdvancingStep), authority.IsIdentityOccupiedForHost).Candidate;
            var receipt = authority.SubmitPaidExchangeCommand(accept, Delivery(accept), NextId());
            Assert.Equal(WorldActionRejection.Unavailable, receipt.Rejection);
            state = authority.ReadStateForHost();
            AssertOwnersUnchanged(before, state);
            Assert.Equal(before.Coordination!.Sources.ContentHash, state.Coordination!.Sources.ContentHash);
            Assert.Equal(before.Coordination.Anchor?.ContentHash, state.Coordination.Anchor?.ContentHash);
            Assert.Equal(before.Coordination.ConsentedMaterial!.ContentHash, state.Coordination.ConsentedMaterial!.ContentHash);
            Assert.Equal(before.Coordination.Proposals!.ContentHash, state.Coordination.Proposals!.ContentHash);
            Assert.Equal(before.ResourceHistory!.ContentHash, state.ResourceHistory!.ContentHash);
            Assert.Equal(passive.Reservoirs!.ContentHash, state.Reservoirs!.ContentHash);
            Assert.Equal(passive.LiquidCells!.ContentHash, state.LiquidCells!.ContentHash);
            Assert.Equal(passive.Calendar, state.Calendar);
            Assert.Equal(passive.AdvancingSteps, state.AdvancingSteps);
            Assert.DoesNotContain(state.Inventory.Custody.Lots, value => value.LotId == child);
            Assert.False(authority.IsIdentityOccupiedForHost(child));
            Assert.True(authority.IsIdentityOccupiedForHost(accept.CommandId));
            Assert.Equal(quote.ContentHash, state.Coordination.PaidExchange!.FindOpen(quote.QuoteId)!.Quote.ContentHash);
            Assert.Empty(state.Coordination.PaidExchange.Fills);

            ContentHash head = state.ContentHash;
            authority.Dispose();
            authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);
            Assert.False(authority.IsIdentityOccupiedForHost(child));
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(
                authority.SubmitPaidExchangeCommand(accept, Delivery(accept), NextId())));
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);
        }
        finally
        {
            authority.Dispose();
        }
    }
}
