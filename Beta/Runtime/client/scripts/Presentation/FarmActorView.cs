using Godot;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Client.Presentation;

/// <summary>Interpolates only observed public positions; animation never submits or determines movement.</summary>
internal partial class FarmActorView : Node2D
{
    private static readonly int[] _walkColumns = [0, 1, 2, 1];
    private const double InterpolationSeconds = 0.05;
    private const double MotionGraceSeconds = 0.12;
    private Sprite2D _sprite = null!;
    private Label _nameplate = null!;
    private Label _healthDelta = null!;
    private readonly FarmHealthFeedback _health = new();
    private bool _incapacitated;
    private Vector2 _previous;
    private Vector2 _target;
    private double _interpolationAge;
    private double _motionAge = MotionGraceSeconds;
    private double _animationClock;
    private int _facingRow;
    private bool _ownActor;
    private SubunitPosition _observedFeet;

    /// <summary>The canvas position this view was last told to show; interpolation only eases towards it.</summary>
    internal Vector2 ProjectedTarget => _target;

    /// <summary>The sprite's stable bounds at its observed target, excluding transient damage motion.</summary>
    internal Rect2 ProjectedSpriteBounds
    {
        get
        {
            Rect2 sprite = _sprite.GetRect();
            return new Rect2(_target + sprite.Position * _sprite.Scale, sprite.Size * _sprite.Scale);
        }
    }

    /// <summary>The actual sprite quad in viewport coordinates, including interpolation and camera framing.</summary>
    internal Rect2 SpriteViewportBounds => _sprite.GetGlobalTransformWithCanvas() * _sprite.GetRect();

    internal void Setup(FarmActorFrame actor, FarmVisualAssets assets)
    {
        TextureFilter = TextureFilterEnum.Nearest;
        _target = _previous = Project(actor);
        _observedFeet = actor.Feet;
        Position = _target;
        _sprite = new Sprite2D
        {
            Texture = assets.ActorTexture(actor.SpriteKind, actor.AppearanceKey), Hframes = 3, Vframes = 4, Frame = 1,
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
        _healthDelta = new Label { Name = "HealthDelta", Position = new Vector2(-30, -88), Size = new Vector2(60, 24),
            HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _healthDelta.AddThemeColorOverride("font_outline_color", new Color("251416"));
        _healthDelta.AddThemeConstantOverride("outline_size", 4);
        AddChild(_healthDelta);
        Present(actor, assets);
    }

    internal void Present(FarmActorFrame actor, FarmVisualAssets assets)
    {
        Vector2 next = Project(actor);
        Vector2 motion = next - _target;
        // A committed vertical displacement is not a north/south walking command. World
        // movement can also project to zero pixels when elevation cancels screen Y.
        Vector2 walking = actor.IsMultilevel
            ? new Vector2(actor.Feet.X - _observedFeet.X, actor.Feet.Y - _observedFeet.Y)
            : motion;
        if (walking != Vector2.Zero)
        {
            _facingRow = Math.Abs(walking.X) > Math.Abs(walking.Y)
                ? walking.X < 0 ? 2 : 3
                : walking.Y < 0 ? 1 : 0;
            _motionAge = 0;
        }
        else if (actor.IsMultilevel && actor.Feet.Z != _observedFeet.Z)
        {
            _motionAge = MotionGraceSeconds;
        }
        if (motion != Vector2.Zero)
        {
            _previous = Position;
            _target = next;
            _interpolationAge = 0;
            if (_previous.DistanceSquaredTo(_target) > 160 * 160)
            {
                _previous = _target;
                Position = _target;
            }
        }
        _observedFeet = actor.Feet;
        ZIndex = actor.IsMultilevel ? WorksiteCoordinates.DepthIndex(actor.Feet.Z, actor: true) : 0;
        _sprite.Texture = assets.ActorTexture(actor.SpriteKind, actor.AppearanceKey);
        _nameplate.Text = actor.DisplayName + (actor.IsIncapacitated ? " · down" : "");
        _incapacitated = actor.IsIncapacitated;
        _health.Observe(actor.CurrentHp);
        // The local player's cyan ground ring identifies them without covering an adjacent NPC's name.
        _nameplate.Visible = !actor.IsOwnActor && (actor.SpriteKind != FarmSpriteKind.Wolf || actor.IsSelected);
        _nameplate.AddThemeColorOverride("font_color", actor.IsOwnActor ? new Color("bce9ff") : new Color("fff0ce"));
        _ownActor = actor.IsOwnActor;
        QueueRedraw();
    }

    private static Vector2 Project(FarmActorFrame actor)
    {
        if (actor.Regional is { } regional)
        {
            Cargo.CargoCanvasPoint point = regional.Project(actor.Feet);
            return new Vector2((float)point.X, (float)point.Y);
        }
        return actor.IsMultilevel ? WorksiteCoordinates.Position(actor.Feet) : FarmCanvasCoordinates.Position(actor.Feet);
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
        _sprite.Frame = _facingRow * 3 + (_incapacitated ? 1 : column);
        _health.Advance(delta);
        double age = _health.AgeSeconds;
        bool damaged = _health.Change == FarmHealthChange.Damage;
        bool restored = _health.Change == FarmHealthChange.Restoration;
        _sprite.Modulate = damaged && age < 0.16 ? new Color(1, 0.45f, 0.4f)
            : restored && age < 0.24 ? new Color(0.7f, 1, 0.82f)
            : _incapacitated ? new Color(0.52f, 0.52f, 0.58f) : Colors.White;
        _sprite.Position = damaged && age < 0.22 ? new Vector2((float)Math.Sin(age * 80) * 2, 0) : Vector2.Zero;
        _healthDelta.Text = (restored ? "+" : "") + _health.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _healthDelta.AddThemeColorOverride("font_color", new Color(restored ? "a8edc5" : "ffb79a"));
        _healthDelta.Visible = _health.IsVisible;
        _healthDelta.Position = new Vector2(-30, -88 - (damaged ? (float)age * 12 : 0));
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
