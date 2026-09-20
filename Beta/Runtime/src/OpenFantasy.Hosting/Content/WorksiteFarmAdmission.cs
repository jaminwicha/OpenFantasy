using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Creates a separate, unplayed worksite profile from the retained farm content owners.</summary>
public static class WorksiteFarmAdmission
{
    public static WorldSnapshot CreateEmbedded(string seedText, string playerName)
    {
        WorldSnapshot gathering = GatheringFarmAdmission.CreateEmbedded(seedText, playerName);
        ProductionSnapshot production = ProductionFarmAdmission.CreateProduction(gathering);
        var productionWorld = new WorldSnapshot(gathering.Inventory, gathering.Space, gathering.Residents,
            WorldProductionMigration.ComputeTargetAdmissionVersion(gathering.AdmissionVersion, production),
            gathering.Calendar, 0, 0, gathering: gathering.Gathering, production: production);
        QuestFarmComponents quests = QuestFarmAdmission.CreateQuests(productionWorld);
        var questWorld = new WorldSnapshot(quests.Inventory, productionWorld.Space, productionWorld.Residents,
            WorldQuestMigration.ComputeTargetAdmissionVersion(productionWorld.AdmissionVersion, quests.Admission),
            productionWorld.Calendar, 0, 0, gathering: productionWorld.Gathering, production: production,
            progression: quests.Progression, quests: quests.Quests);
        return new WorksiteFarmBuilder(ConstructionFarmAdmission.CreateGenesis(questWorld)).Build();
    }
}
