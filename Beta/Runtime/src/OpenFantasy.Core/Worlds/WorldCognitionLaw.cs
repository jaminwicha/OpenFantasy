using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Cognition;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>The committed outcome of one admitted action, as the cognition laws read it.</summary>
internal sealed record WorldCognitionOutcome(WorldActionCommand Action, CountedAuthorityState Inventory,
    GatheringSnapshot? Gathering, QuestSnapshot? Quests, EntityId? SpawnedId,
    WorldConsentedTransferEvidence? MaterialTransfer, WorldMaterialProposalSettlement? ProposalSettlement);

/// <summary>
/// The laws of the cognition interpretation. Evidence comes only from an action's committed outcome and joins that
/// action's admission cohort; a refused or rolled-back action gives nobody anything. World truth never reaches a
/// person who did not take part, so an undisclosed delivery elsewhere changes neither a belief nor a wake. A recorded
/// decision is admitted only at a due, unsuppressed wake and is applied exactly as recorded.
/// </summary>
internal static class WorldCognitionLaw
{
    /// <summary>
    /// The evidence one committed action gives its participants, in knower-ordinal order. Ordinals are fixed per
    /// action kind, so replay derives the same identities. Returns false when a derived identity is already taken,
    /// which refuses the action like any other occupied spawned identity.
    /// </summary>
    internal static bool TryDerive(WorldSnapshot state, WorldCognitionOutcome outcome, Func<EntityId, bool> occupied,
        out IReadOnlyList<CognitionEvidence> evidence)
    {
        WorldActionCommand action = outcome.Action;
        WorldInstant at = state.Calendar;
        var derived = new List<CognitionEvidence>(2);
        CognitionEvidence Make(ulong ordinal, EntityId knower, EvidenceKind kind, EntityId? counterpart, EntityId subject,
            ulong quantity) => new(CognitionEvidence.DeriveIdentity(action.CommandId, ordinal), knower, kind, action.CommandId,
                counterpart, subject, quantity, at);

        switch (action.Intent)
        {
            case GiveItemIntent give:
                derived.Add(Make(0, give.TargetActorId, EvidenceKind.ItemReceived, action.ActorId,
                    outcome.SpawnedId ?? give.LotId, give.Quantity));
                break;
            case OfferMaterialIntent when outcome.MaterialTransfer is { } transfer:
                var offered = transfer.Approval.Command.Terms;
                derived.Add(Make(0, offered.RecipientActorId, EvidenceKind.ItemReceived, offered.SenderActorId,
                    transfer.MovedLotId, offered.Quantity));
                break;
            case AcceptMaterialProposalIntent when outcome.ProposalSettlement is { } settlement:
                var proposed = settlement.Acceptance.Terms;
                derived.Add(Make(0, proposed.RecipientActorId, EvidenceKind.ItemReceived, proposed.SenderActorId,
                    settlement.MovedLotId, proposed.Quantity));
                break;
            case CompleteDeliveryQuestIntent delivered:
                // The giver takes the delivery in person, so the giver directly knows it arrived and from whom.
                DeliveryQuestDefinition definition = outcome.Quests?.FindDefinition(delivered.QuestId)
                    ?? throw new InvalidOperationException("A completed delivery names its admitted quest.");
                derived.Add(Make(0, definition.GiverActorId, EvidenceKind.QuestDeliveryReceived, action.ActorId,
                    delivered.QuestId, definition.Quantity));
                break;
            case GatherIntent gather when outcome.SpawnedId is { } gathered:
                // The gatherer learns its own yield and sees the source it just harvested.
                derived.Add(Make(0, action.ActorId, EvidenceKind.GatherOutcome, null, gather.NodeId,
                    outcome.Inventory.Custody.GetLot(gathered).Quantity));
                derived.Add(Make(1, action.ActorId, EvidenceKind.SourceObserved, null, gather.NodeId,
                    VisibleHarvests(outcome.Gathering!, gather.NodeId)));
                break;
            case InspectGatherSourceIntent inspect:
                derived.Add(Make(0, action.ActorId, EvidenceKind.SourceObserved, null, inspect.NodeId,
                    VisibleHarvests(outcome.Gathering!, inspect.NodeId)));
                break;
        }
        evidence = derived.AsReadOnly();
        return derived.All(entry => !occupied(entry.EvidenceId));
    }

    /// <summary>
    /// Gives each knower its evidence and makes reconsideration due at the step the batch produces. A bound overflow
    /// throws <see cref="CognitionCapacityException"/>, which no caller converts into a refused action.
    /// </summary>
    internal static CognitionSnapshot Acquire(CognitionSnapshot cognition, IReadOnlyList<CognitionEvidence> evidence,
        ulong resultingStep)
    {
        foreach (CognitionEvidence entry in evidence)
        {
            cognition = cognition.With(cognition.For(entry.KnowerId).Acquire(entry, resultingStep));
        }
        return cognition;
    }

    /// <summary>
    /// Work on the actor's own project is a self-owned fact read directly, so each work step is not evidence. The step
    /// that completes the project is the builder's finished action and makes reconsideration due.
    /// </summary>
    internal static CognitionSnapshot ScheduleCompletedWork(CognitionSnapshot cognition, WorldActionCommand action,
        ConstructionSnapshot? before, ConstructionSnapshot? after, ulong resultingStep)
    {
        if (action.Intent is not WorkConstructionIntent work
            || before?.Projects.SingleOrDefault(project => project.ProjectId == work.ProjectId) is not { IsCompleted: false }
            || after?.Projects.SingleOrDefault(project => project.ProjectId == work.ProjectId) is not { IsCompleted: true })
            return cognition;
        ActorCognition builder = cognition.For(action.ActorId);
        ActorCognition scheduled = builder.Schedule(new CognitionWake(resultingStep, WakeCause.ActionFinished));
        return ReferenceEquals(scheduled, builder) ? cognition : cognition.With(scheduled);
    }

    /// <summary>
    /// The perception rule: a deliberate look at one source within the observation range, under the same anchored
    /// sightline query gathering uses for reach. It needs no tool and reads only that source's visible harvests.
    /// Sight is tested before anything else about the source, so an absent source and one out of sight give the same
    /// refusal, and a stale revision is revealed only for a source the actor can see.
    /// </summary>
    internal static WorldActionRejection ResolveInspection(WorldSnapshot state, LocalSpatialSnapshot space,
        GatheringSnapshot gathering, WorldActionCommand action, bool held)
    {
        var inspect = (InspectGatherSourceIntent)action.Intent;
        if (WorldActorCommandAuthority.AuthorizeSelection(state, action) is null) return WorldActionRejection.NotAuthorized;
        if (held) return WorldActionRejection.Unavailable;
        ActorPresence actor = space.GetActor(action.ActorId);
        if (actor.Revision != action.ExpectedActorRevision) return WorldActionRejection.RevisionConflict;
        GatherNodeState? node = gathering.Nodes.FirstOrDefault(value => value.NodeId == inspect.NodeId);
        if (node is null || ResourceInteractionQueries.QueryWithin(space, actor.ActorId, node.Feet,
                WorldCognitionPerception.ObservationRangeSubunits) != SpatialInteraction.Reachable)
            return WorldActionRejection.OutOfReach;
        return node.Revision == inspect.ExpectedNodeRevision ? WorldActionRejection.None : WorldActionRejection.RevisionConflict;
    }

    /// <summary>
    /// Applies one recorded decision to the deciding actor's own cognition. It is admitted only under the actor's own
    /// control, outside a hold, at the expected cognition revision, at a due and unsuppressed wake, with a fallback
    /// within the declared cadence, and only when the decision is supported by the actor's own evidence and project.
    /// The decision is then applied exactly as recorded; nothing here reruns the policy. Deferral reasons and the
    /// personal stock target remain the planner's claims.
    /// </summary>
    internal static (WorldActionRejection Rejection, CognitionSnapshot Cognition) ResolveDecision(WorldSnapshot state,
        CognitionSnapshot cognition, ConstructionSnapshot? construction, WorldActionCommand action, bool held)
    {
        var decide = (RecordCognitionDecisionIntent)action.Intent;
        if (action.ControlWitness is not null || WorldActorCommandAuthority.AuthorizeSelection(state, action) is null)
            return (WorldActionRejection.NotAuthorized, cognition);
        // A hold opened earlier in this batch suppresses reconsideration exactly as one at its start does.
        if (held) return (WorldActionRejection.Unavailable, cognition);
        ActorCognition current = cognition.For(action.ActorId);
        if (current.Revision != decide.ExpectedCognitionRevision) return (WorldActionRejection.RevisionConflict, cognition);
        CognitionDecision decision = decide.Decision;
        if (current.Wake is not { Suppression: WakeSuppression.None } wake || wake.DueStep > state.AdvancingSteps
            || decision.NextWake.DueStep <= state.AdvancingSteps
            || decision.NextWake.DueStep > checked(state.AdvancingSteps + CognitionPolicy.FallbackSteps)
            || decision.Deferral == CognitionDeferral.ReadingHold
            || !Supports(state, current, construction, action.ActorId, decision))
            return (WorldActionRejection.Unavailable, cognition);
        ActorCognition adopted;
        try
        {
            // A memory overflow is a CognitionCapacityException, not an ArgumentException: it stops the authority.
            adopted = current.Adopt(decision, state.Calendar);
        }
        catch (ArgumentException)
        {
            return (WorldActionRejection.Unavailable, cognition);
        }
        return ReferenceEquals(adopted, current) ? (WorldActionRejection.Unavailable, cognition)
            : (WorldActionRejection.None, cognition.With(adopted));
    }

    /// <summary>
    /// What the actor's own evidence and project must show for a decision to be remembered. A new intention is adopted
    /// now; waiting and working name the actor's own project; working cites the delivery it received for that
    /// project's quest; gathering cites an observation of its source. Completion is recorded only for a project that
    /// is complete, or for gathering that yielded something since it was adopted.
    /// </summary>
    private static bool Supports(WorldSnapshot state, ActorCognition current, ConstructionSnapshot? construction,
        EntityId actorId, CognitionDecision decision)
    {
        ConstructionProject? Own(EntityId projectId) => construction?.Projects.SingleOrDefault(project =>
            project.ProjectId == projectId && project.BuilderActorId == actorId);
        CognitionEvidence? Held(EntityId evidenceId) => current.Evidence.FirstOrDefault(entry => entry.EvidenceId == evidenceId);
        if (decision.Intention is { } next && (current.Intention is null || !next.SameAs(current.Intention)))
        {
            if (next.AdoptedAt != state.Calendar) return false;
            bool supported = next.Kind switch
            {
                IntentionKind.AwaitSupply => Own(next.SubjectId) is { IsCompleted: false },
                IntentionKind.WorkFundedProject => Own(next.SubjectId) is { IsCompleted: false } project
                    && next.Basis.Any(id => Held(id) is { Kind: EvidenceKind.QuestDeliveryReceived } delivered
                        && delivered.SubjectId == project.QuestId),
                IntentionKind.GatherPersonalStock => next.Basis.Any(id => Held(id) is { Kind: EvidenceKind.SourceObserved } seen
                    && seen.SubjectId == next.SubjectId),
                _ => false
            };
            if (!supported) return false;
        }
        if (!decision.CompletedCurrent) return true;
        return current.Intention switch
        {
            { Kind: IntentionKind.WorkFundedProject } work => Own(work.SubjectId) is { IsCompleted: true },
            { Kind: IntentionKind.GatherPersonalStock } gather => decision.Deferral == CognitionDeferral.StockTargetMet
                && current.Evidence.Any(entry => entry.Kind == EvidenceKind.GatherOutcome && entry.SubjectId == gather.SubjectId
                    && entry.Quantity != 0 && entry.AcquiredAt >= gather.AdoptedAt),
            _ => false
        };
    }

    /// <summary>
    /// Records why due reconsideration cannot be serviced in the committed root: a reading hold, an encounter, or a
    /// paid continuation such as a pending care obligation. It changes only pending wakes, keeps their due step and
    /// cause, and is applied to a boundary that commits anyway, so it never writes a commit of its own.
    /// </summary>
    internal static WorldSnapshot ReconcileSuppression(WorldSnapshot candidate)
    {
        if (candidate.Cognition is not { } cognition) return candidate;
        WakeSuppression condition = Condition(candidate);
        CognitionSnapshot reconciled = cognition;
        foreach (ActorCognition actor in cognition.Actors)
        {
            if (actor.Wake is { } wake && wake.Suppression != condition)
                reconciled = reconciled.With(actor.Suppress(condition));
        }
        return ReferenceEquals(reconciled, cognition) ? candidate : candidate.WithCognition(reconciled);
    }

    internal static WakeSuppression Condition(WorldSnapshot world) =>
        world.HasReadingHold ? WakeSuppression.ReadingHold
        : world.Pacing?.Encounter is not null || world.EncounterLifecycle?.Phase is not (null or EncounterLifecyclePhase.Exploration)
            ? WakeSuppression.Encounter
        : world.PendingExcavation is not null || world.Care?.Pending is not null ? WakeSuppression.PaidContinuation
        : WakeSuppression.None;

    private static ulong VisibleHarvests(GatheringSnapshot gathering, EntityId nodeId) =>
        (ulong)gathering.Nodes.Single(node => node.NodeId == nodeId).RemainingSwings;
}
