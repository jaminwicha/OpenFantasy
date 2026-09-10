namespace OpenFantasy.Core.Spatial;

public enum SpatialOccupancy
{
    Clear,
    Unavailable,
    BlockedTerrain,
    BlockedActor,
    Unsupported,
    BlockedStructure
}

public enum SpatialMoveStatus
{
    Prepared,
    RevisionConflict,
    NoGroundLocomotion,
    NumericLimit,
    Unavailable,
    BlockedTerrain,
    BlockedActor,
    Unsupported,
    BlockedStructure
}

public enum SpatialInteraction
{
    Reachable,
    DifferentFloor,
    OutOfRange,
    Unavailable,
    Occluded
}

/// <summary>A candidate position only; the world owner must still authorize, schedule and atomically commit it.</summary>
public sealed class SpatialMoveResult
{
    internal SpatialMoveResult(SpatialMoveStatus status, ActorPresence? candidate = null)
    {
        Status = status;
        Candidate = candidate;
    }

    public SpatialMoveStatus Status { get; }
    public ActorPresence? Candidate { get; }
}
