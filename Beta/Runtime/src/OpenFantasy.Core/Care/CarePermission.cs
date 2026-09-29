using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Care;

/// <summary>
/// Patient-owned scope for one actual helper and ordinary use definition. The original grant remains
/// immutable after revocation; constructing the record does not authenticate the patient's permission.
/// </summary>
public sealed class CarePermission
{
    // Retained by this exact owner only. A revoked successor is another owner and encodes its own digests;
    // neither value is shared by permission identity, serialized or evidence of permission.
    private ContentHash? _grantVersion;
    private ContentHash? _version;

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

    /// <summary>The canonical digest of the original grant alone, encoded at the first read and then retained.</summary>
    public ContentHash GrantVersion
    {
        get
        {
            ContentHash? published = Volatile.Read(ref _grantVersion);
            if (published is not null)
            {
                return published;
            }

            ContentHash computed = ContentHash.Compute(CareCodecV2.EncodePermissionGrant(this));
            // Concurrent first readers may compute independently; publish one complete immutable digest.
            return Interlocked.CompareExchange(ref _grantVersion, computed, null) ?? computed;
        }
    }

    /// <summary>The canonical digest of the grant with this owner's revocation state, encoded at the first read.</summary>
    public ContentHash Version
    {
        get
        {
            ContentHash? published = Volatile.Read(ref _version);
            if (published is not null)
            {
                return published;
            }

            ContentHash computed = ContentHash.Compute(CareCodecV2.EncodePermission(this));
            // Concurrent first readers may compute independently; publish one complete immutable digest.
            return Interlocked.CompareExchange(ref _version, computed, null) ?? computed;
        }
    }

    public CarePermission Revoke(CarePermissionRevocation revocation)
    {
        ArgumentNullException.ThrowIfNull(revocation);
        if (Revocation is not null) throw new InvalidOperationException("An authored permission can be revoked only once.");
        return new(PermissionId, PatientActorId, HelperActorId, UseDefinitionId, UseDefinitionVersion, revocation);
    }
}
