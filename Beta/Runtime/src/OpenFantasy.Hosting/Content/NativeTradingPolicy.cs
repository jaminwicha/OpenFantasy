using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Content;

/// <summary>
/// Authored native trading windows and Wren's stall rule. Every window counts fixed-world advancing steps, never
/// wall time: a paused or held world publishes no advancing step, so nothing ages while the player reads with
/// time stopped. The lengths were chosen for human reading at the local session's nominal 50 ms cutoff; they
/// are new authored values for this world and leave the earlier fixture rules and session defaults unchanged.
/// </summary>
public static class NativeTradingPolicy
{
    /// <summary>
    /// Advancing steps Wren's answered offer stays acceptable: one minute of continuously advancing world time at
    /// the nominal cutoff. An offer to buy a shown lot also closes no later than that showing.
    /// </summary>
    public const ulong OfferWindowSteps = 1_200;

    /// <summary>
    /// Advancing steps one showing of the player's own lot stays usable: two minutes, long enough to ask and read
    /// a full offer window. It is also the stall rule's longest quote, so the rule caps both.
    /// </summary>
    public const ulong ShowingWindowSteps = 2_400;

    /// <summary>Advancing steps a request may wait for Wren's answer: thirty seconds of advancing world time.</summary>
    public const ulong InquiryLifetimeSteps = 600;

    /// <summary>
    /// Wren's orchard stall: the retained legacy berry price at an eight-unit stocking target, a one-gil spread on
    /// each side of the mid price, at most four units per offer and no market due.
    /// </summary>
    public static MarketPricePolicy WrenBerryStall() =>
        new(MarketCircuitPolicies.Berries, stockingTarget: 8, holdingLimit: 16, softening: 4, minimumUnitPrice: 1,
            maximumUnitPrice: 20, askMarkup: 1, bidMarkdown: 1, unitFee: 0, feePayeeAccountId: null, maximumLot: 4,
            quoteLifetimeSteps: ShowingWindowSteps);

    /// <summary>
    /// The opt-in session advice for a native market world: Wren answers requests at her own stall under her own
    /// control. No host shopper is configured. Supplied again on every opening; it is never saved.
    /// </summary>
    public static LocalTradingConfiguration Configuration(WorldSnapshot world)
    {
        MarketBinding stall = NativeTradingWorld.FindStall(world)
            ?? throw new InvalidDataException("This save is not the native market world.");
        return new LocalTradingConfiguration([new LocalMerchantPreference(stall.MerchantActorId, OfferWindowSteps)],
            [], InquiryLifetimeSteps);
    }
}
