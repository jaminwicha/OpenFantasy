using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed partial class ConsentedWorldPublicationTests
{
    [Fact]
    public void Both_participants_spend_movement_and_each_get_their_own_canonical_receipt()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis));
        fixture.Journey.Approach(session);
        WorldSnapshot before = session.ReadStateForHost();
        var pair = Pair(before, fixture.Journey, RawTerms(before, fixture.Helper, fixture.Player));
        var basis = fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep, [pair.Acceptance, pair.Offer]);
        var moves = new[] { pair.Offer, pair.Acceptance }.Select(command =>
            new WorldMovementInput(new(command.PrincipalId, command.OriginSessionId), command.ActorId, 32, 0)).ToArray();
        // The same selected movement is physically possible without the successful transaction.
        foreach (WorldMovementInput move in moves)
        {
            var alone = new WorldStepProcessor().Decide(before, new(basis.CauseId, before.WorldId, before.BranchId,
                before.Revision, before.AdvancingSteps, WorldBatchKind.AdvancingStep, [move], [], before.ProfileVersion));
            Assert.NotEqual(before.Space.GetActor(move.ActorId).Feet, alone.Candidate.Space.GetActor(move.ActorId).Feet);
        }
        var result = session.Commit(new(basis.CauseId, before.WorldId, before.BranchId, before.Revision,
            before.AdvancingSteps, WorldBatchKind.AdvancingStep, moves, basis.Actions, before.ProfileVersion));
        Assert.Equal(2, result.Receipts.Count);
        Assert.Equal(result.Receipts.Select(value => value.CommandId).Order(), result.Receipts.Select(value => value.CommandId));
        foreach (var command in basis.Actions)
        {
            var receipt = Assert.Single(result.Receipts, value => value.CommandId == command.CommandId);
            Assert.Equal(WorldActionRejection.None, receipt.Rejection);
            Assert.Equal(command.ContentHash, receipt.CommandHash);
            Assert.Equal(before.Space.GetActor(command.ActorId).Feet, result.Candidate.Space.GetActor(command.ActorId).Feet);
        }
        WorldConsentedTransferEvidence evidence = Assert.Single(result.Candidate.Coordination!.ConsentedMaterial!.Transfers);
        Assert.Equal(pair.Offer.CommandId, basis.Actions[(int)evidence.OfferActionIndex].CommandId);
        Assert.Equal(pair.Acceptance.CommandId, basis.Actions[(int)evidence.AcceptanceActionIndex].CommandId);
    }

    [Theory]
    [InlineData(0)] // Missing acceptance.
    [InlineData(1)] // Captured recipient claims submitted under the sender's session.
    [InlineData(2)] // Both approve an obsolete lot revision.
    public void Invalid_pair_keeps_all_material_owners_and_evidence_unchanged(int variant)
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis));
        fixture.Journey.Approach(session);
        var before = session.ReadStateForHost();
        var terms = RawTerms(before, fixture.Helper, fixture.Player);
        if (variant == 2)
            terms = new(terms.WorldId, terms.BranchId, terms.SenderActorId, terms.RecipientActorId,
                terms.LotKind, terms.LotId, terms.Definition, terms.SourceCustodianId, terms.DestinationCustodianId,
                terms.Quantity, terms.ExpectedLotRevision + 1);
        var pair = Pair(before, fixture.Journey, terms);
        WorldActionCommand acceptance = variant != 1 ? pair.Acceptance : new(pair.Acceptance.CommandId,
            pair.Offer.PrincipalId, pair.Offer.OriginSessionId, pair.Acceptance.ActorId, before.WorldId,
            before.BranchId, pair.Acceptance.ExpectedActorRevision, pair.Acceptance.Intent);
        WorldActionCommand[] commands = variant == 0 ? [pair.Offer] : [pair.Offer, acceptance];
        var result = session.Commit(fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep, commands));
        Assert.Equal(commands.Length, result.Receipts.Count);
        foreach (var receipt in result.Receipts)
        {
            var expected = variant == 2 ? WorldActionRejection.RevisionConflict
                : variant == 1 && receipt.CommandId == acceptance.CommandId ? WorldActionRejection.NotAuthorized
                : WorldActionRejection.Unavailable;
            Assert.Equal(expected, receipt.Rejection);
        }
        Assert.Equal(before.Inventory.ContentHash, result.Candidate.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, result.Candidate.Production!.ContentHash);
        Assert.Equal(before.Reservoirs!.Capacity.ContentHash, result.Candidate.Reservoirs!.Capacity.ContentHash);
        Assert.Equal(before.Coordination!.ConsentedMaterial!.ContentHash, result.Candidate.Coordination!.ConsentedMaterial!.ContentHash);
    }

    [Fact]
    public void Full_transfer_window_cuts_before_the_next_pair_and_retains_closed_identities()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis));
        fixture.Journey.Approach(session);
        var original = RawTerms(session.ReadStateForHost(), fixture.Helper, fixture.Player);
        EntityId firstCommand = default;
        WorldMaterialCareArchiveSegment? closed = null;
        for (int transfer = 0; transfer < WorldConsentedMaterialHistory.MaximumTransfers; transfer++)
        {
            var before = session.ReadStateForHost();
            var lot = before.Inventory.Custody.GetLot(original.LotId);
            EntityId sender = before.Inventory.Policies.Single(value => value.CustodianId == lot.CustodianId).OwnerActorId;
            EntityId recipient = sender == fixture.Helper ? fixture.Player : fixture.Helper;
            var terms = new ConsentedMaterialTerms(before.WorldId, before.BranchId, sender, recipient,
                ConsentedMaterialLotKind.CountedSource, lot.LotId, lot.Definition, lot.CustodianId,
                before.GetResident(recipient).InventoryCustodianId, lot.Quantity, lot.Revision);
            var pair = Pair(before, fixture.Journey, terms);
            if (transfer == 0) firstCommand = pair.Offer.CommandId;
            var result = CareRewardJourney.Commit(session, fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep,
                [pair.Offer, pair.Acceptance]));
            if (transfer < WorldConsentedMaterialHistory.MaximumTransfers - 1)
            {
                Assert.Null(result.SealedMaterialCareSegment);
                Assert.Equal(transfer + 1, result.Candidate.Coordination!.ConsentedMaterial!.Transfers.Count);
            }
            else
            {
                closed = Assert.IsType<WorldMaterialCareArchiveSegment>(result.SealedMaterialCareSegment);
                Assert.Single(result.Candidate.Coordination!.ConsentedMaterial!.Transfers);
                var decoded = WorldMaterialCareArchiveSegment.Decode(closed.Reference, closed.Encode());
                Assert.Equal(closed.Anchor.ContentHash, decoded.Anchor.ContentHash);
                var reopened = WorldSnapshotCodec.DecodeCoordinated(WorldSnapshotCodec.Encode(result.Candidate), decoded.Anchor);
                session = new(reopened, session.OccupiedIdentities);
            }
        }
        Assert.NotNull(closed);
        var current = session.ReadStateForHost();
        var fresh = Pair(current, fixture.Journey, RawTerms(current, fixture.Helper, fixture.Player));
        var reused = new WorldActionCommand(firstCommand, fresh.Offer.PrincipalId, fresh.Offer.OriginSessionId,
            fresh.Offer.ActorId, current.WorldId, current.BranchId, fresh.Offer.ExpectedActorRevision, fresh.Offer.Intent);
        var accepted = fixture.Journey.Command(current, fixture.Player,
            AcceptMaterialIntent.Approving(reused, current.Space.GetActor(fixture.Helper).Revision));
        Assert.Throws<InvalidOperationException>(() => session.Commit(fixture.Journey.Batch(current,
            WorldBatchKind.AdvancingStep, [reused, accepted])));
        Assert.Equal(current.ContentHash, session.ReadStateForHost().ContentHash);
    }

    [Fact]
    public void Actual_completed_construction_stays_reserved_before_and_after_a_decoded_cut()
    {
        var journey = new CoordinatedLiveWriterJourney(consentedMaterial: true);
        journey.Step([journey.PrepareQuestDelivery()]);
        journey.Step([journey.Command(journey.Player, new CloseDialogueIntent(journey.World.Dialogues.Single().DialogueId))]);
        int work = 0;
        for (int step = 0; !journey.World.Construction!.Projects.Single().IsCompleted; step++)
        {
            Assert.True(step < 512);
            work += journey.BuilderStep().Receipts.Count;
        }
        Assert.Equal(200, work);
        // The newly built guard obstructs a straight line; use the admitted ground route.
        var goal = journey.World.Space.GetActor(journey.Player).Feet.Offset(0, -256, 0);
        for (int step = 0; journey.World.Space.GetActor(journey.Helper).Feet != goal; step++)
        {
            Assert.True(step < 512);
            var before = journey.World;
            var route = before.Space.QueryGroundRoute(journey.Helper, goal);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            var waypoint = Assert.IsType<SubunitPosition>(route.NextWaypoint);
            var feet = before.Space.GetActor(journey.Helper).Feet;
            long dx = Math.Clamp(waypoint.X - feet.X, -32, 32);
            long dy = dx == 0 ? Math.Clamp(waypoint.Y - feet.Y, -32, 32) : 0;
            var control = before.Inventory.Controls.Single(value => value.ActorId == journey.Helper);
            journey.Step(movement: [new(new(control.PrincipalId, control.SessionId), journey.Helper, dx, dy)]);
            Assert.Equal(feet.Offset(dx, dy, 0), journey.World.Space.GetActor(journey.Helper).Feet);
        }
        Assert.Equal(SpatialInteraction.Reachable, journey.World.Space.QueryInteraction(journey.Helper, journey.Player));
        for (int cut = 0; cut < 2; cut++)
        {
            var positiveState = journey.World;
            var available = RawTerms(positiveState, journey.Helper, journey.Player);
            var positiveOffer = journey.Command(journey.Helper,
                new OfferMaterialIntent(available, positiveState.Space.GetActor(journey.Player).Revision));
            var positiveAcceptance = journey.Command(journey.Player,
                AcceptMaterialIntent.Approving(positiveOffer, positiveState.Space.GetActor(journey.Helper).Revision));
            // Same actual participants can move available stock at both sides of the archive cut.
            journey.Step([positiveOffer, positiveAcceptance]);
            var before = journey.World;
            var project = before.Construction!.Projects.Single();
            var installed = before.Production!.Lots.Single(value => value.ReservedBy == project.ReservationId);
            var terms = new ConsentedMaterialTerms(before.WorldId, before.BranchId, journey.Helper, journey.Player,
                ConsentedMaterialLotKind.Produced, installed.LotId, installed.Definition, installed.CustodianId,
                before.GetResident(journey.Player).InventoryCustodianId, installed.Quantity, installed.Revision);
            var offer = journey.Command(journey.Helper, new OfferMaterialIntent(terms, before.Space.GetActor(journey.Player).Revision));
            var acceptance = journey.Command(journey.Player, AcceptMaterialIntent.Approving(offer, before.Space.GetActor(journey.Helper).Revision));
            var result = journey.Step([offer, acceptance], accepted: false);
            Assert.All(result.Receipts, receipt => Assert.Equal(WorldActionRejection.Unavailable, receipt.Rejection));
            Assert.Equal(before.Inventory.ContentHash, journey.World.Inventory.ContentHash);
            Assert.Equal(before.Production.ContentHash, journey.World.Production!.ContentHash);
            Assert.Equal(before.Construction.ContentHash, journey.World.Construction!.ContentHash);
            Assert.Equal(before.Coordination!.ConsentedMaterial!.ContentHash,
                journey.World.Coordination!.ConsentedMaterial!.ContentHash);
            if (cut == 0) journey.CutAndReopen();
        }
    }

    private static ConsentedMaterialTerms RawTerms(WorldSnapshot world, EntityId sender, EntityId recipient)
    {
        var source = world.GetResident(sender).InventoryCustodianId;
        CountedSourceLot lot = world.Inventory.Custody.Lots.First(value => value.CustodianId == source
            && value.Quantity > 1 && value.ReservedBy is null && value.Protection == LotProtection.None);
        return new(world.WorldId, world.BranchId, sender, recipient, ConsentedMaterialLotKind.CountedSource,
            lot.LotId, lot.Definition, source, world.GetResident(recipient).InventoryCustodianId, 1, lot.Revision);
    }
}
