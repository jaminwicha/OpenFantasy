using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Custody;

/// <summary>
/// Immutable complete counted contents for the explicitly included custodians in one branch.
/// The adapter must establish completeness and authority; construction verifies local invariants.
/// </summary>
public sealed class CustodySnapshot
{
    public const int MaximumCustodians = 256;
    public const int MaximumLots = 4096;
    private readonly Dictionary<EntityId, CustodianStorage> _storages;
    private readonly Dictionary<EntityId, CountedSourceLot> _lots;

    public CustodySnapshot(EntityId worldId, EntityId branchId, ulong revision,
        IEnumerable<CustodianStorage> storages, IEnumerable<CountedSourceLot> lots)
    {
        ArgumentNullException.ThrowIfNull(storages);
        ArgumentNullException.ThrowIfNull(lots);
        _storages = [];
        foreach (CustodianStorage storage in storages)
        {
            ArgumentNullException.ThrowIfNull(storage);
            if (_storages.Count == MaximumCustodians || !_storages.TryAdd(storage.CustodianId, storage))
            {
                throw new ArgumentException("Excessive or duplicate custodian storage.", nameof(storages));
            }
        }
        _lots = [];
        var quantities = new Dictionary<EntityId, UInt128>();
        foreach (CountedSourceLot lot in lots)
        {
            ArgumentNullException.ThrowIfNull(lot);
            if (_lots.Count == MaximumLots || _storages.ContainsKey(lot.LotId)
                || !_lots.TryAdd(lot.LotId, lot))
            {
                throw new ArgumentException("Excessive lots or conflicting entity identities.", nameof(lots));
            }
            if (!_storages.TryGetValue(lot.CustodianId, out CustodianStorage? storage))
            {
                throw new ArgumentException("A lot names an absent custodian.", nameof(lots));
            }
            UInt128 total = quantities.GetValueOrDefault(lot.CustodianId) + lot.Quantity;
            if (total > storage.UnitCapacity)
            {
                throw new ArgumentException("Counted contents exceed custodian capacity.", nameof(lots));
            }
            quantities[lot.CustodianId] = total;
        }
        ValidateSourceIntervals();
        WorldId = worldId;
        BranchId = branchId;
        Revision = revision;
        Storages = _storages.Values.OrderBy(value => value.CustodianId).ToList().AsReadOnly();
        Lots = _lots.Values.OrderBy(value => value.LotId).ToList().AsReadOnly();
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public ulong Revision { get; }
    public ReadOnlyCollection<CustodianStorage> Storages { get; }
    public ReadOnlyCollection<CountedSourceLot> Lots { get; }
    public ContentHash ContentHash => ContentHash.Compute(CustodyEncodingV1.Encode(this));

    public CountedSourceLot GetLot(EntityId lotId) => _lots.TryGetValue(lotId, out CountedSourceLot? lot)
        ? lot : throw new CustodyConflictException(CustodyConflict.MissingLot);

    public CustodianStorage GetStorage(EntityId custodianId) =>
        _storages.TryGetValue(custodianId, out CustodianStorage? storage)
            ? storage : throw new CustodyConflictException(CustodyConflict.MissingStorage);

    public bool ContainsEntity(EntityId identity) => identity == WorldId || identity == BranchId
        || _lots.ContainsKey(identity) || _storages.ContainsKey(identity)
        || Lots.Any(lot => lot.Source.SourceId == identity || lot.Definition.DefinitionId == identity
            || lot.ReservedBy == identity);

    public ulong GetStoredQuantity(EntityId custodianId)
    {
        GetStorage(custodianId);
        ulong total = 0;
        foreach (CountedSourceLot lot in Lots.Where(lot => lot.CustodianId == custodianId))
        {
            total = checked(total + lot.Quantity);
        }
        return total;
    }

    private void ValidateSourceIntervals()
    {
        foreach (IGrouping<EntityId, CountedSourceLot> source in _lots.Values.GroupBy(lot => lot.Source.SourceId))
        {
            CountedSourceLot? previous = null;
            foreach (CountedSourceLot lot in source.OrderBy(lot => lot.Source.Start))
            {
                if (previous is not null && (previous.Source.End > lot.Source.Start
                    || previous.Definition != lot.Definition))
                {
                    throw new ArgumentException("A source has overlapping claims or inconsistent counted-unit definitions.");
                }
                previous = lot;
            }
        }
    }
}
