using OpenFantasy.Core.Content;
using OpenFantasy.Core.Geography;

namespace OpenFantasy.Core.Worlds;

/// <summary>Admits a complete network only into an unplayed construction genesis, retaining all basis state.</summary>
public static class WorldWatershedNetworkAdmission
{
    public static WorldSnapshot CreateGenesis(WorldSnapshot basis, PreparedWatershedNetwork network)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(network);
        if (basis.ProfileVersion != 5 || basis.Revision != 0 || basis.AdvancingSteps != 0 || basis.HasReadingHold)
        {
            throw new ArgumentException("Watershed network genesis requires an unplayed construction world.", nameof(basis));
        }
        ContentHash version = ContentHash.Compute(CanonicalEncodingV1.Frame("world.watershed-network-admission.v1",
            basis.AdmissionVersion.ToByteArray(), network.ComputeContentHash().ToByteArray()));
        return new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, version, basis.Calendar,
            0, 0, basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests,
            basis.Construction, watershedNetwork: network);
    }
}
