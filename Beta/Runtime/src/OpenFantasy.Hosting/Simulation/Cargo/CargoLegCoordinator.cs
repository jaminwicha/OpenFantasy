using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation.Cargo;

public enum CargoLegState { Moving, Arrived, Blocked }

/// <summary>One cutoff's advice: an ordinary movement sample, arrival, or a stop with the route status that caused it.</summary>
public sealed record CargoLegAdvice(CargoLegState State, int LegIndex, long DeltaX = 0, long DeltaY = 0,
    GroundRouteStatus? RouteStatus = null);

/// <summary>
/// Host itinerary advice for one walking actor over authored waypoints. Each leg is planned from the actor's actual
/// feet with the unchanged bounded ground search, and travel is one ordinary <see cref="WorldMovementInput"/>
/// sample per cutoff; the world still validates every step. When a sample did not move the actor, the leg is
/// replanned from the actual state; a leg that cannot be planned, or a replanned first step that was already
/// refused, stops the itinerary without movement. A stop reports the search status: missing coverage or an
/// exhausted budget is not evidence that no route exists.
/// <para>
/// A stop is latched against the situation it was decided in: the walker's feet, presence revision and ground
/// locomotion; every other body whose feet lie within <see cref="NeighbourhoodCells"/> of the walker or the leg's
/// waypoint; terrain region revisions and support modes; structure identities and geometry; and, when advised from a world, the walker's movement
/// admission (encounter phase, readiness and pending consequences). While that situation is unchanged every call
/// returns the same stop without a query or a sample, so an unchanged world never cycles refused samples. A fresh
/// snapshot object, a world tick or a change elsewhere does not release it; a relevant change revalidates from the
/// actual state. The advice owns no authority and is never saved; after reopening, a new coordinator resumes from
/// the actor's recorded position.
/// </para>
/// </summary>
public sealed class CargoLegCoordinator
{
    /// <summary>
    /// Cells around the walker and its leg waypoint whose bodies are part of a stop's situation: twice the eight-cell
    /// authored leg, which covers the leg and the local detours its bounded search explores on open ground.
    /// </summary>
    public const int NeighbourhoodCells = 16;

    private readonly EntityId _actorId;
    private readonly IReadOnlyList<SubunitPosition> _waypoints;
    private readonly Queue<SubunitPosition> _path = new();
    private SubunitPosition? _expected;
    private SubunitPosition? _refusedAt;
    private (CargoLegAdvice Advice, LegSituation Situation)? _stop;
    private int _legIndex;

    public CargoLegCoordinator(EntityId actorId, IEnumerable<SubunitPosition> waypoints)
    {
        ArgumentNullException.ThrowIfNull(waypoints);
        _actorId = actorId;
        _waypoints = waypoints.ToArray();
        if (_waypoints.Count == 0)
        {
            throw new ArgumentException("An itinerary names at least one waypoint.", nameof(waypoints));
        }
    }

    public int LegIndex => _legIndex;
    public int LegCount => _waypoints.Count;
    public int PlannedLegs { get; private set; }
    public int Replans { get; private set; }

    /// <summary>Route queries made; a latched stop makes none.</summary>
    public int RouteQueries { get; private set; }

    /// <summary>Whether the last advice was a stop that is still latched to an unchanged situation.</summary>
    public bool IsLatched => _stop is not null;

    /// <summary>Advice from the walker's actual presence and movement admission in an admitted world.</summary>
    public CargoLegAdvice Advise(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var admission = new MovementAdmission(world.EncounterLifecycle?.Phase, WorldActorAvailability.CanAct(world, _actorId),
            world.PendingExcavation is not null || world.Care?.Pending is not null);
        return Advise(world.Space, admission);
    }

    /// <summary>Advice from the actor's actual presence in admitted local geometry alone.</summary>
    public CargoLegAdvice Advise(LocalSpatialSnapshot space)
    {
        ArgumentNullException.ThrowIfNull(space);
        return Advise(space, null);
    }

    private CargoLegAdvice Advise(LocalSpatialSnapshot space, MovementAdmission? admission)
    {
        SubunitPosition feet = space.GetActor(_actorId).Feet;
        while (_legIndex < _waypoints.Count && feet == _waypoints[_legIndex])
        {
            _legIndex++;
            _path.Clear();
            _stop = null;
        }
        if (_legIndex == _waypoints.Count)
        {
            _stop = null;
            return new CargoLegAdvice(CargoLegState.Arrived, _legIndex);
        }
        LegSituation situation = LegSituation.Of(space, _actorId, _waypoints[_legIndex], admission);
        if (_stop is { } stop)
        {
            if (stop.Situation.Matches(situation))
            {
                return stop.Advice;
            }
            // A relevant change: revalidate from the actual state as a fresh leg, not as a repeated refusal.
            _stop = null;
            _expected = null;
            _refusedAt = null;
            _path.Clear();
        }
        bool stale = _expected is { } expected && expected != feet;
        if (!stale)
        {
            _refusedAt = null;
        }
        while (!stale && _path.Count > 0 && _path.Peek() == feet)
        {
            _path.Dequeue();
        }
        if (_path.Count == 0 || stale)
        {
            if (stale && _refusedAt == feet)
            {
                // The same position refused the replanned step too: stop instead of repeating a refused sample.
                return Stop(new CargoLegAdvice(CargoLegState.Blocked, _legIndex, RouteStatus: GroundRouteStatus.Blocked),
                    situation);
            }
            if (stale)
            {
                Replans++;
                _refusedAt = feet;
            }
            RouteQueries++;
            GroundRouteResult route = space.QueryGroundRoute(_actorId, feet, _waypoints[_legIndex]);
            _path.Clear();
            if (route.Status != GroundRouteStatus.Reachable || route.Waypoints.Count == 0)
            {
                return Stop(new CargoLegAdvice(CargoLegState.Blocked, _legIndex, RouteStatus: route.Status), situation);
            }
            PlannedLegs++;
            foreach (SubunitPosition waypoint in route.Waypoints.Where(waypoint => waypoint != feet)) _path.Enqueue(waypoint);
            if (_path.Count == 0)
            {
                return Stop(new CargoLegAdvice(CargoLegState.Blocked, _legIndex, RouteStatus: route.Status), situation);
            }
        }
        SubunitPosition target = _path.Peek();
        long dx = Step(target.X - feet.X);
        long dy = dx == 0 ? Step(target.Y - feet.Y) : 0;
        _expected = feet.Offset(dx, dy, 0);
        return new CargoLegAdvice(CargoLegState.Moving, _legIndex, dx, dy);
    }

    private CargoLegAdvice Stop(CargoLegAdvice advice, LegSituation situation)
    {
        _expected = null;
        _path.Clear();
        _stop = (advice, situation);
        return advice;
    }

    private static long Step(long difference) =>
        difference == 0 ? 0 : Math.Sign(difference) * Math.Min(Math.Abs(difference), WorldMovementInput.DisplacementPerStep);

    /// <summary>Whether the walker may take a movement sample now, as the world and session decide it.</summary>
    private readonly record struct MovementAdmission(EncounterLifecyclePhase? Phase, bool CanAct, bool ConsequencesPending);

    /// <summary>The value facts a stop depends on; compared by value, never by snapshot reference or world tick.</summary>
    private sealed record LegSituation(SubunitPosition Feet, ulong Revision, bool CanLocomote, MovementAdmission? Admission,
        (EntityId Actor, SubunitPosition Feet, ulong Revision)[] Nearby, (TerrainRegionBounds Bounds, ulong Revision)[] Terrain,
        GroundSupportMode GroundSupport, StructureSupportMode StructureSupport, SpatialStructure[] Structures)
    {
        internal static LegSituation Of(LocalSpatialSnapshot space, EntityId actorId, SubunitPosition waypoint,
            MovementAdmission? admission)
        {
            ActorPresence walker = space.GetActor(actorId);
            long margin = (long)NeighbourhoodCells * SubunitPosition.SubunitsPerCell;
            long minimumX = Math.Min(walker.Feet.X, waypoint.X) - margin;
            long maximumX = Math.Max(walker.Feet.X, waypoint.X) + margin;
            long minimumY = Math.Min(walker.Feet.Y, waypoint.Y) - margin;
            long maximumY = Math.Max(walker.Feet.Y, waypoint.Y) + margin;
            var nearby = space.Actors.Where(actor => actor.ActorId != actorId
                    && actor.Feet.X >= minimumX && actor.Feet.X <= maximumX && actor.Feet.Y >= minimumY && actor.Feet.Y <= maximumY)
                .Select(actor => (actor.ActorId, actor.Feet, actor.Revision)).OrderBy(actor => actor.ActorId).ToArray();
            return new LegSituation(walker.Feet, walker.Revision,
                walker.Condition.CanLocomote(OpenFantasy.Core.Actors.LocomotionMode.Ground), admission, nearby,
                space.Regions.Select(region => (region.Bounds, region.Revision)).ToArray(),
                space.GroundSupport, space.StructureSupport, space.Structures.ToArray());
        }

        internal bool Matches(LegSituation other) => Feet == other.Feet && Revision == other.Revision
            && CanLocomote == other.CanLocomote && Admission == other.Admission && Nearby.SequenceEqual(other.Nearby)
            && Terrain.SequenceEqual(other.Terrain) && GroundSupport == other.GroundSupport
            && StructureSupport == other.StructureSupport && Structures.Length == other.Structures.Length
            && Structures.Zip(other.Structures).All(pair => pair.First.StructureId == pair.Second.StructureId
                && pair.First.Boxes.SequenceEqual(pair.Second.Boxes)
                && pair.First.BearingSurfaces.SequenceEqual(pair.Second.BearingSurfaces));
    }
}
