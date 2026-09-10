using System.Security.Cryptography;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Client;

/// <summary>Selects an existing quest save or explicitly upgrades retained production state before live play.</summary>
internal static class FarmQuestSaveOpener
{
    internal static SqliteWorldAuthority Open(string savePath, WorldSnapshot gatheringGenesis,
        string? productionSourcePath, string? gatheringSourcePath)
    {
        RequireDistinct(savePath, productionSourcePath);
        RequireDistinct(savePath, gatheringSourcePath);
        if (!File.Exists(savePath) && productionSourcePath is null
            && gatheringSourcePath is not null && File.Exists(gatheringSourcePath))
        {
            throw new ArgumentException("Upgrading a retained gathering save requires an intermediate production destination.");
        }
        WorldSnapshot productionGenesis = FarmSaveOpener.ProductionGenesis(gatheringGenesis);
        WorldSnapshot genesis = CreateGenesis(productionGenesis);
        SqliteWorldAuthority authority;
        if (File.Exists(savePath))
        {
            authority = SqliteWorldAuthority.OpenExisting(savePath);
        }
        else
        {
            RequireCompleteSource(productionSourcePath);
            RequireCompleteSource(gatheringSourcePath);
            if (productionSourcePath is not null && !File.Exists(productionSourcePath)
                && gatheringSourcePath is not null && File.Exists(gatheringSourcePath))
            {
                // Keep the intermediate production copy as well as the original gathering save.
                using SqliteWorldAuthority intermediate = FarmSaveOpener.Open(productionSourcePath,
                    gatheringGenesis, true, gatheringSourcePath);
                FarmSaveOpener.ValidateSelection(intermediate.InspectForHost().State, productionGenesis);
            }
            if (productionSourcePath is not null && File.Exists(productionSourcePath))
            {
                WorldQuestMigration request;
                using (SqliteWorldAuthority source = SqliteWorldAuthority.OpenExisting(productionSourcePath))
                {
                    WorldSnapshot state = source.InspectForHost().State;
                    FarmSaveOpener.ValidateSelection(state, productionGenesis);
                    WorldQuestAdmission admission = QuestFarmAdmission.CreateQuests(state).Admission;
                    request = new WorldQuestMigration(EntityId.FromBytes(RandomNumberGenerator.GetBytes(EntityId.ByteLength)),
                        state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps, state.ContentHash,
                        state.AdmissionVersion, WorldQuestMigration.ComputeTargetAdmissionVersion(state.AdmissionVersion, admission), admission);
                }
                authority = SqliteWorldAuthority.UpgradeProductionToQuests(productionSourcePath, savePath, request);
            }
            else
            {
                authority = SqliteWorldAuthority.CreateNew(savePath, genesis);
            }
        }
        try
        {
            FarmSaveOpener.ValidateSelection(authority.InspectForHost().State, genesis);
            return authority;
        }
        catch
        {
            authority.Dispose();
            throw;
        }
    }

    internal static WorldSnapshot CreateGenesis(WorldSnapshot production)
    {
        QuestFarmComponents content = QuestFarmAdmission.CreateQuests(production);
        return new WorldSnapshot(content.Inventory, production.Space, production.Residents,
            WorldQuestMigration.ComputeTargetAdmissionVersion(production.AdmissionVersion, content.Admission),
            production.Calendar, 0, 0, production.Dialogues, production.Gathering, production.Production,
            content.Progression, content.Quests);
    }

    internal static void RequireDistinct(string destination, string? source)
    {
        if (source is not null && Path.GetFullPath(destination) == Path.GetFullPath(source))
        {
            throw new ArgumentException("The retained source and quest destination must be different save files.");
        }
    }

    internal static void RequireCompleteSource(string? path)
    {
        if (path is null || File.Exists(path))
        {
            return;
        }
        foreach (string candidate in new[] { path, path + "-wal", path + "-shm", path + "-journal" })
        {
            try
            {
                _ = File.GetAttributes(candidate);
                throw new InvalidDataException("A retained source has an incomplete SQLite file set. It requires recovery before upgrade.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
}
