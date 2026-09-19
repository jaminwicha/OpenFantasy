using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

public enum ReservoirBoundaryFailure
{
    MissingTerrain, InteriorTerrain, InteriorConstruction, InteriorActor, UnsealedFloor, UnsealedWall,
    UnprovedGate, WorkLimit, CoordinateLimit
}

public enum ReservoirBoundarySurface { Interior, Floor, West, East, North, South, Gate }

public sealed record ReservoirBoundarySample(ReservoirBoundaryFailure Failure, CellCoordinate Cell,
    ReservoirBoundarySurface Surface);

/// <summary>Complete-or-explicitly-incomplete assessment of one nominal vessel, not structural or hydraulic admission.</summary>
public sealed class ReservoirBoundaryResult
{
    public const int MaximumFailureSamples = 32;
    internal ReservoirBoundaryResult(EntityId reservoirId, bool completed, long interiorCells, long boundaryFaces,
        long terrainFaces, long constructedFaces, IDictionary<ReservoirBoundaryFailure, long> failures,
        IEnumerable<ReservoirBoundarySample> samples)
    {
        ReservoirId = reservoirId;
        Completed = completed;
        InteriorCells = interiorCells;
        BoundaryFaces = boundaryFaces;
        TerrainSealedFaces = terrainFaces;
        ConstructedSealedFaces = constructedFaces;
        Failures = new ReadOnlyDictionary<ReservoirBoundaryFailure, long>(new SortedDictionary<ReservoirBoundaryFailure, long>(failures));
        Samples = samples.ToList().AsReadOnly();
    }
    public EntityId ReservoirId { get; }
    public bool Completed { get; }
    public bool IsContained => Completed && Failures.Count == 0;
    public long InteriorCells { get; }
    public long BoundaryFaces { get; }
    public long TerrainSealedFaces { get; }
    public long ConstructedSealedFaces { get; }
    public ReadOnlyDictionary<ReservoirBoundaryFailure, long> Failures { get; }
    public ReadOnlyCollection<ReservoirBoundarySample> Samples { get; }
}

/// <summary>Pins every consequence-bearing input; it is a derived report and grants no mutation or cached admission right.</summary>
public sealed class ReservoirBoundaryAssessment
{
    internal ReservoirBoundaryAssessment(ContentHash worldHash, ContentHash reservoirHash, ContentHash policyHash,
        IEnumerable<ReservoirBoundaryResult> results)
    {
        WorldHash = worldHash;
        ReservoirHash = reservoirHash;
        PolicyHash = policyHash;
        Results = results.OrderBy(r => r.ReservoirId).ToList().AsReadOnly();
    }
    public ContentHash WorldHash { get; }
    public ContentHash ReservoirHash { get; }
    public ContentHash PolicyHash { get; }
    public ReadOnlyCollection<ReservoirBoundaryResult> Results { get; }
    public bool Completed => Results.All(r => r.Completed);
    public bool IsContained => Completed && Results.All(r => r.IsContained);
}
