using OpenFantasy.Core.Content;
using OpenFantasy.Core.Structures;

namespace OpenFantasy.Core.Worlds;

/// <summary>Fresh-genesis selection of event-ordered intact settlement; no played-save conversion.</summary>
public static class WorldEventSettlementAdmission
{
    public static WorldSnapshot CreateGenesis(WorldSnapshot basis)
    {
        ArgumentNullException.ThrowIfNull(basis);
        if (basis.ProfileVersion != 17 || basis.Revision != 0 || basis.AdvancingSteps != 0
            || basis.HasReadingHold || basis.PendingExcavation is not null)
        {
            throw new ArgumentException("Event settlement admission requires an unplayed, unheld and job-free coupled world.", nameof(basis));
        }
        var version = ContentHash.Compute(CanonicalEncodingV1.Frame("world.event-settlement-admission.v1",
            basis.AdmissionVersion.ToByteArray(), basis.StructuralPolicy!.ContentHash.ToByteArray(),
            basis.StructuralSettlementPolicy!.ContentHash.ToByteArray(), basis.ConsequencePolicy!.ContentHash.ToByteArray(),
            basis.CoupledSettlementPolicy!.ContentHash.ToByteArray(),
            CanonicalEncodingV1.Unsigned64((ulong)ConstructionDescentMode.EventOrdered)));
        return new(basis.Inventory, basis.Space, basis.Residents, version, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests,
            basis.Construction, excavation: basis.Excavation, constructionPhysical: basis.ConstructionPhysical,
            structuralPolicy: basis.StructuralPolicy, structuralSettlementPolicy: basis.StructuralSettlementPolicy,
            consequencePolicy: basis.ConsequencePolicy, coupledSettlementPolicy: basis.CoupledSettlementPolicy,
            descentMode: ConstructionDescentMode.EventOrdered);
    }
}
