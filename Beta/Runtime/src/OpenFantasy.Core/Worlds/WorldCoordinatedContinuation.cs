namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Explicit coordinated interpretation of a complete world. It owns the admitted source baseline,
/// latest bounded joint anchor, optional consent evidence and optional saved proposals; stock, care
/// and resources keep their owners.
/// </summary>
public sealed class WorldCoordinatedContinuation
{
    internal WorldCoordinatedContinuation(WorldSourceIssuanceBaseline sources, WorldMaterialCareArchiveAnchor? anchor = null,
        WorldConsentedMaterialHistory? consentedMaterial = null, WorldMaterialProposalBook? proposals = null)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (anchor is not null && anchor.SourceBaselineHash != sources.ContentHash)
        {
            throw new ArgumentException("A coordinated anchor retains the exact admitted source baseline.", nameof(anchor));
        }
        Sources = sources;
        Anchor = anchor;
        ConsentedMaterial = consentedMaterial;
        Proposals = proposals;
        if (anchor is not null && anchor.Production.SupportsConsentedWorld != (consentedMaterial is not null))
        {
            throw new ArgumentException("A coordinated successor retains its exact consent interpretation.", nameof(anchor));
        }
        if (proposals is not null && consentedMaterial is null)
        {
            throw new ArgumentException("Saved proposals require the consented material interpretation.", nameof(proposals));
        }
        if (anchor is not null && (anchor.Proposals is not null) != (proposals is not null))
        {
            throw new ArgumentException("A coordinated successor retains its exact saved-proposal interpretation.", nameof(anchor));
        }
        // A successor can neither forget, change nor invent an offer of its closing boundary.
        anchor?.Proposals?.ValidateSuccessor(proposals!, anchor.ClosingWorldSequence);
    }

    public WorldSourceIssuanceBaseline Sources { get; }

    /// <summary>Null until the first coordinated cut; never a chain of earlier anchors.</summary>
    public WorldMaterialCareArchiveAnchor? Anchor { get; }
    /// <summary>Null selects the original coordinated interpretation; an empty history still selects consent.</summary>
    public WorldConsentedMaterialHistory? ConsentedMaterial { get; }
    /// <summary>Null selects an interpretation without saved offers; an empty owner still selects them.</summary>
    public WorldMaterialProposalBook? Proposals { get; }

    // A cut archives closed outcomes and carries every open offer into the successor.
    internal WorldCoordinatedContinuation WithAnchor(WorldMaterialCareArchiveAnchor anchor) =>
        new(Sources, anchor, ConsentedMaterial is null ? null : new WorldConsentedMaterialHistory([]),
            Proposals?.ContinueAcrossCut());

    /// <summary>
    /// The successor of an explicit proposal transition: the lived owner had neither consent evidence nor
    /// offers, so it starts both empty under the transition anchor. No other path adds either capability.
    /// </summary>
    internal WorldCoordinatedContinuation WithTransitionAnchor(WorldMaterialCareArchiveAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        if (ConsentedMaterial is not null || Proposals is not null || anchor.TransitionHash is null)
        {
            throw new InvalidOperationException("Only an explicit transition of a lived coordinated owner adopts saved proposals.");
        }
        return new(Sources, anchor, new WorldConsentedMaterialHistory([]), new WorldMaterialProposalBook([], []));
    }

    internal WorldCoordinatedContinuation WithConsentedMaterial(WorldConsentedMaterialHistory history)
    {
        if (ConsentedMaterial is null)
        {
            throw new InvalidOperationException("A lived coordinated owner cannot opt into consent history.");
        }
        return new(Sources, Anchor, history, Proposals);
    }

    internal WorldCoordinatedContinuation WithProposals(WorldMaterialProposalBook proposals)
    {
        ArgumentNullException.ThrowIfNull(proposals);
        if (Proposals is null)
        {
            throw new InvalidOperationException("A lived coordinated owner cannot opt into saved proposals.");
        }
        return ReferenceEquals(proposals, Proposals) ? this : new(Sources, Anchor, ConsentedMaterial, proposals);
    }
}
