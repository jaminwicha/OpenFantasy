using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Bounded selected actor-archive reads and pinned structure lookup; never a directory or publication owner.</summary>
internal sealed class SpatialOccupancyReadPreparation
{
    private readonly Func<SpatialOccupancyReference, int, byte[]?> _readActor;
    private readonly Func<SpatialOccupancyReference, SpatialStructure?> _readStructure;

    internal SpatialOccupancyReadPreparation(Func<SpatialOccupancyReference, int, byte[]?> readActor,
        Func<SpatialOccupancyReference, SpatialStructure?> readStructure)
    {
        ArgumentNullException.ThrowIfNull(readActor);
        ArgumentNullException.ThrowIfNull(readStructure);
        _readActor = readActor;
        _readStructure = readStructure;
    }

    internal SpatialOccupancyReadSet Load(SpatialOccupancyReadPlan plan, int maximumActorBytes)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (maximumActorBytes < 0) throw new ArgumentOutOfRangeException(nameof(maximumActorBytes));
        var actors = new List<ActorPresence>();
        var structures = new List<SpatialStructure>();
        int remaining = maximumActorBytes;
        foreach (var reference in plan.Actors)
        {
            if (remaining == 0) throw new InvalidOperationException("Actor archive read budget is exhausted.");
            int limit = Math.Min(remaining, SpatialActorArchiveV1.MaximumByteLength);
            byte[] bytes = _readActor(reference, limit) ?? throw new InvalidDataException("Required actor detail is missing.");
            if (bytes.Length > limit) throw new InvalidDataException("Actor archive exceeded its remaining byte budget.");
            try { actors.Add(SpatialActorArchiveV1.Decode(bytes, reference)); }
            catch (FormatException error) { throw new InvalidDataException("Actor archive does not match its pinned metadata.", error); }
            remaining -= bytes.Length;
        }
        foreach (var reference in plan.Structures)
            structures.Add(_readStructure(reference) ?? throw new InvalidDataException("Required structure detail is missing."));
        return plan.Resolve(actors, structures);
    }
}
