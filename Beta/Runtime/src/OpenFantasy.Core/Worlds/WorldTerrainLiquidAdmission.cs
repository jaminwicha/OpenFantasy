using OpenFantasy.Core.Content;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Worlds;

/// <summary>Admits a fresh excavatable, vented liquid domain without reinterpreting earlier saved worlds.</summary>
public static class WorldTerrainLiquidAdmission
{
    public static WorldSnapshot CreateGenesis(WorldSnapshot basis, LiquidReservoirState reservoirs,
        ReservoirSealPolicy seals, ExcavationSnapshot excavation, LiquidCellState cells)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(reservoirs);
        ArgumentNullException.ThrowIfNull(seals);
        ArgumentNullException.ThrowIfNull(excavation);
        ArgumentNullException.ThrowIfNull(cells);
        if (basis.ProfileVersion != 5 || basis.Revision != 0 || basis.AdvancingSteps != 0 || basis.HasReadingHold
            || excavation.Sites.Any(site => site.CompletedWork != 0) || cells.Topology is null
            || cells.Revision != 0 || cells.Boundary != basis.Calendar || cells.ReservoirHash != reservoirs.ContentHash)
        {
            throw new ArgumentException("Terrain liquid admission requires an unplayed construction basis and exact unworked initial owners.");
        }
        var admission = ContentHash.Compute(CanonicalEncodingV1.Frame("world.terrain-liquid-admission.v1",
            basis.AdmissionVersion.ToByteArray(), reservoirs.ContentHash.ToByteArray(), seals.Version.ToByteArray(),
            excavation.ContentHash.ToByteArray(), cells.ContentHash.ToByteArray()));
        var world = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: reservoirs, excavation: excavation, reservoirSeals: seals, liquidCells: cells);
        _ = WorldSnapshotCodecV21.Encode(world);
        return world;
    }
}
