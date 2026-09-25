using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>The unchanged fresh farm composition, before its water owner binds the complete actor inventory.</summary>
internal static class EncounterFarmBasis
{
    internal static WorldSnapshot Create(FarmContentFiles files, string? contentRoot, string seedText, string playerName)
    {
        WorldSnapshot gathering = contentRoot is null
            ? GatheringFarmAdmission.CreateEmbedded(seedText, playerName)
            : GatheringFarmAdmission.Create(contentRoot, seedText, playerName);
        ProductionSnapshot production = contentRoot is null
            ? ProductionFarmAdmission.CreateProduction(gathering)
            : ProductionFarmAdmission.CreateProduction(contentRoot, gathering);
        var productionWorld = new WorldSnapshot(gathering.Inventory, gathering.Space, gathering.Residents,
            WorldProductionMigration.ComputeTargetAdmissionVersion(gathering.AdmissionVersion, production),
            gathering.Calendar, 0, 0, gathering: gathering.Gathering, production: production);
        QuestFarmComponents quests = contentRoot is null
            ? QuestFarmAdmission.CreateQuests(productionWorld)
            : QuestFarmAdmission.CreateQuests(contentRoot, productionWorld);
        var questWorld = new WorldSnapshot(quests.Inventory, productionWorld.Space, productionWorld.Residents,
            WorldQuestMigration.ComputeTargetAdmissionVersion(productionWorld.AdmissionVersion, quests.Admission),
            productionWorld.Calendar, 0, 0, gathering: productionWorld.Gathering, production: production,
            progression: quests.Progression, quests: quests.Quests);
        // The filesystem factory must validate that same directory at every layer, including
        // fresh construction. It must not silently substitute embedded construction witnesses.
        ConstructionFarmDefinition metadata = ConstructionFarmDefinition.Load(files);
        QuestFarmSources sources = metadata.ValidateSources(files);
        ConstructionFarmProfileValidation.Validate(questWorld, sources);
        ConstructionFarmComponents components = new ConstructionFarmBuilder(questWorld, metadata).Build(fresh: true);
        return new WorldSnapshot(components.Inventory, questWorld.Space, questWorld.Residents,
            WorldConstructionMigration.ComputeTargetAdmissionVersion(questWorld.AdmissionVersion, components.Admission),
            questWorld.Calendar, 0, 0, questWorld.Dialogues, questWorld.Gathering,
            components.Production, questWorld.Progression, questWorld.Quests, components.Construction);
    }
}
