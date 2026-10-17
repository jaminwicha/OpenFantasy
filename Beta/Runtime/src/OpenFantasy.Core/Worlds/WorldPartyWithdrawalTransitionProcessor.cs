using OpenFantasy.Core.Encounters;

namespace OpenFantasy.Core.Worlds;

public sealed class WorldPartyWithdrawalTransitionDecision
{
    internal WorldPartyWithdrawalTransitionDecision(EntityId causeId, WorldSnapshot candidate)
    {
        CauseId = causeId;
        Candidate = candidate;
    }

    public EntityId CauseId { get; }
    public WorldSnapshot Candidate { get; }
}

/// <summary>Enables collective retreat at a quiet boundary, retaining every existing state owner and archive.</summary>
public sealed class WorldPartyWithdrawalTransitionProcessor
{
    public WorldPartyWithdrawalTransitionDecision Decide(WorldSnapshot state, WorldPartyWithdrawalTransition request,
        Func<EntityId, bool>? historicallyOccupied = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        if (state.ProfileVersion != WorldPartyWithdrawalTransition.SourceProfileVersion
            || state.WorldId != request.WorldId || state.BranchId != request.BranchId
            || state.Revision != request.ExpectedRevision || state.AdvancingSteps != request.ExpectedAdvancingSteps
            || state.ContentHash != request.ExpectedHeadHash || state.AdmissionVersion != request.SourceAdmissionVersion
            || state.Coordination?.Anchor?.ContentHash != request.ExpectedAnchorHash)
        {
            throw new InvalidOperationException("The withdrawal transition does not match the exact effective-control head.");
        }
        if (state.HasReadingHold || state.PendingExcavation is not null
            || state.EncounterLifecycle?.Phase != EncounterLifecyclePhase.Exploration
            || state.Pacing?.Encounter is not null || state.Retreat is not { Active: null, Withdrawal: null })
        {
            throw new InvalidOperationException("Complete the reading, encounter or physical continuation before changing interpretation.");
        }
        if (state.IdentityIndex.Contains(request.CauseId) || historicallyOccupied?.Invoke(request.CauseId) == true)
        {
            throw new InvalidOperationException("The withdrawal transition cause is already occupied.");
        }
        WorldSnapshot candidate = state.WithPartyWithdrawal();
        _ = WorldSnapshotCodec.Encode(candidate);
        return new WorldPartyWithdrawalTransitionDecision(request.CauseId, candidate);
    }
}
