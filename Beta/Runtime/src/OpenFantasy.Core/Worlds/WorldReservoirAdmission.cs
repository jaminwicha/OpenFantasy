using OpenFantasy.Core.Content;
using OpenFantasy.Core.Geography;

namespace OpenFantasy.Core.Worlds;

/// <summary>Admits physical reservoir ownership into an unplayed construction genesis with exactly matching stock.</summary>
public static class WorldReservoirAdmission
{
    public static WorldSnapshot CreateGenesis(WorldSnapshot basis, LiquidReservoirState reservoirs)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(reservoirs);
        if (basis.ProfileVersion != 5 || basis.Revision != 0 || basis.AdvancingSteps != 0 || basis.HasReadingHold)
        {
            throw new ArgumentException("Reservoir genesis requires an unplayed construction world.", nameof(basis));
        }
        ContentHash version = ContentHash.Compute(CanonicalEncodingV1.Frame("world.reservoir-admission.v1",
            basis.AdmissionVersion.ToByteArray(), reservoirs.ContentHash.ToByteArray()));
        return new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, version, basis.Calendar,
            0, 0, basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests,
            basis.Construction, reservoirs: reservoirs);
    }
}
