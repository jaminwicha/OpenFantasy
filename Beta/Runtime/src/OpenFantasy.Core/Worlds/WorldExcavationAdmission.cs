using OpenFantasy.Core.Content;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Worlds;

public static class WorldExcavationAdmission
{
    public static WorldSnapshot CreateGenesis(WorldSnapshot basis, ExcavationSnapshot excavation)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(excavation);
        if (basis.ProfileVersion != 5 || basis.Revision != 0 || basis.AdvancingSteps != 0 || basis.HasReadingHold
            || excavation.Sites.Any(s => s.CompletedWork != 0))
        {
            throw new ArgumentException("Excavation admission requires an unplayed construction basis and unworked sites.");
        }
        var version = ContentHash.Compute(CanonicalEncodingV1.Frame("world.excavation-admission.v1",
            basis.AdmissionVersion.ToByteArray(), excavation.ContentHash.ToByteArray()));
        return new(basis.Inventory, basis.Space, basis.Residents, version, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction, excavation: excavation);
    }
}
