namespace OpenFantasy.Core.Observations;

/// <summary>
/// Trusted acquisition metadata, never a client packet. A claimed commit number does not prove
/// durability. RecordedCommitSequence is the commit that recorded this actor's knowledge,
/// not the older described event. The authority must establish acquisition and identification.
/// </summary>
public sealed class CommittedResourceObservation
{
    public CommittedResourceObservation(ObservationScope scope, ulong recordedCommitSequence,
        EntityId privateSourceEventId, ResourceObservationView observation)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(observation);
        if (recordedCommitSequence == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recordedCommitSequence));
        }

        if (privateSourceEventId == observation.DisclosureId)
        {
            throw new ArgumentException("Disclosure identity must be separate from private source identity.");
        }

        Scope = scope;
        RecordedCommitSequence = recordedCommitSequence;
        PrivateSourceEventId = privateSourceEventId;
        Observation = observation;
    }

    public ObservationScope Scope { get; }
    public ulong RecordedCommitSequence { get; }
    public EntityId PrivateSourceEventId { get; }
    public ResourceObservationView Observation { get; }
}
