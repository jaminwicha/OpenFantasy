using System.Buffers.Binary;
using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>
/// Exact known, vertically vented terrain voids inside fixed nominal reservoir envelopes.
/// This is a geometry proof, not permission to excavate or a body, construction or pressure model.
/// </summary>
public sealed class TerrainLiquidTopology
{
    private readonly ContentHash _materialDomain;
    private readonly ContentHash[] _geometryPins;

    private TerrainLiquidTopology(LiquidReservoirState reservoirs, TerrainRegionSnapshot[] regions,
        ReservoirSealPolicy seals, LiquidCellKey[] nominal, LiquidCellKey[] open)
    {
        _materialDomain = reservoirs.Capacity.Domain.ContentHash;
        _geometryPins = reservoirs.Geometries.Select(geometry => geometry.ContentHash).ToArray();
        NominalCells = Array.AsReadOnly(nominal);
        OpenCells = Array.AsReadOnly(open);
        OpenCapacity = new VolumeMicrolitres(checked((ulong)open.Length * ReservoirGeometry.MicrolitresPerCubicMetre));
        ContentHash = ContentHash.Compute(CanonicalEncodingV1.Frame("geography.terrain-liquid-topology.v1",
            _materialDomain.ToByteArray(), CanonicalEncodingV1.Frame("geography.terrain-liquid-geometries.v1",
                _geometryPins.Select(pin => (ReadOnlyMemory<byte>)pin.ToByteArray()).ToArray()),
            seals.Version.ToByteArray(), CanonicalEncodingV1.Frame("geography.terrain-liquid-regions.v1",
                regions.Select(region => (ReadOnlyMemory<byte>)region.ComputeContentHash().ToByteArray()).ToArray()),
            EncodeCells(nominal), EncodeCells(open)));
    }

    public ContentHash ContentHash { get; }
    public ReadOnlyCollection<LiquidCellKey> NominalCells { get; }
    public ReadOnlyCollection<LiquidCellKey> OpenCells { get; }
    public VolumeMicrolitres OpenCapacity { get; }

    public static TerrainLiquidTopology Create(LiquidReservoirState reservoirs,
        IReadOnlyList<TerrainRegionSnapshot> regions, ReservoirSealPolicy seals)
    {
        ArgumentNullException.ThrowIfNull(reservoirs);
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(seals);
        if (seals.Liquid != reservoirs.Capacity.Domain.Material.Item)
        {
            throw new ArgumentException("Terrain liquid seals must bind the exact retained liquid material.", nameof(seals));
        }
        TerrainRegionSnapshot[] terrain = ReadRegions(reservoirs, regions);
        var query = new TerrainVolumeQueries(terrain);
        var nominal = LiquidCellState.BuildDomain(reservoirs);
        var addresses = nominal.ToHashSet();
        List<LiquidCellKey> open = [];
        foreach (ReservoirGeometry geometry in reservoirs.Geometries)
        {
            foreach (ReservoirColumn column in geometry.Columns)
            {
                bool hasOpenCell = false;
                bool solidAbove = false;
                // A bottom void underneath retained solid is an air pocket, even if a horizontal
                // neighbor happens to reach the open sky. This profile proves direct vertical vents.
                for (long z = geometry.SpillZ - 1; z >= column.BottomZ; z--)
                {
                    var cell = new LiquidCellKey(geometry.ReservoirId, new(column.X, column.Y, z));
                    TerrainCell current = Known(query, cell.Coordinate);
                    if (current.IsSolid)
                    {
                        RequireSeal(current, seals);
                        solidAbove = true;
                    }
                    else
                    {
                        if (solidAbove)
                        {
                            throw new ArgumentException("Each open liquid cell requires an unobstructed vertical vent through its spill plane.", nameof(regions));
                        }
                        hasOpenCell = true;
                        open.Add(cell);
                    }
                    foreach (var direction in BoundaryDirections)
                    {
                        CellCoordinate neighbor;
                        try { neighbor = cell.Coordinate.Offset(direction.X, direction.Y, direction.Z); }
                        catch (OverflowException error)
                        {
                            throw new ArgumentException("A nominal liquid boundary exceeds exact coordinate support.", nameof(reservoirs), error);
                        }
                        if (!addresses.Contains(new(geometry.ReservoirId, neighbor)))
                        {
                            RequireSeal(Known(query, neighbor), seals);
                        }
                    }
                    if (z == long.MinValue) { break; }
                }
                if (hasOpenCell && Known(query, new(column.X, column.Y, geometry.SpillZ)).IsSolid)
                {
                    throw new ArgumentException("An open liquid column cannot be roofed at its spill plane.", nameof(regions));
                }
            }
        }
        return new(reservoirs, terrain, seals, nominal.ToArray(), open.Order().ToArray());
    }

    internal void ValidateReservoirs(LiquidReservoirState reservoirs)
    {
        if (_materialDomain != reservoirs.Capacity.Domain.ContentHash
            || !_geometryPins.SequenceEqual(reservoirs.Geometries.Select(geometry => geometry.ContentHash)))
        {
            throw new ArgumentException("Terrain liquid topology belongs to another material domain or nominal geometry.", nameof(reservoirs));
        }
    }

    private static TerrainRegionSnapshot[] ReadRegions(LiquidReservoirState reservoirs,
        IReadOnlyList<TerrainRegionSnapshot> regions)
    {
        if (regions.Count is 0 or > LocalSpatialSnapshot.MaximumRegions)
        {
            throw new ArgumentException("Terrain liquid topology requires a bounded complete local region catalog.", nameof(regions));
        }
        var copy = new TerrainRegionSnapshot[regions.Count];
        for (int index = 0; index < copy.Length; index++)
        {
            TerrainRegionSnapshot region = regions[index];
            ArgumentNullException.ThrowIfNull(region);
            if (region.Scope != reservoirs.Capacity.Domain.Network.Scope
                || index > 0 && (region.Seed != copy[0].Seed || region.Rules.Version != copy[0].Rules.Version)
                || copy.Take(index).Any(other => Overlaps(region.Bounds, other.Bounds)))
            {
                throw new ArgumentException("Liquid terrain requires exact scope, seed and rules with nonoverlapping regions.", nameof(regions));
            }
            copy[index] = region;
        }
        return copy.OrderBy(region => region.Bounds.Minimum).ToArray();
    }

    private static TerrainCell Known(TerrainVolumeQueries query, CellCoordinate coordinate) =>
        query.FindCell(coordinate) ?? throw new ArgumentException("Liquid topology cannot assume unknown terrain is an empty cell or a seal.");

    private static void RequireSeal(TerrainCell cell, ReservoirSealPolicy seals)
    {
        // An intact terrain cell has exactly one metre of normal thickness. Porous/unlisted
        // material cannot silently become impermeable merely because it has collision geometry.
        if (!cell.IsSolid || cell.Material is not { } material || seals.MinimumThicknessFor(material) is null)
        {
            throw new ArgumentException("Terrain liquid floors, walls and solid dividers require an exact admitted seal material.");
        }
    }

    private static bool Overlaps(TerrainRegionBounds first, TerrainRegionBounds second) =>
        first.Minimum.X <= second.Maximum.X && first.Maximum.X >= second.Minimum.X
        && first.Minimum.Y <= second.Maximum.Y && first.Maximum.Y >= second.Minimum.Y
        && first.Minimum.Z <= second.Maximum.Z && first.Maximum.Z >= second.Minimum.Z;

    private static byte[] EncodeCells(IReadOnlyList<LiquidCellKey> cells)
    {
        var bytes = new byte[checked(cells.Count * 40)];
        for (int index = 0; index < cells.Count; index++)
        {
            Span<byte> row = bytes.AsSpan(index * 40, 40);
            cells[index].ReservoirId.WriteBytes(row);
            BinaryPrimitives.WriteInt64BigEndian(row[16..], cells[index].Coordinate.X);
            BinaryPrimitives.WriteInt64BigEndian(row[24..], cells[index].Coordinate.Y);
            BinaryPrimitives.WriteInt64BigEndian(row[32..], cells[index].Coordinate.Z);
        }
        return bytes;
    }

    private static readonly (long X, long Y, long Z)[] BoundaryDirections =
        [(-1, 0, 0), (1, 0, 0), (0, -1, 0), (0, 1, 0), (0, 0, -1)];
}
