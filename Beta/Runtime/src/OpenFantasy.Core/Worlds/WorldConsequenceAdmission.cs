using OpenFantasy.Core.Content;
using OpenFantasy.Core.Structures;

namespace OpenFantasy.Core.Worlds;

public static class WorldConsequenceAdmission
{
    public static WorldSnapshot CreateGenesis(WorldSnapshot basis, ConsequenceExecutionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(policy);
        if (basis.ProfileVersion != 15 || basis.Revision != 0 || basis.AdvancingSteps != 0 || basis.HasReadingHold)
        {
            throw new ArgumentException("Consequence admission requires an unplayed settlement world.", nameof(basis));
        }
        var version = ContentHash.Compute(CanonicalEncodingV1.Frame("world.consequence-admission.v1",
            basis.AdmissionVersion.ToByteArray(), policy.ContentHash.ToByteArray()));
        return new(basis.Inventory, basis.Space, basis.Residents, version, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests,
            basis.Construction, excavation: basis.Excavation, constructionPhysical: basis.ConstructionPhysical,
            structuralPolicy: basis.StructuralPolicy, structuralSettlementPolicy: basis.StructuralSettlementPolicy, consequencePolicy: policy);
    }
}
