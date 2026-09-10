using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

/// <summary>Actual embedded farm content and ordinary committed actor actions for upgrade regressions.</summary>
internal sealed class ConstructionUpgradeFixture
{
    private ulong _identity = 7_400_000;

    internal WorldSnapshot QuestGenesis()
    {
        WorldSnapshot gathering = GatheringFarmAdmission.CreateEmbedded("construction-recovery", "Traveler");
        ProductionSnapshot production = ProductionFarmAdmission.CreateProduction(gathering);
        var basis = new WorldSnapshot(gathering.Inventory, gathering.Space, gathering.Residents,
            WorldProductionMigration.ComputeTargetAdmissionVersion(gathering.AdmissionVersion, production),
            gathering.Calendar, 0, 0, gathering: gathering.Gathering, production: production);
        QuestFarmComponents quests = QuestFarmAdmission.CreateQuests(basis);
        return new WorldSnapshot(quests.Inventory, basis.Space, basis.Residents,
            WorldQuestMigration.ComputeTargetAdmissionVersion(basis.AdmissionVersion, quests.Admission),
            basis.Calendar, 0, 0, gathering: basis.Gathering, production: basis.Production,
            progression: quests.Progression, quests: quests.Quests);
    }

    internal WorldConstructionMigration Request(WorldSnapshot world)
    {
        WorldConstructionAdmission admission = ConstructionFarmAdmission.CreateConstruction(world).Admission;
        return new WorldConstructionMigration(NextIdentity(), world.WorldId, world.BranchId, world.Revision,
            world.AdvancingSteps, world.ContentHash, world.AdmissionVersion,
            WorldConstructionMigration.ComputeTargetAdmissionVersion(world.AdmissionVersion, admission), admission);
    }

    internal WorldSnapshot CreateSource(string path, bool completeQuest = false)
    {
        using SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(path, QuestGenesis());
        WorldSnapshot world = authority.InspectForHost().State;
        if (!completeQuest)
        {
            return world;
        }
        EntityId player = world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        DeliveryQuestDefinition quest = world.Quests!.Definitions.Single();
        for (int step = 0; world.Space.QueryInteraction(player, quest.GiverActorId) != SpatialInteraction.Reachable; step++)
        {
            Assert.True(step < 128, "The retained farm approach must remain reachable within the fixture bound.");
            ActorPresence actor = world.Space.GetActor(player);
            SubunitPosition target = world.Space.GetActor(quest.GiverActorId).Feet;
            long dx = Math.Sign(target.X - actor.Feet.X) * WorldMovementInput.DisplacementPerStep;
            long dy = dx == 0 ? Math.Sign(target.Y - actor.Feet.Y) * WorldMovementInput.DisplacementPerStep : 0;
            WorldStepBatch batch = Batch(world, [], [new WorldMovementInput(Delivery(world, player), player, dx, dy)]);
            world = authority.Commit(batch).Candidate;
        }
        world = Act(authority, world, player, new SpeakToActorIntent(quest.GiverActorId,
            world.Space.GetActor(quest.GiverActorId).Revision));
        world = Act(authority, world, player, new AcceptDeliveryQuestIntent(quest.DefinitionId, quest.Version,
            quest.GiverActorId, world.Space.GetActor(quest.GiverActorId).Revision, 0,
            world.Progression!.GetActor(player).Revision));
        EntityId bag = world.GetResident(player).InventoryCustodianId;
        var wood = world.Inventory.Custody.Lots.Single(value => value.CustodianId == bag && value.Definition == quest.Item);
        world = Act(authority, world, player, new CompleteDeliveryQuestIntent(quest.DefinitionId, quest.Version,
            quest.GiverActorId, world.Space.GetActor(quest.GiverActorId).Revision, 1,
            world.Progression!.GetActor(player).Revision, world.Inventory.Custody.GetStorage(bag).Revision,
            [new(wood.LotId, wood.Revision, 6)]));
        Assert.Equal(ActorQuestStatus.Completed, world.Quests!.Participations.Single().Status);
        Assert.Equal(200UL, world.Progression!.Currency.GetActorAccount(player).Balance);
        return world;
    }

    internal WorldSnapshot Act(SqliteWorldAuthority authority, WorldSnapshot world, EntityId actor, WorldActionIntent intent)
    {
        TrustedCommandDelivery delivery = Delivery(world, actor);
        var command = new WorldActionCommand(NextIdentity(), delivery.PrincipalId, delivery.SessionId,
            actor, world.WorldId, world.BranchId, world.Space.GetActor(actor).Revision, intent);
        WorldStepDecision decision = authority.Commit(Batch(world, [command], []));
        Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
        return decision.Candidate;
    }

    internal WorldStepBatch Batch(WorldSnapshot world, IEnumerable<WorldActionCommand> actions,
        IEnumerable<WorldMovementInput> movement) => new(NextIdentity(), world.WorldId, world.BranchId,
            world.Revision, world.AdvancingSteps, world.HasReadingHold ? WorldBatchKind.HeldInteraction
                : WorldBatchKind.AdvancingStep, movement, actions, world.ProfileVersion);

    internal static TrustedCommandDelivery Delivery(WorldSnapshot world, EntityId actor)
    {
        ActorControlBinding binding = world.Inventory.Controls.Single(value => value.ActorId == actor);
        return new TrustedCommandDelivery(binding.PrincipalId, binding.SessionId);
    }

    internal EntityId NextIdentity() => new(0, _identity++);
}
