using Godot;

namespace OpenFantasy.Client;

/// <summary>Four broad radial targets with a neutral center and equivalent pointer/discrete/stick selection.</summary>
internal sealed partial class FarmWheelDisc : Control
{
    private const float OuterRadius = 164;
    private const float InnerRadius = 66;
    private readonly Dictionary<FarmSemanticAction, Texture2D> _icons = [];
    private FarmQuickWheelModel? _model;
    private bool _stickReleased;
    internal Vector2? LastPointerPosition { get; private set; }
    internal string? LastButtonInput { get; private set; }
    internal event Action? SelectionChanged;
    internal event Action? ConfirmRequested;

    public override void _Ready()
    {
        foreach ((FarmSemanticAction action, string name) in new[]
        {
            (FarmSemanticAction.Speak, "talk"), (FarmSemanticAction.Gather, "gather"),
            (FarmSemanticAction.Craft, "craft"), (FarmSemanticAction.Give, "give"),
            (FarmSemanticAction.Excavate, "excavate"), (FarmSemanticAction.Traverse, "traverse"),
            (FarmSemanticAction.Build, "gather"),
            (FarmSemanticAction.EnterEncounter, "engage"), (FarmSemanticAction.Strike, "strike"),
            (FarmSemanticAction.Wait, "wait"), (FarmSemanticAction.ReleaseEncounter, "leave"),
            (FarmSemanticAction.AcknowledgeCare, "care"), (FarmSemanticAction.UseRestorative, "care"),
            (FarmSemanticAction.RevokeCarePermission, "leave"),
            (FarmSemanticAction.CancelCare, "withdraw-gift"), (FarmSemanticAction.DeclineCare, "decline-gift"),
            (FarmSemanticAction.AdministerCare, "care"),
            (FarmSemanticAction.ProposeMaterial, "give"), (FarmSemanticAction.AcceptMaterial, "accept-gift"),
            (FarmSemanticAction.DeclineMaterial, "decline-gift"), (FarmSemanticAction.WithdrawMaterial, "withdraw-gift"),
            (FarmSemanticAction.TradeBuy, "market-buy"), (FarmSemanticAction.TradeSell, "market-sell"),
            (FarmSemanticAction.TradeOffers, "market-offers"), (FarmSemanticAction.Retreat, "traverse")
        }) _icons[action] = GD.Load<Texture2D>($"res://assets/ui/{name}.svg");
    }

    internal void ShowModel(FarmQuickWheelModel model)
    {
        _model = model;
        _stickReleased = false;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree() || !HasFocus()) return;
        Vector2 stick = Input.GetVector(FarmInputActions.WalkLeft, FarmInputActions.WalkRight,
            FarmInputActions.WalkUp, FarmInputActions.WalkDown);
        if (stick.Length() < 0.25f) _stickReleased = true;
        else if (_stickReleased && stick.Length() >= 0.55f) Select(Direction(stick));
    }

    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventJoypadButton button)
            LastButtonInput = $"{button.ButtonIndex} pressed={button.Pressed} accept={input.IsAction("ui_accept")}";
        if (input is InputEventMouseMotion motion)
        {
            LastPointerPosition = motion.Position;
            SelectAt(motion.Position);
            AcceptEvent();
        }
        else if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click)
        {
            GrabFocus();
            SelectAt(click.Position);
            AcceptEvent();
        }
        else if (input is InputEventKey or InputEventJoypadButton && !input.IsEcho() && input.IsPressed())
        {
            string[] directions = ["ui_up", "ui_right", "ui_down", "ui_left"];
            string[] walking = [FarmInputActions.WalkUp, FarmInputActions.WalkRight,
                FarmInputActions.WalkDown, FarmInputActions.WalkLeft];
            for (int index = 0; index < directions.Length; index++)
            {
                if (input.IsAction(directions[index]) || input.IsAction(walking[index]))
                {
                    Select(index);
                    AcceptEvent();
                    return;
                }
            }
            if (input.IsAction("ui_accept"))
            {
                ConfirmRequested?.Invoke();
                AcceptEvent();
            }
        }
    }

    private void SelectAt(Vector2 position)
    {
        Vector2 delta = position - Size / 2;
        if (delta.Length() >= InnerRadius && delta.Length() <= OuterRadius) Select(Direction(delta));
    }

    private static int Direction(Vector2 direction) => Math.Abs(direction.X) > Math.Abs(direction.Y)
        ? direction.X > 0 ? 1 : 3 : direction.Y > 0 ? 2 : 0;

    private void Select(int index)
    {
        int? previous = _model?.SelectedIndex;
        _model?.Select(index);
        if (_model?.SelectedIndex == previous) return;
        QueueRedraw();
        SelectionChanged?.Invoke();
    }

    public override void _Draw()
    {
        if (_model is null) return;
        Vector2 center = Size / 2;
        Font font = GetThemeDefaultFont();
        for (int index = 0; index < 4; index++)
        {
            FarmWheelSlot? slot = _model.Slots[index];
            if (slot is null) continue;
            bool selected = _model.SelectedIndex == index;
            float angle = -Mathf.Pi / 2 + index * Mathf.Pi / 2;
            float begin = angle - Mathf.Pi / 4 + 0.035f;
            float end = angle + Mathf.Pi / 4 - 0.035f;
            List<Vector2> polygon = [];
            for (int step = 0; step <= 24; step++)
                polygon.Add(center + Vector2.FromAngle(Mathf.Lerp(begin, end, step / 24f)) * OuterRadius);
            for (int step = 24; step >= 0; step--)
                polygon.Add(center + Vector2.FromAngle(Mathf.Lerp(begin, end, step / 24f)) * InnerRadius);
            DrawColoredPolygon(polygon.ToArray(), selected ? new Color(0.27f, 0.36f, 0.35f) : new Color(0.09f, 0.16f, 0.18f));
            DrawPolyline([.. polygon, polygon[0]], selected ? new Color(1, 0.91f, 0.58f) : new Color(0.51f, 0.56f, 0.51f), selected ? 3 : 1, true);
            Vector2 at = center + Vector2.FromAngle(angle) * 116;
            DrawTextureRect(_icons[slot.Action], new Rect2(at - new Vector2(21, 31), new Vector2(42, 42)), false);
            string label = FarmQuickWheelModel.SectorLabel(slot);
            float width = font.GetStringSize(label, fontSize: 16).X;
            DrawString(font, at + new Vector2(-width / 2, 31), label, fontSize: 16, modulate: new Color(0.94f, 0.91f, 0.81f));
            if (!slot.Offer.CanExecute)
                DrawString(font, at + new Vector2(26, -19), "!", fontSize: 22, modulate: new Color(1, 0.78f, 0.48f));
            if (selected) DrawCircle(center + Vector2.FromAngle(angle) * (InnerRadius + 8), 3, Colors.White);
        }
        string prompt = _model.Selected is null ? "Choose" : "Inspect";
        if (HasFocus()) DrawArc(center, InnerRadius - 7, 0, Mathf.Tau, 48, Colors.White, 1, true);
        float promptWidth = font.GetStringSize(prompt, fontSize: 18).X;
        DrawString(font, center + new Vector2(-promptWidth / 2, 6), prompt, fontSize: 18);
    }
}
