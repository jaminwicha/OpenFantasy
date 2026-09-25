using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Exposure;

namespace OpenFantasy.Core.Geography;

/// <summary>Exclusive detailed placement of live reservoir holdings, backed by the sole custody and Production owners.</summary>
public sealed class LiquidCellState
{
    public const int MaximumCells = 4096;
    public const int MaximumPortions = 16384;
    private const int MaximumInputPortions = MaximumPortions * 7;
    private readonly LiquidReservoirState _reservoirs;
    private readonly Lazy<ContentHash> _contentHash;

    internal LiquidCellState(LiquidReservoirState reservoirs, LiquidFlowProfile profile, WorldInstant boundary,
        ulong revision, IEnumerable<LiquidCellPortion> portions)
    {
        ArgumentNullException.ThrowIfNull(reservoirs);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(portions);
        if (reservoirs.Capacity.Pending.Count != 0)
        {
            throw new ArgumentException("Detailed placements cannot also fund outstanding coarse runoff commitments.", nameof(reservoirs));
        }
        _reservoirs = reservoirs;
        ReservoirHash = reservoirs.ContentHash;
        Profile = profile;
        Boundary = boundary;
        Revision = revision;
        VolumePerUnit = reservoirs.Capacity.Domain.Material.Physical.VolumePerUnit;
        if (VolumePerUnit.Value == 0 || VolumePerUnit.Value > ReservoirGeometry.MicrolitresPerCubicMetre
            || ReservoirGeometry.MicrolitresPerCubicMetre % VolumePerUnit.Value != 0)
        {
            throw new ArgumentException("The liquid unit volume must divide one cubic metre exactly.", nameof(reservoirs));
        }
        UnitsPerCell = ReservoirGeometry.MicrolitresPerCubicMetre / VolumePerUnit.Value;
        DomainCells = BuildDomain(reservoirs);
        Portions = Normalize(portions);
        var domain = DomainCells.ToHashSet();
        var amounts = new SortedDictionary<LiquidCellKey, ulong>();
        foreach (LiquidCellPortion portion in Portions)
        {
            if (!domain.Contains(portion.Cell))
            {
                throw new ArgumentException("A liquid portion lies outside its owned finite vessel.", nameof(portions));
            }
            ulong quantity = checked(amounts.GetValueOrDefault(portion.Cell) + portion.Quantity);
            if (quantity > UnitsPerCell)
            {
                throw new ArgumentException("Liquid portions exceed their cell's real volume.", nameof(portions));
            }
            amounts[portion.Cell] = quantity;
        }
        Quantities = new ReadOnlyDictionary<LiquidCellKey, ulong>(amounts);
        ValidateCoverage();
        _contentHash = new(() => ContentHash.Compute(LiquidCellStateCodecV1.Encode(this)));
    }

    public ContentHash ReservoirHash { get; }
    public LiquidFlowProfile Profile { get; }
    public WorldInstant Boundary { get; }
    public ulong Revision { get; }
    public ReadOnlyCollection<LiquidCellPortion> Portions { get; }
    public ReadOnlyCollection<LiquidCellKey> DomainCells { get; }
    public IReadOnlyDictionary<LiquidCellKey, ulong> Quantities { get; }
    public ulong UnitsPerCell { get; }
    public VolumeMicrolitres VolumePerUnit { get; }
    public ContentHash ContentHash => _contentHash.Value;

    public static LiquidCellState Create(LiquidReservoirState reservoirs, LiquidFlowProfile profile,
        WorldInstant boundary, IEnumerable<LiquidCellPortion> portions) => new(reservoirs, profile, boundary, 0, portions);

    public LiquidCellState Rebind(LiquidReservoirState reservoirs)
    {
        ArgumentNullException.ThrowIfNull(reservoirs);
        if (reservoirs.Capacity.Domain.ContentHash != _reservoirs.Capacity.Domain.ContentHash
            || !reservoirs.Geometries.Select(value => value.ContentHash).SequenceEqual(
                _reservoirs.Geometries.Select(value => value.ContentHash)))
        {
            throw new ArgumentException("Liquid placement cannot silently change its domain or physical envelope.", nameof(reservoirs));
        }
        return reservoirs.ContentHash == ReservoirHash ? this : new(reservoirs, Profile, Boundary, Revision, Portions);
    }

    internal LiquidCellState WithPlacement(WorldInstant boundary, ulong revision, IEnumerable<LiquidCellPortion> portions) =>
        new(_reservoirs, Profile, boundary, revision, portions);

    internal LiquidCellState AdvanceBoundary(WorldInstant nextBoundary)
    {
        if (nextBoundary != Boundary.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep))
        {
            throw new ArgumentException("Liquid time advances by one selected exploration interval.", nameof(nextBoundary));
        }
        return WithPlacement(nextBoundary, checked(Revision + 1), Portions);
    }

    private static ReadOnlyCollection<LiquidCellKey> BuildDomain(LiquidReservoirState reservoirs)
    {
        long count = reservoirs.Geometries.Sum(geometry => geometry.Columns.Sum(column =>
            checked((long)((Int128)geometry.SpillZ - column.BottomZ))));
        if (count > MaximumCells)
        {
            throw new ArgumentException("The detailed liquid domain exceeds its finite cell bound.", nameof(reservoirs));
        }
        var cells = new List<LiquidCellKey>((int)count);
        foreach (ReservoirGeometry geometry in reservoirs.Geometries)
        {
            foreach (ReservoirColumn column in geometry.Columns)
            {
                for (long z = column.BottomZ; z < geometry.SpillZ; z++)
                {
                    cells.Add(new(geometry.ReservoirId, new(column.X, column.Y, z)));
                }
            }
        }
        return cells.Order().ToList().AsReadOnly();
    }

    private static ReadOnlyCollection<LiquidCellPortion> Normalize(IEnumerable<LiquidCellPortion> portions)
    {
        LiquidCellPortion[] input = portions.Take(MaximumInputPortions + 1).ToArray();
        if (input.Length > MaximumInputPortions || input.Any(value => value is null))
        {
            throw new ArgumentException("Liquid placement inputs must be finite and non-null.", nameof(portions));
        }
        var ordered = input.OrderBy(value => value.Cell).ThenBy(value => value.Origin)
            .ThenBy(value => value.OriginId).ThenBy(value => value.Start).ThenBy(value => value.End);
        var result = new List<LiquidCellPortion>();
        foreach (LiquidCellPortion portion in ordered)
        {
            if (result.Count > 0 && result[^1] is { } previous && previous.Cell == portion.Cell
                && previous.Origin == portion.Origin && previous.OriginId == portion.OriginId)
            {
                if (portion.Start < previous.End)
                {
                    throw new ArgumentException("One cell cannot claim overlapping material intervals.", nameof(portions));
                }
                if (portion.Start == previous.End)
                {
                    result[^1] = new(previous.Cell, previous.Origin, previous.OriginId, previous.Start, portion.End);
                    continue;
                }
            }
            result.Add(portion);
        }
        if (result.Count > MaximumPortions)
        {
            throw new ArgumentException("Detailed liquid provenance exceeds its finite portion bound.", nameof(portions));
        }
        return result.AsReadOnly();
    }

    private void ValidateCoverage()
    {
        var expected = new Dictionary<StockKey, List<UnitRange>>();
        foreach (ReservoirGeometry geometry in _reservoirs.Geometries)
        {
            foreach (var lot in _reservoirs.Capacity.Inventory.Custody.Lots.Where(value => value.CustodianId == geometry.CustodianId))
            {
                Add(expected, new(geometry.ReservoirId, LiquidStockOrigin.RawSource, lot.Source.SourceId), new(lot.Source.Start, lot.Source.End));
            }
            foreach (var lot in _reservoirs.Capacity.Production?.Lots.Where(value => value.CustodianId == geometry.CustodianId) ?? [])
            {
                Add(expected, new(geometry.ReservoirId, LiquidStockOrigin.ProducedLot, lot.LotId), new(0, lot.Quantity));
            }
        }
        var actual = new Dictionary<StockKey, List<UnitRange>>();
        foreach (LiquidCellPortion portion in Portions)
        {
            Add(actual, new(portion.Cell.ReservoirId, portion.Origin, portion.OriginId), new(portion.Start, portion.End));
        }
        if (expected.Count != actual.Count || expected.Any(pair => !actual.TryGetValue(pair.Key, out var ranges)
            || !Union(pair.Value).SequenceEqual(Union(ranges))))
        {
            throw new ArgumentException("Detailed placements must exhaust exactly their custodian's live material intervals.");
        }
    }

    private static void Add(Dictionary<StockKey, List<UnitRange>> groups, StockKey key, UnitRange range)
    {
        if (!groups.TryGetValue(key, out var ranges))
        {
            ranges = [];
            groups.Add(key, ranges);
        }
        ranges.Add(range);
    }

    private static IReadOnlyList<UnitRange> Union(IEnumerable<UnitRange> ranges)
    {
        var result = new List<UnitRange>();
        foreach (UnitRange range in ranges.OrderBy(value => value.Start).ThenBy(value => value.End))
        {
            if (result.Count > 0)
            {
                UnitRange previous = result[^1];
                if (range.Start < previous.End)
                {
                    throw new ArgumentException("Liquid placement duplicates live material across cells.");
                }
                if (range.Start == previous.End)
                {
                    result[^1] = new(previous.Start, range.End);
                    continue;
                }
            }
            result.Add(range);
        }
        return result;
    }

    private readonly record struct StockKey(EntityId ReservoirId, LiquidStockOrigin Origin, EntityId OriginId);
    private readonly record struct UnitRange(ulong Start, ulong End);
}
