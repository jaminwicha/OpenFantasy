using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Spatial;

/// <summary>Bounded bidirectional search through validated ordinary movement edges, including derived elevation.</summary>
internal sealed class WalkingRouteSearch(SpatialMovementQueries space, EntityId actorId, int maximumExpansions)
{
    internal GroundRouteResult Find(SubunitPosition from, SubunitPosition destination)
    {
        if (maximumExpansions is < 1 or > LocalSpatialSnapshot.MaximumWalkingRouteExpansions)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumExpansions));
        }
        if (space.GroundSupport != GroundSupportMode.SteppedTerrainAndConstruction)
        {
            return new(GroundRouteStatus.Unavailable);
        }
        if (!space.GetActor(actorId).Condition.CanLocomote(LocomotionMode.Ground))
        {
            return new(GroundRouteStatus.NoGroundLocomotion);
        }
        const long step = WorldMovementInput.DisplacementPerStep;
        if (((Int128)destination.X - from.X) % step != 0 || ((Int128)destination.Y - from.Y) % step != 0)
        {
            return new(GroundRouteStatus.Unaligned);
        }
        try
        {
            SpatialOccupancy start = space.QueryOccupancy(actorId, from);
            SpatialOccupancy finish = space.QueryOccupancy(actorId, destination);
            if (start != SpatialOccupancy.Clear || finish != SpatialOccupancy.Clear)
            {
                return new(start == SpatialOccupancy.Unavailable || finish == SpatialOccupancy.Unavailable
                    ? GroundRouteStatus.Unavailable : GroundRouteStatus.Blocked);
            }
            if (from == destination) return new(GroundRouteStatus.Reachable);
            return Search(from, destination);
        }
        catch (OverflowException)
        {
            return new(GroundRouteStatus.NumericLimit);
        }
    }

    private GroundRouteResult Search(SubunitPosition from, SubunitPosition destination)
    {
        const long step = WorldMovementInput.DisplacementPerStep;
        (long X, long Y)[] directions = [(-step, 0), (0, -step), (0, step), (step, 0)];
        var forward = new WalkingRouteFrontier(from, destination);
        var reverse = new WalkingRouteFrontier(destination, from);
        bool missing = false;
        bool numericLimit = false;
        bool backwards = false;
        int expanded = 0;
        int evaluatedTransitions = 0;
        int transitionBudget = maximumExpansions * 16;
        while (expanded < maximumExpansions)
        {
            WalkingRouteFrontier active = backwards ? reverse : forward;
            WalkingRouteFrontier opposite = backwards ? forward : reverse;
            if (!active.TryPop(out SubunitPosition current, out int cost))
            {
                return new(missing ? GroundRouteStatus.Unavailable : numericLimit ? GroundRouteStatus.NumericLimit
                    : GroundRouteStatus.Blocked, expanded, evaluatedTransitions: evaluatedTransitions);
            }
            expanded++;
            foreach (var direction in directions)
            {
                try
                {
                    IEnumerable<SubunitPosition> origins = [current];
                    if (backwards)
                    {
                        origins = space.CandidateWalkingOrigins(actorId, current, direction.X, direction.Y,
                            out bool missingOriginTerrain);
                        missing |= missingOriginTerrain;
                    }
                    foreach (SubunitPosition origin in origins)
                    {
                        if (evaluatedTransitions == transitionBudget)
                        {
                            return new(GroundRouteStatus.ExpansionLimit, expanded, evaluatedTransitions: evaluatedTransitions);
                        }
                        evaluatedTransitions++;
                        SpatialMoveResult move = space.PrepareWalkFrom(actorId, origin, direction.X, direction.Y);
                        missing |= move.Status == SpatialMoveStatus.Unavailable;
                        numericLimit |= move.Status == SpatialMoveStatus.NumericLimit;
                        if (move.Status != SpatialMoveStatus.Prepared) { continue; }
                        // Contact-height selection is directed: a reverse candidate must reproduce the exact target.
                        if (backwards && move.Candidate!.Feet != current) { continue; }
                        SubunitPosition next = backwards ? origin : move.Candidate!.Feet;
                        if (!active.Add(next, current, cost + 1)) { continue; }
                        if (opposite.Contains(next)) { return Join(next); }
                    }
                }
                catch (OverflowException)
                {
                    numericLimit = true;
                }
            }
            // The constrained upper-floor search must receive an equal share of the finite expansion budget.
            backwards = !backwards;
        }
        return new(GroundRouteStatus.ExpansionLimit, expanded, evaluatedTransitions: evaluatedTransitions);

        GroundRouteResult Join(SubunitPosition meeting)
        {
            List<SubunitPosition> path = forward.TraceToRoot(meeting);
            path.Reverse();
            path.RemoveAt(0);
            path.AddRange(reverse.TraceToRoot(meeting).Skip(1));
            return new(GroundRouteStatus.Reachable, expanded, path, evaluatedTransitions);
        }
    }
}
