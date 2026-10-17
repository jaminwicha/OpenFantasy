using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Terrain;

/// <summary>Exact immutable region identity for a read dependency, not proof of directory ownership.</summary>
public sealed record TerrainRegionReference
{
    public TerrainRegionReference(TerrainScope scope, TerrainRegionBounds bounds, ulong revision, ContentHash contentHash)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(contentHash);
        Scope = scope;
        Bounds = bounds;
        Revision = revision;
        ContentHash = contentHash;
    }

    public TerrainScope Scope { get; }
    public TerrainRegionBounds Bounds { get; }
    public ulong Revision { get; }
    public ContentHash ContentHash { get; }

    public static TerrainRegionReference Capture(TerrainRegionSnapshot region)
    {
        ArgumentNullException.ThrowIfNull(region);
        return new(region.Scope, region.Bounds, region.Revision, region.ComputeContentHash());
    }
}
