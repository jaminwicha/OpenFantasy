using OpenFantasy.Core.Content;
using OpenFantasy.Core.Structures;

namespace OpenFantasy.Core.Worlds;

public static class WorldStructuralSettlementAdmission
{
    public static WorldSnapshot CreateGenesis(WorldSnapshot basis, StructuralSettlementPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(policy);
        if (basis.ProfileVersion != 14 || basis.Revision != 0 || basis.AdvancingSteps != 0 || basis.HasReadingHold)
        {
            throw new ArgumentException("Settlement admission requires an unplayed structural world.", nameof(basis));
        }
        var version = ContentHash.Compute(CanonicalEncodingV1.Frame("world.structural-settlement-admission.v1",
            basis.AdmissionVersion.ToByteArray(), policy.ContentHash.ToByteArray()));
        return new(basis.Inventory, basis.Space, basis.Residents, version, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests,
            basis.Construction, excavation: basis.Excavation, constructionPhysical: basis.ConstructionPhysical,
            structuralPolicy: basis.StructuralPolicy, structuralSettlementPolicy: policy);
    }
}
