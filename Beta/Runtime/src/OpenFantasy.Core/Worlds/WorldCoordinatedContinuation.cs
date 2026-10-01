namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Explicit coordinated interpretation of a complete world. It owns the admitted source baseline,
/// latest bounded joint anchor, optional consent evidence, optional saved proposals and optional paid
/// quotes; stock, care, balances and resources keep their owners.
/// </summary>
public sealed class WorldCoordinatedContinuation
{
    internal WorldCoordinatedContinuation(WorldSourceIssuanceBaseline sources, WorldMaterialCareArchiveAnchor? anchor = null,
        WorldConsentedMaterialHistory? consentedMaterial = null, WorldMaterialProposalBook? proposals = null,
        WorldPaidExchangeBook? paidExchange = null, WorldMarketBook? markets = null)
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
        PaidExchange = paidExchange;
        Markets = markets;
        if (markets is not null && paidExchange is null)
        {
            throw new ArgumentException("Local markets require the paid-exchange interpretation.", nameof(markets));
        }
        if (anchor is not null && (anchor.Markets is not null) != (markets is not null))
        {
            throw new ArgumentException("A coordinated successor retains its exact market interpretation.", nameof(anchor));
        }
        if (anchor is not null && anchor.Production.SupportsConsentedWorld != (consentedMaterial is not null))
        {
            throw new ArgumentException("A coordinated successor retains its exact consent interpretation.", nameof(anchor));
        }
        if (proposals is not null && consentedMaterial is null)
        {
            throw new ArgumentException("Saved proposals require the consented material interpretation.", nameof(proposals));
        }
        if (paidExchange is not null && proposals is null)
        {
            throw new ArgumentException("Paid quotes require the saved-proposal interpretation.", nameof(paidExchange));
        }
        if (anchor is not null && (anchor.Proposals is not null) != (proposals is not null))
        {
            throw new ArgumentException("A coordinated successor retains its exact saved-proposal interpretation.", nameof(anchor));
        }
        if (anchor is not null && (anchor.PaidExchange is not null) != (paidExchange is not null))
        {
            throw new ArgumentException("A coordinated successor retains its exact paid-exchange interpretation.", nameof(anchor));
        }
        // A successor can neither forget, change nor invent an offer or quote of its closing boundary.
        anchor?.Proposals?.ValidateSuccessor(proposals!, anchor.ClosingWorldSequence);
        anchor?.PaidExchange?.ValidateSuccessor(paidExchange!, anchor.ClosingWorldSequence);
        anchor?.Markets?.ValidateSuccessor(markets!, anchor.ClosingWorldSequence);
    }

    public WorldSourceIssuanceBaseline Sources { get; }

    /// <summary>Null until the first coordinated cut; never a chain of earlier anchors.</summary>
    public WorldMaterialCareArchiveAnchor? Anchor { get; }
    /// <summary>Null selects the original coordinated interpretation; an empty history still selects consent.</summary>
    public WorldConsentedMaterialHistory? ConsentedMaterial { get; }
    /// <summary>Null selects an interpretation without saved offers; an empty owner still selects them.</summary>
    public WorldMaterialProposalBook? Proposals { get; }
    /// <summary>Null selects an interpretation without paid quotes; an empty owner still selects profile 32.</summary>
    public WorldPaidExchangeBook? PaidExchange { get; }

    /// <summary>Null selects an interpretation without local markets; a market owner selects profile 33.</summary>
    public WorldMarketBook? Markets { get; }

    // A cut archives closed outcomes and carries every open offer, open quote, market revision, market
    // binding, open market quote record and all price knowledge.
    internal WorldCoordinatedContinuation WithAnchor(WorldMaterialCareArchiveAnchor anchor)
    {
        WorldPaidExchangeBook? paid = PaidExchange?.ContinueAcrossCut();
        return new(Sources, anchor, ConsentedMaterial is null ? null : new WorldConsentedMaterialHistory([]),
            Proposals?.ContinueAcrossCut(), paid, Markets?.ContinueAcrossCut(paid!));
    }

    /// <summary>
    /// The successor of an explicit proposal transition: the lived owner had neither consent evidence nor
    /// offers, so it starts both empty under the transition anchor. No other path adds either capability.
    /// </summary>
    internal WorldCoordinatedContinuation WithTransitionAnchor(WorldMaterialCareArchiveAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        if (ConsentedMaterial is not null || Proposals is not null || PaidExchange is not null || anchor.TransitionHash is null)
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
        return new(Sources, Anchor, history, Proposals, PaidExchange, Markets);
    }

    internal WorldCoordinatedContinuation WithProposals(WorldMaterialProposalBook proposals)
    {
        ArgumentNullException.ThrowIfNull(proposals);
        if (Proposals is null)
        {
            throw new InvalidOperationException("A lived coordinated owner cannot opt into saved proposals.");
        }
        return ReferenceEquals(proposals, Proposals) ? this : new(Sources, Anchor, ConsentedMaterial, proposals, PaidExchange, Markets);
    }

    internal WorldCoordinatedContinuation WithPaidExchange(WorldPaidExchangeBook paidExchange)
    {
        ArgumentNullException.ThrowIfNull(paidExchange);
        if (PaidExchange is null)
        {
            throw new InvalidOperationException("A lived coordinated owner cannot opt into paid quotes.");
        }
        return ReferenceEquals(paidExchange, PaidExchange) ? this : new(Sources, Anchor, ConsentedMaterial, Proposals, paidExchange, Markets);
    }

    internal WorldCoordinatedContinuation WithMarkets(WorldMarketBook markets)
    {
        ArgumentNullException.ThrowIfNull(markets);
        if (Markets is null)
        {
            throw new InvalidOperationException("A lived coordinated owner cannot opt into local markets.");
        }
        return ReferenceEquals(markets, Markets) ? this : new(Sources, Anchor, ConsentedMaterial, Proposals, PaidExchange, markets);
    }
}
