using OpenFantasy.Core;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client.Cargo;

/// <summary>
/// Pointer selection of a stall on the cargo field. The pointer maps back through the frame's own projection to one
/// observed column; a merchant drawn within one cell of it names its stall only when that merchant keeps exactly one
/// stall in view. Two stalls of one merchant are never told apart by position: that choice stays in the selector.
/// Picking selects; it asks, shows and spends nothing.
/// </summary>
internal static class CargoStallPick
{
    /// <summary>Largest distance from the picked column's centre to a merchant's feet, in subunits: one cell.</summary>
    internal const long PickRadiusSubunits = SubunitPosition.SubunitsPerCell;

    internal static EntityId? Pick(CargoViewProjection projection, LocalWorldView view, CargoCanvasPoint point)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(view);
        if (projection.Unproject(point) is not { } picked || view.Markets is not { } markets) return null;
        LocalActorView? merchant = view.Actors
            .Where(actor => actor.ActorId != view.PlayerActorId && projection.Admits(actor.Feet)
                && markets.Stalls.Any(stall => stall.MerchantActorId == actor.ActorId)
                && Math.Abs(actor.Feet.X - picked.X) <= PickRadiusSubunits && Math.Abs(actor.Feet.Y - picked.Y) <= PickRadiusSubunits)
            .OrderBy(actor => Math.Abs(actor.Feet.X - picked.X) + Math.Abs(actor.Feet.Y - picked.Y)).ThenBy(actor => actor.ActorId)
            .FirstOrDefault();
        if (merchant is null) return null;
        LocalMarketStallView[] stalls = markets.Stalls.Where(stall => stall.MerchantActorId == merchant.ActorId).ToArray();
        return stalls.Length == 1 ? stalls[0].MarketId : null;
    }
}
