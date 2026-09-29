using OpenFantasy.Core;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed partial class ConsentedWorldPublicationTests
{
    [Fact]
    public void Fresh_complete_world_admits_exactly_4096_raw_lots_without_permitting_an_extra_split()
    {
        var fixture = new ConsentedPotionFixture(rawLotTarget: CustodySnapshot.MaximumLots);
        var session = new CareWorldSession(WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis));
        fixture.Journey.Approach(session);
        WorldSnapshot before = session.ReadStateForHost();
        Assert.Equal(4096, before.Inventory.Custody.Lots.Count);
        Assert.Empty(before.Production!.Lots);
        Assert.Equal(before.ContentHash, WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(before)).ContentHash);
        var terms = RawTerms(before, fixture.Journey.Helper, fixture.Journey.Player);
        var pair = Pair(before, fixture.Journey, terms);
        var refused = session.Commit(fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep,
            [pair.Offer, pair.Acceptance]));
        Assert.Equal(2, refused.Receipts.Count);
        Assert.All(refused.Receipts, receipt => Assert.Equal(WorldActionRejection.Unavailable, receipt.Rejection));
        Assert.Equal(before.Inventory.ContentHash, refused.Candidate.Inventory.ContentHash);
        Assert.Empty(refused.Candidate.Coordination!.ConsentedMaterial!.Transfers);
        var current = refused.Candidate;
        var unit = current.Inventory.Custody.Lots.First(lot => lot.CustodianId == terms.SourceCustodianId
            && lot.Quantity == 1 && lot.ReservedBy is null && lot.Protection == LotProtection.None);
        var whole = Pair(current, fixture.Journey, new(current.WorldId, current.BranchId, terms.SenderActorId,
            terms.RecipientActorId, ConsentedMaterialLotKind.CountedSource, unit.LotId, unit.Definition,
            terms.SourceCustodianId, terms.DestinationCustodianId, 1, unit.Revision));
        var applied = CareRewardJourney.Commit(session, fixture.Journey.Batch(current, WorldBatchKind.AdvancingStep,
            [whole.Offer, whole.Acceptance]));
        Assert.Equal(4096, applied.Candidate.Inventory.Custody.Lots.Count);
        Assert.Equal(terms.DestinationCustodianId, applied.Candidate.Inventory.Custody.GetLot(unit.LotId).CustodianId);
    }

    [Fact]
    public void Combined_live_lot_bound_admits_a_split_one_below_then_refuses_splits_and_still_moves_whole_lots()
    {
        const int bound = CustodySnapshot.MaximumLots;
        Assert.Equal(4096, bound);
        var fixture = new ConsentedPotionFixture(rawLotTarget: bound - 1);
        var journey = fixture.Journey;
        WorldSnapshot genesis = WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis);
        Assert.Equal((bound - 1, 0), (genesis.Inventory.Custody.Lots.Count, genesis.Production!.Lots.Count));
        Assert.Equal(genesis.Inventory.Sources.Aggregate(0UL, (sum, source) => sum + source.UnitCapacity),
            genesis.Inventory.AdmittedUnitCapacity);
        var session = new CareWorldSession(genesis);
        journey.Approach(session);
        WorldSnapshot state = session.ReadStateForHost();
        EntityId helperBag = state.GetResident(journey.Helper).InventoryCustodianId;
        EntityId playerBag = state.GetResident(journey.Player).InventoryCustodianId;

        var berries = state.Inventory.Custody.GetLot(fixture.BerryLot);
        WorldActionCommand craft = CraftCommand(state, journey, journey.Helper, fixture.Bulk,
            new(IngredientLotKind.Source, berries.LotId, berries.Revision, 12));
        state = CareRewardJourney.Commit(session, journey.Batch(state, WorldBatchKind.AdvancingStep, [craft])).Candidate;
        TransformedLot stock = Assert.Single(state.Production!.Lots);
        Assert.Equal((bound - 2, 4UL), (state.Inventory.Custody.Lots.Count, stock.Quantity));

        // One below the bound an actual produced split creates the final admitted live lot.
        var split = Pair(state, journey, new ConsentedMaterialTerms(state.WorldId, state.BranchId, journey.Helper,
            journey.Player, ConsentedMaterialLotKind.Produced, stock.LotId, stock.Definition, helperBag, playerBag, 2,
            stock.Revision));
        var created = CareRewardJourney.Commit(session, journey.Batch(state, WorldBatchKind.AdvancingStep,
            [split.Offer, split.Acceptance]));
        EntityId childId = EntityId.DeriveSpawned(split.Offer.CommandId, 0);
        Assert.Contains(created.Identities, value => value.Identity == childId);
        state = created.Candidate;
        Assert.Equal(bound, state.Inventory.Custody.Lots.Count + state.Production!.Lots.Count);
        Assert.Equal(state.ContentHash, WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(state)).ContentHash);

        // At the bound neither a produced nor a raw split can create another live lot.
        TransformedLot child = state.Production.Lots.Single(value => value.LotId == childId);
        var producedSplit = new ConsentedMaterialTerms(state.WorldId, state.BranchId, journey.Player, journey.Helper,
            ConsentedMaterialLotKind.Produced, child.LotId, child.Definition, playerBag, helperBag, 1, child.Revision);
        foreach (ConsentedMaterialTerms terms in new[] { producedSplit, RawTerms(state, journey.Helper, journey.Player) })
        {
            WorldSnapshot before = session.ReadStateForHost();
            var refusedPair = Pair(before, journey, terms);
            var refused = session.Commit(journey.Batch(before, WorldBatchKind.AdvancingStep,
                [refusedPair.Offer, refusedPair.Acceptance]));
            Assert.Equal(2, refused.Receipts.Count);
            Assert.All(refused.Receipts, receipt => Assert.Equal(WorldActionRejection.Unavailable, receipt.Rejection));
            Assert.Equal(before.Inventory.ContentHash, refused.Candidate.Inventory.ContentHash);
            Assert.Equal(before.Production!.ContentHash, refused.Candidate.Production!.ContentHash);
            Assert.Equal(before.Coordination!.ConsentedMaterial!.ContentHash,
                refused.Candidate.Coordination!.ConsentedMaterial!.ContentHash);
            Assert.DoesNotContain(refused.Identities, value => value.Kind == WorldCommittedIdentityKind.SpawnedEntity);
        }

        // The same produced and raw owners still move whole lots without a new identity.
        state = session.ReadStateForHost();
        var wholeProduced = Pair(state, journey, new ConsentedMaterialTerms(state.WorldId, state.BranchId,
            journey.Player, journey.Helper, ConsentedMaterialLotKind.Produced, child.LotId, child.Definition, playerBag,
            helperBag, child.Quantity, child.Revision));
        var movedProduced = CareRewardJourney.Commit(session, journey.Batch(state, WorldBatchKind.AdvancingStep,
            [wholeProduced.Offer, wholeProduced.Acceptance]));
        Assert.DoesNotContain(movedProduced.Identities, value => value.Kind == WorldCommittedIdentityKind.SpawnedEntity);
        state = movedProduced.Candidate;
        Assert.Equal(helperBag, state.Production!.Lots.Single(value => value.LotId == childId).CustodianId);
        CountedSourceLot unit = state.Inventory.Custody.Lots.First(value => value.CustodianId == helperBag
            && value.Quantity == 1 && value.ReservedBy is null && value.Protection == LotProtection.None);
        var wholeRaw = Pair(state, journey, new ConsentedMaterialTerms(state.WorldId, state.BranchId, journey.Helper,
            journey.Player, ConsentedMaterialLotKind.CountedSource, unit.LotId, unit.Definition, helperBag, playerBag,
            1, unit.Revision));
        var movedRaw = CareRewardJourney.Commit(session, journey.Batch(state, WorldBatchKind.AdvancingStep,
            [wholeRaw.Offer, wholeRaw.Acceptance]));
        Assert.DoesNotContain(movedRaw.Identities, value => value.Kind == WorldCommittedIdentityKind.SpawnedEntity);
        state = movedRaw.Candidate;
        Assert.Equal(playerBag, state.Inventory.Custody.GetLot(unit.LotId).CustodianId);
        Assert.Equal(bound, state.Inventory.Custody.Lots.Count + state.Production!.Lots.Count);
        Assert.Equal(3, state.Coordination!.ConsentedMaterial!.Transfers.Count);
        // The complete root at the bound remains canonical through its decoded archive cut.
        _ = CutAndDecode(session, null, out WorldSnapshot reopened);
        Assert.Equal(bound, reopened.Inventory.Custody.Lots.Count + reopened.Production!.Lots.Count);
    }
}
