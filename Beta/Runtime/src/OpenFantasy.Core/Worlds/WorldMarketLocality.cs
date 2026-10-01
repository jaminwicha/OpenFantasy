using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// The in-person gate of a mobile actor-market. A market is locally available to an actor only when its
/// merchant is another actor with live presence, both can actually reach each other where they stand, and
/// the merchant is alive and able to act. A home-place identity is never consulted: it survives movement and
/// proves no location. Missing and unavailable markets are indistinguishable to the caller.
/// </summary>
public static class WorldMarketLocality
{
    /// <summary>
    /// The market the actor could read or show goods to right now, or null for a missing, distant, absent or
    /// unavailable one. Nothing about a market it returns null for is revealed.
    /// </summary>
    public static MarketBinding? FindAvailable(WorldSnapshot world, EntityId actorId, EntityId marketId)
    {
        ArgumentNullException.ThrowIfNull(world);
        MarketBinding? market = world.Coordination?.Markets?.FindMarket(marketId);
        return market is not null && world.Progression is { } progression && IsReachable(world.Space, actorId, market)
            && IsTrading(world, progression, market) ? market : null;
    }

    internal static bool IsReachable(LocalSpatialSnapshot space, EntityId actorId, MarketBinding market) =>
        market.MerchantActorId != actorId && space.Actors.Any(value => value.ActorId == actorId)
        && space.Actors.Any(value => value.ActorId == market.MerchantActorId)
        && space.QueryInteraction(actorId, market.MerchantActorId) == SpatialInteraction.Reachable
        && space.QueryInteraction(market.MerchantActorId, actorId) == SpatialInteraction.Reachable;

    internal static bool IsTrading(WorldSnapshot world, ProgressionSnapshot progression, MarketBinding market) =>
        progression.GetActor(market.MerchantActorId).CurrentHp > 0 && WorldActorAvailability.CanAct(world, market.MerchantActorId);
}
