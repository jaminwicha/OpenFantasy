using Godot;

namespace OpenFantasy.Client.Presentation;

/// <summary>Interpolates only observed public positions; animation never submits or determines movement.</summary>
internal partial class FarmActorView : Node2D
{
    private static readonly int[] _walkColumns = [0, 1, 2, 1];
    private const double InterpolationSeconds = 0.05;
    private const double MotionGraceSeconds = 0.12;
    private Sprite2D _sprite = null!;
    private Label _nameplate = null!;
    private Vector2 _previous;
    private Vector2 _target;
    private double _interpolationAge;
    private double _motionAge = MotionGraceSeconds;
    private double _animationClock;
    private int _facingRow;
    private bool _ownActor;

    internal void Setup(FarmActorFrame actor, FarmVisualAssets assets)
    {
        TextureFilter = TextureFilterEnum.Nearest;
        _target = _previous = FarmCanvasCoordinates.Position(actor.Feet);
        Position = _target;
        _sprite = new Sprite2D
        {
            Texture = assets.ActorTexture(actor.SpriteKind), Hframes = 3, Vframes = 4, Frame = 1,
            Offset = new Vector2(0, -10), Scale = new Vector2(2, 2), TextureFilter = TextureFilterEnum.Nearest
        };
        AddChild(_sprite);
        _nameplate = new Label
        {
            Position = new Vector2(-80, -66), Size = new Vector2(160, 20),
            HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipText = true
        };
        _nameplate.AddThemeFontSizeOverride("font_size", 13);
        _nameplate.AddThemeColorOverride("font_outline_color", new Color("15261f"));
        _nameplate.AddThemeConstantOverride("outline_size", 4);
        AddChild(_nameplate);
        Present(actor, assets);
    }

    internal void Present(FarmActorFrame actor, FarmVisualAssets assets)
    {
        Vector2 next = FarmCanvasCoordinates.Position(actor.Feet);
        Vector2 motion = next - _target;
        if (motion != Vector2.Zero)
        {
            _facingRow = Math.Abs(motion.X) > Math.Abs(motion.Y)
                ? motion.X < 0 ? 2 : 3
                : motion.Y < 0 ? 1 : 0;
            _previous = Position;
            _target = next;
            _interpolationAge = 0;
            _motionAge = 0;
            if (_previous.DistanceSquaredTo(_target) > 160 * 160)
            {
                _previous = _target;
                Position = _target;
            }
        }
        _sprite.Texture = assets.ActorTexture(actor.SpriteKind);
        _nameplate.Text = actor.DisplayName;
        _nameplate.AddThemeColorOverride("font_color", actor.IsOwnActor ? new Color("bce9ff") : new Color("fff0ce"));
        _ownActor = actor.IsOwnActor;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _interpolationAge = Math.Min(_interpolationAge + delta, InterpolationSeconds);
        _motionAge = Math.Min(_motionAge + delta, MotionGraceSeconds);
        Position = _previous.Lerp(_target, (float)(_interpolationAge / InterpolationSeconds)).Round();
        int column = 1;
        if (_motionAge < MotionGraceSeconds)
        {
            _animationClock = (_animationClock + delta) % (4.0 / 7.0);
            column = _walkColumns[(int)(_animationClock * 7) % _walkColumns.Length];
        }
        _sprite.Frame = _facingRow * 3 + column;
    }

    public override void _Draw()
    {
        DrawSetTransform(Vector2.Zero, 0, new Vector2(1, 0.45f));
        DrawCircle(Vector2.Zero, 11, new Color(0.06f, 0.11f, 0.08f, 0.24f));
        if (_ownActor)
        {
            DrawArc(Vector2.Zero, 13, 0, Mathf.Tau, 32, new Color("90d8ee"), 1.5f);
        }
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }
}
