using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Care;

/// <summary>Patient selection of one exact authored permission; the world authenticates its control.</summary>
public sealed record RevokeCarePermissionIntent : WorldActionIntent
{
    public RevokeCarePermissionIntent(EntityId permissionId, ulong expectedPermissionRevision,
        ContentHash expectedPermissionGrantVersion)
    {
        ArgumentNullException.ThrowIfNull(expectedPermissionGrantVersion);
        PermissionId = permissionId;
        ExpectedPermissionRevision = expectedPermissionRevision;
        ExpectedPermissionGrantVersion = expectedPermissionGrantVersion;
    }

    public EntityId PermissionId { get; }
    public ulong ExpectedPermissionRevision { get; }
    public ContentHash ExpectedPermissionGrantVersion { get; }
}
