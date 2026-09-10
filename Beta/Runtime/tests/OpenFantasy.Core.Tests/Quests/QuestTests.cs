using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;

namespace OpenFantasy.Core.Tests.Quests;

public sealed class QuestTests
{
    [Fact]
    public void AcceptanceCreatesFundedObligationWithoutPayingOrMovingMaterials()
    {
        QuestFixture fixture = QuestFixture.Create();
        DeliveryQuestPlan plan = fixture.Plan(fixture.Accept());
        Assert.True(plan.Succeeded);
        Assert.Same(fixture.World.Inventory, plan.CandidateInventory);
        Assert.Same(fixture.Progression, plan.CandidateProgression);
        Assert.Equal((UInt128)200, plan.CandidateQuests.OutstandingReward(QuestFixture.Reserve));
        Assert.Equal(ActorQuestStatus.Accepted, plan.CandidateQuests.Participations.Single().Status);
        Assert.Empty(fixture.Quests.Participations);
    }

    [Fact]
    public void CompletionMovesReservedWoodAndPaysActualLevelAndCurrencyTogether()
    {
        QuestFixture original = QuestFixture.Create();
        QuestFixture accepted = original.Apply(original.Plan(original.Accept()));
        DeliveryQuestPlan plan = accepted.Plan(accepted.Complete());
        Assert.True(plan.Succeeded);
        var actor = plan.CandidateProgression.GetActor(Id(10));
        Assert.Equal(3, actor.Level);
        Assert.Equal(38UL, actor.Experience);
        Assert.Equal((78, 14, 18, 6, 12, 10),
            (actor.Stats.MaxHp, actor.Stats.MaxMp, actor.Stats.Attack, actor.Stats.Magic, actor.Stats.Defense, actor.Stats.Speed));
        Assert.Equal((78, 14), (actor.CurrentHp, actor.CurrentMp));
        Assert.Equal(200UL, plan.CandidateProgression.Currency.GetActorAccount(Id(10)).Balance);
        Assert.Equal(0UL, plan.CandidateProgression.GetAccount(QuestFixture.Reserve).Balance);
        Assert.Equal(200UL, plan.CandidateProgression.Currency.AdmittedSupply);
        CountedSourceLot wood = plan.CandidateInventory.Custody.GetLot(Id(70));
        Assert.Equal(QuestFixture.Project, wood.CustodianId);
        Assert.Equal(QuestFixture.Reservation, wood.ReservedBy);
        Assert.Equal(original.World.Inventory.Custody.GetLot(Id(70)).Source, wood.Source);
        Assert.Equal(ActorQuestStatus.Completed, plan.CandidateQuests.Participations.Single().Status);
        Assert.Equal((UInt128)0, plan.CandidateQuests.OutstandingReward(QuestFixture.Reserve));
        Assert.Empty(plan.CreatedIds);
        Assert.Equal(0UL, original.Progression.Currency.GetActorAccount(Id(10)).Balance);
    }

    [Fact]
    public void CompletionWithoutAcceptanceAndRepeatedRewardAreRejected()
    {
        QuestFixture original = QuestFixture.Create();
        Assert.False(original.Plan(original.Complete()).Succeeded);
        QuestFixture accepted = original.Apply(original.Plan(original.Accept()));
        QuestFixture completed = accepted.Apply(accepted.Plan(accepted.Complete()));
        DeliveryQuestPlan repeat = completed.Plan(completed.Complete(command: 2410));
        Assert.False(repeat.Succeeded);
        Assert.Same(completed.Progression, repeat.CandidateProgression);
        Assert.Same(completed.World.Inventory, repeat.CandidateInventory);
        Assert.Same(completed.Quests, repeat.CandidateQuests);
    }

    [Fact]
    public void FailedProjectCapacityPreservesEveryComponent()
    {
        QuestFixture original = QuestFixture.Create(projectCapacity: 5);
        QuestFixture accepted = original.Apply(original.Plan(original.Accept()));
        DeliveryQuestPlan failure = accepted.Plan(accepted.Complete());
        Assert.False(failure.Succeeded);
        Assert.Same(accepted.World.Inventory, failure.CandidateInventory);
        Assert.Same(accepted.Progression, failure.CandidateProgression);
        Assert.Same(accepted.Quests, failure.CandidateQuests);
        Assert.Empty(failure.CreatedIds);
    }

    [Fact]
    public void StaleParticipationAndIngredientRevisionsDoNotPayOrMove()
    {
        QuestFixture initial = QuestFixture.Create();
        WorldActionCommand premature = initial.Complete();
        QuestFixture accepted = initial.Apply(initial.Plan(initial.Accept()));
        Assert.Equal(WorldActionRejection.RevisionConflict, accepted.Plan(premature).Rejection);
        Assert.Equal(WorldActionRejection.RevisionConflict,
            accepted.Plan(accepted.Complete(selections: [new(Id(70), 123, 6)])).Rejection);
        Assert.Equal(0UL, accepted.Progression.Currency.GetActorAccount(Id(10)).Balance);
    }

    [Fact]
    public void ActualWorldProcessorPreservesWaitAndCommitsQuestRewardOnce()
    {
        QuestFixture fixture = QuestFixture.Create();
        WorldSnapshot initial = fixture.AsWorld();
        var processor = new WorldStepProcessor();
        WorldStepDecision acceptance = processor.Decide(initial, Batch(initial, 2500, fixture.Accept()));
        Assert.Equal(CommandReceiptStatus.Applied, acceptance.Receipts.Single().Status);
        WorldSnapshot accepted = acceptance.Candidate;
        var next = new QuestFixture(accepted, accepted.Progression!, accepted.Quests!);
        WorldStepBatch completionBatch = Batch(accepted, 2501, next.Complete());
        WorldStepDecision completion = processor.Decide(accepted, completionBatch);
        Assert.Equal(CommandReceiptStatus.Applied, completion.Receipts.Single().Status);
        Assert.Equal(initial.Calendar, completion.Candidate.Calendar);
        Assert.Equal(initial.AdvancingSteps, completion.Candidate.AdvancingSteps);
        Assert.Equal(initial.Gathering!.ContentHash, completion.Candidate.Gathering!.ContentHash);
        Assert.Equal(initial.Production!.ContentHash, completion.Candidate.Production!.ContentHash);
        Assert.Equal(200UL, completion.Candidate.Progression!.Currency.GetActorAccount(Id(10)).Balance);
        WorldSnapshot restored = WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(accepted));
        WorldStepDecision replay = processor.Decide(restored, WorldStepCodec.Decode(WorldStepCodec.Encode(completionBatch)));
        Assert.Equal(completion.Candidate.ContentHash, replay.Candidate.ContentHash);
        Assert.Equal(WorldReceiptCodecV1.Encode(completion.Receipts.Single()), WorldReceiptCodecV1.Encode(replay.Receipts.Single()));
        Assert.Throws<InvalidOperationException>(() => processor.Decide(completion.Candidate, completionBatch));
    }

    [Fact]
    public void CraftedConsumptionCompetesWithTheQuestInsteadOfCreatingWoodCredit()
    {
        QuestFixture original = QuestFixture.Create(dialogue: false);
        QuestFixture spoken = QuestFixture.Create();
        QuestFixture accepted = spoken.Apply(spoken.Plan(spoken.Accept()));
        WorldSnapshot world = original.World;
        CraftRecipeDefinition recipe = world.Production!.Recipes.Single();
        var control = world.Inventory.Controls.Single(value => value.ActorId == Id(10));
        CraftPlan crafted = new InstantCraftPlanner().Plan(world.Inventory, world.Production,
            new OpenFantasy.Core.Authority.TrustedCommandDelivery(control.PrincipalId, control.SessionId),
            Id(10), Id(2510), new CraftRequest(recipe.DefinitionId, recipe.Version, world.Production.Revision,
                Id(60), 0, [new(IngredientLotKind.Source, Id(70), 0, 2)]));
        Assert.Equal(WorldActionRejection.None, crafted.Rejection);
        var changedWorld = new WorldSnapshot(crafted.CandidateInventory, accepted.World.Space, accepted.World.Residents,
            accepted.World.AdmissionVersion, accepted.World.Calendar, accepted.World.Revision + 1,
            accepted.World.AdvancingSteps, accepted.World.Dialogues, accepted.World.Gathering, crafted.CandidateProduction);
        var changed = accepted with { World = changedWorld };
        DeliveryQuestPlan failure = changed.Plan(changed.Complete(selections: [new(Id(70), 1, 4)]));
        Assert.False(failure.Succeeded);
        Assert.Same(changed.Progression, failure.CandidateProgression);
        Assert.Equal(4UL, changed.World.Inventory.Custody.GetLot(Id(70)).Quantity);
        Assert.Single(changed.World.Production!.Batches);
    }

    internal static WorldStepBatch Batch(WorldSnapshot world, ulong cause, params WorldActionCommand[] actions) =>
        new(Id(cause), world.WorldId, world.BranchId, world.Revision, world.AdvancingSteps,
            world.HasReadingHold ? WorldBatchKind.HeldInteraction : WorldBatchKind.AdvancingStep, [], actions, 4);
}
