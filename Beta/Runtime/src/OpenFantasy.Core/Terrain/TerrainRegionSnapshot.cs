using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Terrain;

/// <summary>An immutable complete local occupancy snapshot; mutation methods prepare candidates for the owning transaction.</summary>
public sealed class TerrainRegionSnapshot
{
    public const int MaximumExcavationBatch = 256;
    private readonly TerrainCell[] _cells;

    internal TerrainRegionSnapshot(TerrainScope scope, EntityId seed, StratifiedTerrainRules rules,
        TerrainRegionBounds bounds, ulong revision, TerrainCell[] cells)
    {
        Scope = scope;
        Seed = seed;
        Rules = rules;
        Bounds = bounds;
        Revision = revision;
        _cells = (TerrainCell[])cells.Clone();
        Cells = Array.AsReadOnly(_cells);
    }

    public TerrainScope Scope { get; }
    public EntityId Seed { get; }
    public StratifiedTerrainRules Rules { get; }
    public TerrainRegionBounds Bounds { get; }
    public ulong Revision { get; }
    public ReadOnlyCollection<TerrainCell> Cells { get; }
    public TerrainCell GetCell(CellCoordinate coordinate) => _cells[Bounds.IndexOf(coordinate)];

    /// <summary>One-cell upright clearance and direct full-cell contact only, not load-bearing stability or path reachability.</summary>
    public StandingSpace QueryStanding(CellCoordinate coordinate)
    {
        if (!Bounds.Contains(coordinate))
        {
            return StandingSpace.Unavailable;
        }
        if (GetCell(coordinate).IsSolid)
        {
            return StandingSpace.Blocked;
        }
        if (coordinate.Z == long.MinValue)
        {
            return StandingSpace.Unavailable;
        }
        CellCoordinate below = coordinate.Offset(0, 0, -1);
        if (!Bounds.Contains(below))
        {
            return StandingSpace.Unavailable;
        }
        return GetCell(below).IsSolid ? StandingSpace.Supported : StandingSpace.Unsupported;
    }

    /// <summary>
    /// Prepares whole-cell excavation atomically within this snapshot. Does not issue loot, pay work,
    /// admit occupants/hazards, or commit storage; the owner must couple those dependencies before publication.
    /// </summary>
    public TerrainRegionSnapshot Excavate(ulong expectedRevision, IEnumerable<CellCoordinate> coordinates)
    {
        ArgumentNullException.ThrowIfNull(coordinates);
        if (expectedRevision != Revision)
        {
            throw new InvalidOperationException("The terrain revision has changed.");
        }
        ulong nextRevision = checked(Revision + 1);
        HashSet<int> indices = [];
        foreach (CellCoordinate coordinate in coordinates)
        {
            if (indices.Count == MaximumExcavationBatch)
            {
                throw new ArgumentException("The excavation batch exceeds its supported size.", nameof(coordinates));
            }
            int index = Bounds.IndexOf(coordinate);
            if (!indices.Add(index))
            {
                throw new ArgumentException("An excavation batch repeats a cell.", nameof(coordinates));
            }
            if (!_cells[index].IsSolid)
            {
                throw new ArgumentException("Only remaining solid substrate can be excavated.", nameof(coordinates));
            }
        }
        if (indices.Count == 0)
        {
            throw new ArgumentException("An excavation batch must contain at least one cell.", nameof(coordinates));
        }
        TerrainCell[] changed = (TerrainCell[])_cells.Clone();
        foreach (int index in indices)
        {
            changed[index] = changed[index].Excavate();
        }
        return new TerrainRegionSnapshot(Scope, Seed, Rules, Bounds, nextRevision, changed);
    }

    public ContentHash ComputeContentHash() => ContentHash.Compute(TerrainRegionCodecV1.Encode(this));

    internal TerrainRegionSnapshot RestoreExcavations(ulong revision, IReadOnlyList<int> indices)
    {
        if (revision == 0 && indices.Count != 0)
        {
            throw new FormatException("Initial terrain cannot contain lived excavation edits.");
        }
        TerrainCell[] changed = (TerrainCell[])_cells.Clone();
        int previous = -1;
        foreach (int index in indices)
        {
            if (index <= previous || index >= changed.Length || !changed[index].IsSolid)
            {
                throw new FormatException("Excavated indices must be strictly ordered existing substrate cells.");
            }
            previous = index;
            changed[index] = changed[index].Excavate();
        }
        return new TerrainRegionSnapshot(Scope, Seed, Rules, Bounds, revision, changed);
    }
}
