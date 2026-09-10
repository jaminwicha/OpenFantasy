using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Core.Tests.Worlds;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;
using static OpenFantasy.Core.Tests.Gathering.GatheringFixture;

namespace OpenFantasy.Core.Tests.Gathering;

public sealed class GatheringStepTests
{
    private readonly WorldStepProcessor _processor = new();

    [Fact]
    public void HistoricalSpawnCollisionRejectsAtomicallyButTheSelectedStepStillAdvances()
    {
        WorldSnapshot world = GatheringFixture.Create(effort: 500);
        WorldActionCommand command = Command(world);
        EntityId collision = EntityId.DeriveSpawned(command.CommandId, 0);
        WorldStepDecision decision = _processor.Decide(world, Batch(world, 600, [command]), id => id == collision);
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(decision.Receipts).Rejection);
        Assert.Equal(world.Inventory.ContentHash, decision.Candidate.Inventory.ContentHash);
        Assert.Equal(0UL, decision.Candidate.Gathering!.GetNode(Node).NextUnissuedUnit);
        Assert.Equal(5, decision.Candidate.Gathering.GetNode(Node).RemainingSwings);
        Assert.Equal(510U, decision.Candidate.Gathering.GetActorWork(Id(10)).Effort);
        Assert.Equal(1UL, decision.Candidate.AdvancingSteps);
        Assert.DoesNotContain(decision.Identities, value => value.Kind == WorldCommittedIdentityKind.SpawnedEntity);
    }

    [Fact]
    public void StaleNodeToolAndPresenceRevisionsNeverIssueAClaim()
    {
        WorldSnapshot world = GatheringFixture.Create(effort: 500);
        WorldActionCommand valid = Command(world);
        WorldActionCommand[] commands =
        [
            Command(world, intent: new GatherIntent(Node, 1, ToolLot, 0)),
            Command(world, intent: new GatherIntent(Node, 0, ToolLot, 1)),
            new(valid.CommandId, valid.PrincipalId, valid.OriginSessionId, valid.ActorId,
                valid.WorldId, valid.BranchId, 1, valid.Intent)
        ];
        foreach (WorldActionCommand command in commands)
        {
            WorldStepDecision decision = _processor.Decide(world, Batch(world, 600, [command]));
            Assert.Equal(CommandReceiptStatus.Rejected, Assert.Single(decision.Receipts).Status);
            Assert.Equal(world.Inventory.ContentHash, decision.Candidate.Inventory.ContentHash);
            Assert.Equal(0UL, decision.Candidate.Gathering!.GetNode(Node).NextUnissuedUnit);
            Assert.Equal(510U, decision.Candidate.Gathering.GetActorWork(Id(10)).Effort);
        }
    }

    [Fact]
    public void GatherWhileReadingRejectsWithoutAnyCalendarExposureOrEffortGrowth()
    {
        WorldSnapshot world = With(GatheringFixture.Create(effort: 500), dialogues: [Dialogue()]);
        WorldStepDecision decision = _processor.Decide(world, Batch(world, 600, [Command(world)]));
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(decision.Receipts).Rejection);
        Assert.Equal(world.Calendar, decision.Candidate.Calendar);
        Assert.Equal(world.AdvancingSteps, decision.Candidate.AdvancingSteps);
        Assert.Equal(world.Gathering!.ContentHash, decision.Candidate.Gathering!.ContentHash);
        Assert.Equal(world.Residents.Select(value => value.ExperiencedTime),
            decision.Candidate.Residents.Select(value => value.ExperiencedTime));
    }

    [Fact]
    public void AnNpcUsesItsActualOwnedToolAndReceivesItsOwnYield()
    {
        WorldSnapshot world = GatheringFixture.Create(toolOwner: Id(61));
        WorldStepDecision decision = _processor.Decide(world, Batch(world, 600, [Command(world, actor: 11)]));
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(decision.Receipts).Status);
        Assert.Equal(2UL, decision.Candidate.Inventory.Custody.GetStoredQuantity(Id(61)));
        Assert.Equal(610U, decision.Candidate.Gathering!.GetActorWork(Id(11)).Effort);
        Assert.Equal(1000U, decision.Candidate.Gathering.GetActorWork(Id(10)).Effort);
    }

    [Fact]
    public void CompetingActorsUseCanonicalOrderAndCannotSpendTheSameNodeRevision()
    {
        WorldSnapshot world = WithSecondTool(GatheringFixture.Create());
        WorldActionCommand player = Command(world, 601);
        WorldActionCommand npc = Command(world, 602, 11, new GatherIntent(Node, 0, Id(207), 0));
        WorldStepDecision decision = _processor.Decide(world, Batch(world, 600, [npc, player]));
        Assert.Equal(player.CommandId, decision.Receipts[0].CommandId);
        Assert.Equal(WorldActionRejection.None, decision.Receipts[0].Rejection);
        Assert.Equal(WorldActionRejection.RevisionConflict, decision.Receipts[1].Rejection);
        Assert.Equal(1UL, decision.Candidate.Gathering!.GetNode(Node).NextUnissuedUnit);
        Assert.Equal(610U, decision.Candidate.Gathering.GetActorWork(Id(10)).Effort);
        Assert.Equal(1000U, decision.Candidate.Gathering.GetActorWork(Id(11)).Effort);
    }

    [Fact]
    public void GatherBeforeSpeakAppliesOnceAndNewReadingHoldSuppressesMovement()
    {
        WorldSnapshot world = GatheringFixture.Create();
        WorldActionCommand speak = StepFixture.Action(world, 602, new SpeakToActorIntent(Id(10), 0), 11);
        WorldStepDecision decision = _processor.Decide(world, Batch(world, 600, [speak, Command(world, 601)],
            [StepFixture.Move(world, 10)]));
        Assert.All(decision.Receipts, receipt => Assert.Equal(CommandReceiptStatus.Applied, receipt.Status));
        Assert.Single(decision.Candidate.Dialogues);
        Assert.Equal(1UL, decision.Candidate.Gathering!.GetNode(Node).NextUnissuedUnit);
        Assert.Equal(610U, decision.Candidate.Gathering.GetActorWork(Id(10)).Effort);
        Assert.Equal(1UL, decision.Candidate.AdvancingSteps);
        Assert.Equal(world.Space.GetActor(Id(10)).Feet, decision.Candidate.Space.GetActor(Id(10)).Feet);
    }

    [Fact]
    public void SpeakBeforeGatherDoesNotPermitAFreeSwingAfterTheHoldOpens()
    {
        WorldSnapshot world = GatheringFixture.Create(toolOwner: Id(61));
        WorldActionCommand speak = StepFixture.Action(world, 601, new SpeakToActorIntent(Id(11), 0));
        WorldStepDecision decision = _processor.Decide(world, Batch(world, 600,
            [Command(world, 602, 11), speak]));
        Assert.Equal(WorldActionRejection.None, decision.Receipts[0].Rejection);
        Assert.Equal(WorldActionRejection.Unavailable, decision.Receipts[1].Rejection);
        Assert.Equal(world.Inventory.ContentHash, decision.Candidate.Inventory.ContentHash);
        Assert.Equal(0UL, decision.Candidate.Gathering!.GetNode(Node).NextUnissuedUnit);
        Assert.Equal(1UL, decision.Candidate.AdvancingSteps);
    }

    private static WorldStepBatch Batch(WorldSnapshot world, ulong cause, IEnumerable<WorldActionCommand> actions,
        IEnumerable<WorldMovementInput>? movement = null) => new(Id(cause), world.WorldId, world.BranchId,
            world.Revision, world.AdvancingSteps, world.HasReadingHold ? WorldBatchKind.HeldInteraction : WorldBatchKind.AdvancingStep,
            movement ?? [], actions, world.ProfileVersion);

    private static WorldSnapshot WithSecondTool(WorldSnapshot world)
    {
        CountedAuthorityState old = world.Inventory;
        var custody = new CustodySnapshot(world.WorldId, world.BranchId, old.Custody.Revision, old.Custody.Storages,
            [.. old.Custody.Lots, new(Id(207), Id(61), Axe, new SourceInterval(Id(208), 0, 1), 0)]);
        var inventory = new CountedAuthorityState(custody, old.AdmittedUnitCapacity + 1, old.Actors,
            old.Controls, old.Policies, [.. old.Sources, new(Id(208), Axe, 1)]);
        return With(world, inventory);
    }
}
