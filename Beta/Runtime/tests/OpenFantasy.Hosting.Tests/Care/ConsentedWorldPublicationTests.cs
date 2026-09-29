using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed partial class ConsentedWorldPublicationTests
{
    [Fact]
    public void Fresh_interpretation_and_two_decoded_cuts_keep_the_complete_owners()
    {
        var fixture = new OrdinaryCareWorldFixture();
        WorldSnapshot genesis = WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis);
        Assert.Equal((ushort)28, genesis.ProfileVersion);
        Assert.Equal(genesis.ContentHash, WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(genesis)).ContentHash);
        Assert.Throws<ArgumentException>(() => WorldSnapshotCodecV27.Encode(genesis));
        Assert.Throws<FormatException>(() => WorldSnapshotCodecV27.Decode(WorldSnapshotCodec.Encode(genesis)));
        var session = new CareWorldSession(genesis);
        Step(session, fixture.Journey);
        WorldMaterialCareArchiveSegment first = CutAndDecode(session, null, out WorldSnapshot reopened);
        Assert.True(first.Continued.Production!.Continuation!.SupportsConsentedWorld);
        Assert.Throws<ArgumentException>(() => ProductionCodecV9.Encode(first.Continued.Production));
        Assert.Throws<ArgumentException>(() => ProductionCodecV9.Decode(
            ProductionCodecV11.Encode(first.Continued.Production), first.Anchor.Production));
        var oldSession = new CareWorldSession(WorldCoordinatedAdmission.CreateFresh(fixture.Genesis));
        Step(oldSession, fixture.Journey);
        var old = CutAndDecode(oldSession, null, out var oldReopened);
        Assert.Equal("The contract version is unsupported.", Assert.Throws<ProductionFormatException>(() => ProductionCodecV9.Decode(
            ProductionCodecV11.Encode(first.Continued.Production), old.Anchor.Production)).Message);
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.DecodeCoordinated(
            WorldSnapshotCodec.Encode(reopened), old.Anchor));
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.DecodeCoordinated(
            WorldSnapshotCodec.Encode(oldReopened), first.Anchor));
        session = new(reopened, session.OccupiedIdentities);
        Step(session, fixture.Journey);
        WorldMaterialCareArchiveSegment second = CutAndDecode(session, first.Anchor, out _);
        Assert.Equal(1UL, second.Reference.Ordinal);
        Assert.Equal((ushort)28, second.Continued.ProfileVersion);
        Assert.Empty(second.Continued.Coordination!.ConsentedMaterial!.Transfers);
    }

    [Fact]
    public void A_raw_pair_publishes_two_outcomes_and_continues_from_decoded_history()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis));
        fixture.Journey.Approach(session);
        WorldSnapshot before = session.ReadStateForHost();
        EntityId source = before.GetResident(fixture.Helper).InventoryCustodianId;
        EntityId destination = before.GetResident(fixture.Player).InventoryCustodianId;
        CountedSourceLot lot = before.Inventory.Custody.Lots.First(value => value.CustodianId == source
            && value.Quantity > 1 && value.ReservedBy is null && value.Protection == LotProtection.None);
        var terms = new ConsentedMaterialTerms(before.WorldId, before.BranchId, fixture.Helper, fixture.Player,
            ConsentedMaterialLotKind.CountedSource, lot.LotId, lot.Definition, source, destination, 1, lot.Revision);
        var pair = Pair(before, fixture.Journey, terms);
        WorldStepDecision result = session.Commit(fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep,
            [pair.Offer, pair.Acceptance]));
        Assert.Equal(2, result.Receipts.Count);
        Assert.All(result.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
        WorldSnapshot after = result.Candidate;
        WorldConsentedTransferEvidence evidence = Assert.Single(after.Coordination!.ConsentedMaterial!.Transfers);
        Assert.Equal((pair.Offer.CommandId, pair.Acceptance.CommandId), (evidence.CommandId, evidence.AcceptanceId));
        Assert.Equal((destination, 1UL, lot.Source.Start), (after.Inventory.Custody.GetLot(evidence.MovedLotId).CustodianId,
            after.Inventory.Custody.GetLot(evidence.MovedLotId).Quantity, evidence.MovedStart));
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
        Assert.Contains(result.Identities, value => value.Identity == pair.Offer.CommandId && value.Kind == WorldCommittedIdentityKind.Command);
        Assert.Contains(result.Identities, value => value.Identity == pair.Acceptance.CommandId && value.Kind == WorldCommittedIdentityKind.Command);
        WorldMaterialCareArchiveSegment first = CutAndDecode(session, null, out WorldSnapshot reopened);
        session = new(reopened, session.OccupiedIdentities);
        var current = session.ReadStateForHost();
        CountedSourceLot moved = current.Inventory.Custody.GetLot(evidence.MovedLotId);
        var onward = new ConsentedMaterialTerms(current.WorldId, current.BranchId, fixture.Player, fixture.Helper,
            ConsentedMaterialLotKind.CountedSource, moved.LotId, moved.Definition, destination, source, 1, moved.Revision);
        var secondPair = Pair(current, fixture.Journey, onward);
        var secondResult = session.Commit(fixture.Journey.Batch(current, WorldBatchKind.AdvancingStep,
            [secondPair.Offer, secondPair.Acceptance]));
        Assert.All(secondResult.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
        Assert.Equal(source, secondResult.Candidate.Inventory.Custody.GetLot(moved.LotId).CustodianId);
        _ = CutAndDecode(session, first.Anchor, out _);
    }

    private static (WorldActionCommand Offer, WorldActionCommand Acceptance) Pair(WorldSnapshot state,
        CareRewardJourney journey, ConsentedMaterialTerms terms)
    {
        var offer = journey.Command(state, terms.SenderActorId,
            new OfferMaterialIntent(terms, state.Space.GetActor(terms.RecipientActorId).Revision));
        var acceptance = journey.Command(state, terms.RecipientActorId,
            AcceptMaterialIntent.Approving(offer, state.Space.GetActor(terms.SenderActorId).Revision));
        return (offer, acceptance);
    }

    private static void Step(CareWorldSession session, CareRewardJourney journey) =>
        session.Commit(journey.Batch(session.ReadStateForHost(), WorldBatchKind.AdvancingStep));

    private static WorldMaterialCareArchiveSegment CutAndDecode(CareWorldSession session,
        WorldMaterialCareArchiveAnchor? previous, out WorldSnapshot reopened)
    {
        WorldSnapshot closing = session.ReadStateForHost();
        var sealedSegment = WorldMaterialCareArchiveSegment.Seal(closing);
        // Only decoded closing bytes construct the context supplied to the reopened successor.
        var decoded = WorldMaterialCareArchiveSegment.Decode(sealedSegment.Reference, sealedSegment.Encode(), previous);
        Assert.Equal(sealedSegment.Anchor.ContentHash, decoded.Anchor.ContentHash);
        reopened = WorldSnapshotCodec.DecodeCoordinated(WorldSnapshotCodec.Encode(decoded.Continued), decoded.Anchor);
        Assert.Equal(decoded.Continued.ContentHash, reopened.ContentHash);
        Assert.Equal(closing.Inventory.ContentHash, reopened.Inventory.ContentHash);
        Assert.Equal(closing.Calendar, reopened.Calendar);
        Assert.Equal(closing.Progression!.ContentHash, reopened.Progression!.ContentHash);
        Assert.Equal(closing.Production!.TotalBatchCount, reopened.Production!.TotalBatchCount);
        Assert.Equal(closing.Care!.TotalTreatments, reopened.Care!.TotalTreatments);
        return decoded;
    }
}
