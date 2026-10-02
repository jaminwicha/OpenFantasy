namespace OpenFantasy.Core.Care;

public enum CareDispositionKind
{
    PatientCancelled = 1,
    HelperDeclined = 2
}

/// <summary>A voluntary terminal decision about one request, without treatment or resource effects.</summary>
public sealed class CareDisposition
{
    public CareDisposition(EntityId dispositionId, EntityId commandId, EntityId acknowledgmentId,
        EntityId decidingActorId, CareDispositionKind kind, ulong worldSequence)
    {
        if (dispositionId != EntityId.DeriveSpawned(commandId, 0) || worldSequence == 0
            || kind is not (CareDispositionKind.PatientCancelled or CareDispositionKind.HelperDeclined))
            throw new ArgumentException("A disposition requires its derived identity, a known decision and a positive world sequence.");
        DispositionId = dispositionId;
        CommandId = commandId;
        AcknowledgmentId = acknowledgmentId;
        DecidingActorId = decidingActorId;
        Kind = kind;
        WorldSequence = worldSequence;
    }

    public EntityId DispositionId { get; }
    public EntityId CommandId { get; }
    public EntityId AcknowledgmentId { get; }
    public EntityId DecidingActorId { get; }
    public CareDispositionKind Kind { get; }
    public ulong WorldSequence { get; }
}
