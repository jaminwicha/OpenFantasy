using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>One square metre of horizontal support, with an inclusive bottom and the reservoir's exclusive spill plane.</summary>
public readonly record struct ReservoirColumn(long X, long Y, long BottomZ);

/// <summary>A finite open basin with connected rising floor contours. This is geometry, not terrain admission or a pressure solver.</summary>
public sealed class ReservoirGeometry
{
    public const int MaximumColumns = 4096;
    public const int MaximumDepth = 1024;
    public const ulong MicrolitresPerCubicMetre = 1_000_000_000;
    private readonly Lazy<ContentHash> _contentHash;

    public ReservoirGeometry(TerrainScope scope, EntityId reservoirId, EntityId custodianId, long spillZ,
        IEnumerable<ReservoirColumn> columns)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(columns);
        var copy = columns.Take(MaximumColumns + 1).OrderBy(c => c.Y).ThenBy(c => c.X).ToArray();
        if (copy.Length is 0 or > MaximumColumns || copy.Select(c => (c.X, c.Y)).Distinct().Count() != copy.Length
            || copy.Any(c => (Int128)spillZ - c.BottomZ < 1 || (Int128)spillZ - c.BottomZ > MaximumDepth))
        {
            throw new ArgumentException("Reservoir columns require unique bounded support below one spill plane.");
        }
        if (reservoirId == custodianId || reservoirId == scope.WorldId || reservoirId == scope.BranchId || reservoirId == scope.RealmId)
        {
            throw new ArgumentException("A reservoir identity cannot alias its custody or scope.");
        }
        ValidateContours(copy);
        Scope = scope;
        ReservoirId = reservoirId;
        CustodianId = custodianId;
        SpillZ = spillZ;
        Columns = Array.AsReadOnly(copy);
        Capacity = new(copy.Aggregate(0UL, (sum, c) => checked(sum + (ulong)((Int128)spillZ - c.BottomZ) * MicrolitresPerCubicMetre)));
        _contentHash = new(() => ContentHash.Compute(LiquidReservoirCodecV1.EncodeGeometry(this)));
    }

    public TerrainScope Scope { get; }
    public EntityId ReservoirId { get; }
    public EntityId CustodianId { get; }
    public long SpillZ { get; }
    public ReadOnlyCollection<ReservoirColumn> Columns { get; }
    public VolumeMicrolitres Capacity { get; }
    public ContentHash ContentHash => _contentHash.Value;

    public ReservoirFill Fill(VolumeMicrolitres volume)
    {
        if (volume.Value > Capacity.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(volume), "Liquid cannot exceed the finite spill capacity.");
        }
        var levels = Columns.GroupBy(c => c.BottomZ).OrderBy(g => g.Key).ToArray();
        ulong remaining = volume.Value;
        int active = 0;
        long floor = levels[0].Key;
        for (int index = 0; index < levels.Length; index++)
        {
            floor = levels[index].Key;
            active += levels[index].Count();
            long next = index + 1 == levels.Length ? SpillZ : levels[index + 1].Key;
            ulong band = checked((ulong)((Int128)next - floor) * (ulong)active * MicrolitresPerCubicMetre);
            if (remaining < band)
            {
                break;
            }
            remaining -= band;
            if (index + 1 == levels.Length)
            {
                floor = SpillZ;
            }
        }
        ulong height = remaining / (ulong)active;
        ulong residual = remaining % (ulong)active;
        long surface = checked(floor + (long)(height / MicrolitresPerCubicMetre));
        uint fraction = (uint)(height % MicrolitresPerCubicMetre);
        var fills = new List<ReservoirColumnFill>();
        ulong undistributed = residual;
        foreach (var column in Columns)
        {
            ulong amount = 0;
            if (column.BottomZ <= floor)
            {
                amount = checked((ulong)((Int128)floor - column.BottomZ) * MicrolitresPerCubicMetre + height);
                if (undistributed > 0)
                {
                    amount++;
                    undistributed--;
                }
            }
            fills.Add(new(column, new(amount)));
        }
        return new(volume, surface, fraction, residual, fills);
    }

    private static void ValidateContours(ReservoirColumn[] columns)
    {
        var byAddress = columns.ToDictionary(c => (c.X, c.Y));
        var start = columns.MinBy(c => c.BottomZ);
        var visited = new HashSet<(long X, long Y)> { (start.X, start.Y) };
        var queue = new Queue<ReservoirColumn>();
        queue.Enqueue(start);
        while (queue.TryDequeue(out var current))
        {
            foreach (var address in Neighbors(current))
            {
                if (byAddress.TryGetValue(address, out var next) && next.BottomZ >= current.BottomZ && visited.Add(address))
                {
                    queue.Enqueue(next);
                }
            }
        }
        if (visited.Count != columns.Length)
        {
            throw new ArgumentException("Open equilibrium reservoirs require connected rising contours without perched pockets.");
        }
    }

    private static IEnumerable<(long X, long Y)> Neighbors(ReservoirColumn column)
    {
        if (column.X > long.MinValue)
        {
            yield return (column.X - 1, column.Y);
        }
        if (column.X < long.MaxValue)
        {
            yield return (column.X + 1, column.Y);
        }
        if (column.Y > long.MinValue)
        {
            yield return (column.X, column.Y - 1);
        }
        if (column.Y < long.MaxValue)
        {
            yield return (column.X, column.Y + 1);
        }
    }
}
