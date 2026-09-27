using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Care;

/// <summary>Captures the exact agreement and encounter outcome for authority-owned care acknowledgment.</summary>
public sealed record AcknowledgeCareIntent : WorldActionIntent
{
    public AcknowledgeCareIntent(EntityId agreementId, EntityId encounterId, ulong expectedLifecycleRevision,
        ContentHash expectedActionHash)
    {
        ArgumentNullException.ThrowIfNull(expectedActionHash);
        AgreementId = agreementId;
        EncounterId = encounterId;
        ExpectedLifecycleRevision = expectedLifecycleRevision;
        ExpectedActionHash = expectedActionHash;
    }

    public EntityId AgreementId { get; }
    public EntityId EncounterId { get; }
    public ulong ExpectedLifecycleRevision { get; }
    public ContentHash ExpectedActionHash { get; }
}
