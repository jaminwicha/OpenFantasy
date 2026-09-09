namespace OpenFantasy.Core.History;

/// <summary>
/// A caller-supplied exact temporal boundary. Pinning these immutable values does
/// not prove that the caller obtained them from a durable or authorized source.
/// </summary>
public sealed record HistoryBoundary
{
    public HistoryBoundary(WorldInstant historicalOrigin, WorldInstant knownHead,
        WorldInstant requestedInstant, ulong pinnedHeadRevision, ulong? commitCursor = null)
    {
        if (knownHead < historicalOrigin)
        {
            throw new ArgumentOutOfRangeException(nameof(knownHead),
                "The known head cannot precede the historical origin.");
        }

        if (requestedInstant < historicalOrigin || requestedInstant > knownHead)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedInstant),
                "The requested instant must be within the inclusive known history window.");
        }

        if (commitCursor > pinnedHeadRevision)
        {
            throw new ArgumentOutOfRangeException(nameof(commitCursor),
                "The commit cursor cannot exceed the pinned head revision.");
        }

        HistoricalOrigin = historicalOrigin;
        KnownHead = knownHead;
        RequestedInstant = requestedInstant;
        PinnedHeadRevision = pinnedHeadRevision;
        CommitCursor = commitCursor;
    }

    public WorldInstant HistoricalOrigin { get; }
    public WorldInstant KnownHead { get; }
    public WorldInstant RequestedInstant { get; }
    public ulong PinnedHeadRevision { get; }
    public ulong? CommitCursor { get; }

    /// <summary>Zero selects an empty committed prefix, without changing the requested date.</summary>
    public ulong EffectiveCommitBoundary => CommitCursor ?? PinnedHeadRevision;

    public bool Includes(WorldInstant authoritativeInstant, ulong commitSequence) =>
        commitSequence > 0 && commitSequence <= EffectiveCommitBoundary
        && authoritativeInstant >= HistoricalOrigin && authoritativeInstant <= RequestedInstant;
}
