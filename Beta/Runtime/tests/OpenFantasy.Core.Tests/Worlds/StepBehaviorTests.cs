using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;
using static OpenFantasy.Core.Tests.Worlds.StepFixture;

namespace OpenFantasy.Core.Tests.Worlds;

public sealed class StepBehaviorTests
{
    private readonly WorldStepProcessor _processor = new();

    [Fact]
    public void ASelectedStepAdvancesEveryResidentOnceEvenWithoutInput()
    {
        WorldSnapshot world = StepFixture.Create();
        WorldStepDecision decision = _processor.Decide(world, Batch(world, 1000));
        Assert.Equal(world.Calendar.AddMilliseconds(1800), decision.Candidate.Calendar);
        Assert.Equal(1UL, decision.Candidate.AdvancingSteps);
        Assert.Equal(1UL, decision.Candidate.Revision);
        Assert.All(decision.Candidate.Residents, person => Assert.Equal(ExposurePacingV1.FromExplorationSteps(1), person.ExperiencedTime));
        Assert.Equal(world.Inventory.ContentHash, decision.Candidate.Inventory.ContentHash);
        Assert.Empty(decision.Receipts);
        Assert.Equal(new WorldCommittedIdentity(Id(1000), WorldCommittedIdentityKind.CommitCause), Assert.Single(decision.Identities));
        Assert.Equal(0UL, world.AdvancingSteps);
    }

    [Fact]
    public void PlayerAndHostUseIdenticalFixedMovementAndBodyOccupancy()
    {
        WorldSnapshot world = StepFixture.Create();
        WorldStepDecision moved = _processor.Decide(world, Batch(world, 1000,
            movement: [Move(world, 10), Move(world, 11)]));
        foreach (ulong actor in new ulong[] { 10, 11 })
        {
            Assert.Equal(world.Space.GetActor(Id(actor)).Feet.Offset(32, 0, 0), moved.Candidate.Space.GetActor(Id(actor)).Feet);
            Assert.Equal(1UL, moved.Candidate.Space.GetActor(Id(actor)).Revision);
        }
        Assert.Equal(world.Calendar.AddMilliseconds(1800), moved.Candidate.Calendar);
    }

    [Fact]
    public void OpeningDialogueHoldsAllMovementButContributesTheAlreadySelectedStep()
    {
        WorldSnapshot world = StepFixture.Create();
        WorldActionCommand speak = Action(world, 1001, new SpeakToActorIntent(Id(11), 0));
        WorldStepDecision opened = _processor.Decide(world, Batch(world, 1000, [speak], [Move(world, 10), Move(world, 11)]));
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(opened.Receipts).Status);
        DialogueSession dialogue = Assert.Single(opened.Candidate.Dialogues);
        Assert.Equal(EntityId.DeriveSpawned(speak.CommandId, 0), dialogue.DialogueId);
        Assert.Equal(speak.CommandId, dialogue.CauseId);
        Assert.Equal(world.Calendar, dialogue.StartedAt);
        Assert.Equal(world.Calendar.AddMilliseconds(1800), opened.Candidate.Calendar);
        Assert.Equal(world.Space.Actors.Select(actor => actor.Feet), opened.Candidate.Space.Actors.Select(actor => actor.Feet));

        WorldActionCommand close = Action(opened.Candidate, 1003, new CloseDialogueIntent(dialogue.DialogueId));
        WorldStepDecision closed = _processor.Decide(opened.Candidate, Batch(opened.Candidate, 1002, [close]));
        Assert.Empty(closed.Candidate.Dialogues);
        Assert.Equal(opened.Candidate.Calendar, closed.Candidate.Calendar);
        Assert.Equal(opened.Candidate.AdvancingSteps, closed.Candidate.AdvancingSteps);
        Assert.Equal(opened.Candidate.Residents.Select(resident => resident.ExperiencedTime),
            closed.Candidate.Residents.Select(resident => resident.ExperiencedTime));
        WorldStepDecision resumed = _processor.Decide(closed.Candidate, Batch(closed.Candidate, 1004));
        Assert.Equal(2UL, resumed.Candidate.AdvancingSteps);
        Assert.Equal(3UL, resumed.Candidate.Revision);
    }

    [Fact]
    public void OnlyTheAddressedListenerCanCloseTheirDialogue()
    {
        WorldSnapshot world = StepFixture.Create();
        WorldSnapshot opened = _processor.Decide(world, Batch(world, 1000,
            [Action(world, 1001, new SpeakToActorIntent(Id(11), 0))])).Candidate;
        WorldActionCommand wrongActor = Action(opened, 1003, new CloseDialogueIntent(opened.Dialogues[0].DialogueId), 11);
        WorldStepDecision rejected = _processor.Decide(opened, Batch(opened, 1002, [wrongActor]));
        Assert.Equal(WorldActionRejection.Unavailable, rejected.Receipts[0].Rejection);
        Assert.Single(rejected.Candidate.Dialogues);
        Assert.Equal(opened.Calendar, rejected.Candidate.Calendar);
    }

    [Fact]
    public void AStepCannotBypassAHoldOrReuseAStaleClockRevision()
    {
        WorldSnapshot world = StepFixture.Create();
        WorldStepBatch first = Batch(world, 1000, [Action(world, 1001, new SpeakToActorIntent(Id(11), 0))]);
        WorldSnapshot held = _processor.Decide(world, first).Candidate;
        Assert.Throws<InvalidOperationException>(() => _processor.Decide(held, first));
        Assert.Throws<InvalidOperationException>(() => _processor.Decide(held,
            new WorldStepBatch(Id(1002), world.WorldId, world.BranchId, held.Revision, held.AdvancingSteps,
                WorldBatchKind.AdvancingStep, [], [])));
        Assert.Throws<InvalidOperationException>(() => _processor.Decide(world,
            new WorldStepBatch(Id(1002), world.WorldId, world.BranchId, 0, 0, WorldBatchKind.HeldInteraction,
                [], [Action(world, 1003, new SpeakToActorIntent(Id(11), 0))])));
    }

    [Fact]
    public void UnauthorizedMovementIsIgnoredWithoutBuyingExtraTime()
    {
        WorldSnapshot world = StepFixture.Create();
        var forged = new WorldMovementInput(new TrustedCommandDelivery(Id(101), Id(111)), Id(10), 32, 0);
        WorldStepDecision decision = _processor.Decide(world, Batch(world, 1000, movement: [forged]));
        Assert.Equal(world.Space.GetActor(Id(10)).Feet, decision.Candidate.Space.GetActor(Id(10)).Feet);
        Assert.Equal(1UL, decision.Candidate.AdvancingSteps);
    }

    [Fact]
    public void StaleActorRevisionRejectsActionWhileItsSelectedMovementCanProceed()
    {
        WorldSnapshot initial = StepFixture.Create();
        WorldActionCommand stale = Action(initial, 1002, new SpeakToActorIntent(Id(11), 0));
        WorldSnapshot moved = _processor.Decide(initial, Batch(initial, 1000, movement: [Move(initial, 10)])).Candidate;
        WorldStepDecision decision = _processor.Decide(moved, Batch(moved, 1001, [stale], [Move(moved, 10)]));
        Assert.Equal(WorldActionRejection.RevisionConflict, decision.Receipts[0].Rejection);
        Assert.False(decision.Candidate.HasReadingHold);
        Assert.Equal(64, decision.Candidate.Space.GetActor(Id(10)).Feet.X);
    }

    [Fact]
    public void MissingAnatomicalFunctionBlocksItsActionButScarsDoNot()
    {
        foreach (BodyInjuryKind kind in new[] { BodyInjuryKind.FunctionLoss, BodyInjuryKind.Scar })
        {
            WorldSnapshot initial = StepFixture.Create();
            ActorPresence target = initial.Space.GetActor(Id(11));
            var injury = new BodyInjury(Id(120), Id(130), new BodySlotKey("core"), kind,
                kind == BodyInjuryKind.FunctionLoss ? BodyFunction.Vocalization : null);
            var impaired = new ActorPresence(target.Binding,
                new BodyConditionSnapshot(target.Binding.FormInstanceId, target.Condition.Definition, [injury]),
                target.Scope, target.Feet, target.Revision, target.HomePlaceId);
            WorldSnapshot world = Copy(initial, space: new LocalSpatialSnapshot(initial.Space.Scope, initial.Space.Regions,
                [initial.Space.GetActor(Id(10)), impaired]));
            WorldStepDecision result = _processor.Decide(world, Batch(world, 1000,
                [Action(world, 1001, new SpeakToActorIntent(Id(11), 0))]));
            Assert.Equal(kind == BodyInjuryKind.Scar ? WorldActionRejection.None : WorldActionRejection.UnableToCommunicate,
                result.Receipts[0].Rejection);
        }
    }

    [Fact]
    public void CalendarOverflowCannotProduceAPartialPreparedTransition()
    {
        WorldSnapshot world = Copy(StepFixture.Create(), calendar: new WorldInstant(long.MaxValue, 86_399_999));
        byte[] before = WorldSnapshotCodecV1.Encode(world);
        Assert.Throws<OverflowException>(() => _processor.Decide(world, Batch(world, 1000, movement: [Move(world, 10)])));
        Assert.Equal(before, WorldSnapshotCodecV1.Encode(world));
    }
}
