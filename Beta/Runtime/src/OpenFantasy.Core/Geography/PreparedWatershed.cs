using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Geography;

/// <summary>
/// Immutable complete finite macro preparation. Windows share these established constraints; requesting detail cannot reroll them.
/// It is not a lived terrain snapshot, a fluid ledger, or authority to overwrite existing geography.
/// </summary>
public sealed class PreparedWatershed
{
    private readonly Dictionary<(int, int), WatershedBoundary> _boundaries;

    internal PreparedWatershed(WatershedRequest request, EntityId basinId, EntityId formationId,
        WatershedColumn[] columns, Dictionary<(int, int), WatershedBoundary> boundaries)
    {
        Request = request;
        BasinId = basinId;
        FormationId = formationId;
        Columns = Array.AsReadOnly((WatershedColumn[])columns.Clone());
        _boundaries = new Dictionary<(int, int), WatershedBoundary>(boundaries);
        Boundaries = Array.AsReadOnly(_boundaries.Values.OrderBy(value => value.FirstIndex)
            .ThenBy(value => value.SecondIndex).ToArray());
    }

    public WatershedRequest Request { get; }
    public EntityId BasinId { get; }
    public EntityId FormationId { get; }
    public ReadOnlyCollection<WatershedColumn> Columns { get; }
    public ReadOnlyCollection<WatershedBoundary> Boundaries { get; }

    public IReadOnlyList<WatershedColumn> ReadWindow(WatershedBounds window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _ = Request.Bounds.IndexOf(window.MinimumX, window.MinimumY);
        _ = Request.Bounds.IndexOf(window.MaximumX, window.MaximumY);
        WatershedColumn[] selected = new WatershedColumn[window.ColumnCount];
        for (int index = 0; index < selected.Length; index++)
        {
            (long x, long y) = window.CoordinateAt(index);
            selected[index] = Columns[Request.Bounds.IndexOf(x, y)];
        }
        return Array.AsReadOnly(selected);
    }

    public WatershedBoundary SharedBoundary(int first, int second)
    {
        if (!_boundaries.TryGetValue((Math.Min(first, second), Math.Max(first, second)), out WatershedBoundary? boundary))
        {
            throw new ArgumentException("A shared face requires two cardinally adjacent columns in this basin.");
        }
        return boundary;
    }

    public ContentHash ComputeContentHash() => ContentHash.Compute(WatershedCodecV1.Encode(this));
}
