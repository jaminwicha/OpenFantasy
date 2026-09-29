using OpenFantasy.Core;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed partial class ConsentedWorldPublicationTests
{
    [Fact]
    public void A_valid_pair_refused_by_physical_storage_rolls_back_both_owners_and_both_opportunities()
    {
        var fixture = new ConsentedPotionFixture(reservoirHeadroom: true);
        var journey = fixture.Journey;
        var session = new CareWorldSession(WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis));
        journey.Approach(session);
        var before = session.ReadStateForHost();
        var reservoir = before.Reservoirs!;
        EntityId receiver = reservoir.Geometries.Single().CustodianId;
        Assert.Equal(journey.Helper, before.Inventory.Policies.Single(value => value.CustodianId == receiver).OwnerActorId);
        EntityId source = before.GetResident(journey.Player).InventoryCustodianId;
        var stock = before.Inventory.Custody.Lots.First(value => value.CustodianId == source
            && value.Quantity > 1 && value.Protection == LotProtection.None && value.ReservedBy is null);
        Assert.NotEqual(reservoir.Capacity.Domain.Material.Item, stock.Definition);
        var terms = new ConsentedMaterialTerms(before.WorldId, before.BranchId, journey.Player, journey.Helper,
            ConsentedMaterialLotKind.CountedSource, stock.LotId, stock.Definition, source, receiver, 1, stock.Revision);
        var pair = Pair(before, journey, terms);
        var plan = new WorldConsentedMaterialPlanner().Plan(before, pair.Offer, pair.Acceptance,
            WorldBatchKind.AdvancingStep, journey.Delivery(pair.Offer), journey.Delivery(pair.Acceptance));
        Assert.True(plan.IsApplied);
        Assert.Equal("Each receiver requires the same owner and exact calibrated raw/derived contents.",
            Assert.Throws<ArgumentException>(() => reservoir.Capacity.PlanReconcile(plan.CandidateInventory,
                plan.CandidateProduction, TestContext.Current.CancellationToken)).Message);

        var moves = new[] { pair.Offer, pair.Acceptance }.Select(command =>
            new WorldMovementInput(journey.Delivery(command), command.ActorId, 32, 0)).ToArray();
        var selected = journey.Batch(before, WorldBatchKind.AdvancingStep, [pair.Offer, pair.Acceptance]);
        var control = new WorldStepProcessor().Decide(before, new(selected.CauseId, before.WorldId, before.BranchId,
            before.Revision, before.AdvancingSteps, WorldBatchKind.AdvancingStep, moves, [], before.ProfileVersion));
        var result = session.Commit(new(selected.CauseId, before.WorldId, before.BranchId, before.Revision,
            before.AdvancingSteps, WorldBatchKind.AdvancingStep, moves, selected.Actions, before.ProfileVersion));
        Assert.Equal(2, result.Receipts.Count);
        Assert.All(result.Receipts, receipt => Assert.Equal(WorldActionRejection.Unavailable, receipt.Rejection));
        Assert.Equal(before.Inventory.ContentHash, result.Candidate.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, result.Candidate.Production!.ContentHash);
        Assert.Equal(reservoir.ContentHash, result.Candidate.Reservoirs!.ContentHash);
        Assert.Equal(before.Construction!.ContentHash, result.Candidate.Construction!.ContentHash);
        Assert.Equal(before.Coordination!.ConsentedMaterial!.ContentHash, result.Candidate.Coordination!.ConsentedMaterial!.ContentHash);
        foreach (var movement in moves)
        {
            Assert.NotEqual(before.Space.GetActor(movement.ActorId).Feet, control.Candidate.Space.GetActor(movement.ActorId).Feet);
            Assert.Equal(control.Candidate.Space.GetActor(movement.ActorId).Feet, result.Candidate.Space.GetActor(movement.ActorId).Feet);
        }
        EntityId refusedChild = EntityId.DeriveSpawned(pair.Offer.CommandId, 0);
        Assert.DoesNotContain(result.Identities, identity => identity.Identity == refusedChild);
        Assert.DoesNotContain(refusedChild, session.OccupiedIdentities);
        Assert.All(selected.Actions, command => Assert.Contains(command.CommandId, session.OccupiedIdentities));
        var next = result.Candidate;
        // These same participants can subsequently make an ordinary gift into available bag space.
        var valid = Pair(next, journey, RawTerms(next, journey.Helper, journey.Player));
        var applied = CareRewardJourney.Commit(session, journey.Batch(next, WorldBatchKind.AdvancingStep,
            [valid.Offer, valid.Acceptance]));
        Assert.Contains(applied.Identities, identity => identity.Identity == EntityId.DeriveSpawned(valid.Offer.CommandId, 0)
            && identity.Kind == WorldCommittedIdentityKind.SpawnedEntity);
        Assert.Single(applied.Candidate.Coordination!.ConsentedMaterial!.Transfers);
    }
}
