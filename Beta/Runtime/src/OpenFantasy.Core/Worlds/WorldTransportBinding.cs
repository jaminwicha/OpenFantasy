using OpenFantasy.Core.Geography;

namespace OpenFantasy.Core.Worlds;

internal static class WorldTransportBinding
{
    internal static bool TryReconcile(ReservoirTransportState? current, LiquidReservoirState? reservoirs,
        out ReservoirTransportState? candidate)
    {
        candidate = current;
        if (current is null)
        {
            return true;
        }
        if (reservoirs is null)
        {
            return false;
        }
        try
        {
            candidate = current.PlanRebind(reservoirs).ApplyTo(current);
            return true;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            return false;
        }
    }
}
