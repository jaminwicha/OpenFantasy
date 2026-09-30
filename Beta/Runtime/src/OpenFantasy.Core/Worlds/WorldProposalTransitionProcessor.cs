namespace OpenFantasy.Core.Worlds;

public sealed class WorldProposalTransitionDecision
{
    internal WorldProposalTransitionDecision(EntityId causeId, WorldSnapshot candidate, WorldMaterialCareArchiveSegment archive)
    {
        CauseId = causeId;
        Candidate = candidate;
        Archive = archive;
    }

    public EntityId CauseId { get; }

    /// <summary>The saved-proposal successor, one commit after the closed source head.</summary>
    public WorldSnapshot Candidate { get; }

    /// <summary>The exact source head sealed as the transition archive, with its derived anchor.</summary>
    public WorldMaterialCareArchiveSegment Archive { get; }
}

/// <summary>
/// Exact-head transition of a lived coordinated world into the saved-proposal interpretation. It closes
/// the complete source head as one joint archive and continues it with empty consent evidence and an
/// empty proposal owner. Time, readiness, calendar, actors, stock, care, resources, permissions and
/// controls stay exact; only the world commit revision advances.
/// </summary>
public sealed class WorldProposalTransitionProcessor
{
    public WorldProposalTransitionDecision Decide(WorldSnapshot state, WorldProposalTransition request,
        Func<EntityId, bool>? historicallyOccupied = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        if (state.ProfileVersion != WorldProposalTransition.SourceProfileVersion
            || state.Coordination is not { ConsentedMaterial: null, Proposals: null } coordination
            || state.WorldId != request.WorldId || state.BranchId != request.BranchId
            || state.Revision != request.ExpectedRevision || state.AdvancingSteps != request.ExpectedAdvancingSteps
            || state.ContentHash != request.ExpectedHeadHash || state.AdmissionVersion != request.SourceAdmissionVersion
            || coordination.Anchor?.ContentHash != request.ExpectedAnchorHash
            || (coordination.Anchor is { } anchor ? checked(anchor.Reference.Ordinal + 1) : 0) != request.TransitionOrdinal)
        {
            throw new InvalidOperationException("The proposal transition does not match the exact lived coordinated head.");
        }
        if (state.PendingExcavation is not null)
        {
            throw new InvalidOperationException("A pending interval must resolve before the interpretation changes.");
        }
        if (state.IdentityIndex.Contains(request.CauseId) || historicallyOccupied?.Invoke(request.CauseId) == true)
        {
            throw new InvalidOperationException("The proposal transition cause is occupied.");
        }
        WorldMaterialCareArchiveSegment archive = WorldMaterialCareArchiveSegment.SealTransition(state, request);
        WorldSnapshot continued = archive.Continued;
        // The lifecycle mirrors the world commit revision; retaining its boundary changes no phase,
        // clock, debt or encounter, exactly as an actionless commit does.
        WorldSnapshot candidate = continued.WithState(continued.Inventory, continued.Space, continued.Residents,
            continued.Calendar, checked(continued.Revision + 1), continued.AdvancingSteps, continued.Dialogues,
            continued.Gathering, continued.Production, continued.Progression, continued.Quests, continued.Construction,
            continued.Reservoirs, continued.Transport, continued.Excavation, continued.ConstructionPhysical,
            continued.LiquidCells, continued.Pacing, null, continued.EncounterLifecycle!.RetainBoundary(), continued.Care,
            continued.ResourceHistory, continued.Coordination);
        if (candidate.ProfileVersion != WorldProposalTransition.TargetProfileVersion)
        {
            throw new InvalidOperationException("The proposal transition must select exactly the saved-proposal interpretation.");
        }
        return new WorldProposalTransitionDecision(request.CauseId, candidate, archive);
    }
}
