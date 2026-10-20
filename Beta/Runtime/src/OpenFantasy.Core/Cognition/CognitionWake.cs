namespace OpenFantasy.Core.Cognition;

/// <summary>What made reconsideration due. Only the actor's own acquisitions, facts and outcomes wake it.</summary>
public enum WakeCause { EvidenceAcquired = 1, OwnResourceChanged = 2, ActionFinished = 3, Fallback = 4 }

/// <summary>Why a due reconsideration is not being serviced yet; the wake keeps its cause meanwhile.</summary>
public enum WakeSuppression { None = 0, ReadingHold = 1, Encounter = 2, PaidContinuation = 3 }

/// <summary>
/// Pending reconsideration in the exploration advancing-step domain: the earliest unserviced boundary and its
/// cause. It is persistent, so a restart keeps the wake and due age survives reopening.
/// </summary>
public sealed record CognitionWake(ulong DueStep, WakeCause Cause, WakeSuppression Suppression = WakeSuppression.None)
{
    public CognitionWake Validated() => Enum.IsDefined(Cause) && Enum.IsDefined(Suppression) ? this
        : throw new ArgumentOutOfRangeException(nameof(Cause), "A wake names a known cause and suppression.");

    /// <summary>The earlier of two wakes; an existing suppression persists until it is lifted.</summary>
    public static CognitionWake Earliest(CognitionWake? existing, CognitionWake candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (existing is null) return candidate.Validated();
        CognitionWake earlier = candidate.DueStep < existing.DueStep ? candidate : existing;
        return earlier with { Suppression = existing.Suppression };
    }
}
