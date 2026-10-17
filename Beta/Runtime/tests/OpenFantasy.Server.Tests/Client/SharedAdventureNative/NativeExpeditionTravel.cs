using System.Globalization;
using OpenFantasy.Core;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;

namespace OpenFantasy.Server.Tests;

/// <summary>Read-only route advice to native held-key input; every movement is still decided by the host.</summary>
internal sealed class NativeExpeditionTravel(WorldSnapshot basis, IReadOnlyDictionary<EntityId, NativeRosterProcess> peers,
    EntityId observer, Func<Task> observe, CancellationToken token)
{
    private const long Envelope = 4 * WorldMovementInput.DisplacementPerStep;
    internal SubunitPosition Feet(EntityId actor) => peers[observer].View.Actors.Single(row => row.ActorId == actor).Feet;

    internal async Task Lane(EntityId actor, params CellCoordinate[] corners)
    {
        foreach (var target in CargoCorridor.Lane(corners).Select(CargoCorridor.FeetAt)) await Move(actor, target);
    }

    internal async Task Move(EntityId actor, SubunitPosition target)
    {
        await MoveTogether([(actor, target)]);
    }

    internal async Task Lanes(params (EntityId Actor, CellCoordinate[] Corners)[] routes)
    {
        var paths = routes.Select(row => (row.Actor, Points: CargoCorridor.Lane(row.Corners).Select(CargoCorridor.FeetAt).ToArray())).ToArray();
        for (int index = 0; index < paths.Max(row => row.Points.Length); index++)
            await MoveTogether(paths.Where(row => index < row.Points.Length).Select(row => (row.Actor, row.Points[index])).ToArray());
    }

    private async Task MoveTogether((EntityId Actor, SubunitPosition Target)[] targets)
    {
        for (int iteration = 0; targets.Any(row => Distance(Feet(row.Actor), row.Target) > Envelope); iteration++)
        {
            token.ThrowIfCancellationRequested();
            Assert.True(iteration < 96, "Native travel exceeded its bounded route legs.");
            var space = basis.Space.WithActors(basis.Space.Actors.Select(original =>
            {
                var row = peers[observer].View.Actors.Single(value => value.ActorId == original.ActorId);
                return new ActorPresence(original.Binding, original.Condition, original.Scope, row.Feet,
                    row.PresenceRevision, original.HomePlaceId);
            }));
            var legs = targets.Where(row => Distance(Feet(row.Actor), row.Target) > Envelope)
                .Select(row => (row.Actor, row.Target, Before: RouteLength(space, row.Actor, Feet(row.Actor), row.Target),
                    Next: NextLeg(space, row.Actor, row.Target))).ToArray();
            await Task.WhenAll(legs.Select(row => peers[row.Actor].Send("walk-to", Coordinates(row.Next))));
            await observe();
            foreach (var row in legs)
            {
                // The host owns the actual position; an advisory waypoint is never installed as state.
                Assert.True(Distance(Feet(row.Actor), row.Target) <= Envelope
                    || RouteLength(space, row.Actor, Feet(row.Actor), row.Target) < row.Before,
                    "Native travel did not make actual route progress.");
            }
        }
    }

    internal static SubunitPosition NextLeg(LocalSpatialSnapshot space, EntityId actor, SubunitPosition target)
    {
        var original = space.GetActor(actor);
        long remaining = RouteLength(space, actor, original.Feet, target);
        Assert.NotEqual(long.MaxValue, remaining);
        var choices = new List<(SubunitPosition End, long Remaining)>();
        foreach (var direction in new (long X, long Y)[] { (-32, 0), (0, -32), (0, 32), (32, 0) })
        {
            bool improves = Enumerable.Range(1, 4).All(sample =>
                space.PrepareMove(actor, original.Revision, direction.X * sample, direction.Y * sample).Status == SpatialMoveStatus.Prepared
                && RouteLength(space, actor, original.Feet.Offset(direction.X * sample, direction.Y * sample, 0), target) < remaining);
            if (!improves) continue;
            var walked = space;
            var safe = new List<SubunitPosition>();
            for (int sample = 0; sample < 68; sample++)
            {
                var movement = walked.PrepareMove(actor, walked.GetActor(actor).Revision, direction.X, direction.Y);
                if (movement.Status != SpatialMoveStatus.Prepared) break;
                var candidate = movement.Candidate!;
                safe.Add(candidate.Feet);
                walked = walked.WithActors(walked.Actors.Select(row => row.ActorId == actor ? candidate : row));
            }
            var costs = safe.Select(point => RouteLength(space, actor, point, target)).ToArray();
            for (int samples = 1; samples <= Math.Min(64, safe.Count - 4); samples++)
            {
                var end = safe[samples - 1];
                int earliest = Math.Max(0, samples - 4);
                // Input can release up to three samples early; its final four-sample envelope stays unchanged.
                long worst = costs.Skip(earliest).Take(samples + 4 - earliest).Max();
                if (worst < remaining) choices.Add((end, worst));
            }
        }
        Assert.NotEmpty(choices);
        return choices.OrderBy(row => row.Remaining).First().End;
    }

    private static long RouteLength(LocalSpatialSnapshot space, EntityId actor, SubunitPosition from, SubunitPosition target)
    {
        var original = space.GetActor(actor);
        var placed = space.WithActors(space.Actors.Select(row => row.ActorId == actor
            ? new ActorPresence(original.Binding, original.Condition, original.Scope, from, original.Revision, original.HomePlaceId) : row));
        var route = placed.QueryGroundRoute(actor, target);
        if (route.Status != GroundRouteStatus.Reachable) return long.MaxValue;
        long length = 0;
        foreach (var point in route.Waypoints) { length = checked(length + Distance(from, point)); from = point; }
        return length;
    }

    private static long Distance(SubunitPosition a, SubunitPosition b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    private static string Coordinates(SubunitPosition target) => string.Create(CultureInfo.InvariantCulture, $"{target.X},{target.Y},{target.Z}");

    internal async Task Send(EntityId actor, SubunitPosition target, bool allowEncounter)
    {
        string coordinates = Coordinates(target);
        await peers[actor].Send(allowEncounter ? "walk-to-threat" : "walk-to", coordinates);
        await observe();
    }

    internal static CellCoordinate Cell(SubunitPosition feet) => new(feet.Cell.X, feet.Cell.Y, feet.Cell.Z - 1);
}
