using System.Collections.ObjectModel;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

public enum EncounterRetreatAdviceStatus
{
    Found,
    NoClearGround,
    ExpansionLimit,
    NoGroundLocomotion
}

/// <summary>
/// One offered retreat: the fewest-sample cardinal route from the actor's current feet to ground clear of contact.
/// It is advice for presentation and host controllers only; queue admission and every sample revalidate.
/// </summary>
public sealed class EncounterRetreatAdvice
{
    internal EncounterRetreatAdvice(EncounterRetreatAdviceStatus status, SubunitPosition origin, int expandedPositions,
        IEnumerable<EncounterRetreatDirection>? route = null)
    {
        Status = status;
        Origin = origin;
        ExpandedPositions = expandedPositions;
        Route = Array.AsReadOnly((route ?? []).ToArray());
        Destination = QueueEncounterRetreatIntent.Endpoint(origin, Route);
    }

    public EncounterRetreatAdviceStatus Status { get; }
    public SubunitPosition Origin { get; }
    public SubunitPosition Destination { get; }
    public ReadOnlyCollection<EncounterRetreatDirection> Route { get; }
    public int ExpandedPositions { get; }
    public bool IsFound => Status == EncounterRetreatAdviceStatus.Found;
}

/// <summary>
/// Deterministic bounded breadth-first search over ordinary 32-subunit samples using the authority's own swept-body
/// ground clearance. Among the fewest-sample positions clear of every living opponent's contact radius (the same
/// body-centre rule as the authority) it prefers the greatest margin, then search order (north, east, south, west
/// expansion), and verifies each candidate with the queue's own route admission: every prefix through the movement
/// owner and complete root admission, then contact. Placement admission is memoised within one search; nothing is
/// published and no root is encoded per expansion.
/// </summary>
public static class WorldEncounterRetreatAdvisor
{
    public const int DefaultMaximumExpansions = 1_024;
    private static readonly EncounterRetreatDirection[] Order =
        [EncounterRetreatDirection.North, EncounterRetreatDirection.East, EncounterRetreatDirection.South, EncounterRetreatDirection.West];

    public static EncounterRetreatAdvice Advise(WorldSnapshot world, EntityId actorId, int maximumExpansions = DefaultMaximumExpansions)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumExpansions, 1);
        LocalSpatialSnapshot space = world.Space;
        ActorPresence actor = space.GetActor(actorId);
        SubunitPosition origin = actor.Feet;
        if (!actor.Condition.CanLocomote(LocomotionMode.Ground))
        {
            return new(EncounterRetreatAdviceStatus.NoGroundLocomotion, origin, 0);
        }
        Dictionary<SubunitPosition, bool> admitted = [];
        Dictionary<SubunitPosition, (SubunitPosition Parent, EncounterRetreatDirection Direction)> parents = [];
        HashSet<SubunitPosition> visited = [origin];
        List<SubunitPosition> level = [origin];
        int expanded = 0;
        for (int depth = 1; depth <= EncounterRetreatRules.MaximumRouteSamples && level.Count != 0; depth++)
        {
            List<SubunitPosition> next = [];
            foreach (SubunitPosition from in level)
            {
                foreach (EncounterRetreatDirection direction in Order)
                {
                    (long x, long y) = QueueEncounterRetreatIntent.Displacement(direction);
                    SubunitPosition to;
                    try { to = from.Offset(x, y, 0); }
                    catch (OverflowException) { continue; }
                    if (!visited.Add(to)) continue;
                    if (++expanded > maximumExpansions)
                    {
                        return new(EncounterRetreatAdviceStatus.ExpansionLimit, origin, expanded - 1);
                    }
                    if (space.QueryGroundSegment(actorId, from, to) != SpatialOccupancy.Clear) continue;
                    parents.Add(to, (from, direction));
                    next.Add(to);
                }
            }
            // Fewest samples first; among them the most open ground, then the deterministic search order.
            var candidates = next.Select((position, index) => (Position: position, Index: index,
                    Margin: WorldEncounterRetreats.ContactExcess(world, space, actorId, position)))
                .Where(value => value.Margin > 0).OrderByDescending(value => value.Margin).ThenBy(value => value.Index);
            foreach (var candidate in candidates)
            {
                List<EncounterRetreatDirection> route = Reconstruct(candidate.Position, origin, parents);
                if (WorldEncounterRetreats.EvaluateRoute(world, actorId, route, admitted) == WorldActionRejection.None)
                {
                    return new(EncounterRetreatAdviceStatus.Found, origin, expanded, route);
                }
            }
            level = next;
        }
        return new(EncounterRetreatAdviceStatus.NoClearGround, origin, expanded);
    }

    private static List<EncounterRetreatDirection> Reconstruct(SubunitPosition end, SubunitPosition origin,
        Dictionary<SubunitPosition, (SubunitPosition Parent, EncounterRetreatDirection Direction)> parents)
    {
        List<EncounterRetreatDirection> reversed = [];
        SubunitPosition current = end;
        while (current != origin)
        {
            var (parent, direction) = parents[current];
            reversed.Add(direction);
            current = parent;
        }
        reversed.Reverse();
        return reversed;
    }
}
