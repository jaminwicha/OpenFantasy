using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;
using static OpenFantasy.Core.Tests.Worlds.StepFixture;

namespace OpenFantasy.Core.Tests.Worlds;

public sealed class StepCustodyTests
{
    private readonly WorldStepProcessor _processor = new();

    [Fact]
    public void ASpatialHandoverConservesSourceUnitsAndPublishesOneOuterReceipt()
    {
        WorldSnapshot world = StepFixture.Create();
        WorldActionCommand command = Give(world, 1001, 4);
        WorldStepDecision decision = _processor.Decide(world, Batch(world, 1000, [command]));
        WorldActionReceipt receipt = Assert.Single(decision.Receipts);
        Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
        Assert.Equal(command.ContentHash, receipt.CommandHash);
        Assert.Equal(2UL, decision.Candidate.Inventory.Custody.GetStoredQuantity(Id(60)));
        Assert.Equal(4UL, decision.Candidate.Inventory.Custody.GetStoredQuantity(Id(61)));
        Assert.Equal(6UL, world.Inventory.Custody.GetStoredQuantity(Id(60)));
        CountedSourceLot child = decision.Candidate.Inventory.Custody.GetLot(EntityId.DeriveSpawned(command.CommandId, 0));
        Assert.Equal(new SourceInterval(Id(71), 0, 4), child.Source);
        Assert.Equal(new SourceInterval(Id(71), 4, 6), decision.Candidate.Inventory.Custody.GetLot(Id(70)).Source);
        Assert.Equal(world.Inventory.Custody.Revision + 1, decision.Candidate.Inventory.Custody.Revision);
        Assert.Equal(4, decision.Identities.Count);
    }

    [Fact]
    public void FullTransferKeepsLotIdentityAndDoesNotInventAChild()
    {
        WorldSnapshot world = StepFixture.Create();
        WorldStepDecision decision = _processor.Decide(world, Batch(world, 1000, [Give(world, 1001, 6)]));
        Assert.Equal(Id(61), Assert.Single(decision.Candidate.Inventory.Custody.Lots).CustodianId);
        Assert.DoesNotContain(decision.Identities, identity => identity.Kind == WorldCommittedIdentityKind.SpawnedEntity);
    }

    [Fact]
    public void AHostControlledActorCanGiveThroughTheSameRules()
    {
        WorldSnapshot original = StepFixture.Create();
        CountedAuthorityState old = original.Inventory;
        var custody = new CustodySnapshot(old.WorldId, old.BranchId, old.Custody.Revision, old.Custody.Storages,
            [new CountedSourceLot(Id(70), Id(61), Material, new SourceInterval(Id(71), 0, 6), 0)]);
        var inventory = new CountedAuthorityState(custody, old.AdmittedUnitCapacity, old.Actors, old.Controls,
            old.Policies.Select(policy => new CustodianAccessPolicy(policy.CustodianId, policy.OwnerActorId,
                CustodyDepositRule.AnyControlledActor)), old.Sources);
        var world = new WorldSnapshot(inventory, original.Space, original.Residents, original.AdmissionVersion,
            original.Calendar, 0, 0);
        WorldActionCommand command = Action(world, 1001, new GiveItemIntent(Id(10), 0, Id(70), 0, Id(61), 0, 4), 11);
        WorldStepDecision decision = _processor.Decide(world, Batch(world, 1000, [command]));
        Assert.Equal(CommandReceiptStatus.Applied, decision.Receipts[0].Status);
        Assert.Equal(4UL, decision.Candidate.Inventory.Custody.GetStoredQuantity(Id(60)));
        Assert.Equal(2UL, decision.Candidate.Inventory.Custody.GetStoredQuantity(Id(61)));
    }

    [Fact]
    public void OutOfReachIsAdjudicatedBeforeLookingUpAClaimedPrivateLot()
    {
        WorldSnapshot initial = StepFixture.Create();
        ActorPresence target = initial.Space.GetActor(Id(11));
        var distant = new ActorPresence(target.Binding, target.Condition, target.Scope,
            new SubunitPosition(1024, 0, 256), 0, target.HomePlaceId);
        WorldSnapshot world = Copy(initial, space: new LocalSpatialSnapshot(initial.Space.Scope, initial.Space.Regions,
            [initial.Space.GetActor(Id(10)), distant]));
        WorldActionCommand unknownLot = Action(world, 1001, new GiveItemIntent(Id(11), 0, Id(999), 0, Id(60), 0, 4));
        WorldStepDecision result = _processor.Decide(world, Batch(world, 1000, [unknownLot], [Move(world, 10)]));
        Assert.Equal(WorldActionRejection.OutOfReach, result.Receipts[0].Rejection);
        Assert.Equal(world.Inventory.ContentHash, result.Candidate.Inventory.ContentHash);
        Assert.Equal(32, result.Candidate.Space.GetActor(Id(10)).Feet.X);
    }

    [Fact]
    public void ASelectedHeldHandoverDoesNotAdvanceCalendarOrExposure()
    {
        WorldSnapshot initial = StepFixture.Create();
        WorldSnapshot held = _processor.Decide(initial, Batch(initial, 1000,
            [Action(initial, 1001, new SpeakToActorIntent(Id(11), 0))])).Candidate;
        WorldStepDecision result = _processor.Decide(held, Batch(held, 1002, [Give(held, 1003, 4)]));
        Assert.Equal(CommandReceiptStatus.Applied, result.Receipts[0].Status);
        Assert.Equal(held.Calendar, result.Candidate.Calendar);
        Assert.Equal(held.AdvancingSteps, result.Candidate.AdvancingSteps);
        Assert.True(result.Candidate.HasReadingHold);
    }

    [Fact]
    public void StaleLotAndInsufficientUnitsRejectWithoutCustodyMutation()
    {
        WorldSnapshot world = StepFixture.Create();
        foreach (GiveItemIntent intent in new[]
        {
            new GiveItemIntent(Id(11), 0, Id(70), 1, Id(60), 0, 4),
            new GiveItemIntent(Id(11), 0, Id(70), 0, Id(60), 0, 7),
            new GiveItemIntent(Id(11), 0, Id(60), 0, Id(60), 0, 1),
            new GiveItemIntent(Id(11), 0, Id(61), 0, Id(60), 0, 1),
            new GiveItemIntent(Id(11), 0, Id(999), 0, Id(60), 0, 1)
        })
        {
            WorldStepDecision result = _processor.Decide(world, Batch(world, 1000, [Action(world, 1001, intent)]));
            Assert.Equal(WorldActionRejection.Unavailable, result.Receipts[0].Rejection);
            Assert.Equal(world.Inventory.ContentHash, result.Candidate.Inventory.ContentHash);
            Assert.Equal(1UL, result.Candidate.AdvancingSteps);
        }
    }

    [Fact]
    public void AnotherActorsStorageCannotBeUsedAsTheSource()
    {
        WorldSnapshot world = StepFixture.Create();
        WorldActionCommand stolen = Action(world, 1001, new GiveItemIntent(Id(11), 0, Id(70), 0, Id(61), 0, 1));
        WorldStepDecision result = _processor.Decide(world, Batch(world, 1000, [stolen]));
        Assert.Equal(WorldActionRejection.NotAuthorized, result.Receipts[0].Rejection);
        Assert.Equal(world.Inventory.ContentHash, result.Candidate.Inventory.ContentHash);
    }

    [Fact]
    public void HistoricalChildCollisionRejectsTheTransferAndReservesOnlyItsCauseAndOutcome()
    {
        WorldSnapshot world = StepFixture.Create();
        WorldActionCommand command = Give(world, 1001, 4);
        EntityId child = EntityId.DeriveSpawned(command.CommandId, 0);
        WorldStepDecision result = _processor.Decide(world, Batch(world, 1000, [command]), id => id == child);
        Assert.Equal(WorldActionRejection.Unavailable, result.Receipts[0].Rejection);
        Assert.Equal(world.Inventory.ContentHash, result.Candidate.Inventory.ContentHash);
        Assert.Equal(3, result.Identities.Count);
    }

    [Fact]
    public void PreviouslyOccupiedCauseCommandOrResultCannotProduceACommit()
    {
        WorldSnapshot world = StepFixture.Create();
        WorldActionCommand command = Give(world, 1001, 4);
        WorldStepBatch batch = Batch(world, 1000, [command]);
        WorldStepDecision anticipated = _processor.Decide(world, batch);
        foreach (EntityId collision in new[] { batch.CauseId, command.CommandId, anticipated.Receipts[0].ResultId })
        {
            Assert.Throws<InvalidOperationException>(() => _processor.Decide(world, batch, id => id == collision));
            Assert.Equal(6UL, world.Inventory.Custody.GetStoredQuantity(Id(60)));
        }
    }

    private static WorldActionCommand Give(WorldSnapshot world, ulong command, ulong quantity) =>
        Action(world, command, new GiveItemIntent(Id(11), world.Space.GetActor(Id(11)).Revision, Id(70), 0, Id(60), 0, quantity));
}
