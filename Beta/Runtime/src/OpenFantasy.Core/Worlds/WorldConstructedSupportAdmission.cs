using OpenFantasy.Core.Content;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

public static class WorldConstructedSupportAdmission
{
    public static WorldSnapshot CreateGenesis(WorldSnapshot basis)
    {
        ArgumentNullException.ThrowIfNull(basis);
        if (basis.ProfileVersion != 10 || basis.Revision != 0 || basis.AdvancingSteps != 0 || basis.HasReadingHold)
        {
            throw new ArgumentException("Constructed support admission requires an unplayed dry extraction basis.", nameof(basis));
        }
        var version = ContentHash.Compute(CanonicalEncodingV1.Frame("world.constructed-support-admission.v1",
            basis.AdmissionVersion.ToByteArray()));
        var space = new LocalSpatialSnapshot(basis.Space.Scope, basis.Space.Regions, basis.Space.Actors,
            basis.Construction!.GetStructures(supportsActors: true), GroundSupportMode.TerrainAndConstruction);
        return new(basis.Inventory, space, basis.Residents, version, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests,
            basis.Construction, excavation: basis.Excavation);
    }
}
