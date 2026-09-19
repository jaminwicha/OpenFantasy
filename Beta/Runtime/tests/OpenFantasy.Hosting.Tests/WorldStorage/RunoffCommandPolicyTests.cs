using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class RunoffCommandPolicyTests
{
    internal static AdmitRunoffIntent Admit(WorldSnapshot state, long startOffset = 0, EntityId? reservation = null)
    {
        var owner = state.Reservoirs!;
        var start = state.Calendar.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep + startOffset);
        return new(owner.ContentHash, owner.Capacity.Revision, reservation ?? ReservoirWorldFixture.SecondReservation, start,
            start.AddMilliseconds(1000), [new(owner.Capacity.Domain.Network.Basins[0].BasinId, 3000,
                new(ReservoirWorldFixture.Source, 1300, 1900), ReservoirWorldFixture.SourceStore)]);
    }

    internal static CancelRunoffIntent Cancel(WorldSnapshot state, EntityId? reservation = null) =>
        new(state.Reservoirs!.ContentHash, state.Reservoirs.Capacity.Revision, reservation ?? ReservoirWorldFixture.Reservation);

    internal static WorldStepDecision Act(ReservoirWorldFixture fixture, WorldSnapshot state, RunoffCommitmentIntent intent) =>
        new WorldStepProcessor().Decide(state, fixture.Actions.Batch(state,
            [fixture.Command(state, state.Reservoirs!.Capacity.Domain.OwnerActorId, intent)], []));

    [Fact]
    public void AlreadyFundedAdmissionChangesOnlyPhysicalObligationsAndRejectsDuplicateOrClosedReuse()
    {
        var fixture = new ReservoirWorldFixture(spareBudget: true);
        var state = fixture.Genesis;
        var result = Act(fixture, state, Admit(state));
        Assert.Equal(WorldActionRejection.None, Assert.Single(result.Receipts).Rejection);
        var next = result.Candidate;
        Assert.Equal(2, next.Reservoirs!.Capacity.Pending.Count);
        Assert.Equal(1UL, next.Reservoirs.Capacity.Revision);
        Assert.Equal(state.Inventory.ContentHash, next.Inventory.ContentHash);
        Assert.Equal(state.Production!.ContentHash, next.Production!.ContentHash);
        Assert.Equal(1200UL, next.Reservoirs.Capacity.Receivers.Single().IncomingUnits);
        Assert.Equal(3, result.Identities.Count);
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(Act(fixture, next, Admit(next)).Receipts).Rejection);
        next = Act(fixture, next, Cancel(next, ReservoirWorldFixture.SecondReservation)).Candidate;
        Assert.Contains(ReservoirWorldFixture.SecondReservation, next.Reservoirs!.Capacity.Closed);
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(Act(fixture, next, Admit(next)).Receipts).Rejection);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void AdmissionCannotBeginBeforeTheSelectedStepsPublicationCalendar(long offset, bool accepted)
    {
        var fixture = new ReservoirWorldFixture(spareBudget: true);
        var state = fixture.Genesis;
        var result = Act(fixture, state, Admit(state, offset));
        Assert.Equal(accepted ? WorldActionRejection.None : WorldActionRejection.Unavailable, Assert.Single(result.Receipts).Rejection);
        Assert.Equal(state.Calendar.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep), result.Candidate.Calendar);
        Assert.Equal(state.Inventory.ContentHash, result.Candidate.Inventory.ContentHash);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StaleWholeOwnerHashOrCapacityRevisionCannotHideBehindUnchangedCustody(bool staleHash)
    {
        var fixture = new ReservoirWorldFixture(spareBudget: true);
        var state = fixture.Genesis;
        var next = Act(fixture, state, Cancel(state)).Candidate;
        var intent = new CancelRunoffIntent(staleHash ? state.Reservoirs!.ContentHash : next.Reservoirs!.ContentHash,
            staleHash ? next.Reservoirs!.Capacity.Revision : state.Reservoirs!.Capacity.Revision, ReservoirWorldFixture.Reservation);
        var result = Act(fixture, next, intent);
        Assert.Equal(WorldActionRejection.RevisionConflict, Assert.Single(result.Receipts).Rejection);
        Assert.Equal(state.Inventory.ContentHash, next.Inventory.ContentHash);
        Assert.Equal(next.Reservoirs!.ContentHash, result.Candidate.Reservoirs!.ContentHash);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WrongControllerOrNonOwnerCannotManagePrivateCommitments(bool wrongController)
    {
        var fixture = new ReservoirWorldFixture();
        var state = fixture.Genesis;
        EntityId owner = state.Reservoirs!.Capacity.Domain.OwnerActorId;
        EntityId other = state.Residents.Single(r => r.ActorId != owner).ActorId;
        var valid = fixture.Command(state, other, Cancel(state));
        var command = wrongController ? new WorldActionCommand(valid.CommandId, valid.PrincipalId, valid.OriginSessionId,
            owner, state.WorldId, state.BranchId, state.Space.GetActor(owner).Revision, valid.Intent) : valid;
        var result = new WorldStepProcessor().Decide(state, fixture.Actions.Batch(state, [command], []));
        Assert.Equal(WorldActionRejection.NotAuthorized, Assert.Single(result.Receipts).Rejection);
        Assert.Equal(state.Reservoirs.ContentHash, result.Candidate.Reservoirs!.ContentHash);
    }

    [Fact]
    public void StaleActorAndUnfundedOrCompetingReservationsRejectWithoutChangingPhysicalState()
    {
        var fixture = new ReservoirWorldFixture(spareBudget: true);
        var state = fixture.Genesis;
        var valid = fixture.Command(state, state.Reservoirs!.Capacity.Domain.OwnerActorId, Cancel(state));
        var stale = new WorldActionCommand(valid.CommandId, valid.PrincipalId, valid.OriginSessionId, valid.ActorId,
            valid.WorldId, valid.BranchId, valid.ExpectedActorRevision + 1, valid.Intent);
        var decision = new WorldStepProcessor().Decide(state, fixture.Actions.Batch(state, [stale], []));
        Assert.Equal(WorldActionRejection.RevisionConflict, Assert.Single(decision.Receipts).Rejection);
        foreach (var id in new[] { ReservoirWorldFixture.Id(999), state.Quests!.Definitions.Single().MaterialReservationId })
        {
            decision = Act(fixture, state, Admit(state, reservation: id));
            Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(decision.Receipts).Rejection);
            Assert.Equal(state.Reservoirs.ContentHash, decision.Candidate.Reservoirs!.ContentHash);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadingHoldRejectsBothOperationsWithoutAdvancingTime(bool admit)
    {
        var fixture = new ReservoirWorldFixture(spareBudget: true);
        var state = ApproachOther(fixture, fixture.Genesis);
        EntityId player = state.Reservoirs!.Capacity.Domain.OwnerActorId;
        EntityId wren = state.Residents.Single(r => r.ActorId != player).ActorId;
        state = new WorldStepProcessor().Decide(state, fixture.Actions.Batch(state,
            [fixture.Command(state, player, new SpeakToActorIntent(wren, state.Space.GetActor(wren).Revision))], [])).Candidate;
        Assert.True(state.HasReadingHold);
        var decision = Act(fixture, state, admit ? Admit(state) : Cancel(state));
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(decision.Receipts).Rejection);
        Assert.Equal(state.Calendar, decision.Candidate.Calendar);
        Assert.Equal(state.Reservoirs!.ContentHash, decision.Candidate.Reservoirs!.ContentHash);
    }


    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void CapacityControlsAndIncomingStockUseLatestAcceptedOwnerInEitherOrder(bool cancel, bool controlFirst)
    {
        ulong quantity = cancel ? 1351UL : 1000UL;
        var fixture = Enumerable.Range(0, 16).Select(i => new ReservoirWorldFixture(primaryReceiver: true,
            incomingSupply: quantity, worldSeed: "reservoir-action-order-" + i, spareBudget: !cancel)).First(f =>
            (f.Reservoirs.Capacity.Domain.OwnerActorId.CompareTo(f.Basis.Residents.Single(r =>
                r.ControllerKind == ResidentControllerKind.Host).ActorId) < 0) == controlFirst);
        var state = ApproachOther(fixture, fixture.Genesis);
        EntityId owner = state.Reservoirs!.Capacity.Domain.OwnerActorId;
        EntityId giver = state.Residents.Single(r => r.ActorId != owner).ActorId;
        var lot = state.Inventory.Custody.GetLot(ReservoirWorldFixture.Id(13));
        var give = fixture.Command(state, giver, new GiveItemIntent(owner, state.Space.GetActor(owner).Revision,
            lot.LotId, lot.Revision, lot.CustodianId, state.Inventory.Custody.GetStorage(lot.CustodianId).Revision, quantity));
        var control = fixture.Command(state, owner, cancel ? Cancel(state) : Admit(state));
        var batch = fixture.Actions.Batch(state, [give, control], []);
        Assert.Equal(controlFirst ? control.CommandId : give.CommandId, batch.Actions[0].CommandId);
        var decision = new WorldStepProcessor().Decide(state, batch);
        var controlReceipt = decision.Receipts.Single(r => r.CommandId == control.CommandId);
        var giveReceipt = decision.Receipts.Single(r => r.CommandId == give.CommandId);
        bool giftAccepted = cancel ? controlFirst : !controlFirst;
        Assert.Equal(giftAccepted ? WorldActionRejection.None : WorldActionRejection.Unavailable, giveReceipt.Rejection);
        Assert.Equal(cancel || controlFirst ? WorldActionRejection.None : WorldActionRejection.RevisionConflict, controlReceipt.Rejection);
        var physical = decision.Candidate.Reservoirs!;
        Assert.Equal(cancel ? 0 : controlFirst ? 2 : 1, physical.Capacity.Pending.Count);
        Assert.Equal(giftAccepted ? 50 + quantity : 50UL,
            physical.Projections.Single(p => p.Geometry.CustodianId == ReservoirWorldFixture.Receiver).ExistingUnits);
        Assert.Equal(cancel && giftAccepted ? 2UL : 1UL, physical.Capacity.Revision);
        Assert.Equal(decision.Candidate.Inventory.ContentHash, physical.Capacity.Inventory.ContentHash);
        Assert.Equal(1900UL - (cancel ? 600UL : 0UL), physical.Projections.Single(p =>
            p.Geometry.CustodianId == ReservoirWorldFixture.SourceStore).ExistingUnits);
    }

    [Fact]
    public void AReadingHoldOpenedEarlierInTheSameAdvancingBatchBlocksCommitmentControl()
    {
        var fixture = Enumerable.Range(0, 16).Select(i => new ReservoirWorldFixture(worldSeed: "reservoir-action-order-" + i))
            .First(f => f.Reservoirs.Capacity.Domain.OwnerActorId.CompareTo(f.Basis.Residents.Single(r =>
                r.ControllerKind == ResidentControllerKind.Host).ActorId) > 0);
        var basis = fixture.Genesis;
        EntityId owner = basis.Reservoirs!.Capacity.Domain.OwnerActorId;
        var other = basis.Residents.Single(r => r.ActorId != owner);
        // An explicit authored speaker variant gives both actors an utterance in their already admitted shared channel.
        var residents = basis.Residents.Select(r => r.ActorId == owner ? new WorldResident(r.ActorId,
            r.InventoryCustodianId, r.DisplayName, r.ControllerKind, r.HomeFeet, r.WanderRadiusCells,
            r.Comprehension, r.ExperiencedTime, other.OpeningUtterance) : r);
        var state = new WorldSnapshot(basis.Inventory, basis.Space, residents, basis.AdmissionVersion, basis.Calendar, 0, 0,
            gathering: basis.Gathering, production: basis.Production, progression: basis.Progression, quests: basis.Quests,
            construction: basis.Construction, reservoirs: basis.Reservoirs);
        state = ApproachOther(fixture, state);
        var speak = fixture.Command(state, other.ActorId, new SpeakToActorIntent(owner, state.Space.GetActor(owner).Revision));
        var control = fixture.Command(state, owner, Cancel(state));
        var batch = fixture.Actions.Batch(state, [control, speak], []);
        Assert.Equal(speak.CommandId, batch.Actions[0].CommandId);
        var result = new WorldStepProcessor().Decide(state, batch);
        Assert.Equal(WorldActionRejection.None, result.Receipts.Single(r => r.CommandId == speak.CommandId).Rejection);
        Assert.Equal(WorldActionRejection.Unavailable, result.Receipts.Single(r => r.CommandId == control.CommandId).Rejection);
        Assert.True(result.Candidate.HasReadingHold);
        Assert.Equal(state.Reservoirs!.ContentHash, result.Candidate.Reservoirs!.ContentHash);
    }

    internal static WorldSnapshot ApproachOther(ReservoirWorldFixture fixture, WorldSnapshot state)
    {
        EntityId player = state.Reservoirs!.Capacity.Domain.OwnerActorId;
        EntityId wren = state.Residents.Single(r => r.ActorId != player).ActorId;
        for (int i = 0; state.Space.QueryInteraction(player, wren) != SpatialInteraction.Reachable; i++)
        {
            Assert.True(i < 128);
            var feet = state.Space.GetActor(player).Feet; var target = state.Space.GetActor(wren).Feet;
            long x = Math.Sign(target.X - feet.X) * WorldMovementInput.DisplacementPerStep;
            long y = x == 0 ? Math.Sign(target.Y - feet.Y) * WorldMovementInput.DisplacementPerStep : 0;
            state = new WorldStepProcessor().Decide(state, fixture.Actions.Batch(state, [],
                [new(ConstructionUpgradeFixture.Delivery(state, player), player, x, y)])).Candidate;
        }
        return state;
    }
}
