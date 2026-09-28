namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Explicit coordinated interpretation of a complete world. It owns only the admitted source baseline
/// and the latest bounded joint anchor; material, care and resource facts keep their actual owners.
/// </summary>
public sealed class WorldCoordinatedContinuation
{
    internal WorldCoordinatedContinuation(WorldSourceIssuanceBaseline sources, WorldMaterialCareArchiveAnchor? anchor = null)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (anchor is not null && anchor.SourceBaselineHash != sources.ContentHash)
        {
            throw new ArgumentException("A coordinated anchor retains the exact admitted source baseline.", nameof(anchor));
        }
        Sources = sources;
        Anchor = anchor;
    }

    public WorldSourceIssuanceBaseline Sources { get; }

    /// <summary>Null until the first coordinated cut; never a chain of earlier anchors.</summary>
    public WorldMaterialCareArchiveAnchor? Anchor { get; }

    internal WorldCoordinatedContinuation WithAnchor(WorldMaterialCareArchiveAnchor anchor) => new(Sources, anchor);
}
