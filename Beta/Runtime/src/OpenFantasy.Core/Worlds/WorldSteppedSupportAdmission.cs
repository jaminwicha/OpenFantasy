using OpenFantasy.Core.Content;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>Explicitly admits stepped traversal for a fresh construction-supported excavation world.</summary>
public static class WorldSteppedSupportAdmission
{
    public static WorldSnapshot CreateGenesis(WorldSnapshot basis)
    {
        ArgumentNullException.ThrowIfNull(basis);
        if (basis.ProfileVersion != 11 || basis.Revision != 0 || basis.AdvancingSteps != 0 || basis.HasReadingHold)
        {
            throw new ArgumentException("Stepped support admission requires an unplayed construction-supported basis.", nameof(basis));
        }
        var version = ContentHash.Compute(CanonicalEncodingV1.Frame("world.stepped-support-admission.v1",
            basis.AdmissionVersion.ToByteArray()));
        var space = new LocalSpatialSnapshot(basis.Space.Scope, basis.Space.Regions, basis.Space.Actors,
            basis.Construction!.GetStructures(supportsActors: true), GroundSupportMode.SteppedTerrainAndConstruction);
        return new(basis.Inventory, space, basis.Residents, version, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests,
            basis.Construction, excavation: basis.Excavation);
    }
}
