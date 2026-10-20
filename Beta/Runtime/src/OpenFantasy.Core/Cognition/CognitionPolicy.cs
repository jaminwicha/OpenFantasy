namespace OpenFantasy.Core.Cognition;

/// <summary>
/// The actor's own situation at a decision boundary: facts it can know directly about itself and its own project,
/// plus which of its known sources the host found reachable by the ordinary route rules. It never contains world
/// facts the actor has not acquired.
/// </summary>
public sealed record CognitionSituation(ulong Step, EntityId ProjectId, EntityId ProjectQuestId, bool ProjectNeedsWork,
    bool HasGatheringTool, ulong PersonalStock, ulong PersonalStockTarget, bool HasEffort, bool ReadingHold,
    IReadOnlySet<EntityId> ReachableSources);

/// <summary>
/// The decision at one boundary: the policy version that made it, the intention to hold, why something preferred is
/// deferred, whether the current intention just completed and the next fallback wake. It enters canonical state only
/// through an admitted, recorded command; replay applies it and never runs the planner.
/// </summary>
public sealed record CognitionDecision(string PolicyVersion, CognitionIntention? Intention, CognitionDeferral Deferral,
    CognitionWake NextWake, bool CompletedCurrent = false);

/// <summary>
/// Deterministic decision policy for the delayed-supply episode. Priority is conditional: work on the funded project
/// once its delivery is known; otherwise gather a small personal stock when a tool, a known reachable source and
/// effort allow; otherwise wait, with the honest reason. Spent effort defers an existing gathering plan's next swing
/// without abandoning the plan. A reading hold keeps the current intention. The policy
/// proposes; ordinary commands still decide every effect.
/// </summary>
public static class CognitionPolicy
{
    public const string Version = "cognition.delayed-supply.v2";

    /// <summary>The declared fallback cadence in advancing steps: five seconds at the nominal 50 ms step.</summary>
    public const ulong FallbackSteps = 100;

    public static CognitionDecision Decide(ActorCognition cognition, CognitionSituation situation, WorldInstant now)
    {
        ArgumentNullException.ThrowIfNull(cognition);
        ArgumentNullException.ThrowIfNull(situation);
        ArgumentNullException.ThrowIfNull(situation.ReachableSources);
        var next = new CognitionWake(checked(situation.Step + FallbackSteps), WakeCause.Fallback);
        if (situation.ReadingHold)
            return new CognitionDecision(Version, cognition.Intention, CognitionDeferral.ReadingHold, next);
        CognitionBeliefs beliefs = cognition.Beliefs;
        bool working = cognition.Intention?.Kind == IntentionKind.WorkFundedProject;
        if (!situation.ProjectNeedsWork)
            return new CognitionDecision(Version, null, CognitionDeferral.None, next, CompletedCurrent: working);
        if (beliefs.DeliveryEvidence(situation.ProjectQuestId) is { } delivered)
            return new CognitionDecision(Version,
                Keep(cognition, IntentionKind.WorkFundedProject, situation.ProjectId, now, [delivered]), CognitionDeferral.None, next);
        CognitionDeferral reason = situation.PersonalStock >= situation.PersonalStockTarget ? CognitionDeferral.StockTargetMet
            : !situation.HasGatheringTool ? CognitionDeferral.MissingTool
            : beliefs.Sources.Count == 0 ? CognitionDeferral.NoKnownSource
            : beliefs.Sources.All(source => source.BelievedDepleted) ? CognitionDeferral.SourceDepleted
            : !beliefs.Sources.Any(source => !source.BelievedDepleted && situation.ReachableSources.Contains(source.SourceId))
                ? CognitionDeferral.Unreachable
            : !situation.HasEffort ? CognitionDeferral.InsufficientEffort
            : CognitionDeferral.None;
        // Spent effort only delays the next swing: an intention to gather a known, reachable source is kept.
        if (reason == CognitionDeferral.InsufficientEffort && cognition.Intention is { Kind: IntentionKind.GatherPersonalStock } gathering
            && beliefs.Sources.Any(source => source.SourceId == gathering.SubjectId && !source.BelievedDepleted
                && situation.ReachableSources.Contains(source.SourceId)))
            return new CognitionDecision(Version, gathering, reason, next);
        if (reason != CognitionDeferral.None)
            return new CognitionDecision(Version, Keep(cognition, IntentionKind.AwaitSupply, situation.ProjectId, now, []),
                reason, next, CompletedCurrent: cognition.Intention?.Kind == IntentionKind.GatherPersonalStock
                    && reason == CognitionDeferral.StockTargetMet);
        KnownSource target = beliefs.Sources.First(source => !source.BelievedDepleted
            && situation.ReachableSources.Contains(source.SourceId));
        return new CognitionDecision(Version,
            Keep(cognition, IntentionKind.GatherPersonalStock, target.SourceId, now, [target.EvidenceId]),
            CognitionDeferral.AwaitingSupply, next);
    }

    /// <summary>An unchanged intention keeps its original adoption, so re-deciding never rewrites history.</summary>
    private static CognitionIntention Keep(ActorCognition cognition, IntentionKind kind, EntityId subject, WorldInstant now,
        IEnumerable<EntityId> basis) =>
        cognition.Intention is { } current && current.Kind == kind && current.SubjectId == subject
            ? current : new CognitionIntention(kind, subject, now, basis);
}
