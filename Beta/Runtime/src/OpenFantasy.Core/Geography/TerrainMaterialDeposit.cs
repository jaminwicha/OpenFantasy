using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>A finite homogeneous slab backed by a pre-admitted source interval, independent of requested detail windows.</summary>
public sealed class TerrainMaterialDeposit
{
    public const int MaximumDepth = 1024;
    public const ulong CellVolumeMicrolitres = 1_000_000_000;

    public TerrainMaterialDeposit(PreparedWatershedTerrain terrain, WatershedColumnReference macroColumn,
        int depth, ItemPhysicalBinding material, ulong unitsPerCell, SourceInterval source,
        EntityId coarseCustodianId, EntityId fineCustodianId)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(source);
        if (depth is < 1 or > MaximumDepth || unitsPerCell == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(depth));
        }
        if (coarseCustodianId == fineCustodianId)
        {
            throw new ArgumentException("Coarse and detailed material require distinct custodians.");
        }
        PreparedWatershed? basin = terrain.Network.Basins.SingleOrDefault(value => value.BasinId == macroColumn.BasinId);
        if (basin is null || macroColumn.ColumnIndex < 0 || macroColumn.ColumnIndex >= basin.Columns.Count)
        {
            throw new ArgumentException("The material parcel must belong to the pinned network.", nameof(macroColumn));
        }
        int side = terrain.Mapping.CellsPerColumn;
        ulong cellCount = checked((ulong)side * (ulong)side * (ulong)depth);
        if ((UInt128)cellCount * unitsPerCell != source.Quantity
            || (UInt128)material.Physical.VolumePerUnit.Value * unitsPerCell != CellVolumeMicrolitres)
        {
            throw new ArgumentException("The complete finite slab must have exact source funding and one cubic metre per cell.");
        }
        (long macroX, long macroY) = basin.Request.Bounds.CoordinateAt(macroColumn.ColumnIndex);
        long surface = terrain.Mapping.SurfaceZ(basin.Columns[macroColumn.ColumnIndex].NaturalElevation);
        Minimum = new(checked(macroX * side), checked(macroY * side), checked(surface - (depth - 1)));
        Maximum = new(checked(Minimum.X + (side - 1)), checked(Minimum.Y + (side - 1)), surface);
        Terrain = terrain;
        MacroColumn = macroColumn;
        Depth = depth;
        Material = material;
        UnitsPerCell = unitsPerCell;
        Source = source;
        CoarseCustodianId = coarseCustodianId;
        FineCustodianId = fineCustodianId;
        TotalMass = material.Physical.MassPerUnit.Multiply(source.Quantity);
        TotalVolume = material.Physical.VolumePerUnit.Multiply(source.Quantity);
        NetworkHash = terrain.Network.ComputeContentHash();
        if (terrain.Network.MaterialIdentities.Contains(material.Physical.DefinitionId))
        {
            throw new ArgumentException("Physical calibration cannot alias macro geography.");
        }
    }

    public PreparedWatershedTerrain Terrain { get; }
    public WatershedColumnReference MacroColumn { get; }
    public int Depth { get; }
    public ItemPhysicalBinding Material { get; }
    public ulong UnitsPerCell { get; }
    public SourceInterval Source { get; }
    public EntityId CoarseCustodianId { get; }
    public EntityId FineCustodianId { get; }
    public CellCoordinate Minimum { get; }
    public CellCoordinate Maximum { get; }
    public MassMilligrams TotalMass { get; }
    public VolumeMicrolitres TotalVolume { get; }
    internal ContentHash NetworkHash { get; }
    internal bool ReservesIdentity(EntityId identity) => Terrain.Network.MaterialIdentities.Contains(identity)
        || identity == Material.Physical.DefinitionId;

    public SourceInterval IntervalFor(CellCoordinate cell)
    {
        if (cell.X < Minimum.X || cell.X > Maximum.X || cell.Y < Minimum.Y || cell.Y > Maximum.Y
            || cell.Z < Minimum.Z || cell.Z > Maximum.Z)
        {
            throw new ArgumentOutOfRangeException(nameof(cell), "The cell is outside the funded slab.");
        }
        ulong side = (ulong)Terrain.Mapping.CellsPerColumn;
        ulong ordinal = checked(((ulong)(cell.Z - Minimum.Z) * side + (ulong)(cell.Y - Minimum.Y)) * side
            + (ulong)(cell.X - Minimum.X));
        ulong start = checked(Source.Start + ordinal * UnitsPerCell);
        return new(Source.SourceId, start, checked(start + UnitsPerCell));
    }

    public CellCoordinate CellForUnit(ulong unit)
    {
        if (unit < Source.Start || unit >= Source.End)
        {
            throw new ArgumentOutOfRangeException(nameof(unit));
        }
        ulong side = (ulong)Terrain.Mapping.CellsPerColumn;
        ulong ordinal = (unit - Source.Start) / UnitsPerCell;
        return new(checked(Minimum.X + (long)(ordinal % side)),
            checked(Minimum.Y + (long)(ordinal / side % side)), checked(Minimum.Z + (long)(ordinal / side / side)));
    }

    internal SourceInterval? Intersect(SourceInterval interval)
    {
        ulong start = Math.Max(Source.Start, interval.Start);
        ulong end = Math.Min(Source.End, interval.End);
        return interval.SourceId == Source.SourceId && start < end ? new(Source.SourceId, start, end) : null;
    }

}
