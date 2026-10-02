namespace OpenFantasy.Core.Care;

/// <summary>Exact latest closed request per patient, independent of the latest actual treatment.</summary>
public sealed class CareClosedDefeatWitness
{
    internal CareClosedDefeatWitness(EntityId patientActorId, CareAcknowledgment acknowledgment,
        CareTreatment? treatment, CareDisposition? disposition)
    {
        ArgumentNullException.ThrowIfNull(acknowledgment);
        if ((treatment is null) == (disposition is null) || acknowledgment.Defeat.TargetActorId != patientActorId
            || (treatment?.AcknowledgmentId ?? disposition!.AcknowledgmentId) != acknowledgment.AcknowledgmentId
            || (treatment?.WorldSequence ?? disposition!.WorldSequence) <= acknowledgment.WorldSequence)
            throw new ArgumentException("A closed defeat witness retains one exact request and exactly one later terminal record.");
        PatientActorId = patientActorId;
        Acknowledgment = acknowledgment;
        Treatment = treatment;
        Disposition = disposition;
    }

    public EntityId PatientActorId { get; }
    public CareAcknowledgment Acknowledgment { get; }
    public CareTreatment? Treatment { get; }
    public CareDisposition? Disposition { get; }
    public ulong TerminalSequence => Treatment?.WorldSequence ?? Disposition!.WorldSequence;
}
