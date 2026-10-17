using OpenFantasy.Core.Content;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Spatial;

public enum SpatialOccupancyKind { Actor = 1, Structure = 2 }

/// <summary>Conservative cell coverage and exact immutable detail pin, not an entity owner or placement grant.</summary>
public sealed record SpatialOccupancyReference
{
    public SpatialOccupancyReference(TerrainScope scope, SpatialOccupancyKind kind, EntityId entityId,
        TerrainRegionBounds bounds, ContentHash contentHash)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(contentHash);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Scope = scope; Kind = kind; EntityId = entityId; Bounds = bounds; ContentHash = contentHash;
    }

    public TerrainScope Scope { get; }
    public SpatialOccupancyKind Kind { get; }
    public EntityId EntityId { get; }
    public TerrainRegionBounds Bounds { get; }
    public ContentHash ContentHash { get; }

    public static SpatialOccupancyReference Capture(ActorPresence actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return new(actor.Scope, SpatialOccupancyKind.Actor, actor.ActorId,
            Cells(BodyVolume.At(actor.Feet, actor.Condition.Definition.ClearanceDimensions)),
            ContentHash.Compute(SpatialActorArchiveV1.Encode(actor)));
    }

    public static SpatialOccupancyReference Capture(TerrainScope scope, SpatialStructure structure)
    {
        ArgumentNullException.ThrowIfNull(structure);
        BodyVolume volume = structure.Boxes.Select(box => box.Volume).Aggregate((left, right) => left.Union(right));
        return new(scope, SpatialOccupancyKind.Structure, structure.StructureId, Cells(volume),
            ContentHash.Compute(VerticalMotionEncoding.Structure(structure)));
    }

    internal static bool Intersects(TerrainRegionBounds left, TerrainRegionBounds right) =>
        left.Minimum.X <= right.Maximum.X && left.Maximum.X >= right.Minimum.X
        && left.Minimum.Y <= right.Maximum.Y && left.Maximum.Y >= right.Minimum.Y
        && left.Minimum.Z <= right.Maximum.Z && left.Maximum.Z >= right.Minimum.Z;

    private static TerrainRegionBounds Cells(BodyVolume volume)
    {
        var minimum = volume.MinimumCell;
        var maximum = volume.MaximumCell;
        return new(minimum, checked((int)(maximum.X - minimum.X + 1)),
            checked((int)(maximum.Y - minimum.Y + 1)), checked((int)(maximum.Z - minimum.Z + 1)));
    }
}
