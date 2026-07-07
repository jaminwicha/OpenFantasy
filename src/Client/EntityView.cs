using Godot;
using OpenFantasy.Net;
using OpenFantasy.Sim;

namespace OpenFantasy.Client;

/// <summary>
/// One entity's presentation: animated sprite sheet frame, nameplate, HP bar,
/// target ring, smoothed movement toward snapshot positions.
/// </summary>
public partial class EntityView : Node2D
{
    private const int FrameW = 16;
    private const int FrameH = 24;
    private static readonly int[] WalkCycle = { 0, 1, 2, 1 };

    private Sprite2D _sprite = null!;
    private Label _nameLabel = null!;
    private ColorRect _hpFill = null!;
    private Control _hpRoot = null!;
    private Node2D _targetRing = null!;

    private Vector2 _renderPos;
    private Vector2 _targetPos;
    private Facing _facing;
    private bool _moving;
    private float _animClock;
    private bool _initialized;

    public int EntityId { get; private set; }
    public EntityKind Kind { get; private set; }

    public void Setup(NetEntityState state, bool isLocal)
    {
        EntityId = state.Id;
        Kind = state.Kind;

        _sprite = new Sprite2D
        {
            Texture = GD.Load<Texture2D>($"res://assets/units/{state.ArchetypeId}.png"),
            Hframes = 3,
            Vframes = 4,
            Offset = new Vector2(0, -FrameH / 2f + 2),
            TextureFilter = TextureFilterEnum.Nearest,
        };
        AddChild(_sprite);

        _targetRing = new Node2D { Visible = false };
        _targetRing.Draw += () =>
        {
            _targetRing.DrawArc(Vector2.Zero, 11f, 0, Mathf.Tau, 24, new Color("f8e050"), 1.5f);
        };
        AddChild(_targetRing);

        _nameLabel = UiTheme.MakeLabel(state.Name, 8);
        _nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _nameLabel.Position = new Vector2(-40, -FrameH - 8);
        _nameLabel.Size = new Vector2(80, 10);
        if (isLocal) _nameLabel.Modulate = new Color("a0e0ff");
        else if (state.Kind == EntityKind.Monster) _nameLabel.Modulate = new Color("ffb0a0");
        AddChild(_nameLabel);

        (_hpRoot, _hpFill) = UiTheme.MakeBar(UiTheme.HpColor, new Vector2(22, 3));
        _hpRoot.Position = new Vector2(-11, -FrameH + 1);
        AddChild(_hpRoot);

        _renderPos = _targetPos = WorldRenderer.UnitsToPixels(state.X, state.Y);
        Position = _renderPos;
        ApplyState(state);
        _initialized = true;
    }

    public void ApplyState(NetEntityState state)
    {
        var newTarget = WorldRenderer.UnitsToPixels(state.X, state.Y);
        _moving = _initialized && newTarget.DistanceSquaredTo(_targetPos) > 0.2f;
        _targetPos = newTarget;
        _facing = state.Facing;

        float hpFrac = state.MaxHp > 0 ? (float)state.Hp / state.MaxHp : 0;
        UiTheme.SetBarFraction(_hpFill, hpFrac);
        _hpFill.Color = hpFrac < 0.3f ? UiTheme.HpLowColor : UiTheme.HpColor;
        _hpRoot.Visible = state.Alive && hpFrac < 1f;

        Modulate = state.Alive ? Colors.White : new Color(0.6f, 0.6f, 0.7f, 0.45f);
        _nameLabel.Text = state.Kind == EntityKind.Player && state.Level > 1
            ? $"{state.Name} L{state.Level}"
            : state.Name;
    }

    public void SetTargeted(bool targeted)
    {
        _targetRing.Visible = targeted;
        _targetRing.QueueRedraw();
    }

    public override void _Process(double delta)
    {
        // Exponential smoothing toward the last snapshot position; snap when far (teleport/respawn).
        if (_renderPos.DistanceSquaredTo(_targetPos) > 80 * 80)
            _renderPos = _targetPos;
        else
            _renderPos = _renderPos.Lerp(_targetPos, 1f - Mathf.Exp(-14f * (float)delta));
        Position = _renderPos;
        ZIndex = (int)_renderPos.Y;

        _animClock += (float)delta;
        int row = (int)_facing;
        int col;
        if (_moving && _renderPos.DistanceSquaredTo(_targetPos) > 0.5f)
            col = WalkCycle[(int)(_animClock * 7) % WalkCycle.Length];
        else
            col = 1;
        _sprite.Frame = row * 3 + col;
    }
}
