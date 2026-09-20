using OpenFantasy.Core.Content;
using OpenFantasy.Core.Geography;

namespace OpenFantasy.Core.Worlds;

/// <summary>Installs complete immutable sealing rules in an unplayed fixed-reservoir world.</summary>
public static class WorldReservoirContainmentAdmission
{
    public static WorldSnapshot CreateGenesis(WorldSnapshot basis, ReservoirSealPolicy reservoirSeals)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(reservoirSeals);
        if (basis.ProfileVersion != 8 || basis.Revision != 0 || basis.AdvancingSteps != 0 || basis.HasReadingHold)
        {
            throw new ArgumentException("Reservoir containment admission requires an unplayed fixed-reservoir world.", nameof(basis));
        }
        ContentHash admission = ContentHash.Compute(CanonicalEncodingV1.Frame("world.reservoir-containment-admission.v1",
            basis.AdmissionVersion.ToByteArray(), reservoirSeals.Version.ToByteArray()));
        return new(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests,
            basis.Construction, reservoirs: basis.Reservoirs, reservoirSeals: reservoirSeals);
    }
}
