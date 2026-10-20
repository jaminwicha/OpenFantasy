using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Client.Presentation.Audio;

/// <summary>
/// Derives transient sound cues from consecutive permitted observations of one listener.
/// The first observation of a listener visit, a newly visible source and a relocation establish baselines only, so
/// opening, reopening, reconnecting or switching the presented character never replays earlier actions. Each cue key
/// is delivered at most once per visit, and at most one cue per source is derived from one observation, so a delayed
/// frame cannot release a burst of old sounds.
/// </summary>
internal sealed class SoundscapeCueDeriver
{
    /// <summary>Horizontal travel per footstep. Presentation tuning validated by listening, not a simulation rule.</summary>
    internal const long StrideSubunits = 192;

    /// <summary>Travel in one observation beyond this is a relocation or region change, never a run of footsteps.</summary>
    internal const long RelocationSubunits = 4L * SubunitPosition.SubunitsPerCell;

    /// <summary>Bound on remembered delivered keys; older keys can no longer recur because sources only advance.</summary>
    internal const int DeliveredKeyCapacity = 512;

    private readonly Dictionary<EntityId, ActorTrack> _actors = [];
    private readonly Dictionary<EntityId, SoundscapeGatherNode> _nodes = [];
    private readonly Dictionary<(EntityId, SoundscapeWorkKind), SoundscapeWorkSite> _sites = [];
    private readonly HashSet<SoundCueKey> _delivered = [];
    private readonly Queue<SoundCueKey> _deliveryOrder = new();
    private SoundscapeListener? _listener;

    /// <summary>The listener whose visit is in progress, or null before the first observation or after a reset.</summary>
    internal SoundscapeListener? Listener => _listener;

    /// <summary>Ends the current visit. The next observation is a baseline and derives no cues.</summary>
    internal void Reset()
    {
        _listener = null;
        _actors.Clear();
        _nodes.Clear();
        _sites.Clear();
        _delivered.Clear();
        _deliveryOrder.Clear();
    }

    internal IReadOnlyList<SoundCue> Observe(SoundscapeObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (_listener != observation.Listener)
        {
            Reset();
            _listener = observation.Listener;
            Baseline(observation);
            return [];
        }
        var cues = new List<SoundCue>();
        ObserveActors(observation, cues);
        ObserveGatherNodes(observation, cues);
        ObserveWorkSites(observation, cues);
        return cues.AsReadOnly();
    }

    private void Baseline(SoundscapeObservation observation)
    {
        foreach (SoundscapeActor actor in observation.Actors) _actors[actor.ActorId] = new ActorTrack(actor);
        foreach (SoundscapeGatherNode node in observation.GatherNodes) _nodes[node.NodeId] = node;
        foreach (SoundscapeWorkSite site in observation.WorkSites) _sites[(site.SiteId, site.Kind)] = site;
    }

    private void ObserveActors(SoundscapeObservation observation, List<SoundCue> cues)
    {
        var visible = new HashSet<EntityId>();
        foreach (SoundscapeActor actor in observation.Actors)
        {
            visible.Add(actor.ActorId);
            if (!_actors.TryGetValue(actor.ActorId, out ActorTrack? track))
            {
                _actors[actor.ActorId] = new ActorTrack(actor);
                continue;
            }
            if (track.Advance(actor) is { } stride)
                Deliver(cues, new SoundCue(new SoundCueKey(SoundCueKind.Footstep, actor.ActorId, actor.PresenceRevision,
                    stride), SoundMaterial.Ground, actor.Feet));
        }
        // An actor leaving the view loses its track; reappearing is a new baseline rather than a walk from afar.
        foreach (EntityId gone in _actors.Keys.Where(id => !visible.Contains(id)).ToList()) _actors.Remove(gone);
    }

    private void ObserveGatherNodes(SoundscapeObservation observation, List<SoundCue> cues)
    {
        var visible = new HashSet<EntityId>();
        foreach (SoundscapeGatherNode node in observation.GatherNodes)
        {
            visible.Add(node.NodeId);
            bool struck = _nodes.TryGetValue(node.NodeId, out SoundscapeGatherNode before)
                && node.Kind == before.Kind && node.RemainingSwings < before.RemainingSwings;
            _nodes[node.NodeId] = node;
            // Regrowth raises remaining swings; it is not a contact and makes no sound here.
            if (struck)
                Deliver(cues, new SoundCue(new SoundCueKey(SoundCueKind.GatherContact, node.NodeId, node.Revision,
                    node.RemainingSwings), MaterialOf(node.Kind), node.Feet));
        }
        foreach (EntityId gone in _nodes.Keys.Where(id => !visible.Contains(id)).ToList()) _nodes.Remove(gone);
    }

    private void ObserveWorkSites(SoundscapeObservation observation, List<SoundCue> cues)
    {
        var visible = new HashSet<(EntityId, SoundscapeWorkKind)>();
        foreach (SoundscapeWorkSite site in observation.WorkSites)
        {
            var key = (site.SiteId, site.Kind);
            visible.Add(key);
            bool worked = _sites.TryGetValue(key, out SoundscapeWorkSite before) && site.CompletedWork > before.CompletedWork;
            _sites[key] = site;
            if (!worked) continue;
            (SoundCueKind kind, SoundMaterial material) = site.Kind == SoundscapeWorkKind.Construction
                ? (SoundCueKind.ConstructionContact, SoundMaterial.Joinery)
                : (SoundCueKind.ExcavationContact, SoundMaterial.Earth);
            Deliver(cues, new SoundCue(new SoundCueKey(kind, site.SiteId, site.CompletedWork, 0), material, site.Origin));
        }
        foreach (var gone in _sites.Keys.Where(key => !visible.Contains(key)).ToList()) _sites.Remove(gone);
    }

    private void Deliver(List<SoundCue> cues, SoundCue cue)
    {
        if (!_delivered.Add(cue.Key)) return;
        _deliveryOrder.Enqueue(cue.Key);
        while (_deliveryOrder.Count > DeliveredKeyCapacity) _delivered.Remove(_deliveryOrder.Dequeue());
        cues.Add(cue);
    }

    private static SoundMaterial MaterialOf(GatherNodeKind kind) => kind switch
    {
        GatherNodeKind.Oak => SoundMaterial.Oak,
        GatherNodeKind.Berry => SoundMaterial.Berry,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "A gathering kind has no declared contact material."),
    };

    /// <summary>Accumulated walking distance of one visible actor since its last footstep.</summary>
    private sealed class ActorTrack(SoundscapeActor baseline)
    {
        private SoundscapeActor _last = baseline;
        private double _carried;
        private ulong _strides;

        /// <summary>Returns the ordinal of a completed stride, or null when this observation completes none.</summary>
        internal ulong? Advance(SoundscapeActor actor)
        {
            SoundscapeActor before = _last;
            _last = actor;
            if (actor.PresenceRevision == before.PresenceRevision) return null;
            double dx = actor.Feet.X - before.Feet.X;
            double dy = actor.Feet.Y - before.Feet.Y;
            double travel = Math.Sqrt(dx * dx + dy * dy);
            if (travel > RelocationSubunits)
            {
                _carried = 0;
                return null;
            }
            // An incapacitated body that is moved is not walking.
            if (actor.IsIncapacitated || before.IsIncapacitated) return null;
            _carried += travel;
            if (_carried < StrideSubunits) return null;
            _carried %= StrideSubunits;
            return ++_strides;
        }
    }
}
