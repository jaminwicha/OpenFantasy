using OpenFantasy.Core.Content;
using OpenFantasy.Core.Geography;

namespace OpenFantasy.Core.Worlds;

/// <summary>Explicit admission into an unplayed world; existing histories require a future migration contract.</summary>
public static class WorldWatershedAdmission
{
    public static WorldSnapshot CreateGenesis(WorldSnapshot basis, PreparedWatershed watershed)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(watershed);
        if (basis.ProfileVersion != 5 || basis.Revision != 0 || basis.AdvancingSteps != 0 || basis.HasReadingHold)
        {
            throw new ArgumentException("Watershed genesis requires an unplayed construction world.", nameof(basis));
        }
        ContentHash version = ContentHash.Compute(CanonicalEncodingV1.Frame("world.watershed-admission.v1",
            basis.AdmissionVersion.ToByteArray(), watershed.ComputeContentHash().ToByteArray()));
        return new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, version, basis.Calendar,
            0, 0, basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests,
            basis.Construction, watershed);
    }
}
