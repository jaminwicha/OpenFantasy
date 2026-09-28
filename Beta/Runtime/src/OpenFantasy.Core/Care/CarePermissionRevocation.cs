using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Care;

/// <summary>Retained position of one patient-controlled revocation, separate from its command receipt.</summary>
public sealed class CarePermissionRevocation
{
    public CarePermissionRevocation(EntityId revocationId, EntityId commandId, EntityId permissionId,
        EntityId patientActorId, ulong worldSequence, uint commandOrdinal)
    {
        if (revocationId != EntityId.DeriveSpawned(commandId, 0) || worldSequence == 0
            || commandOrdinal >= WorldStepBatch.MaximumActorsPerBatch
            || new HashSet<EntityId> { revocationId, commandId, permissionId, patientActorId }.Count != 4)
            throw new ArgumentException("A revocation requires its derived identity, patient and actual command position.");
        RevocationId = revocationId;
        CommandId = commandId;
        PermissionId = permissionId;
        PatientActorId = patientActorId;
        WorldSequence = worldSequence;
        CommandOrdinal = commandOrdinal;
    }

    public EntityId RevocationId { get; }
    public EntityId CommandId { get; }
    public EntityId PermissionId { get; }
    public EntityId PatientActorId { get; }
    public ulong WorldSequence { get; }
    public uint CommandOrdinal { get; }
    public ulong ExpectedPermissionRevision => 0;
    public ulong PermissionRevisionAfter => 1;
}
