using Godot;
using OpenFantasy.Core.Gathering;

namespace OpenFantasy.Client.Presentation;

/// <summary>Displays observed resource state using retained source sheets; animation grants no harvest or renewal.</summary>
internal partial class FarmGatherNodeView : Node2D
{
    private static readonly int[] _idleColumns = [0, 1, 2, 1];
    private const double IdlePhasesPerSecond = 2.5;
    private Sprite2D _sprite = null!;
    private double _animationClock;

    /// <summary>The current rendered sprite quad, including its node scale and canvas transform.</summary>
    internal Rect2? SpriteViewportBounds => IsVisibleInTree() && _sprite is not null
        ? _sprite.GetGlobalTransformWithCanvas() * _sprite.GetRect() : null;

    internal void Setup(FarmGatherNodeFrame frame, FarmVisualAssets assets) =>
        Setup(frame, assets, FarmCanvasCoordinates.Position(frame.Feet));

    internal void Setup(FarmGatherNodeFrame frame, FarmVisualAssets assets, Vector2 position)
    {
        TextureFilter = TextureFilterEnum.Nearest;
        _sprite = new Sprite2D
        {
            Texture = assets.GatherNodeTexture(frame.RequiredToolKind), Hframes = 3, Vframes = 4,
            Frame = 1, Offset = new Vector2(0, -10), Scale = new Vector2(2, 2),
            TextureFilter = TextureFilterEnum.Nearest
        };
        AddChild(_sprite);
        Present(frame, assets, position);
    }

    internal void Present(FarmGatherNodeFrame frame, FarmVisualAssets assets) =>
        Present(frame, assets, FarmCanvasCoordinates.Position(frame.Feet));

    internal void Present(FarmGatherNodeFrame frame, FarmVisualAssets assets, Vector2 position)
    {
        Position = position;
        _sprite.Texture = assets.GatherNodeTexture(frame.RequiredToolKind);
        Visible = frame.Visibility != GatherNodeVisibility.Absent;
        // The predecessor uses the same dimmed sheet for the brief depleted interval, not a new stump asset.
        Modulate = frame.Visibility == GatherNodeVisibility.Ready ? Colors.White : new Color(0.6f, 0.6f, 0.7f, 0.45f);
    }

    public override void _Process(double delta)
    {
        _animationClock = (_animationClock + delta) % (_idleColumns.Length / IdlePhasesPerSecond);
        _sprite.Frame = _idleColumns[(int)(_animationClock * IdlePhasesPerSecond) % _idleColumns.Length];
    }
}
