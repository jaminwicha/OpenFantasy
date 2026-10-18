using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.CanonicalEncodingV1;

namespace OpenFantasy.Core.Construction.Ownership;

/// <summary>Selected holders/project plus globally established reservation membership at the exact source hash.</summary>
internal sealed class StoredConstructionWorkOwners : IConstructionWorkOwners
{
    private readonly TerrainScope _scope;
    private readonly CustodySnapshot _custody;
    private readonly ConstructionSnapshot _construction;
    private readonly CustodianAccessPolicy[] _policies;

    internal StoredConstructionWorkOwners(TerrainScope scope, ulong custodyRevision, ulong constructionRevision,
        EntityId projectId, EntityId definitionId, EntityId reservationId, EntityId[] expectedReservedLots,
        IReadOnlyDictionary<ConstructionOwnerKey, byte[][]> fields)
    {
        _scope = scope;
        var holders = fields.Where(value => value.Key.Kind == ConstructionOwnerKind.Custodian)
            .OrderBy(value => value.Key.SubjectId).ToArray();
        foreach (var holder in holders) RequireCount(holder.Value, 5);
        var lots = holders.SelectMany(value => StoredConstructionOwners.Collection(value.Value[3],
            "owner.counted-lots", CustodySnapshot.MaximumLots)).OrderBy(ConstructionOwnerReconstruction.CountedLotIdentity);
        _custody = CustodyEncodingV1.Decode(Frame("custody.snapshot.v1", scope.WorldId.ToByteArray(), scope.BranchId.ToByteArray(),
            Unsigned64(custodyRevision), Collection("custody.storages.v1", holders.Select(value => value.Value[0])),
            Collection("custody.lots.v1", lots)));
        _policies = holders.Select(value => new CustodianAccessPolicy(value.Key.SubjectId,
            EntityId.FromBytes(value.Value[1]), (CustodyDepositRule)checked((int)StoredConstructionOwners.Number(value.Value[2])))).ToArray();
        if (!_custody.Lots.Where(value => value.ReservedBy == reservationId).Select(value => value.LotId).Order()
            .SequenceEqual(expectedReservedLots))
            throw new FormatException("Selected holders do not contain the source's complete reserved stock.");
        _construction = ConstructionCodecV1.Decode(Frame("construction.snapshot", Unsigned64(1), scope.WorldId.ToByteArray(),
            scope.BranchId.ToByteArray(), Unsigned64(constructionRevision),
            Collection("construction.definitions", [Get(fields, new(ConstructionOwnerKind.ConstructionDefinition, definitionId), 1)[0]]),
            Collection("construction.projects", [Get(fields, new(ConstructionOwnerKind.Project, projectId), 1)[0]])));
    }

    public bool BelongsTo(EntityId worldId, EntityId branchId) => _scope.WorldId == worldId && _scope.BranchId == branchId;
    public ConstructionProject? FindProject(EntityId projectId) => _construction.FindProject(projectId);
    public bool OwnsCustodian(EntityId custodianId, EntityId actorId) => _policies.Any(value =>
        value.CustodianId == custodianId && value.OwnerActorId == actorId);
    public CountedSourceLot? FindLot(EntityId lotId) => _custody.Lots.FirstOrDefault(value => value.LotId == lotId);
    public CustodianStorage GetStorage(EntityId custodianId) => _custody.GetStorage(custodianId);
    public CountedSourceLot[] ReservedStock(EntityId reservationId) =>
        _custody.Lots.Where(value => value.ReservedBy == reservationId).ToArray();

    internal static byte[][] Get(IReadOnlyDictionary<ConstructionOwnerKey, byte[][]> fields, ConstructionOwnerKey key, int count)
    {
        if (!fields.TryGetValue(key, out var value)) throw new FormatException("A required work owner is absent.");
        RequireCount(value, count);
        return value;
    }

    private static void RequireCount(byte[][] fields, int count)
    {
        if (fields.Length != count) throw new FormatException("A selected work owner has the wrong field count.");
    }

    private static byte[] Collection(string tag, IEnumerable<byte[]> values) =>
        Frame(tag, values.Select(value => (ReadOnlyMemory<byte>)value).ToArray());
}
