namespace OpenFantasy.Core.Spatial;

/// <summary>A required connection evaluated with the named actor's actual body; not a teleport request.</summary>
public sealed record GroundAccessRequirement(EntityId ActorId, SubunitPosition From, SubunitPosition To);

public enum StructurePlacementStatus
{
    Prepared,
    Unavailable,
    BlockedTerrain,
    BlockedActor,
    BlockedStructure,
    Unsupported,
    AccessBlocked,
    AccessLimit,
    NumericLimit
}

/// <summary>Immutable physical candidate only; the world must couple installation to its material/job commit.</summary>
public sealed class StructurePlacementResult
{
    internal StructurePlacementResult(StructurePlacementStatus status, LocalSpatialSnapshot? candidate = null)
    {
        Status = status;
        Candidate = candidate;
    }

    public StructurePlacementStatus Status { get; }
    public LocalSpatialSnapshot? Candidate { get; }
}
