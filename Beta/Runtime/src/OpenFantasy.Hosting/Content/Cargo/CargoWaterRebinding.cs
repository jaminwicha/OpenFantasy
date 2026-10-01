using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content.Cargo;

/// <summary>
/// The water owners pin the exact counted admission and terrain, so custody-only reconciliation cannot carry added
/// residents or sources. The retained dry capacity domain is therefore prepared again, unchanged, against the
/// extended inventory: the same network, terrain mapping, owner, material, terminals, closed budget identity and
/// reservoir geometry, with a zero-length dry forcing interval that is cancelled exactly as the farm's own
/// admission does. The placed cells (quantities, portions, profile, boundary and seals) are kept and rebound to
/// those reservoirs and to the extended regions.
/// <para>
/// The closed budget identity is read from the retained capacity state, not re-derived from a profile name. The
/// re-preparation is lossless only for the fresh dry genesis, so that assumption is checked, not trusted: the
/// same preparation against the retained pins must reproduce the retained capacity and reservoir hashes exactly.
/// Any other retained water state (pending runoff, received totals, a second closed budget or lived reservoir
/// state) is refused rather than reset. Every fresh genesis extension that adds residents or sources uses this.
/// </para>
/// </summary>
internal static class CargoWaterRebinding
{
    internal static (LiquidReservoirState Reservoirs, LiquidCellState Cells) Rebind(WorldSnapshot basis,
        CountedAuthorityState inventory, LocalSpatialSnapshot space)
    {
        LiquidReservoirState retained = basis.Reservoirs!;
        WatershedRunoffCapacityState retainedCapacity = retained.Capacity;
        if (retainedCapacity.Pending.Count != 0 || retainedCapacity.Closed.Count != 1)
        {
            throw new InvalidDataException("The cargo extension supports only the fresh dry water genesis with one closed budget.");
        }
        EntityId budget = retainedCapacity.Closed[0];
        if (PrepareDry(basis, retainedCapacity.Inventory, retainedCapacity.Production, budget).ContentHash != retainedCapacity.ContentHash
            || LiquidReservoirState.Create(retainedCapacity, retained.Geometries).ContentHash != retained.ContentHash)
        {
            throw new InvalidDataException("The retained water owners carry state beyond the fresh dry genesis; it is not reset.");
        }
        WatershedRunoffCapacityState capacity = PrepareDry(basis, inventory, basis.Production, budget);
        LiquidReservoirState reservoirs = LiquidReservoirState.Create(capacity, retained.Geometries);
        LiquidCellState cells = basis.LiquidCells!;
        cells = cells.Topology is not null
            ? cells.RebindTerrain(reservoirs, TerrainLiquidTopology.Create(reservoirs, space.Regions, basis.ReservoirSeals!))
            : cells.Rebind(reservoirs);
        return (reservoirs, cells);
    }

    /// <summary>The retained domain prepared with a zero-length dry interval and cancelled, as the farm admission does.</summary>
    private static WatershedRunoffCapacityState PrepareDry(WorldSnapshot basis, CountedAuthorityState inventory,
        ProductionSnapshot? production, EntityId budget)
    {
        WatershedRunoffDomain domain = basis.Reservoirs!.Capacity.Domain;
        var request = new WatershedRunoffRequest(domain.Network, domain.Mapping, inventory, production, budget,
            domain.OwnerActorId, basis.Calendar, basis.Calendar.AddMilliseconds(1), domain.Material,
            domain.Terminals.Select(terminal => WatershedRunoffInput.Dry(terminal.BasinId)), domain.Terminals);
        WatershedRunoffCapacityState capacity = WatershedRunoffCapacityState.Create(new WatershedRunoffPlanner().Prepare(request));
        return capacity.PlanCancel(budget).ApplyTo(capacity);
    }
}
