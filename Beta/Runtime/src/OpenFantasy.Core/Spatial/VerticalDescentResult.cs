namespace OpenFantasy.Core.Spatial;

public enum VerticalDescentStatus
{
    Landed,
    IncompleteSupport,
    ActorContact,
    InitialTerrainOverlap,
    InitialStructureOverlap,
    InitialActorOverlap,
    Unavailable,
    NoContactWithinLimit,
    NumericLimit
}

/// <summary>A checked geometric prefix, not permission to publish a body, advance time or apply impact damage.</summary>
public sealed class VerticalDescentResult
{
    internal VerticalDescentResult(VerticalDescentStatus status, long dropSubunits = 0, SubunitBox? reachedVolume = null)
    {
        Status = status;
        DropSubunits = dropSubunits;
        ReachedVolume = reachedVolume;
    }

    public VerticalDescentStatus Status { get; }
    public long DropSubunits { get; }

    /// <summary>
    /// Last clear translated box at the first contact or search limit. Only Landed has complete geometric
    /// footing; other contact results need a coupled response, and the search limit is not a landing.
    /// </summary>
    public SubunitBox? ReachedVolume { get; }
}
