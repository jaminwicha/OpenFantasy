using Godot;
using OpenFantasy.Client.Presentation;
using OpenFantasy.Core;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>A bounded source-based farm view consuming only permitted public projection values.</summary>
public partial class FarmCanvas : Node2D
{
    private readonly Dictionary<EntityId, FarmActorView> _actors = [];
    private readonly Dictionary<EntityId, FarmGatherNodeView> _gatheringNodes = [];
    private readonly Dictionary<EntityId, FarmStructureView> _structures = [];
    private FarmCanvasFrame? _frame;
    private FarmVisualAssets _assets = null!;
    private Node2D _actorLayer = null!;
    private FarmDaylight _daylight = null!;
    private WorksiteView _worksite = null!;
    private WatercourseView _watercourse = null!;
    private FarmGroundView _ground = null!;
    private Cargo.CargoTerrainView _cargoTerrain = null!;
    private Cargo.CargoMerchantAppearances? _merchants;
    private bool _ready;
    private readonly FarmTargetMarker _targetMarker = new() { Name = "TargetMarker", ZIndex = 20, Visible = false };

    /// <summary>The farm grid's fixed extent, or the observed regions' extent in a cargo frame.</summary>
    public Rect2 LocalBounds => _frame?.Regional is { } regional
        ? new Rect2((float)regional.Bounds.X, (float)regional.Bounds.Y, (float)regional.Bounds.Width, (float)regional.Bounds.Height)
        : FarmCanvasCoordinates.LocalBounds;
    internal FarmDaylight Daylight => _daylight;
    public Vector2 FocusPosition => _frame is null ? new Vector2(176, 176)
        : Project(_frame.Actors.Single(actor => actor.IsOwnActor).Feet);

    /// <summary>The cargo frame's projection while one is presented; probes compare it with drawn positions.</summary>
    internal Cargo.CargoViewProjection? Regional => _frame?.Regional;

    /// <summary>The public feet one presented actor was drawn from in the current frame, or null when it is absent.</summary>
    internal SubunitPosition? PresentedFeet(EntityId actorId) =>
        _frame?.Actors.FirstOrDefault(actor => actor.ActorId == actorId)?.Feet;

    /// <summary>Where one presented actor is drawn (its projected target), or null when it is not presented.</summary>
    internal Vector2? DrawnActor(EntityId actorId) => _actors.TryGetValue(actorId, out FarmActorView? view) ? view.ProjectedTarget : null;

    /// <summary>The actor node's actual interpolated transform this frame, or null when it is not presented.</summary>
    internal Vector2? ActorTransform(EntityId actorId) => _actors.TryGetValue(actorId, out FarmActorView? view) ? view.Position : null;

    /// <summary>The sprite extent at an observed actor's projected target; absent actors cannot influence framing.</summary>
    internal Rect2? ProjectedActorSpriteBounds(EntityId actorId) =>
        _actors.TryGetValue(actorId, out FarmActorView? view) ? view.ProjectedSpriteBounds : null;

    /// <summary>The rendered actor's sprite quad, for post-draw visibility checks against the actual HUD.</summary>
    internal Rect2? ActorSpriteViewportBounds(EntityId actorId) =>
        _actors.TryGetValue(actorId, out FarmActorView? view) ? view.SpriteViewportBounds : null;

    /// <summary>The terrain the regional layer actually drew last, with its shape, revisions and column choices.</summary>
    internal Cargo.CargoDrawnTerrain? DrawnTerrain => _cargoTerrain.LastDrawn;

    /// <summary>The column-choice digest one observation implies, to compare with <see cref="DrawnTerrain"/>.</summary>
    internal string ExpectedTerrainDigest(Hosting.Simulation.CargoPresentation.LocalRegionObservation observation) =>
        _cargoTerrain.ExpectedDigest(observation);

    /// <summary>The target cue's canvas position while it is shown.</summary>
    internal Vector2? TargetCue => _targetMarker.Visible ? _targetMarker.Position : null;

    /// <summary>Whether the observed-ground layer, rather than the farm grid's fixed floor, is drawn.</summary>
    internal bool DrawsObservedGround => _cargoTerrain.Visible && !_ground.Visible;

    /// <summary>Uses the same explicit projection as actors, terrain, target cues and camera framing.</summary>
    public Vector2 Project(SubunitPosition feet)
    {
        if (_frame?.Regional is { } regional)
        {
            Cargo.CargoCanvasPoint point = regional.Project(feet);
            return new Vector2((float)point.X, (float)point.Y);
        }
        return _frame?.IsMultilevel == true ? WorksiteCoordinates.Position(feet) : FarmCanvasCoordinates.Position(feet);
    }

    /// <summary>Binds the retained merchants' source appearances; only the cargo world presents merchants.</summary>
    internal void BindMerchants(Cargo.CargoMerchantAppearances merchants)
    {
        _merchants = merchants;
        if (_assets is not null) _assets.Merchants = merchants;
    }

    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.Nearest;
        _daylight = GD.Load<PackedScene>("res://scenes/FarmDaylight.tscn").Instantiate<FarmDaylight>();
        AddChild(_daylight);
        _assets = new FarmVisualAssets { Merchants = _merchants };
        _ground = new FarmGroundView { Name = "Ground" };
        _ground.Setup(_assets);
        AddChild(_ground);
        _cargoTerrain = new Cargo.CargoTerrainView { Name = "CargoTerrain", Visible = false };
        _cargoTerrain.Setup(_assets);
        AddChild(_cargoTerrain);
        _actorLayer = new Node2D { Name = "Actors", YSortEnabled = true };
        AddChild(_actorLayer);
        _worksite = new WorksiteView(this, _actorLayer);
        _watercourse = new WatercourseView { Name = "Watercourse", ZIndex = 2 };
        _watercourse.Setup(_assets);
        AddChild(_watercourse);
        AddChild(_targetMarker);
        _ready = true;
        if (_frame is not null)
        {
            Present(_frame);
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            _assets?.Dispose();
        }
    }

    /// <summary>Call on the Godot main thread after the host has produced an authorized public projection.</summary>
    public void Present(FarmCanvasFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        _frame = frame;
        if (!_ready)
        {
            return;
        }
        _daylight.Present(frame.Calendar);
        // A cargo frame draws its observed ground; the farm grid's fixed source floor is drawn only without one.
        _ground.Visible = frame.Regional is null;
        _cargoTerrain.Present(frame.Regional, frame.RegionalWorkTargets.Select(target => target.Cell).ToArray());
        // Both public views may describe the same committed terrain. The liquid cutaway owns
        // that terrain layer when present; physical members retain their independent owner.
        _worksite.Present(frame.Worksite, frame.Actors.Single(actor => actor.IsOwnActor).Feet.Z,
            showTerrain: frame.Liquids is null);
        _watercourse.Present(frame.Liquids, frame.Calendar, frame.Actors, frame.Worksite);
        _targetMarker.ZIndex = frame.IsMultilevel ? 4_095 : 20;
        HashSet<EntityId> included = frame.Actors.Select(actor => actor.ActorId).ToHashSet();
        foreach (EntityId id in _actors.Keys.Where(id => !included.Contains(id)).ToArray())
        {
            _actors[id].QueueFree();
            _actors.Remove(id);
        }
        foreach (FarmActorFrame actor in frame.Actors)
        {
            if (!_actors.TryGetValue(actor.ActorId, out FarmActorView? view))
            {
                view = new FarmActorView { Name = "Actor_" + actor.ActorId };
                view.Setup(actor, _assets);
                _actorLayer.AddChild(view);
                _actors.Add(actor.ActorId, view);
            }
            else
            {
                view.Present(actor, _assets);
            }
        }
        PresentGatheringNodes(frame);
        PresentStructures(frame);
    }

    internal void PresentTarget(FarmTarget? target)
    {
        if (_frame is null)
        {
            // No presented frame yet (a first regional view may be unsupported): there is no projection to cue with.
            _targetMarker.Visible = false;
            return;
        }
        _targetMarker.ZIndex = _frame?.IsMultilevel == true ? 4_095 : 20;
        if (_frame?.Regional is { } regional && target?.Kind == FarmTargetKind.Excavation)
        {
            // The worked cell itself is cued on the observed floor, as on the worksite grid; never the worker's stance.
            LocalExcavationView? work = _frame.RegionalWorkTargets.FirstOrDefault(value => value.SiteId == target.Id);
            SubunitPosition? cue = work is null ? null : new SubunitPosition(checked(work.Cell.X * 256 + 128),
                checked(work.Cell.Y * 256 + 128), regional.Observation.FloorFeetZ);
            _targetMarker.Visible = cue is { } worked && regional.Admits(worked);
            if (_targetMarker.Visible) _targetMarker.Position = Project(cue!.Value);
            return;
        }
        // In a cargo frame a target outside the observed regions has no cue: it is not drawn anywhere plausible.
        _targetMarker.Visible = target?.Feet is { } observed && (_frame?.Regional?.Admits(observed) ?? true);
        if (!_targetMarker.Visible) return;
        if (target?.Feet is { } feet)
        {
            var site = target.Kind == FarmTargetKind.Excavation
                ? _frame?.Worksite?.ExcavationTargets.FirstOrDefault(value => value.SiteId == target.Id) : null;
            if (site is { ReachMode: ExcavationReachMode.RaisedBankTop })
            {
                // The raised stance remains the action anchor. The field cue identifies the
                // worked top/opening itself, rather than marking the worker's dry bank as ore.
                bool solid = _frame!.Worksite!.Terrain.Single(region => region.Bounds.Contains(site.Cell)).IsSolid(site.Cell);
                feet = new SubunitPosition(checked(site.Cell.X * 256 + 128), checked(site.Cell.Y * 256 + 128),
                    checked((site.Cell.Z + (solid ? 1 : 0)) * 256));
            }
            _targetMarker.Position = Project(feet);
        }
    }

    private void PresentStructures(FarmCanvasFrame frame)
    {
        // Excavation alone does not supply physical member appearances. Retain the ordinary
        // completed guard art until the view explicitly owns physical construction geometry.
        IEnumerable<FarmStructureFrame> structures = frame.Worksite?.HasPhysicalConstruction == true ? [] : frame.Structures;
        HashSet<EntityId> included = structures.Select(value => value.ProjectId).ToHashSet();
        foreach (EntityId identity in _structures.Keys.Where(identity => !included.Contains(identity)).ToArray())
        {
            _structures[identity].QueueFree();
            _structures.Remove(identity);
        }
        foreach (FarmStructureFrame structure in structures)
        {
            if (!_structures.TryGetValue(structure.ProjectId, out FarmStructureView? view))
            {
                view = GD.Load<PackedScene>("res://scenes/FarmStructureView.tscn").Instantiate<FarmStructureView>();
                view.Name = "Structure_" + structure.ProjectId;
                _actorLayer.AddChild(view);
                _structures.Add(structure.ProjectId, view);
            }
            view.Present(structure);
            view.ZIndex = frame.IsMultilevel ? WorksiteCoordinates.DepthIndex(structure.Feet.Z, actor: true) : 0;
        }
    }

    private void PresentGatheringNodes(FarmCanvasFrame frame)
    {
        HashSet<EntityId> included = frame.GatheringNodes.Select(node => node.NodeId).ToHashSet();
        foreach (EntityId id in _gatheringNodes.Keys.Where(id => !included.Contains(id)).ToArray())
        {
            _gatheringNodes[id].QueueFree();
            _gatheringNodes.Remove(id);
        }
        foreach (FarmGatherNodeFrame node in frame.GatheringNodes)
        {
            if (!_gatheringNodes.TryGetValue(node.NodeId, out FarmGatherNodeView? view))
            {
                view = new FarmGatherNodeView { Name = "Resource_" + node.NodeId };
                view.Setup(node, _assets);
                _actorLayer.AddChild(view);
                _gatheringNodes.Add(node.NodeId, view);
            }
            else
            {
                view.Present(node, _assets);
            }
            view.ZIndex = frame.IsMultilevel ? WorksiteCoordinates.DepthIndex(node.Feet.Z, actor: true) : 0;
        }
    }
}
