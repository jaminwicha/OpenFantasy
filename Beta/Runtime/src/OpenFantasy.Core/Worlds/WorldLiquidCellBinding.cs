using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>Joins exact material placement to its retained reservoir owner and one world time boundary.</summary>
internal static class WorldLiquidCellBinding
{
    internal static void Validate(WorldSnapshot world)
    {
        if (world.LiquidCells is not { } cells)
        {
            return;
        }
        if (world.Reservoirs is not { } reservoirs || world.ReservoirSeals is null
            || cells.ReservoirHash != reservoirs.ContentHash || cells.Boundary != (world.Pacing?.ProcessBoundary ?? world.Calendar)
            || cells.Revision != world.ProcessIntervals)
        {
            throw new ArgumentException("Detailed liquid requires the exact contained material owner, calendar and advancing-step boundary.");
        }
    }

    internal static bool TryReconcile(LiquidCellState? current, LiquidReservoirState? reservoirs,
        out LiquidCellState? candidate, LocalSpatialSnapshot? space = null, ReservoirSealPolicy? seals = null)
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
            if (current.Topology is not null)
            {
                if (space is null || seals is null) return false;
                var topology = TerrainLiquidTopology.Create(reservoirs, space.Regions, seals);
                candidate = current.RebindTerrain(reservoirs, topology);
                return true;
            }
            candidate = current.ReservoirHash == reservoirs.ContentHash ? current : current.Rebind(reservoirs);
            return true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            return false;
        }
    }
}
