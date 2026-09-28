using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Care;

/// <summary>Exact ordinary dose selection; no aftermath acknowledgment or patient control over the supplier.</summary>
public sealed record UseRestorativeItemIntent : WorldActionIntent
{
    public UseRestorativeItemIntent(EntityId recipientActorId, EntityId useDefinitionId, ContentHash useDefinitionVersion,
        EntityId? permissionId, ContentHash? permissionGrantVersion, ulong? expectedPermissionRevision,
        ulong expectedPatientPresenceRevision, ulong expectedPatientProgressionRevision,
        ulong expectedStorageRevision, ulong expectedProductionRevision, IngredientRequest ingredient)
    {
        ArgumentNullException.ThrowIfNull(useDefinitionVersion);
        ArgumentNullException.ThrowIfNull(ingredient);
        if ((permissionId is null) != (permissionGrantVersion is null)
            || (permissionId is null) != (expectedPermissionRevision is null))
            throw new ArgumentException("A selected permission requires its complete identity, grant and revision tuple.");
        RecipientActorId = recipientActorId;
        UseDefinitionId = useDefinitionId;
        UseDefinitionVersion = useDefinitionVersion;
        PermissionId = permissionId;
        PermissionGrantVersion = permissionGrantVersion;
        ExpectedPermissionRevision = expectedPermissionRevision;
        ExpectedPatientPresenceRevision = expectedPatientPresenceRevision;
        ExpectedPatientProgressionRevision = expectedPatientProgressionRevision;
        ExpectedStorageRevision = expectedStorageRevision;
        ExpectedProductionRevision = expectedProductionRevision;
        Ingredient = ingredient;
    }

    public EntityId RecipientActorId { get; }
    public EntityId UseDefinitionId { get; }
    public ContentHash UseDefinitionVersion { get; }
    public EntityId? PermissionId { get; }
    public ContentHash? PermissionGrantVersion { get; }
    public ulong? ExpectedPermissionRevision { get; }
    public ulong ExpectedPatientPresenceRevision { get; }
    public ulong ExpectedPatientProgressionRevision { get; }
    public ulong ExpectedStorageRevision { get; }
    public ulong ExpectedProductionRevision { get; }
    public IngredientRequest Ingredient { get; }
}
