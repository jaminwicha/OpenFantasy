using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.NativeCargo;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Client;

internal sealed partial class FarmClientSession
{
    /// <summary>
    /// Opens the exact solo adventure selected by its seed and name. Existing worlds retain their
    /// original genesis, including an explicitly recorded collective-withdrawal transition. No new roster
    /// or fresh interpretation is substituted for this selection.
    /// </summary>
    internal static Task<FarmClientSession> OpenAdventureAsync(string savePath, string seedText,
        string playerName) => Task.Run(() =>
    {
        WorldSnapshot genesis = AdventureWorldAdmission.CreateEmbedded(seedText, [playerName]);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(savePath))!);
        SqliteWorldAuthority authority = File.Exists(savePath)
            ? SqliteWorldAuthority.OpenExisting(savePath, WorldSnapshotCodec.Encode(genesis))
            : SqliteWorldAuthority.CreateNew(savePath, genesis);
        try
        {
            WorldSnapshot current = authority.InspectForHost().State;
            // OpenExisting has already replayed the exact original genesis and admitted transition.
            // This recognizes that lineage; it never upgrades or weakens other save selectors.
            bool transitioned = current.ProfileVersion == WorldPartyWithdrawalAdmission.ProfileVersion
                && genesis.ProfileVersion == WorldDelegatedControlAdmission.ProfileVersion
                && current.WorldId == genesis.WorldId && current.BranchId == genesis.BranchId
                && current.AdmissionVersion == genesis.AdmissionVersion;
            if (!transitioned) FarmSaveOpener.ValidateSelection(current, genesis);
            if (current.ProfileVersion is not (WorldDelegatedControlAdmission.ProfileVersion or WorldPartyWithdrawalAdmission.ProfileVersion)
                || !NativeCargoWorld.IsCargoWorld(current))
            {
                throw new InvalidDataException("This save is not the selected adventure.");
            }
            return AttachCargoSession(authority, enableRetreat: true);
        }
        catch
        {
            authority.Dispose();
            throw;
        }
    });
}
