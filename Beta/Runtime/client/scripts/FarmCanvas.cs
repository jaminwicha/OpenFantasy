using Godot;
using OpenFantasy.Client.Presentation;
using OpenFantasy.Core;
using OpenFantasy.Core.Spatial;

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
    private bool _ready;
    private readonly FarmTargetMarker _targetMarker = new() { Name = "TargetMarker", ZIndex = 20, Visible = false };

    public Rect2 LocalBounds => FarmCanvasCoordinates.LocalBounds;
    internal FarmDaylight Daylight => _daylight;
    public Vector2 FocusPosition => _frame is null ? new Vector2(176, 176)
        : Project(_frame.Actors.Single(actor => actor.IsOwnActor).Feet);

    /// <summary>Uses the same explicit projection as actors, terrain, target cues and camera framing.</summary>
    public Vector2 Project(SubunitPosition feet) => _frame?.Worksite is not null
        ? WorksiteCoordinates.Position(feet) : FarmCanvasCoordinates.Position(feet);

    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.Nearest;
        _daylight = GD.Load<PackedScene>("res://scenes/FarmDaylight.tscn").Instantiate<FarmDaylight>();
        AddChild(_daylight);
        _assets = new FarmVisualAssets();
        var ground = new FarmGroundView { Name = "Ground" };
        ground.Setup(_assets);
        AddChild(ground);
        _actorLayer = new Node2D { Name = "Actors", YSortEnabled = true };
        AddChild(_actorLayer);
        _worksite = new WorksiteView(this, _actorLayer);
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
        _worksite.Present(frame.Worksite, frame.Actors.Single(actor => actor.IsOwnActor).Feet.Z);
        _targetMarker.ZIndex = frame.Worksite is null ? 20 : 4_095;
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
        _targetMarker.ZIndex = _frame?.Worksite is null ? 20 : 4_095;
        _targetMarker.Visible = target?.Feet is not null;
        if (target?.Feet is { } feet)
        {
            _targetMarker.Position = Project(feet);
        }
    }

    private void PresentStructures(FarmCanvasFrame frame)
    {
        // The source sapling-guard sprite remains correct only for the legacy farm profile.
        IEnumerable<FarmStructureFrame> structures = frame.Worksite is null ? frame.Structures : [];
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
            view.ZIndex = frame.Worksite is null ? 0 : WorksiteCoordinates.DepthIndex(node.Feet.Z, actor: true);
        }
    }
}
