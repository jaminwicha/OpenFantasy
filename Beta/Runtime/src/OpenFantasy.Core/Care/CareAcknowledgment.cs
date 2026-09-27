using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;

namespace OpenFantasy.Core.Care;

/// <summary>A completed controller acknowledgment of exact defeat, not an unconscious body's physical action.</summary>
public sealed class CareAcknowledgment
{
    public CareAcknowledgment(EntityId acknowledgmentId, EntityId commandId, EntityId agreementId,
        EntityId encounterId, ulong worldSequence, ContentHash actionHash, ContentHash timingHash,
        EncounterActionResolution defeat)
    {
        ArgumentNullException.ThrowIfNull(actionHash);
        ArgumentNullException.ThrowIfNull(timingHash);
        ArgumentNullException.ThrowIfNull(defeat);
        if (acknowledgmentId != EntityId.DeriveSpawned(commandId, 0) || worldSequence <= defeat.WorldSequence
            || defeat.Status != EncounterActionResolutionStatus.Damaged || defeat.TargetHpAfter != 0)
            throw new ArgumentException("Acknowledgment requires its command identity and an earlier actual lethal damage result.");
        AcknowledgmentId = acknowledgmentId;
        CommandId = commandId;
        AgreementId = agreementId;
        EncounterId = encounterId;
        WorldSequence = worldSequence;
        ActionHash = actionHash;
        TimingHash = timingHash;
        Defeat = defeat;
    }

    public EntityId AcknowledgmentId { get; }
    public EntityId CommandId { get; }
    public EntityId AgreementId { get; }
    public EntityId EncounterId { get; }
    public ulong WorldSequence { get; }
    public ContentHash ActionHash { get; }
    public ContentHash TimingHash { get; }
    public EncounterActionResolution Defeat { get; }
}
