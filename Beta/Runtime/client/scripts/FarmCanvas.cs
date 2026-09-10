using Godot;
using OpenFantasy.Client.Presentation;
using OpenFantasy.Core;

namespace OpenFantasy.Client;

/// <summary>A bounded source-based farm view consuming only permitted public projection values.</summary>
public partial class FarmCanvas : Node2D
{
    private readonly Dictionary<EntityId, FarmActorView> _actors = [];
    private FarmCanvasFrame? _frame;
    private FarmVisualAssets _assets = null!;
    private Node2D _actorLayer = null!;
    private bool _ready;

    public Rect2 LocalBounds => FarmCanvasCoordinates.LocalBounds;
    public Vector2 FocusPosition => _frame is null ? new Vector2(176, 176)
        : FarmCanvasCoordinates.Position(_frame.Actors.Single(actor => actor.IsOwnActor).Feet);

    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.Nearest;
        _assets = new FarmVisualAssets();
        var ground = new FarmGroundView { Name = "Ground" };
        ground.Setup(_assets);
        AddChild(ground);
        _actorLayer = new Node2D { Name = "Actors", YSortEnabled = true };
        AddChild(_actorLayer);
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
    }
}
