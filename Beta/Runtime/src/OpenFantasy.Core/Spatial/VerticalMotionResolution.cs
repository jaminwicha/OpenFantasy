using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Spatial;

/// <summary>
/// Immutable common-gravity continuation with separate first-contact arrests. A unit performs one
/// collider probe, actual-prefix sweep or complete base-footing query. Catalog preparation, canonical
/// hashing, bounded copies and event closure are unsliced; the work bound is not a latency guarantee.
/// Provisional paths and completed event prefixes confer no movement, damage or publication authority.
/// </summary>
public sealed class VerticalMotionResolution
{
    public const int DefaultWorkBudget = 16;
    public const int MaximumWorkBudget = 256;
    private readonly VerticalMotionScene _scene;
    private readonly Progress _progress;

    private VerticalMotionResolution(VerticalMotionScene scene, Progress progress)
    {
        _scene = scene;
        _progress = progress;
        Events = Array.AsReadOnly(progress.Events.ToArray());
    }

    public ContentHash InputPin => _scene.InputPin;
    public int MaximumTotalWorkUnits => _scene.MaximumTotalWorkUnits;
    public ulong CompletedWorkUnits => _progress.CompletedWorkUnits;
    public bool IsComplete => _progress.Phase is Phase.Complete or Phase.Failed;
    public bool IsFailed => _progress.Phase == Phase.Failed;
    public string? FailureReason => _progress.FailureReason;
    public VerticalDescentStatus? FailureStatus => _progress.FailureStatus;
    public ReadOnlyCollection<VerticalMotionEvent> Events { get; }
    public VerticalMotionResult Result => _progress.Result
        ?? throw new InvalidOperationException("Only a complete successful motion calculation has final geometry.");

    public static VerticalMotionResolution Begin(VerticalMotionScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var progress = new Progress(scene);
        progress.PrepareRound(scene);
        return new(scene, progress);
    }

    /// <summary>Executes actual forward geometric work without replaying any previously completed unit.</summary>
    public VerticalMotionResolution Advance(int workBudget = DefaultWorkBudget)
    {
        if (workBudget is < 1 or > MaximumWorkBudget)
        {
            throw new ArgumentOutOfRangeException(nameof(workBudget));
        }
        if (IsComplete) { return this; }
        var next = new Progress(_progress);
        for (int unit = 0; unit < workBudget && next.Phase is not Phase.Complete and not Phase.Failed; unit++)
        {
            if (next.CompletedWorkUnits >= (ulong)MaximumTotalWorkUnits)
            {
                throw new InvalidOperationException("Vertical motion exceeded its finite geometric work bound.");
            }
            next.CompletedWorkUnits++;
            try
            {
                next.PerformUnit(_scene);
                next.Normalize(_scene);
            }
            catch (OverflowException error)
            {
                next.Fail(VerticalDescentStatus.NumericLimit, error.Message);
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException)
            {
                next.Fail(VerticalDescentStatus.Unavailable, error.Message);
            }
        }
        return new(_scene, next);
    }

    private enum Phase { Probes, Sweeps, Support, Complete, Failed }
    private sealed record ColliderWork(int BodyIndex, SubunitBox Box);

    /// <summary>Private mutable working copy, never shared between advancing immutable resolutions.</summary>
    private sealed class Progress
    {
        internal Phase Phase;
        internal ulong CompletedWorkUnits;
        internal readonly long[] Drops;
        internal readonly bool[] Stopped;
        internal readonly List<VerticalMotionEvent> Events;
        internal VerticalMotionResult? Result;
        internal string? FailureReason;
        internal VerticalDescentStatus? FailureStatus;
        private long _distance;
        private long _increment;
        private int _cursor;
        private ColliderWork[] _work = [];
        private VerticalDescentResult[] _probes = [];
        private int[] _stopping = [];
        private SpatialStructure[] _supports = [];
        private VerticalDescentQuery? _query;
        private readonly TerrainVolumeQueries _terrain;

        internal Progress(VerticalMotionScene scene)
        {
            Drops = new long[scene.MovingBodies.Count];
            Stopped = new bool[scene.MovingBodies.Count];
            Events = [];
            _terrain = new TerrainVolumeQueries(scene.Regions);
        }

        internal Progress(Progress previous)
        {
            Phase = previous.Phase;
            CompletedWorkUnits = previous.CompletedWorkUnits;
            Drops = (long[])previous.Drops.Clone();
            Stopped = (bool[])previous.Stopped.Clone();
            Events = new(previous.Events);
            Result = previous.Result;
            FailureReason = previous.FailureReason;
            FailureStatus = previous.FailureStatus;
            _distance = previous._distance;
            _increment = previous._increment;
            _cursor = previous._cursor;
            _work = previous._work;
            _probes = (VerticalDescentResult[])previous._probes.Clone();
            _stopping = previous._stopping;
            _supports = previous._supports;
            _query = previous._query;
            _terrain = previous._terrain;
        }

        internal void Fail(VerticalDescentStatus status, string reason)
        {
            Phase = Phase.Failed;
            FailureStatus = status;
            FailureReason = reason;
            Result = null;
        }

        internal void PrepareRound(VerticalMotionScene scene)
        {
            if (Stopped.All(value => value))
            {
                Result = new(scene, Drops, Events);
                Phase = Phase.Complete;
                return;
            }
            _query = new(scene.Scope, scene.Regions,
                scene.StationaryStructures.Concat(StoppedBodies(scene).Where(body => body.Body.Structure is not null)
                    .Select(body => body.Body.StructureAt(body.Drop))),
                scene.StationaryActors.Concat(StoppedBodies(scene).Where(body => body.Body.Actor is not null)
                    .Select(body => body.Body.ActorAt(body.Drop))));
            _work = scene.MovingBodies.SelectMany((body, index) => Stopped[index] ? []
                : body.Boxes.Select(box => new ColliderWork(index, VerticalMotionBody.At(box, Drops[index])))).ToArray();
            _probes = new VerticalDescentResult[_work.Length];
            _cursor = 0;
            Phase = Phase.Probes;
        }

        internal void PerformUnit(VerticalMotionScene scene)
        {
            ColliderWork work = _work[_cursor];
            switch (Phase)
            {
                case Phase.Probes:
                    // At the horizon, a one-subunit probe can still discover immediate zero contact;
                    // later contacts are ignored and never credited beyond the original finite horizon.
                    _probes[_cursor] = _query!.QueryBox(work.Box, Math.Max(1, scene.MaximumDropSubunits - _distance));
                    break;
                case Phase.Sweeps:
                    ValidatePrefix(_query!.QueryBox(work.Box, _increment));
                    break;
                case Phase.Support:
                    EntityId owner = scene.MovingBodies[work.BodyIndex].BodyId;
                    SpatialOccupancy footing = FootingQuery.Query(_terrain,
                        _supports.Where(structure => structure.StructureId != owner).ToArray(), work.Box.Volume);
                    if (footing != SpatialOccupancy.Clear)
                    {
                        Fail(footing == SpatialOccupancy.Unavailable ? VerticalDescentStatus.Unavailable
                            : VerticalDescentStatus.IncompleteSupport,
                            "The stopped body lacks known complete same-plane bearing beneath every lowest collider.");
                    }
                    break;
                default:
                    throw new InvalidOperationException("Terminal motion cannot perform geometric work.");
            }
            _cursor++;
        }

        internal void Normalize(VerticalMotionScene scene)
        {
            if (Phase == Phase.Failed || _cursor != _work.Length) { return; }
            if (Phase == Phase.Probes)
            {
                SelectEvent(scene);
                if (Phase == Phase.Failed) { return; }
                if (_increment > 0)
                {
                    Phase = Phase.Sweeps;
                    _cursor = 0;
                    return;
                }
                PrepareSupport(scene);
                return;
            }
            if (Phase == Phase.Sweeps)
            {
                PrepareSupport(scene);
                return;
            }
            if (Phase == Phase.Support)
            {
                foreach (int index in _stopping) { Stopped[index] = true; }
                Events.Add(new(_distance, _stopping.Select(index => scene.MovingBodies[index].BodyId)));
                PrepareRound(scene);
            }
        }

        private void SelectEvent(VerticalMotionScene scene)
        {
            long remaining = scene.MaximumDropSubunits - _distance;
            int[] contacts = Enumerable.Range(0, _probes.Length).Where(index =>
                (_probes[index].Status is VerticalDescentStatus.Landed or VerticalDescentStatus.IncompleteSupport
                    or VerticalDescentStatus.ActorContact) && _probes[index].DropSubunits <= remaining).ToArray();
            if (contacts.Length == 0)
            {
                VerticalDescentStatus status = _probes.Any(probe => probe.Status == VerticalDescentStatus.Unavailable)
                    ? VerticalDescentStatus.Unavailable
                    : _probes.Any(probe => probe.Status == VerticalDescentStatus.NumericLimit)
                        ? VerticalDescentStatus.NumericLimit : VerticalDescentStatus.NoContactWithinLimit;
                Fail(status, "No complete next contact event is available within the known finite horizon.");
                return;
            }
            _increment = contacts.Min(index => _probes[index].DropSubunits);
            int[] first = contacts.Where(index => _probes[index].DropSubunits == _increment).ToArray();
            if (first.Any(index => _probes[index].Status == VerticalDescentStatus.ActorContact))
            {
                Fail(VerticalDescentStatus.ActorContact, "An actor intercepts the next descent; actors cannot act as floors.");
                return;
            }
            HashSet<int> stopping = first.Select(index => _work[index].BodyIndex).ToHashSet();
            Queue<int> queue = new(stopping.Order());
            while (queue.TryDequeue(out int index))
            {
                foreach (int dependent in scene.Dependents(index))
                {
                    if (!Stopped[dependent] && stopping.Add(dependent)) { queue.Enqueue(dependent); }
                }
            }
            _stopping = stopping.Order().ToArray();
        }

        private void ValidatePrefix(VerticalDescentResult sweep)
        {
            if ((sweep.Status is VerticalDescentStatus.Landed or VerticalDescentStatus.IncompleteSupport
                or VerticalDescentStatus.NoContactWithinLimit) && sweep.DropSubunits == _increment)
            {
                return;
            }
            Fail(sweep.Status, "The actual event prefix encounters unknown geometry, an actor or an earlier obstruction.");
        }

        private void PrepareSupport(VerticalMotionScene scene)
        {
            _distance = checked(_distance + _increment);
            for (int index = 0; index < Drops.Length; index++)
            {
                if (!Stopped[index]) { Drops[index] = _distance; }
            }
            HashSet<int> supports = _stopping.ToHashSet();
            _supports = scene.StationaryStructures.Concat(scene.MovingBodies.Select((body, index) => (Body: body, Index: index))
                .Where(value => value.Body.Structure is not null && (Stopped[value.Index] || supports.Contains(value.Index)))
                .Select(value => value.Body.StructureAt(Drops[value.Index])))
                // A conservative collider top cannot substitute for an exact physical bearing plane.
                .Select(structure => new SpatialStructure(structure.StructureId, structure.Boxes,
                    structure.BearingSurfaces.Where(surface => surface.TopTicks
                        == (Int128)surface.StandingZ * StructureBearingSurface.TicksPerSubunit))).ToArray();
            _work = _stopping.SelectMany(index => scene.MovingBodies[index].Bases.Select(box =>
                new ColliderWork(index, VerticalMotionBody.At(box, Drops[index])))).ToArray();
            _cursor = 0;
            Phase = Phase.Support;
        }

        private IEnumerable<(VerticalMotionBody Body, long Drop)> StoppedBodies(VerticalMotionScene scene) =>
            scene.MovingBodies.Select((body, index) => (Body: body, Index: index)).Where(value => Stopped[value.Index])
                .Select(value => (value.Body, Drops[value.Index]));
    }
}
