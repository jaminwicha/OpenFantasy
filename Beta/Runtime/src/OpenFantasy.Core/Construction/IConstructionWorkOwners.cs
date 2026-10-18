using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Construction;

/// <summary>Construction qualification reads. Implementations must establish complete reservation membership.</summary>
internal interface IConstructionWorkOwners
{
    bool BelongsTo(EntityId worldId, EntityId branchId);
    ConstructionProject? FindProject(EntityId projectId);
    bool OwnsCustodian(EntityId custodianId, EntityId actorId);
    CountedSourceLot? FindLot(EntityId lotId);
    CustodianStorage GetStorage(EntityId custodianId);
    CountedSourceLot[] ReservedStock(EntityId reservationId);
}

internal sealed class CompleteConstructionWorkOwners(CountedAuthorityState inventory,
    ProductionSnapshot production, ConstructionSnapshot construction) : IConstructionWorkOwners
{
    public bool BelongsTo(EntityId worldId, EntityId branchId) => inventory.WorldId == worldId
        && inventory.BranchId == branchId && production.WorldId == worldId && production.BranchId == branchId
        && construction.WorldId == worldId && construction.BranchId == branchId;
    public ConstructionProject? FindProject(EntityId projectId) => construction.FindProject(projectId);
    public bool OwnsCustodian(EntityId custodianId, EntityId actorId) => inventory.Policies.Any(value =>
        value.CustodianId == custodianId && value.OwnerActorId == actorId);
    public CountedSourceLot? FindLot(EntityId lotId) => inventory.Custody.Lots.FirstOrDefault(value => value.LotId == lotId);
    public CustodianStorage GetStorage(EntityId custodianId) => inventory.Custody.GetStorage(custodianId);
    public CountedSourceLot[] ReservedStock(EntityId reservationId) =>
        inventory.Custody.Lots.Where(value => value.ReservedBy == reservationId).ToArray();
}
