using System.Security.Cryptography;
using OpenFantasy.Core;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Client;

/// <summary>Chooses a verified save or a preserved-copy upgrade before the live session takes ownership.</summary>
internal static class FarmSaveOpener
{
    internal static SqliteWorldAuthority Open(string savePath, WorldSnapshot gatheringGenesis,
        bool productionProfile, string? upgradeSourcePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(savePath))!);
        WorldSnapshot genesis = productionProfile ? ProductionGenesis(gatheringGenesis) : gatheringGenesis;
        SqliteWorldAuthority authority;
        if (File.Exists(savePath))
        {
            authority = SqliteWorldAuthority.OpenExisting(savePath);
        }
        else if (productionProfile && upgradeSourcePath is not null && File.Exists(upgradeSourcePath))
        {
            WorldProductionMigration request;
            using (SqliteWorldAuthority source = SqliteWorldAuthority.OpenExisting(upgradeSourcePath))
            {
                WorldSnapshot current = source.InspectForHost().State;
                ValidateSelection(current, gatheringGenesis);
                ProductionSnapshot production = ProductionFarmAdmission.CreateProduction(current);
                request = new WorldProductionMigration(EntityId.FromBytes(RandomNumberGenerator.GetBytes(EntityId.ByteLength)),
                    current.WorldId, current.BranchId, current.Revision, current.AdvancingSteps, current.ContentHash,
                    current.AdmissionVersion, WorldProductionMigration.ComputeTargetAdmissionVersion(current.AdmissionVersion, production), production);
            }
            authority = SqliteWorldAuthority.UpgradeGatheringToProduction(upgradeSourcePath, savePath, request);
        }
        else
        {
            authority = SqliteWorldAuthority.CreateNew(savePath, genesis);
        }
        try
        {
            ValidateSelection(authority.InspectForHost().State, genesis);
            return authority;
        }
        catch
        {
            authority.Dispose();
            throw;
        }
    }

    private static WorldSnapshot ProductionGenesis(WorldSnapshot gathering)
    {
        ProductionSnapshot production = ProductionFarmAdmission.CreateProduction(gathering);
        return new WorldSnapshot(gathering.Inventory, gathering.Space, gathering.Residents,
            WorldProductionMigration.ComputeTargetAdmissionVersion(gathering.AdmissionVersion, production),
            gathering.Calendar, 0, 0, gathering.Dialogues, gathering.Gathering, production);
    }

    private static void ValidateSelection(WorldSnapshot current, WorldSnapshot expected)
    {
        if (current.WorldId != expected.WorldId || current.BranchId != expected.BranchId
            || current.ProfileVersion != expected.ProfileVersion || current.AdmissionVersion != expected.AdmissionVersion)
        {
            throw new InvalidDataException("This save needs its original content version or an explicit supported migration.");
        }
    }
}
