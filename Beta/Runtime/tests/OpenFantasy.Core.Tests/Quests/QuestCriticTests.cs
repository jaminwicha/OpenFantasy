using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Language;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Tests.Worlds;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;

namespace OpenFantasy.Core.Tests.Quests;

public sealed class QuestCriticTests
{
    [Fact]
    public void FabricatedConversationCannotSubstituteForCommittedKnowledge()
    {
        QuestFixture fixture = QuestFixture.Create(dialogue: false);
        var invented = new DialogueSession(Id(3100), Id(10), Id(11), Id(92), Id(3101), fixture.World.Calendar);
        DeliveryQuestPlan result = new DeliveryQuestPlanner().Plan(fixture.World, fixture.World.Inventory,
            fixture.World.Production!, fixture.Progression, fixture.Quests, fixture.Accept(), [invented]);

        AssertRejectedUnchanged(fixture, result, WorldActionRejection.UnableToCommunicate);
    }

    [Fact]
    public void ReplacingOnlyTheCauseOfACommittedConversationIsRejected()
    {
        QuestFixture fixture = QuestFixture.Create();
        DialogueSession actual = fixture.World.Dialogues.Single();
        var replaced = new DialogueSession(actual.DialogueId, actual.ListenerActorId, actual.SpeakerActorId,
            actual.UtteranceDefinitionId, Id(3102), actual.StartedAt);
        DeliveryQuestPlan result = new DeliveryQuestPlanner().Plan(fixture.World, fixture.World.Inventory,
            fixture.World.Production!, fixture.Progression, fixture.Quests, fixture.Accept(), [replaced]);

        AssertRejectedUnchanged(fixture, result, WorldActionRejection.UnableToCommunicate);
    }

    [Fact]
    public void ConversationRemovedFromCurrentWorkingStateCannotAuthorizeAcceptance()
    {
        QuestFixture fixture = QuestFixture.Create();
        DeliveryQuestPlan result = new DeliveryQuestPlanner().Plan(fixture.World, fixture.World.Inventory,
            fixture.World.Production!, fixture.Progression, fixture.Quests, fixture.Accept(), []);

        AssertRejectedUnchanged(fixture, result, WorldActionRejection.UnableToCommunicate);
    }

    [Fact]
    public void UnfundedSecondActorCannotCreateAnUnpaidPromise()
    {
        QuestFixture fixture = WithSecondParticipant(200);
        WorldActionCommand secondAcceptance = fixture.Accept(3111, actor: 12);
        DeliveryQuestPlan first = fixture.Plan(fixture.Accept(3110));
        Assert.True(first.Succeeded);
        QuestFixture accepted = fixture.Apply(first);

        DeliveryQuestPlan second = accepted.Plan(secondAcceptance);

        AssertRejectedUnchanged(accepted, second, WorldActionRejection.Unavailable);
        Assert.Equal((UInt128)200, accepted.Quests.OutstandingReward(QuestFixture.Reserve));
        Assert.Null(accepted.Quests.FindParticipation(Id(12), fixture.Definition.DefinitionId));
    }

    [Fact]
    public void TwoExplicitlyFundedActorsCompleteWithoutGlobalRevisionDependencies()
    {
        QuestFixture fixture = WithSecondParticipant(400);
        WorldActionCommand secondAcceptance = fixture.Accept(3121, actor: 12);
        DeliveryQuestPlan firstAccept = fixture.Plan(fixture.Accept(3120));
        Assert.True(firstAccept.Succeeded);
        fixture = fixture.Apply(firstAccept);
        DeliveryQuestPlan secondAccept = fixture.Plan(secondAcceptance);
        Assert.True(secondAccept.Succeeded);
        fixture = fixture.Apply(secondAccept);

        WorldActionCommand secondCompletion = fixture.Complete(3123, [new(Id(2501), 0, 6)], actor: 12);
        DeliveryQuestPlan firstCompletion = fixture.Plan(fixture.Complete(3122));
        Assert.True(firstCompletion.Succeeded);
        fixture = fixture.Apply(firstCompletion);
        DeliveryQuestPlan secondComplete = fixture.Plan(secondCompletion);
        Assert.True(secondComplete.Succeeded);
        fixture = fixture.Apply(secondComplete);

        Assert.Equal(0UL, fixture.Progression.Currency.GetAccount(QuestFixture.Reserve).Balance);
        Assert.Equal(200UL, fixture.Progression.Currency.GetActorAccount(Id(10)).Balance);
        Assert.Equal(200UL, fixture.Progression.Currency.GetActorAccount(Id(12)).Balance);
        Assert.Equal(0UL, fixture.Progression.Currency.GetActorAccount(Id(11)).Balance);
        Assert.Equal(400UL, fixture.Progression.Currency.AdmittedSupply);
        Assert.Equal((UInt128)0, fixture.Quests.OutstandingReward(QuestFixture.Reserve));
        Assert.Equal(4UL, fixture.Quests.Revision);
        Assert.All(fixture.Quests.Participations, entry => Assert.Equal(ActorQuestStatus.Completed, entry.Status));
        Assert.Equal(12UL, fixture.World.Inventory.Custody.Lots
            .Where(lot => lot.CustodianId == QuestFixture.Project).Aggregate(0UL, (sum, lot) => sum + lot.Quantity));
        Assert.Equal(3, fixture.Progression.GetActor(Id(12)).Level);
        Assert.Equal(38UL, fixture.Progression.GetActor(Id(12)).Experience);
        Assert.Equal(1, fixture.Progression.GetActor(Id(11)).Stats.MaxHp);
    }

    [Fact]
    public void MultiplePartialLotsRetainDistinctSourceIntervalsAndAdvanceStorageOnce()
    {
        QuestFixture fixture = WithTwoPartialLots();
        fixture = fixture.Apply(fixture.Plan(fixture.Accept(3130)));
        WorldActionCommand command = fixture.Complete(3131, [new(Id(72), 0, 3), new(Id(70), 0, 3)]);
        ulong sourceRevision = fixture.World.Inventory.Custody.GetStorage(Id(60)).Revision;
        ulong projectRevision = fixture.World.Inventory.Custody.GetStorage(QuestFixture.Project).Revision;
        DeliveryQuestPlan result = fixture.Plan(command);

        Assert.True(result.Succeeded);
        EntityId first = EntityId.DeriveSpawned(command.CommandId, 0);
        EntityId second = EntityId.DeriveSpawned(command.CommandId, 1);
        Assert.Equal(new[] { first, second }, result.CreatedIds);
        Assert.Equal(new SourceInterval(Id(71), 0, 3), result.CandidateInventory.Custody.GetLot(first).Source);
        Assert.Equal(new SourceInterval(Id(71), 4, 7), result.CandidateInventory.Custody.GetLot(second).Source);
        Assert.Equal(new SourceInterval(Id(71), 3, 4), result.CandidateInventory.Custody.GetLot(Id(70)).Source);
        Assert.Equal(new SourceInterval(Id(71), 7, 8), result.CandidateInventory.Custody.GetLot(Id(72)).Source);
        foreach (EntityId child in result.CreatedIds)
        {
            CountedSourceLot lot = result.CandidateInventory.Custody.GetLot(child);
            Assert.Equal(QuestFixture.Project, lot.CustodianId);
            Assert.Equal(QuestFixture.Reservation, lot.ReservedBy);
            Assert.Equal(0UL, lot.Revision);
        }
        Assert.Equal(8UL, result.CandidateInventory.Custody.Lots.Where(lot => lot.Source.SourceId == Id(71))
            .Aggregate(0UL, (sum, lot) => sum + lot.Quantity));
        Assert.Equal(sourceRevision + 1, result.CandidateInventory.Custody.GetStorage(Id(60)).Revision);
        Assert.Equal(projectRevision + 1, result.CandidateInventory.Custody.GetStorage(QuestFixture.Project).Revision);
        Assert.Equal(fixture.World.Inventory.Custody.Revision + 1, result.CandidateInventory.Custody.Revision);
    }

    [Fact]
    public void CollisionOnLaterPartialChildRejectsEveryMaterialAndRewardChange()
    {
        QuestFixture fixture = WithTwoPartialLots();
        fixture = fixture.Apply(fixture.Plan(fixture.Accept(3140)));
        WorldActionCommand command = fixture.Complete(3141, [new(Id(70), 0, 3), new(Id(72), 0, 3)]);
        EntityId occupied = EntityId.DeriveSpawned(command.CommandId, 1);

        DeliveryQuestPlan result = fixture.Plan(command, identity => identity == occupied);

        AssertRejectedUnchanged(fixture, result, WorldActionRejection.Unavailable);
        Assert.Equal(200UL, fixture.Progression.Currency.GetAccount(QuestFixture.Reserve).Balance);
        Assert.Equal(ActorQuestStatus.Accepted, fixture.Quests.Participations.Single().Status);
    }

    [Theory]
    [InlineData(LotProtection.Equipped)]
    [InlineData(LotProtection.Favorite)]
    [InlineData(LotProtection.Heirloom)]
    [InlineData(LotProtection.UserReserved)]
    public void QuestCannotBypassExplicitMaterialProtection(LotProtection protection)
    {
        QuestFixture fixture = QuestFixture.Create();
        fixture = WithSourceLots(fixture, 6,
            [new(Id(70), Id(60), Material, new SourceInterval(Id(71), 0, 6), 0, protection)]);
        fixture = fixture.Apply(fixture.Plan(fixture.Accept(3150)));

        DeliveryQuestPlan result = fixture.Plan(fixture.Complete(3151));

        AssertRejectedUnchanged(fixture, result, WorldActionRejection.Unavailable);
    }

    [Fact]
    public void ExistingReservationCannotBeReusedAsAQuestContribution()
    {
        QuestFixture fixture = QuestFixture.Create();
        fixture = WithSourceLots(fixture, 6,
            [new(Id(70), Id(60), Material, new SourceInterval(Id(71), 0, 6), 0,
                LotProtection.None, QuestFixture.Reservation)]);
        fixture = fixture.Apply(fixture.Plan(fixture.Accept(3160)));

        AssertRejectedUnchanged(fixture, fixture.Plan(fixture.Complete(3161)), WorldActionRejection.Unavailable);
    }

    [Fact]
    public void SpoofedPrincipalCannotReadOrMutateQuestState()
    {
        QuestFixture fixture = QuestFixture.Create();
        WorldActionCommand actual = fixture.Accept(3170);
        var spoofed = new WorldActionCommand(actual.CommandId, Id(3999), actual.OriginSessionId, actual.ActorId,
            actual.WorldId, actual.BranchId, actual.ExpectedActorRevision, actual.Intent);

        AssertRejectedUnchanged(fixture, fixture.Plan(spoofed), WorldActionRejection.NotAuthorized);
    }

    [Fact]
    public void ComposedProcessorAllowsItsOwnReservedCommandAndCommitsPartialDelivery()
    {
        QuestFixture fixture = WithTwoPartialLots();
        var processor = new WorldStepProcessor();
        WorldSnapshot initial = Compose(fixture);
        WorldStepDecision acceptance = processor.Decide(initial, QuestBatch(initial, 3200, fixture.Accept(3201)));
        fixture = new QuestFixture(acceptance.Candidate, acceptance.Candidate.Progression!, acceptance.Candidate.Quests!);
        WorldActionCommand command = fixture.Complete(3203, [new(Id(70), 0, 3), new(Id(72), 0, 3)]);

        WorldStepDecision completion = processor.Decide(fixture.World, QuestBatch(fixture.World, 3202, command));

        Assert.Equal(WorldActionRejection.None, completion.Receipts.Single().Rejection);
        Assert.Equal(ActorQuestStatus.Completed, completion.Candidate.Quests!.Participations.Single().Status);
        Assert.Equal(200UL, completion.Candidate.Progression!.Currency.GetActorAccount(Id(10)).Balance);
        Assert.Equal(fixture.World.Calendar, completion.Candidate.Calendar);
        Assert.Equal(fixture.World.AdvancingSteps, completion.Candidate.AdvancingSteps);
        Assert.Contains(completion.Identities, identity => identity.Identity == command.CommandId
            && identity.Kind == WorldCommittedIdentityKind.Command);
        Assert.Equal(2, completion.Identities.Count(identity => identity.Kind == WorldCommittedIdentityKind.SpawnedEntity));
    }

    [Fact]
    public void ComposedProcessorRejectsHistoricallyOccupiedChildWithoutDeliveringOrPaying()
    {
        QuestFixture fixture = WithTwoPartialLots();
        fixture = fixture.Apply(fixture.Plan(fixture.Accept(3210)));
        WorldSnapshot current = Compose(fixture);
        WorldActionCommand command = fixture.Complete(3212, [new(Id(70), 0, 3), new(Id(72), 0, 3)]);
        EntityId occupied = EntityId.DeriveSpawned(command.CommandId, 1);

        WorldStepDecision decision = new WorldStepProcessor().Decide(current, QuestBatch(current, 3211, command),
            identity => identity == occupied);

        Assert.Equal(WorldActionRejection.Unavailable, decision.Receipts.Single().Rejection);
        Assert.Same(current.Inventory, decision.Candidate.Inventory);
        Assert.Same(current.Progression, decision.Candidate.Progression);
        Assert.Same(current.Quests, decision.Candidate.Quests);
        Assert.DoesNotContain(decision.Identities, identity => identity.Kind == WorldCommittedIdentityKind.SpawnedEntity);
    }

    [Fact]
    public void ExistingCurrencyAccountCannotBeReusedAsAnAcceptanceCommand()
    {
        QuestFixture fixture = QuestFixture.Create();
        WorldSnapshot current = Compose(fixture);
        WorldActionCommand original = fixture.Accept();
        var aliased = new WorldActionCommand(QuestFixture.Reserve, original.PrincipalId, original.OriginSessionId,
            original.ActorId, original.WorldId, original.BranchId, original.ExpectedActorRevision, original.Intent);

        Assert.Throws<InvalidOperationException>(() => new WorldStepProcessor().Decide(current,
            QuestBatch(current, 3220, aliased)));
        Assert.Empty(current.Quests!.Participations);
        Assert.Equal(200UL, current.Progression!.Currency.GetAccount(QuestFixture.Reserve).Balance);
    }

    [Fact]
    public void RewardDefinitionCannotAliasAnExistingMaterialIdentity()
    {
        QuestFixture fixture = QuestFixture.Create();
        DeliveryQuestDefinition actual = fixture.Definition;
        var reward = new ActorRewardDefinition(Material.DefinitionId, actual.Reward.Key, actual.Reward.Experience,
            actual.Reward.Currency, actual.Reward.ReserveAccountId, actual.Reward.CurrencyAmount);
        var definition = new DeliveryQuestDefinition(actual.DefinitionId, actual.Key, actual.Title, actual.Description,
            actual.GiverActorId, actual.MinimumLevel, actual.Item, actual.Quantity,
            actual.DestinationCustodianId, actual.MaterialReservationId, reward);
        var quests = new QuestSnapshot(fixture.World.WorldId, fixture.World.BranchId, 0, [definition], []);

        Assert.Throws<ArgumentException>(() => Compose(fixture with { Quests = quests }));
    }

    private static WorldSnapshot Compose(QuestFixture fixture)
    {
        WorldSnapshot world = fixture.World;
        return new WorldSnapshot(world.Inventory, world.Space, world.Residents, world.AdmissionVersion,
            world.Calendar, world.Revision, world.AdvancingSteps, world.Dialogues, world.Gathering, world.Production,
            fixture.Progression, fixture.Quests);
    }

    private static WorldStepBatch QuestBatch(WorldSnapshot world, ulong cause, WorldActionCommand action) =>
        new(Id(cause), world.WorldId, world.BranchId, world.Revision, world.AdvancingSteps,
            world.HasReadingHold ? WorldBatchKind.HeldInteraction : WorldBatchKind.AdvancingStep, [], [action], 4);

    private static void AssertRejectedUnchanged(QuestFixture fixture, DeliveryQuestPlan result,
        WorldActionRejection rejection)
    {
        Assert.False(result.Succeeded);
        Assert.Equal(rejection, result.Rejection);
        Assert.Same(fixture.World.Inventory, result.CandidateInventory);
        Assert.Same(fixture.Progression, result.CandidateProgression);
        Assert.Same(fixture.Quests, result.CandidateQuests);
        Assert.Empty(result.CreatedIds);
    }

    private static QuestFixture WithTwoPartialLots() => WithSourceLots(QuestFixture.Create(), 8,
        [new(Id(70), Id(60), Material, new SourceInterval(Id(71), 0, 4), 0),
         new(Id(72), Id(60), Material, new SourceInterval(Id(71), 4, 8), 0)]);

    private static QuestFixture WithSourceLots(QuestFixture fixture, ulong capacity, IEnumerable<CountedSourceLot> lots)
    {
        CountedAuthorityState prior = fixture.World.Inventory;
        var inventory = new CountedAuthorityState(new CustodySnapshot(prior.WorldId, prior.BranchId,
            prior.Custody.Revision, prior.Custody.Storages,
            prior.Custody.Lots.Where(lot => lot.Source.SourceId != Id(71)).Concat(lots)),
            prior.AdmittedUnitCapacity - 6 + capacity, prior.Actors, prior.Controls, prior.Policies,
            prior.Sources.Select(source => source.SourceId == Id(71) ? new AdmittedCountedSource(Id(71), Material, capacity) : source),
            prior.ReservationIds);
        WorldSnapshot world = fixture.World;
        return fixture with { World = new WorldSnapshot(inventory, world.Space, world.Residents,
            world.AdmissionVersion, world.Calendar, world.Revision, world.AdvancingSteps, world.Dialogues,
            world.Gathering, world.Production) };
    }

    private static QuestFixture WithSecondParticipant(ulong funding)
    {
        QuestFixture fixture = QuestFixture.Create(funds: funding, projectCapacity: 12);
        WorldSnapshot basis = fixture.World;
        CountedAuthorityState prior = basis.Inventory;
        var inventory = new CountedAuthorityState(new CustodySnapshot(prior.WorldId, prior.BranchId,
            prior.Custody.Revision, [.. prior.Custody.Storages, new(Id(62), 64, 0)],
            [.. prior.Custody.Lots, new(Id(2501), Id(62), Material, new SourceInterval(Id(2502), 0, 6), 0)]),
            prior.AdmittedUnitCapacity + 6, [.. prior.Actors, Id(12)],
            [.. prior.Controls, new(Id(102), Id(112), Id(12))],
            [.. prior.Policies, new(Id(62), Id(12), CustodyDepositRule.OwnerOnly)],
            [.. prior.Sources, new(Id(2502), Material, 6)], prior.ReservationIds);
        BodyDefinition body = basis.Space.GetActor(Id(10)).Condition.Definition;
        ActorFormBinding binding = ActorFormBinding.Corporeal(Id(12), Id(22), Id(32), body);
        var feet = new SubunitPosition(0, 512, 256);
        var presence = new ActorPresence(binding, new BodyConditionSnapshot(binding.FormInstanceId, body, []),
            basis.Space.Scope, feet, 0, Id(52));
        WorldResident first = basis.GetResident(Id(10));
        var resident = new WorldResident(Id(12), Id(62), "Second contributor", ResidentControllerKind.Host, feet, 0,
            new ActorComprehensionProfile(Id(12), 0, first.Comprehension.RulesVersion, first.Comprehension.Entries),
            default, first.OpeningUtterance);
        GatheringSnapshot gathering = basis.Gathering!;
        var work = new GatheringSnapshot(basis.WorldId, basis.BranchId, gathering.AdvancingStep,
            gathering.Nodes, gathering.Tools, [.. gathering.Actors, new(Id(12), 1000)]);
        var world = new WorldSnapshot(inventory,
            new LocalSpatialSnapshot(basis.Space.Scope, basis.Space.Regions, [.. basis.Space.Actors, presence]),
            [.. basis.Residents, resident], basis.AdmissionVersion, basis.Calendar, basis.Revision, basis.AdvancingSteps,
            [.. basis.Dialogues, new(Id(2503), Id(12), Id(11), Id(92), Id(2504), basis.Calendar)], work, basis.Production);
        ActorProgressionState actor = fixture.Progression.GetActor(Id(10));
        CurrencyLedger currency = fixture.Progression.Currency;
        var funded = new CurrencyLedger(world.WorldId, world.BranchId, currency.Revision, currency.Currency,
            currency.AdmittedSupply, [.. currency.Accounts, new(Id(2207), CurrencyAccountKind.Actor, Id(12), 0, 0)]);
        var progression = new ProgressionSnapshot(world.WorldId, world.BranchId, fixture.Progression.Revision,
            fixture.Progression.Profiles, fixture.Progression.Curves,
            [.. fixture.Progression.Actors, new(Id(12), 0, actor.Profile, actor.Curve, 1, 0, 7, 2)], funded);
        fixture.Quests.ValidateForAuthority(inventory, progression);
        return new QuestFixture(world, progression, fixture.Quests);
    }
}
