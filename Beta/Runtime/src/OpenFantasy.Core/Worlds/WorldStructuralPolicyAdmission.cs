using OpenFantasy.Core.Content;
using OpenFantasy.Core.Structures;

namespace OpenFantasy.Core.Worlds;

/// <summary>Admits immutable load rules only into an unplayed physical world; existing saves are not migrated.</summary>
public static class WorldStructuralPolicyAdmission
{
    public static WorldSnapshot CreateGenesis(WorldSnapshot basis, ConstructionLoadPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(policy);
        if (basis.ProfileVersion != 13 || basis.Revision != 0 || basis.AdvancingSteps != 0 || basis.HasReadingHold)
        {
            throw new ArgumentException("Structural policy admission requires an unplayed physical construction world.", nameof(basis));
        }
        var version = ContentHash.Compute(CanonicalEncodingV1.Frame("world.structural-policy-admission.v1",
            basis.AdmissionVersion.ToByteArray(), policy.ContentHash.ToByteArray()));
        return new(basis.Inventory, basis.Space, basis.Residents, version, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests,
            basis.Construction, excavation: basis.Excavation, constructionPhysical: basis.ConstructionPhysical,
            structuralPolicy: policy);
    }
}
