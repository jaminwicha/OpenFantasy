using OpenFantasy.Core.Cognition;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// A deliberate look at one gathering source in plain view: the admitted perception rule of the cognition
/// interpretation. The actor learns the source and how many harvests it visibly offers now. The source must lie within
/// the observation range along an unobstructed sightline, so inspection never reveals a source the actor cannot see.
/// </summary>
public sealed record InspectGatherSourceIntent(EntityId NodeId, ulong ExpectedNodeRevision) : WorldActionIntent;

/// <summary>
/// One actor's recorded decision at a due, unsuppressed reconsideration. It is an ordinary actor action in canonical
/// order. Admission applies the recorded decision to the actor's own cognition; replay applies the same bytes and never
/// runs the policy that proposed them. It grants no material, permission or work.
/// </summary>
public sealed record RecordCognitionDecisionIntent : WorldActionIntent
{
    public RecordCognitionDecisionIntent(ulong expectedCognitionRevision, CognitionDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        // Encoding validates the policy version, deferral and wake before the command can exist.
        _ = CognitionCodecV2.EncodeDecision(decision);
        ExpectedCognitionRevision = expectedCognitionRevision;
        Decision = decision;
    }

    public ulong ExpectedCognitionRevision { get; }
    public CognitionDecision Decision { get; }
}
