using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>Admits an explicit physical construction owner without changing existing saves or material history.</summary>
public static class WorldConstructionPhysicalAdmission
{
    public static WorldSnapshot CreateGenesis(WorldSnapshot basis)
    {
        ArgumentNullException.ThrowIfNull(basis);
        if (basis.ProfileVersion != 12 || basis.Revision != 0 || basis.AdvancingSteps != 0 || basis.HasReadingHold)
        {
            throw new ArgumentException("Physical construction admission requires an unplayed stepped dry world.", nameof(basis));
        }
        ConstructionPhysicalSnapshot physical = ConstructionPhysicalSnapshot.Create(basis.Construction!);
        var version = ContentHash.Compute(CanonicalEncodingV1.Frame("world.construction-physical-admission.v1",
            basis.AdmissionVersion.ToByteArray(), physical.ContentHash.ToByteArray()));
        return new(basis.Inventory, basis.Space, basis.Residents, version, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests,
            basis.Construction, excavation: basis.Excavation, constructionPhysical: physical);
    }
}
