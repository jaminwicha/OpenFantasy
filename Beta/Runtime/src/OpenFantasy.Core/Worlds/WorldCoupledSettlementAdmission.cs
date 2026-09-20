using OpenFantasy.Core.Content;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;

namespace OpenFantasy.Core.Worlds;

public static class WorldCoupledSettlementAdmission
{
    public static WorldSnapshot CreateGenesis(WorldSnapshot basis, CoupledSettlementPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(policy);
        if (basis.ProfileVersion != 16 || basis.Revision != 0 || basis.AdvancingSteps != 0
            || basis.HasReadingHold || basis.PendingExcavation is not null)
        {
            throw new ArgumentException("Coupled settlement admission requires an unplayed, unheld consequence world.", nameof(basis));
        }
        var space = new LocalSpatialSnapshot(basis.Space.Scope, basis.Space.Regions, basis.Space.Actors,
            basis.Space.Structures, basis.Space.GroundSupport, StructureSupportMode.TerrainAndConstruction);
        var version = ContentHash.Compute(CanonicalEncodingV1.Frame("world.coupled-settlement-admission.v1",
            basis.AdmissionVersion.ToByteArray(), basis.StructuralPolicy!.ContentHash.ToByteArray(), policy.ContentHash.ToByteArray()));
        return new(basis.Inventory, space, basis.Residents, version, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests,
            basis.Construction, excavation: basis.Excavation, constructionPhysical: basis.ConstructionPhysical,
            structuralPolicy: basis.StructuralPolicy, structuralSettlementPolicy: basis.StructuralSettlementPolicy,
            consequencePolicy: basis.ConsequencePolicy, coupledSettlementPolicy: policy);
    }
}
