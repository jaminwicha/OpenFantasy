using OpenFantasy.Core.Content;
using OpenFantasy.Core.Geography;

namespace OpenFantasy.Core.Worlds;

/// <summary>Retains exact liquid placement in an unplayed terrain-contained reservoir world.</summary>
public static class WorldLiquidCellAdmission
{
    public static WorldSnapshot CreateGenesis(WorldSnapshot basis, LiquidCellState liquidCells)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(liquidCells);
        if (basis.ProfileVersion != 19 || basis.Revision != 0 || basis.AdvancingSteps != 0 || basis.HasReadingHold
            || liquidCells.Revision != 0 || liquidCells.Boundary != basis.Calendar
            || liquidCells.ReservoirHash != basis.Reservoirs!.ContentHash)
        {
            throw new ArgumentException("Detailed liquid admission requires an unplayed contained world and its exact initial material and clock.");
        }
        ContentHash admission = ContentHash.Compute(CanonicalEncodingV1.Frame("world.liquid-cell-admission.v1",
            basis.AdmissionVersion.ToByteArray(), liquidCells.ContentHash.ToByteArray()));
        return new(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests,
            basis.Construction, reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: liquidCells);
    }
}
