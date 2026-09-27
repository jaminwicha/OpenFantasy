using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Care;

/// <summary>Prior voluntary care permission between two actual actors for one pinned remedy definition.</summary>
public sealed class CareAgreement
{
    public CareAgreement(EntityId agreementId, EntityId patientActorId, EntityId helperActorId,
        EntityId remedyDefinitionId, ContentHash remedyVersion)
    {
        ArgumentNullException.ThrowIfNull(remedyVersion);
        if (new HashSet<EntityId> { agreementId, patientActorId, helperActorId, remedyDefinitionId }.Count != 4)
            throw new ArgumentException("Care agreement, participants and remedy require distinct identities.");
        AgreementId = agreementId;
        PatientActorId = patientActorId;
        HelperActorId = helperActorId;
        RemedyDefinitionId = remedyDefinitionId;
        RemedyVersion = remedyVersion;
    }

    public EntityId AgreementId { get; }
    public EntityId PatientActorId { get; }
    public EntityId HelperActorId { get; }
    public EntityId RemedyDefinitionId { get; }
    public ContentHash RemedyVersion { get; }
    public ContentHash Version => ContentHash.Compute(CareCodecV1.EncodeAgreement(this));
}
