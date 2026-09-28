namespace OpenFantasy.Core.Care;

/// <summary>
/// Exact latest completed treatment of one patient at a closed boundary. It is a retained chronology and
/// resource reference; the patient's current health and revision still belong to actor progression.
/// </summary>
public sealed class CarePatientWitness
{
    internal CarePatientWitness(EntityId patientActorId, CareTreatment treatment)
    {
        ArgumentNullException.ThrowIfNull(treatment);
        PatientActorId = patientActorId;
        Treatment = treatment;
    }

    public EntityId PatientActorId { get; }
    public CareTreatment Treatment { get; }
}
