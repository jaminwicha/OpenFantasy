using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Reproduces the original fresh admission pin without replacing any current progress or material rights.</summary>
internal static class ConstructionFarmProfileValidation
{
    internal static void Validate(WorldSnapshot world, QuestFarmSources source)
    {
        if (world.ProfileVersion != 4 || world.Production is null || world.Gathering is null
            || world.Quests is null || world.Progression is null || world.Residents.Count != 2
            || world.Residents.Count(value => value.ControllerKind == ResidentControllerKind.Player) != 1
            || world.Residents.Count(value => value.ControllerKind == ResidentControllerKind.Host) != 1
            || world.Production.Recipes.Count != 1 || world.Production.Recipes[0].Version.Value
                != "747fe3e60f887cce01aa5d6838c60b732dd39321235bdb9bac6a508b74c29142")
        {
            throw new ArgumentException("Construction admission requires the exact two-resident quest farm with its retained ration recipe.", nameof(world));
        }
        WorldResident player = world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player);
        WorldResident wren = world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Host);
        var home = new SubunitPosition(source.Wren.GetProperty("tileX").GetInt64() * 256 + 128,
            source.Wren.GetProperty("tileY").GetInt64() * 256 + 128, 256);
        if (wren.DisplayName != source.Wren.GetProperty("name").GetString() || wren.HomeFeet != home
            || wren.WanderRadiusCells != source.Wren.GetProperty("wander").GetInt32()
            || wren.OpeningUtterance?.Text != source.Wren.GetProperty("dialog").GetString())
        {
            throw new ArgumentException("The retained host resident does not match the admitted Wren source persona.", nameof(world));
        }
        WorldQuestAdmission expected = ReproduceFreshAdmission(world, source, player, wren);
        var originalProduction = new ProductionSnapshot(world.WorldId, world.BranchId, 0, world.Production.Recipes, [], []);
        ContentHash productionPin = WorldProductionMigration.ComputeTargetAdmissionVersion(
            new ContentHash(GatheringFarmDefinition.AcceptedHash), originalProduction);
        if (world.AdmissionVersion != WorldQuestMigration.ComputeTargetAdmissionVersion(productionPin, expected)
            || world.Quests.Definitions.Count != 1 || world.Quests.Definitions[0].Version != expected.Quests.Definitions[0].Version
            || !world.Progression.Profiles.Select(value => value.Version).SequenceEqual(expected.Progression.Profiles.Select(value => value.Version))
            || !world.Progression.Curves.Select(value => value.Version).SequenceEqual(expected.Progression.Curves.Select(value => value.Version))
            || world.Progression.Currency.Currency.Version != expected.Progression.Currency.Currency.Version
            || world.Progression.Currency.AdmittedSupply != expected.Progression.Currency.AdmittedSupply
            || !world.Progression.Currency.Accounts.Select(value => (value.AccountId, value.Kind, value.ActorId))
                .SequenceEqual(expected.Progression.Currency.Accounts.Select(value => (value.AccountId, value.Kind, value.ActorId))))
        {
            throw new ArgumentException("The quest, progression or admission pin differs from the retained source farm.", nameof(world));
        }
        foreach (ActorProgressionState initial in expected.Progression.Actors)
        {
            ActorProgressionState actual = world.Progression.GetActor(initial.ActorId);
            if (actual.Profile.Version != initial.Profile.Version || actual.Curve.Version != initial.Curve.Version)
            {
                throw new ArgumentException("An actor's progression definition differs from its original explicit admission.", nameof(world));
            }
        }
        QuestProjectCustody project = expected.Projects[0];
        CustodianStorage storage = world.Inventory.Custody.GetStorage(project.CustodianId);
        CustodianAccessPolicy policy = world.Inventory.Policies.Single(value => value.CustodianId == project.CustodianId);
        if (storage.UnitCapacity != project.UnitCapacity || policy.OwnerActorId != wren.ActorId
            || policy.DepositRule != project.DepositRule || !world.Inventory.ReservationIds.Contains(project.ReservationId))
        {
            throw new ArgumentException("The existing project is not the retained Wren-owned reserved custody.", nameof(world));
        }
    }

    private static WorldQuestAdmission ReproduceFreshAdmission(WorldSnapshot world, QuestFarmSources source,
        WorldResident player, WorldResident wren)
    {
        EntityId Definition(string key) => ConstructionFarmIdentityScope.DefinitionId("quest-farm", key);
        EntityId Instance(string role) => ConstructionFarmIdentityScope.InstanceId(world, "quest-farm", role);
        JsonElement stats = source.Squire.GetProperty("baseStats");
        JsonElement growth = source.Squire.GetProperty("growth");
        var squire = new ActorStatProfileDefinition(Definition("profiles/human-squire"), "human_squire", "Squire",
            ActorStatProfileKind.Class, new ActorStatValues(stats.GetProperty("hp").GetInt32(), stats.GetProperty("mp").GetInt32(),
                stats.GetProperty("attack").GetInt32(), stats.GetProperty("magic").GetInt32(), stats.GetProperty("defense").GetInt32(), stats.GetProperty("speed").GetInt32()),
            new ActorStatModifiers(growth.GetProperty("hp").GetInt32(), growth.GetProperty("mp").GetInt32(),
                growth.GetProperty("attack").GetInt32(), growth.GetProperty("magic").GetInt32(), growth.GetProperty("defense").GetInt32(), growth.GetProperty("speed").GetInt32()),
            ActorStatModifiers.Zero);
        var unassigned = new ActorStatProfileDefinition(Definition("profiles/wren-unassigned"), "wren_unassigned", "Unassigned",
            ActorStatProfileKind.Unassigned, new ActorStatValues(1, 0, 0, 0, 0, 1), ActorStatModifiers.Zero, ActorStatModifiers.Zero);
        var curve = new LevelProgressionDefinition(Definition("curves/source-levels"), "source_levels_1_10",
            [100, 282, 519, 800, 1118, 1469, 1852, 2262, 2700]);
        var currency = new CurrencyDefinition(Definition("currencies/gil"), "gil", "Gil", "gil");
        EntityId reserve = Instance("quest.reward-reserve");
        var ledger = new CurrencyLedger(world.WorldId, world.BranchId, 0, currency, 200,
            [new CurrencyAccount(Instance("quest.player-wallet"), CurrencyAccountKind.Actor, player.ActorId, 0, 0),
             new CurrencyAccount(Instance("quest.wren-wallet"), CurrencyAccountKind.Actor, wren.ActorId, 0, 0),
             new CurrencyAccount(reserve, CurrencyAccountKind.Reserve, null, 200, 0)]);
        var progression = new ProgressionSnapshot(world.WorldId, world.BranchId, 0, [squire, unassigned], [curve],
            [new ActorProgressionState(player.ActorId, 0, squire, curve, 1, 0, squire.Calculate(1).MaxHp, squire.Calculate(1).MaxMp),
             new ActorProgressionState(wren.ActorId, 0, unassigned, curve, 1, 0, 1, 0)], ledger);
        var reward = new ActorRewardDefinition(Instance("quest.wren-reward"), "wren_orchard_reward",
            source.Quest.GetProperty("rewardXp").GetUInt64(), currency, reserve, source.Quest.GetProperty("rewardGil").GetUInt64());
        var project = new QuestProjectCustody(Instance("quest.project-custodian"), wren.ActorId, 6,
            Instance("quest.project-reservation"), CustodyDepositRule.AnyControlledActor);
        var wood = new CountedItemDefinition(EntityId.Parse("f20a24bbcf04b36ebcb90a1d04ca58c0"),
            new ContentHash("c603d104fd21c40819d8dc0f6a4f5f3bc976936f7ac2c2cfe6ffefd62820614d"));
        var quest = new DeliveryQuestDefinition(Instance("quest.wren-saplings"), source.Quest.GetProperty("id").GetString()!,
            source.Quest.GetProperty("name").GetString()!, source.Quest.GetProperty("offer").GetString()!, wren.ActorId,
            source.Quest.GetProperty("minLevel").GetUInt32(), wood, source.Quest.GetProperty("targetCount").GetUInt64(),
            project.CustodianId, project.ReservationId, reward);
        return new WorldQuestAdmission(QuestFarmAdmission.AdmissionVersion, progression,
            new QuestSnapshot(world.WorldId, world.BranchId, 0, [quest], []), [project]);
    }
}
