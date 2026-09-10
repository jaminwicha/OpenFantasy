using System.Security.Cryptography;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Client;

/// <summary>Preserves the complete predecessor chain before admitting construction to a selected local world.</summary>
internal static class FarmConstructionSaveOpener
{
    internal static SqliteWorldAuthority Open(string savePath, WorldSnapshot gatheringGenesis,
        string? questSourcePath, string? productionSourcePath, string? gatheringSourcePath)
    {
        FarmQuestSaveOpener.RequireDistinct(savePath, questSourcePath);
        FarmQuestSaveOpener.RequireDistinct(savePath, productionSourcePath);
        FarmQuestSaveOpener.RequireDistinct(savePath, gatheringSourcePath);
        WorldSnapshot questGenesis = FarmQuestSaveOpener.CreateGenesis(FarmSaveOpener.ProductionGenesis(gatheringGenesis));
        WorldSnapshot genesis = ConstructionFarmAdmission.CreateGenesis(questGenesis);
        SqliteWorldAuthority authority = File.Exists(savePath)
            ? SqliteWorldAuthority.OpenExisting(savePath)
            : CreateOrUpgrade(savePath, gatheringGenesis, questGenesis, genesis,
                questSourcePath, productionSourcePath, gatheringSourcePath);
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

    private static SqliteWorldAuthority CreateOrUpgrade(string savePath, WorldSnapshot gatheringGenesis,
        WorldSnapshot questGenesis, WorldSnapshot genesis, string? questSourcePath,
        string? productionSourcePath, string? gatheringSourcePath)
    {
        FarmQuestSaveOpener.RequireCompleteSource(questSourcePath);
        if (questSourcePath is null || !File.Exists(questSourcePath))
        {
            FarmQuestSaveOpener.RequireCompleteSource(productionSourcePath);
            FarmQuestSaveOpener.RequireCompleteSource(gatheringSourcePath);
            bool earlierSaveExists = productionSourcePath is not null && File.Exists(productionSourcePath)
                || gatheringSourcePath is not null && File.Exists(gatheringSourcePath);
            if (earlierSaveExists)
            {
                if (questSourcePath is null)
                {
                    throw new ArgumentException("Continuing a predecessor requires an intermediate quest destination.");
                }
                using SqliteWorldAuthority intermediate = FarmQuestSaveOpener.Open(questSourcePath, gatheringGenesis,
                    productionSourcePath, gatheringSourcePath);
                FarmSaveOpener.ValidateSelection(intermediate.InspectForHost().State, questGenesis);
            }
        }
        if (questSourcePath is null || !File.Exists(questSourcePath))
        {
            return SqliteWorldAuthority.CreateNew(savePath, genesis);
        }
        WorldConstructionMigration request;
        using (SqliteWorldAuthority source = SqliteWorldAuthority.OpenExisting(questSourcePath))
        {
            WorldSnapshot retained = source.InspectForHost().State;
            FarmSaveOpener.ValidateSelection(retained, questGenesis);
            WorldConstructionAdmission admission = ConstructionFarmAdmission.CreateConstruction(retained).Admission;
            request = new WorldConstructionMigration(EntityId.FromBytes(RandomNumberGenerator.GetBytes(EntityId.ByteLength)),
                retained.WorldId, retained.BranchId, retained.Revision, retained.AdvancingSteps, retained.ContentHash,
                retained.AdmissionVersion, WorldConstructionMigration.ComputeTargetAdmissionVersion(retained.AdmissionVersion, admission), admission);
        }
        return SqliteWorldAuthority.UpgradeQuestsToConstruction(questSourcePath, savePath, request);
    }
}
