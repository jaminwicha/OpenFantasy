namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Explicit coordinated interpretation of a complete world. It owns the admitted source baseline,
/// latest bounded joint anchor and optional consent evidence; stock, care and resources keep their owners.
/// </summary>
public sealed class WorldCoordinatedContinuation
{
    internal WorldCoordinatedContinuation(WorldSourceIssuanceBaseline sources, WorldMaterialCareArchiveAnchor? anchor = null,
        WorldConsentedMaterialHistory? consentedMaterial = null)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (anchor is not null && anchor.SourceBaselineHash != sources.ContentHash)
        {
            throw new ArgumentException("A coordinated anchor retains the exact admitted source baseline.", nameof(anchor));
        }
        Sources = sources;
        Anchor = anchor;
        ConsentedMaterial = consentedMaterial;
        if (anchor is not null && anchor.Production.SupportsConsentedWorld != (consentedMaterial is not null))
        {
            throw new ArgumentException("A coordinated successor retains its exact consent interpretation.", nameof(anchor));
        }
    }

    public WorldSourceIssuanceBaseline Sources { get; }

    /// <summary>Null until the first coordinated cut; never a chain of earlier anchors.</summary>
    public WorldMaterialCareArchiveAnchor? Anchor { get; }
    /// <summary>Null selects the original coordinated interpretation; an empty history still selects consent.</summary>
    public WorldConsentedMaterialHistory? ConsentedMaterial { get; }

    internal WorldCoordinatedContinuation WithAnchor(WorldMaterialCareArchiveAnchor anchor) =>
        new(Sources, anchor, ConsentedMaterial is null ? null : new WorldConsentedMaterialHistory([]));

    internal WorldCoordinatedContinuation WithConsentedMaterial(WorldConsentedMaterialHistory history)
    {
        if (ConsentedMaterial is null)
        {
            throw new InvalidOperationException("A lived coordinated owner cannot opt into consent history.");
        }
        return new(Sources, Anchor, history);
    }
}
