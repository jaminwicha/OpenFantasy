using OpenFantasy.Core.Exposure;

namespace OpenFantasy.Core.Encounters;

public enum EncounterTimingStatus
{
    Advanced = 1,
    Confirmed = 2,
    RequiredDecisionMissing = 3,
    AutomaticDecisionMissing = 4,
    ActionBoundaryPending = 5,
    NoDueActors = 6
}

/// <summary>
/// An immutable timing proposal. Only an accepted five-unit tick supplies exposure; confirmation
/// supplies a request for the outer authority to validate and publish atomically with its effects.
/// </summary>
public sealed class EncounterTimingTransition
{
    internal EncounterTimingTransition(EncounterTimingSnapshot basis, EncounterTimingSnapshot state,
        EncounterTimingStatus status, EncounterDecision? confirmedDecision = null)
    {
        Basis = basis;
        State = state;
        Status = status;
        ConfirmedDecision = confirmedDecision;
        Experienced = status == EncounterTimingStatus.Advanced
            ? ExposurePacingV1.FromTacticalUnits(EncounterTimingSnapshot.UnitsPerTick)
            : LocalExposureDuration.Zero;
    }

    public EncounterTimingSnapshot Basis { get; }
    public EncounterTimingSnapshot State { get; }
    public EncounterTimingStatus Status { get; }
    public EncounterDecision? ConfirmedDecision { get; }
    public LocalExposureDuration Experienced { get; }
    public bool IsAccepted => Status is EncounterTimingStatus.Advanced or EncounterTimingStatus.Confirmed;
}
