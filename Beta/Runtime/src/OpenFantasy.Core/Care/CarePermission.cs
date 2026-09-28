using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Care;

/// <summary>
/// Patient-owned scope for one actual helper and ordinary use definition. The original grant remains
/// immutable after revocation; constructing the record does not authenticate the patient's permission.
/// </summary>
public sealed class CarePermission
{
    public CarePermission(EntityId permissionId, EntityId patientActorId, EntityId helperActorId,
        EntityId useDefinitionId, ContentHash useDefinitionVersion, CarePermissionRevocation? revocation = null)
    {
        ArgumentNullException.ThrowIfNull(useDefinitionVersion);
        if (new HashSet<EntityId> { permissionId, patientActorId, helperActorId, useDefinitionId }.Count != 4
            || revocation is not null && (revocation.PermissionId != permissionId || revocation.PatientActorId != patientActorId))
            throw new ArgumentException("Care permission requires distinct scoped participants and its patient's exact revocation.");
        PermissionId = permissionId;
        PatientActorId = patientActorId;
        HelperActorId = helperActorId;
        UseDefinitionId = useDefinitionId;
        UseDefinitionVersion = useDefinitionVersion;
        Revocation = revocation;
    }

    public EntityId PermissionId { get; }
    public EntityId PatientActorId { get; }
    public EntityId HelperActorId { get; }
    public EntityId UseDefinitionId { get; }
    public ContentHash UseDefinitionVersion { get; }
    public CarePermissionRevocation? Revocation { get; }
    public ulong Revision => Revocation is null ? 0UL : 1UL;
    public bool IsActive => Revocation is null;
    public ContentHash GrantVersion => ContentHash.Compute(CareCodecV2.EncodePermissionGrant(this));
    public ContentHash Version => ContentHash.Compute(CareCodecV2.EncodePermission(this));

    public CarePermission Revoke(CarePermissionRevocation revocation)
    {
        ArgumentNullException.ThrowIfNull(revocation);
        if (Revocation is not null) throw new InvalidOperationException("An authored permission can be revoked only once.");
        return new(PermissionId, PatientActorId, HelperActorId, UseDefinitionId, UseDefinitionVersion, revocation);
    }
}
