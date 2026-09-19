using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Spatial;

/// <summary>One deterministic search direction, retaining validated links back to its root.</summary>
internal sealed class WalkingRouteFrontier
{
    private readonly SubunitPosition _target;
    private readonly PriorityQueue<(SubunitPosition Position, int Cost),
        (Int128 Total, Int128 Distance, long X, long Y, long Z)> _queue = new();
    private readonly Dictionary<SubunitPosition, int> _costs = [];
    private readonly Dictionary<SubunitPosition, SubunitPosition> _parents = [];

    internal WalkingRouteFrontier(SubunitPosition root, SubunitPosition target)
    {
        Root = root;
        _target = target;
        _costs.Add(root, 0);
        Enqueue(root, 0);
    }

    internal SubunitPosition Root { get; }
    internal bool Contains(SubunitPosition position) => _costs.ContainsKey(position);

    internal bool TryPop(out SubunitPosition position, out int cost)
    {
        while (_queue.TryDequeue(out var entry, out _))
        {
            if (_costs[entry.Position] != entry.Cost) { continue; }
            position = entry.Position;
            cost = entry.Cost;
            return true;
        }
        position = default;
        cost = 0;
        return false;
    }

    internal bool Add(SubunitPosition position, SubunitPosition parent, int cost)
    {
        if (_costs.TryGetValue(position, out int previous) && previous <= cost) { return false; }
        _costs[position] = cost;
        _parents[position] = parent;
        Enqueue(position, cost);
        return true;
    }

    internal List<SubunitPosition> TraceToRoot(SubunitPosition position)
    {
        List<SubunitPosition> path = [position];
        while (position != Root)
        {
            position = _parents[position];
            path.Add(position);
        }
        return path;
    }

    private void Enqueue(SubunitPosition position, int cost)
    {
        Int128 horizontal = (Int128.Abs((Int128)position.X - _target.X) + Int128.Abs((Int128)position.Y - _target.Y))
            / WorldMovementInput.DisplacementPerStep;
        Int128 elevation = Int128.Abs((Int128)position.Z - _target.Z);
        Int128 vertical = (elevation + LocalSpatialSnapshot.MaximumOrdinaryStepHeightSubunits - 1)
            / LocalSpatialSnapshot.MaximumOrdinaryStepHeightSubunits;
        Int128 distance = Int128.Max(horizontal, vertical);
        _queue.Enqueue((position, cost), (cost + distance, distance, position.X, position.Y, position.Z));
    }
}
