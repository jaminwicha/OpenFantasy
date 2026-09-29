using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Care;

/// <summary>Prior voluntary care permission between two actual actors for one pinned remedy definition.</summary>
public sealed class CareAgreement
{
    // Retained by this exact owner only; never serialized, shared by identity or copied to another agreement.
    private ContentHash? _version;

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
    /// <summary>The canonical digest of these immutable facts, encoded at the first read and then retained.</summary>
    public ContentHash Version
    {
        get
        {
            ContentHash? published = Volatile.Read(ref _version);
            if (published is not null)
            {
                return published;
            }

            ContentHash computed = ContentHash.Compute(CareCodecV1.EncodeAgreement(this));
            // Concurrent first readers may compute independently; publish one complete immutable digest.
            return Interlocked.CompareExchange(ref _version, computed, null) ?? computed;
        }
    }
}
