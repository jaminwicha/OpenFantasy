using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

public sealed partial class SqliteWorldAuthority
{
    /// <summary>Explicitly upgrades one exact save into a new destination; ordinary opening never upgrades.</summary>
    public static SqliteWorldAuthority UpgradePartyWithdrawal(string sourcePath, string destinationPath,
        WorldPartyWithdrawalTransition request) => UpgradePartyWithdrawal(sourcePath, destinationPath, request, null);

    internal static SqliteWorldAuthority UpgradePartyWithdrawal(string sourcePath, string destinationPath,
        WorldPartyWithdrawalTransition request, Action<WorldPartyWithdrawalTransitionBoundary>? fault)
    {
        new WorldPartyWithdrawalUpgrade().Publish(sourcePath, destinationPath, request, fault);
        return OpenExisting(destinationPath);
    }
}
