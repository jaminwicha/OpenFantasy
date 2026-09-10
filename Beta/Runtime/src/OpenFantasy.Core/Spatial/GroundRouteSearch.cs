using OpenFantasy.Core.Actors;

namespace OpenFantasy.Core.Spatial;

/// <summary>
/// Bounded deterministic breadth-first search on cell centers, with checked cardinal connectors for
/// arbitrary subunit endpoints. Conservative failure does not imply no finer or unloaded route exists.
/// </summary>
internal sealed class GroundRouteSearch
{
    internal const int MaximumExpansions = 256;
    private static readonly (long X, long Y)[] Directions = [(-256, 0), (0, -256), (0, 256), (256, 0)];
    private readonly LocalSpatialSnapshot _space;
    private readonly EntityId _actorId;
    private readonly int _budget;
    private bool _sawMissing;
    private bool _sawNumericLimit;

    internal GroundRouteSearch(LocalSpatialSnapshot space, EntityId actorId, int maximumExpansions)
    {
        ValidateBudget(maximumExpansions);
        _space = space;
        _actorId = actorId;
        _budget = maximumExpansions;
    }

    internal static void ValidateBudget(int maximumExpansions)
    {
        if (maximumExpansions is < 1 or > MaximumExpansions)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumExpansions));
        }
    }

    internal GroundRouteResult Find(SubunitPosition from, SubunitPosition destination)
    {
        if (from.Z != destination.Z)
        {
            return new GroundRouteResult(GroundRouteStatus.DifferentFloor);
        }
        if (!_space.GetActor(_actorId).Condition.CanLocomote(LocomotionMode.Ground))
        {
            return new GroundRouteResult(GroundRouteStatus.NoGroundLocomotion);
        }
        try
        {
            SpatialOccupancy start = _space.QueryOccupancy(_actorId, from);
            SpatialOccupancy end = _space.QueryOccupancy(_actorId, destination);
            if (start != SpatialOccupancy.Clear || end != SpatialOccupancy.Clear)
            {
                return new GroundRouteResult(start == SpatialOccupancy.Unavailable || end == SpatialOccupancy.Unavailable
                    ? GroundRouteStatus.Unavailable : GroundRouteStatus.Blocked);
            }
            if (from == destination)
            {
                return new GroundRouteResult(GroundRouteStatus.Reachable);
            }
            List<SubunitPosition>? direct = Connect(from, destination);
            if (direct is not null)
            {
                return new GroundRouteResult(GroundRouteStatus.Reachable, waypoints: direct);
            }
            return Search(from, destination);
        }
        catch (OverflowException)
        {
            return new GroundRouteResult(GroundRouteStatus.NumericLimit);
        }
    }

    private GroundRouteResult Search(SubunitPosition from, SubunitPosition destination)
    {
        Dictionary<SubunitPosition, List<SubunitPosition>> starts = [];
        Dictionary<SubunitPosition, List<SubunitPosition>> finishes = [];
        foreach (SubunitPosition center in NearbyCenters(from))
        {
            List<SubunitPosition>? path = Connect(from, center);
            if (path is not null) { starts.Add(center, path); }
        }
        foreach (SubunitPosition center in NearbyCenters(destination))
        {
            List<SubunitPosition>? path = Connect(center, destination);
            if (path is not null) { finishes.Add(center, path); }
        }
        Dictionary<SubunitPosition, SubunitPosition?> parents = starts.Keys.ToDictionary(value => value, _ => (SubunitPosition?)null);
        Queue<SubunitPosition> frontier = new(starts.Keys);
        int expanded = 0;
        while (frontier.TryDequeue(out SubunitPosition current))
        {
            if (expanded == _budget)
            {
                return new GroundRouteResult(GroundRouteStatus.ExpansionLimit, expanded);
            }
            expanded++;
            if (finishes.TryGetValue(current, out List<SubunitPosition>? finish))
            {
                return Reconstruct(current, parents, starts, finish, expanded);
            }
            foreach ((long x, long y) in Directions)
            {
                SubunitPosition next;
                try { next = current.Offset(x, y, 0); }
                catch (OverflowException) { _sawNumericLimit = true; continue; }
                if (parents.ContainsKey(next) || !IsClear(current, next))
                {
                    continue;
                }
                parents.Add(next, current);
                frontier.Enqueue(next);
            }
        }
        return new GroundRouteResult(_sawMissing ? GroundRouteStatus.Unavailable
            : _sawNumericLimit ? GroundRouteStatus.NumericLimit : GroundRouteStatus.Blocked, expanded);
    }

    private static GroundRouteResult Reconstruct(SubunitPosition current,
        Dictionary<SubunitPosition, SubunitPosition?> parents,
        Dictionary<SubunitPosition, List<SubunitPosition>> starts, List<SubunitPosition> finish, int expanded)
    {
        List<SubunitPosition> reversed = [];
        while (parents[current] is { } parent)
        {
            reversed.Add(current);
            current = parent;
        }
        reversed.Reverse();
        List<SubunitPosition> path = [.. starts[current], .. reversed, .. finish];
        return new GroundRouteResult(GroundRouteStatus.Reachable, expanded, path);
    }

    private IEnumerable<SubunitPosition> NearbyCenters(SubunitPosition point)
    {
        List<SubunitPosition> centers = [];
        Int128 lowerX = LowerCenter(point.X);
        Int128 lowerY = LowerCenter(point.Y);
        for (int x = 0; x < 2; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                try { centers.Add(new SubunitPosition(checked((long)(lowerX + x * 256)),
                    checked((long)(lowerY + y * 256)), point.Z)); }
                catch (OverflowException) { _sawNumericLimit = true; }
            }
        }
        return centers.OrderBy(value => Int128.Abs((Int128)value.X - point.X) + Int128.Abs((Int128)value.Y - point.Y))
            .ThenBy(value => value.X).ThenBy(value => value.Y);
    }

    private static Int128 LowerCenter(long coordinate)
    {
        Int128 offset = (Int128)coordinate - 128;
        Int128 cell = offset / 256;
        if (offset % 256 < 0) { cell--; }
        return cell * 256 + 128;
    }

    private List<SubunitPosition>? Connect(SubunitPosition from, SubunitPosition to)
    {
        if (Int128.Abs((Int128)to.X - from.X) > 256 || Int128.Abs((Int128)to.Y - from.Y) > 256)
        {
            return null;
        }
        if (from == to)
        {
            return _space.QueryOccupancy(_actorId, from) == SpatialOccupancy.Clear ? [] : null;
        }
        SubunitPosition xFirst = new(to.X, from.Y, from.Z);
        List<SubunitPosition>? first = ConnectVia(from, xFirst, to);
        return first ?? ConnectVia(from, new SubunitPosition(from.X, to.Y, from.Z), to);
    }

    private List<SubunitPosition>? ConnectVia(SubunitPosition from, SubunitPosition middle, SubunitPosition to)
    {
        List<SubunitPosition> path = [];
        if (from != middle)
        {
            if (!IsClear(from, middle)) { return null; }
            path.Add(middle);
        }
        if (middle != to)
        {
            if (!IsClear(middle, to)) { return null; }
            path.Add(to);
        }
        return path;
    }

    private bool IsClear(SubunitPosition from, SubunitPosition to)
    {
        try
        {
            SpatialOccupancy occupancy = _space.QueryGroundSegment(_actorId, from, to);
            _sawMissing |= occupancy == SpatialOccupancy.Unavailable;
            return occupancy == SpatialOccupancy.Clear;
        }
        catch (OverflowException)
        {
            _sawNumericLimit = true;
            return false;
        }
    }
}
