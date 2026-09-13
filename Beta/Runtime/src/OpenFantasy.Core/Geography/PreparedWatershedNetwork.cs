using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>Immutable finite drainage composition. Preparation grants no world admission, extraction or liquid custody.</summary>
public sealed class PreparedWatershedNetwork
{
    public const int MaximumBasins = 16;
    public const int MaximumColumns = MaximumBasins * WatershedBounds.MaximumColumns;
    public const int MaximumSharedFaces = MaximumBasins * 2 * WatershedBounds.MaximumAxis;

    private readonly Dictionary<EntityId, int[]> _contributions;
    private readonly Dictionary<(WatershedColumnReference, WatershedColumnReference), WatershedNetworkFace> _faces;
    private readonly Lazy<ContentHash> _contentHash;
    private readonly Lazy<WatershedMaterialIdentities> _materialIdentities;

    internal PreparedWatershedNetwork(EntityId provinceId, PreparedWatershed[] basins,
        WatershedOutletClosure[] closures, WatershedNetworkFace[] faces,
        Dictionary<EntityId, int[]> contributions, WatershedTerminalContribution[] terminals)
    {
        ProvinceId = provinceId;
        Scope = basins[0].Request.Scope;
        Seed = basins[0].Request.Seed;
        Basins = Array.AsReadOnly((PreparedWatershed[])basins.Clone());
        Closures = Array.AsReadOnly((WatershedOutletClosure[])closures.Clone());
        SharedFaces = Array.AsReadOnly((WatershedNetworkFace[])faces.Clone());
        Terminals = Array.AsReadOnly((WatershedTerminalContribution[])terminals.Clone());
        _contributions = contributions.ToDictionary(pair => pair.Key, pair => (int[])pair.Value.Clone());
        _faces = [];
        foreach (WatershedNetworkFace face in faces)
        {
            _faces.Add((face.First, face.Second), face);
            _faces.Add((face.Second, face.First), face);
        }
        ColumnCount = basins.Sum(basin => basin.Columns.Count);
        _contentHash = new(() => ContentHash.Compute(WatershedNetworkCodecV1.Encode(this)));
        _materialIdentities = new(() => new WatershedMaterialIdentities(this));
    }

    public EntityId ProvinceId { get; }
    public TerrainScope Scope { get; }
    public EntityId Seed { get; }
    public int ColumnCount { get; }
    public ReadOnlyCollection<PreparedWatershed> Basins { get; }
    public ReadOnlyCollection<WatershedOutletClosure> Closures { get; }
    public ReadOnlyCollection<WatershedNetworkFace> SharedFaces { get; }
    public ReadOnlyCollection<WatershedTerminalContribution> Terminals { get; }

    public int ContributingColumns(EntityId basinId, int columnIndex)
    {
        if (!_contributions.TryGetValue(basinId, out int[]? counts) || columnIndex < 0 || columnIndex >= counts.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(columnIndex), "The column is outside this prepared network.");
        }
        return counts[columnIndex];
    }

    public WatershedNetworkFace SharedFace(WatershedColumnReference first, WatershedColumnReference second)
    {
        if (!_faces.TryGetValue((first, second), out WatershedNetworkFace? face))
        {
            throw new ArgumentException("The columns do not share a cross-basin face in this network.");
        }
        return face;
    }

    public ContentHash ComputeContentHash() => _contentHash.Value;
    internal WatershedMaterialIdentities MaterialIdentities => _materialIdentities.Value;
}
