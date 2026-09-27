namespace OpenFantasy.Core.Care;

/// <summary>An ordered health mutation bound to one acknowledgment and one separately retained material disposition.</summary>
public sealed class CareTreatment
{
    public CareTreatment(EntityId treatmentId, EntityId commandId, EntityId acknowledgmentId,
        EntityId consumptionId, ulong worldSequence, ulong timingRevision, ulong patientRevisionBefore,
        int maximumHp, int hpAfter, uint helperNormalizedSpeed, ulong helperDelay)
    {
        if (treatmentId != EntityId.DeriveSpawned(commandId, 1) || consumptionId != EntityId.DeriveSpawned(commandId, 0)
            || worldSequence == 0 || timingRevision == 0 || patientRevisionBefore == ulong.MaxValue
            || maximumHp <= 0 || hpAfter <= 0 || hpAfter > maximumHp || helperNormalizedSpeed == 0
            || helperDelay != (10_000UL + helperNormalizedSpeed - 1) / helperNormalizedSpeed)
            throw new ArgumentException("Treatment requires exact derived identities, a positive capped restoration and an earned standard action debt.");
        TreatmentId = treatmentId;
        CommandId = commandId;
        AcknowledgmentId = acknowledgmentId;
        ConsumptionId = consumptionId;
        WorldSequence = worldSequence;
        TimingRevision = timingRevision;
        PatientRevisionBefore = patientRevisionBefore;
        MaximumHp = maximumHp;
        HpAfter = hpAfter;
        HelperNormalizedSpeed = helperNormalizedSpeed;
        HelperDelay = helperDelay;
    }

    public EntityId TreatmentId { get; }
    public EntityId CommandId { get; }
    public EntityId AcknowledgmentId { get; }
    public EntityId ConsumptionId { get; }
    public ulong WorldSequence { get; }
    public ulong TimingRevision { get; }
    public ulong PatientRevisionBefore { get; }
    public ulong PatientRevisionAfter => PatientRevisionBefore + 1;
    public int MaximumHp { get; }
    public int HpAfter { get; }
    public uint HelperNormalizedSpeed { get; }
    public ulong HelperDelay { get; }
}
