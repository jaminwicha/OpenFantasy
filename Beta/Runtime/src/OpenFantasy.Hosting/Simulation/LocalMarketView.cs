using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>One of the player's own usable lots of a stall's commodity: exactly what it may choose to show.</summary>
public sealed record LocalMarketLotView(SpotLotKind Kind, EntityId LotId, CountedItemDefinition Definition, ulong Revision,
    ulong Quantity);

/// <summary>
/// The player's own recorded showing to one stall. <see cref="MatchesOwnLot"/> compares only the player's own
/// current lot with the recorded pins; a changed lot no longer authorizes an offer to buy it.
/// </summary>
public sealed record LocalMarketShowingView(SpotLotKind Kind, EntityId LotId, ulong LotRevision, ulong Units,
    ulong ShownAtStep, ulong ExpiresAtStep, bool IsLive, bool MatchesOwnLot);

/// <summary>
/// One stall as the player standing in this world may perceive it: who trades there, its commodity, its posted
/// per-offer limits and whether the merchant is available in person right now. The player's own showing and
/// own usable lots complete it. The merchant's stock count, funds, rule parameters, other showings and every
/// other actor's quotes or memories are never projected.
/// </summary>
public sealed class LocalMarketStallView
{
    private readonly EntityId _playerAccountId;
    private readonly ulong _advancingStep;

    internal LocalMarketStallView(WorldSnapshot state, MarketBinding market, EntityId playerActorId)
    {
        MarketId = market.MarketId;
        MerchantActorId = market.MerchantActorId;
        Commodity = market.Commodity;
        CommodityName = market.Policy.Source.ItemKey;
        MaximumUnitsPerOffer = market.Policy.MaximumLot;
        MaximumWindowSteps = market.Policy.QuoteLifetimeSteps;
        IsAvailableInPerson = WorldMarketLocality.FindAvailable(state, playerActorId, market.MarketId) is not null;
        _playerAccountId = state.Progression!.Currency.GetActorAccount(playerActorId).AccountId;
        _advancingStep = state.AdvancingSteps;
        OwnLots = Array.AsReadOnly(OwnUsableLots(state, playerActorId, market.Commodity).ToArray());
        if (state.Coordination!.Markets!.FindDisclosure(playerActorId, market.MarketId) is { } shown)
        {
            LocalMarketLotView? current = OwnLots.FirstOrDefault(lot => lot.LotId == shown.LotId && lot.Kind == shown.LotKind);
            OwnShowing = new LocalMarketShowingView(shown.LotKind, shown.LotId, shown.LotRevision, shown.Quantity,
                shown.DisclosedAtStep, shown.ExpiresAtStep, shown.IsLiveAt(state.AdvancingSteps),
                current is not null && current.Revision == shown.LotRevision && current.Quantity >= shown.Quantity);
        }
    }

    public EntityId MarketId { get; }
    public EntityId MerchantActorId { get; }
    public CountedItemDefinition Commodity { get; }

    /// <summary>The cited source item key naming the stall's goods, such as "berries"; never a price or stock fact.</summary>
    public string CommodityName { get; }

    /// <summary>The most units one fixed offer at this stall may name.</summary>
    public ulong MaximumUnitsPerOffer { get; }

    /// <summary>The longest window, in advancing steps, any offer or showing at this stall may have.</summary>
    public ulong MaximumWindowSteps { get; }

    /// <summary>The merchant is present, mutually reachable where both stand, alive and able to act.</summary>
    public bool IsAvailableInPerson { get; }
    public LocalMarketShowingView? OwnShowing { get; }
    public ReadOnlyCollection<LocalMarketLotView> OwnLots { get; }

    /// <summary>
    /// The player's own in-person showing of one of these usable lots, paid into its own account, captured once
    /// from this observation. Preparing submits nothing and consents to no sale; null when the lot is not listed
    /// here, the units exceed it, the window exceeds the stall's limit or the merchant is not available in person.
    /// </summary>
    public DiscloseMarketLotIntent? PrepareShowing(EntityId lotId, ulong units, ulong windowSteps)
    {
        if (!IsAvailableInPerson || units == 0 || windowSteps == 0 || windowSteps > MaximumWindowSteps
            || OwnLots.FirstOrDefault(lot => lot.LotId == lotId) is not { } lot || units > lot.Quantity)
        {
            return null;
        }
        return new DiscloseMarketLotIntent(MarketId, lot.Kind, lot.LotId, lot.Definition, lot.Revision, units,
            _playerAccountId, checked(_advancingStep + 1 + windowSteps));
    }

    /// <summary>Unreserved, unprotected raw and produced lots of the commodity in storage the player owns.</summary>
    private static IEnumerable<LocalMarketLotView> OwnUsableLots(WorldSnapshot state, EntityId playerActorId,
        CountedItemDefinition commodity)
    {
        HashSet<EntityId> owned = state.Inventory.Policies.Where(policy => policy.OwnerActorId == playerActorId)
            .Select(policy => policy.CustodianId).ToHashSet();
        IEnumerable<LocalMarketLotView> raw = state.Inventory.Custody.Lots.Where(lot => owned.Contains(lot.CustodianId)
                && lot.Definition.Equals(commodity) && lot.ReservedBy is null && lot.Protection == LotProtection.None)
            .Select(lot => new LocalMarketLotView(SpotLotKind.CountedSource, lot.LotId, lot.Definition, lot.Revision, lot.Quantity));
        IEnumerable<LocalMarketLotView> produced = state.Production?.Lots.Where(lot => owned.Contains(lot.CustodianId)
                && lot.Definition.Equals(commodity) && lot.ReservedBy is null && lot.Protection == LotProtection.None)
            .Select(lot => new LocalMarketLotView(SpotLotKind.Produced, lot.LotId, lot.Definition, lot.Revision, lot.Quantity)) ?? [];
        return raw.Concat(produced).OrderBy(lot => lot.LotId);
    }
}

/// <summary>
/// The player's market context in a market world: the fixed-world advancing step that dates every offer, showing
/// and request, and each stall the player may perceive. Absent in worlds without markets.
/// </summary>
public sealed class LocalMarketView
{
    private LocalMarketView(ulong advancingStep, IEnumerable<LocalMarketStallView> stalls)
    {
        AdvancingStep = advancingStep;
        Stalls = Array.AsReadOnly(stalls.ToArray());
    }

    /// <summary>Advancing steps the world has published; offers and showings expire against this count.</summary>
    public ulong AdvancingStep { get; }
    public ReadOnlyCollection<LocalMarketStallView> Stalls { get; }

    internal static LocalMarketView? Project(WorldSnapshot state, EntityId playerActorId) =>
        state.Coordination?.Markets is { } book && state.Progression is not null
            ? new LocalMarketView(state.AdvancingSteps,
                book.Markets.Select(market => new LocalMarketStallView(state, market, playerActorId)))
            : null;
}
