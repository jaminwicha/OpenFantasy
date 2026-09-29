using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed partial class ConsentedWorldPublicationTests
{
    [Fact]
    public void Two_valid_pairs_publish_together_with_independent_stock_receipts_and_opportunities()
    {
        var company = new ConsentedCompanyFixture();
        var fixture = new OrdinaryCareWorldFixture(company.Create());
        var journey = fixture.Journey;
        var session = new CareWorldSession(WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis));
        journey.Approach(session);
        WorldSnapshot before = session.ReadStateForHost();
        var first = Pair(before, journey, RawTerms(before, journey.Helper, journey.Player));
        var second = Pair(before, journey, RawTerms(before, company.First, company.Second));
        WorldActionCommand[] commands = [first.Offer, first.Acceptance, second.Offer, second.Acceptance];
        // Every member has a real independent movement opportunity on the same starting root.
        var movement = commands.Select(command => new WorldMovementInput(new(command.PrincipalId,
            command.OriginSessionId), command.ActorId, 32, 0)).ToArray();
        var control = new WorldStepProcessor().Decide(before, new(new EntityId(0xc028c04a00000000, 500),
            before.WorldId, before.BranchId, before.Revision, before.AdvancingSteps, WorldBatchKind.AdvancingStep,
            movement, [], before.ProfileVersion));
        foreach (var command in commands)
        {
            Assert.Equal(before.Space.GetActor(command.ActorId).Feet.Offset(32, 0, 0),
                control.Candidate.Space.GetActor(command.ActorId).Feet);
        }
        var batch = new WorldStepBatch(new EntityId(0xc028c04a00000000, 501), before.WorldId, before.BranchId,
            before.Revision, before.AdvancingSteps, WorldBatchKind.AdvancingStep, movement, commands.Reverse(), before.ProfileVersion);
        var result = CareRewardJourney.Commit(session, batch);
        Assert.Equal(4, result.Receipts.Count);
        Assert.Equal(2, result.Candidate.Coordination!.ConsentedMaterial!.Transfers.Count);
        foreach (var pair in new[] { first, second })
        {
            var terms = ((OfferMaterialIntent)pair.Offer.Intent).Terms;
            var evidence = Assert.Single(result.Candidate.Coordination.ConsentedMaterial.Transfers,
                value => value.CommandId == pair.Offer.CommandId);
            Assert.Equal(pair.Acceptance.CommandId, evidence.AcceptanceId);
            Assert.Equal(terms.DestinationCustodianId, result.Candidate.Inventory.Custody.GetLot(evidence.MovedLotId).CustodianId);
            Assert.Equal(before.Inventory.Custody.GetLot(terms.LotId).Quantity - 1,
                result.Candidate.Inventory.Custody.GetLot(terms.LotId).Quantity);
            Assert.Contains(result.Identities, value => value.Identity == evidence.MovedLotId
                && value.Kind == WorldCommittedIdentityKind.SpawnedEntity);
        }
        foreach (var command in commands)
        {
            Assert.Equal(before.Space.GetActor(command.ActorId).Feet, result.Candidate.Space.GetActor(command.ActorId).Feet);
            Assert.Equal(command.ContentHash, Assert.Single(result.Receipts,
                receipt => receipt.CommandId == command.CommandId).CommandHash);
        }
        var transfers = result.Candidate.Coordination.ConsentedMaterial.Transfers;
        Assert.Equal(transfers[0].CandidateInventoryHash, transfers[1].OriginalInventoryHash);
        Assert.Equal(result.Candidate.Inventory.ContentHash, transfers[1].CandidateInventoryHash);
        Assert.Equal(result.Candidate.ContentHash, WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(result.Candidate)).ContentHash);
        _ = CutAndDecode(session, null, out _);
    }

    [Fact]
    public void A_third_actor_ordered_between_acceptance_and_offer_keeps_its_own_outcome_beside_the_applied_pair()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var journey = fixture.Journey;
        var session = new CareWorldSession(WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis));
        journey.Approach(session);
        WorldSnapshot before = session.ReadStateForHost();
        // The recipient sorts first, so its acceptance is deferred to the later offer's ordinal.
        EntityId recipient = new[] { journey.Player, journey.Helper }.Min();
        EntityId sender = recipient == journey.Player ? journey.Helper : journey.Player;
        EntityId[] others = [journey.First.TargetActorId, journey.Second.TargetActorId];
        EntityId[] between = others.Where(actor => actor.CompareTo(recipient) > 0 && actor.CompareTo(sender) < 0).ToArray();
        Assert.True(between.Length != 0, "An admitted third actor must sort between the recipient and the sender.");
        var pair = Pair(before, journey, RawTerms(before, sender, recipient));
        WorldActionCommand[] thirds = others.Distinct().Select(actor => journey.Command(before, actor,
            new SpeakToActorIntent(recipient, before.Space.GetActor(recipient).Revision))).ToArray();
        // Each third submission is first decided alone against the same root; nothing is published.
        var alone = thirds.ToDictionary(command => command.CommandId, command => Assert.Single(new WorldStepProcessor()
            .Decide(before, journey.Batch(before, WorldBatchKind.AdvancingStep, [command])).Receipts).Rejection);
        var batch = journey.Batch(before, WorldBatchKind.AdvancingStep, [pair.Offer, .. thirds, pair.Acceptance]);
        Assert.Equal(batch.Actions.Select(value => value.ActorId).Order(), batch.Actions.Select(value => value.ActorId));
        int offerIndex = batch.Actions.ToList().FindIndex(value => value.CommandId == pair.Offer.CommandId);
        int acceptanceIndex = batch.Actions.ToList().FindIndex(value => value.CommandId == pair.Acceptance.CommandId);
        Assert.True(acceptanceIndex + 1 < offerIndex);

        WorldStepDecision result = session.Commit(batch);
        Assert.Equal(batch.Actions.Count, result.Receipts.Count);
        foreach (WorldActionCommand command in batch.Actions)
        {
            var receipt = Assert.Single(result.Receipts, value => value.CommandId == command.CommandId);
            Assert.Equal(command.ContentHash, receipt.CommandHash);
            Assert.Equal(alone.TryGetValue(command.CommandId, out var expected) ? expected : WorldActionRejection.None,
                receipt.Rejection);
            Assert.Contains(result.Identities, value => value.Identity == command.CommandId
                && value.Kind == WorldCommittedIdentityKind.Command);
        }
        WorldConsentedTransferEvidence evidence = Assert.Single(result.Candidate.Coordination!.ConsentedMaterial!.Transfers);
        Assert.Equal(((uint)offerIndex, (uint)acceptanceIndex), (evidence.OfferActionIndex, evidence.AcceptanceActionIndex));
        Assert.Equal((pair.Offer.CommandId, pair.Acceptance.CommandId), (evidence.CommandId, evidence.AcceptanceId));
        Assert.Equal(before.GetResident(recipient).InventoryCustodianId,
            result.Candidate.Inventory.Custody.GetLot(evidence.MovedLotId).CustodianId);
        Assert.Equal(before.AdvancingSteps + 1, result.Candidate.AdvancingSteps);
    }

    [Fact]
    public void Two_pairs_in_one_batch_are_decided_independently_with_four_receipts()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var journey = fixture.Journey;
        var session = new CareWorldSession(WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis));
        journey.Approach(session);
        WorldSnapshot before = session.ReadStateForHost();
        var first = Pair(before, journey, RawTerms(before, journey.Helper, journey.Player));
        EntityId otherSender = journey.First.TargetActorId;
        EntityId otherRecipient = journey.Second.TargetActorId;
        Assert.NotEqual(otherSender, otherRecipient);
        EntityId Storage(EntityId actor) => before.Inventory.Policies.Where(policy => policy.OwnerActorId == actor)
            .Select(policy => policy.CustodianId).DefaultIfEmpty(first.Offer.CommandId).First();
        var stock = before.Inventory.Custody.GetLot(((OfferMaterialIntent)first.Offer.Intent).Terms.LotId);
        // The second pair is exact and mutually addressed, but names goods its sender does not hold.
        var second = Pair(before, journey, new ConsentedMaterialTerms(before.WorldId, before.BranchId, otherSender,
            otherRecipient, ConsentedMaterialLotKind.CountedSource, stock.LotId, stock.Definition, Storage(otherSender),
            Storage(otherRecipient), 1, stock.Revision));
        var secondAlone = new WorldStepProcessor().Decide(before, journey.Batch(before, WorldBatchKind.AdvancingStep,
            [second.Offer, second.Acceptance]));
        Assert.Equal(2, secondAlone.Receipts.Count);
        Assert.All(secondAlone.Receipts, receipt => Assert.NotEqual(WorldActionRejection.None, receipt.Rejection));
        Assert.Equal(before.Inventory.ContentHash, secondAlone.Candidate.Inventory.ContentHash);

        var batch = journey.Batch(before, WorldBatchKind.AdvancingStep,
            [second.Acceptance, first.Acceptance, second.Offer, first.Offer]);
        WorldStepDecision result = session.Commit(batch);
        Assert.Equal(4, result.Receipts.Count);
        Assert.Equal(result.Receipts.Select(value => value.CommandId).Order(), result.Receipts.Select(value => value.CommandId));
        foreach (WorldActionCommand command in new[] { first.Offer, first.Acceptance })
        {
            var receipt = Assert.Single(result.Receipts, value => value.CommandId == command.CommandId);
            Assert.Equal((WorldActionRejection.None, command.ContentHash), (receipt.Rejection, receipt.CommandHash));
        }
        foreach (WorldActionCommand command in new[] { second.Offer, second.Acceptance })
        {
            var receipt = Assert.Single(result.Receipts, value => value.CommandId == command.CommandId);
            Assert.Equal((secondAlone.Receipts.Single(value => value.CommandId == command.CommandId).Rejection,
                command.ContentHash), (receipt.Rejection, receipt.CommandHash));
        }
        // Only the applied pair owns evidence, a split child and moved stock.
        WorldConsentedTransferEvidence evidence = Assert.Single(result.Candidate.Coordination!.ConsentedMaterial!.Transfers);
        Assert.Equal((first.Offer.CommandId, first.Acceptance.CommandId), (evidence.CommandId, evidence.AcceptanceId));
        Assert.Equal(first.Offer.CommandId, batch.Actions[(int)evidence.OfferActionIndex].CommandId);
        Assert.Equal(first.Acceptance.CommandId, batch.Actions[(int)evidence.AcceptanceActionIndex].CommandId);
        Assert.Contains(result.Identities, value => value.Identity == EntityId.DeriveSpawned(first.Offer.CommandId, 0)
            && value.Kind == WorldCommittedIdentityKind.SpawnedEntity);
        Assert.DoesNotContain(result.Identities, value => value.Identity == EntityId.DeriveSpawned(second.Offer.CommandId, 0));
        Assert.Equal(4, result.Identities.Count(value => value.Kind == WorldCommittedIdentityKind.Command));
        Assert.Equal(stock.Quantity - 1, result.Candidate.Inventory.Custody.GetLot(stock.LotId).Quantity);
    }

    [Theory]
    [InlineData(false)] // A third actor approves the offer that names the player.
    [InlineData(true)]  // The player approves an offer that names a third actor.
    public void Acceptance_addressed_to_an_offer_for_someone_else_moves_nothing(bool offerNamesThirdActor)
    {
        var fixture = new OrdinaryCareWorldFixture();
        var journey = fixture.Journey;
        var session = new CareWorldSession(WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis));
        journey.Approach(session);
        WorldSnapshot before = session.ReadStateForHost();
        EntityId third = journey.First.TargetActorId;
        ConsentedMaterialTerms valid = RawTerms(before, journey.Helper, journey.Player);
        // Positive control: the same goods, sender and correctly addressed recipient apply on this root.
        var control = Pair(before, journey, valid);
        Assert.All(new WorldStepProcessor().Decide(before, journey.Batch(before, WorldBatchKind.AdvancingStep,
            [control.Offer, control.Acceptance])).Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));

        ConsentedMaterialTerms offered = !offerNamesThirdActor ? valid : new(valid.WorldId, valid.BranchId,
            valid.SenderActorId, third, valid.LotKind, valid.LotId, valid.Definition, valid.SourceCustodianId,
            valid.DestinationCustodianId, valid.Quantity, valid.ExpectedLotRevision);
        EntityId accepting = offerNamesThirdActor ? journey.Player : third;
        WorldActionCommand offer = journey.Command(before, journey.Helper,
            new OfferMaterialIntent(offered, before.Space.GetActor(offered.RecipientActorId).Revision));
        WorldActionCommand acceptance = journey.Command(before, accepting,
            AcceptMaterialIntent.Approving(offer, before.Space.GetActor(journey.Helper).Revision));
        WorldStepDecision result = session.Commit(journey.Batch(before, WorldBatchKind.AdvancingStep, [offer, acceptance]));
        Assert.Equal(2, result.Receipts.Count);
        // The named recipient never submitted; the other actor's approval is not its consent.
        Assert.Equal(WorldActionRejection.Unavailable,
            result.Receipts.Single(value => value.CommandId == offer.CommandId).Rejection);
        Assert.Equal(WorldActionRejection.NotAuthorized,
            result.Receipts.Single(value => value.CommandId == acceptance.CommandId).Rejection);
        Assert.Equal(before.Inventory.ContentHash, result.Candidate.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, result.Candidate.Production!.ContentHash);
        Assert.Empty(result.Candidate.Coordination!.ConsentedMaterial!.Transfers);
        Assert.DoesNotContain(result.Identities, value => value.Kind == WorldCommittedIdentityKind.SpawnedEntity);

        WorldSnapshot next = result.Candidate;
        var later = Pair(next, journey, RawTerms(next, journey.Helper, journey.Player));
        var applied = CareRewardJourney.Commit(session, journey.Batch(next, WorldBatchKind.AdvancingStep,
            [later.Offer, later.Acceptance]));
        Assert.Single(applied.Candidate.Coordination!.ConsentedMaterial!.Transfers);
    }
}
