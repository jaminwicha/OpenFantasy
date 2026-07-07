using Godot;

namespace OpenFantasy.Client;

/// <summary>
/// FF6-flavored UI builders: gradient navy window panels with white double
/// borders (from assets/ui/panel.png as a 9-patch) and shared label styling.
/// </summary>
public static class UiTheme
{
    public static readonly Color TextColor = new("f0f0e6");
    public static readonly Color ShadowColor = new(0, 0, 0, 0.55f);
    public static readonly Color HpColor = new("58c85c");
    public static readonly Color HpLowColor = new("d8b040");
    public static readonly Color MpColor = new("5878e0");
    public static readonly Color AtbColor = new("e8c050");
    public static readonly Color AtbReadyColor = new("f8f0a0");

    private static Texture2D? _panelTexture;

    private static Texture2D PanelTexture()
        => _panelTexture ??= GD.Load<Texture2D>("res://assets/ui/panel.png");

    /// <summary>A window panel in the classic style; margins match the art's border.</summary>
    public static NinePatchRect MakePanel()
    {
        return new NinePatchRect
        {
            Texture = PanelTexture(),
            PatchMarginLeft = 5,
            PatchMarginTop = 5,
            PatchMarginRight = 5,
            PatchMarginBottom = 5,
        };
    }

    public static Label MakeLabel(string text, int size)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", TextColor);
        label.AddThemeColorOverride("font_shadow_color", ShadowColor);
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        return label;
    }

    /// <summary>Thin stat bar (HP/MP/ATB) with a dark inset background.</summary>
    public static (Control root, ColorRect fill) MakeBar(Color color, Vector2 size)
    {
        var root = new Control { CustomMinimumSize = size };
        var back = new ColorRect { Color = new Color(0.05f, 0.06f, 0.1f, 0.9f) };
        back.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(back);
        var fill = new ColorRect { Color = color };
        fill.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        fill.OffsetLeft = 1;
        fill.OffsetTop = 1;
        fill.OffsetBottom = -1;
        root.AddChild(fill);
        return (root, fill);
    }

    /// <summary>Sets a bar's fill fraction (0..1) inside its parent.</summary>
    public static void SetBarFraction(ColorRect fill, float fraction)
    {
        fraction = Mathf.Clamp(fraction, 0f, 1f);
        var parent = (Control)fill.GetParent();
        fill.OffsetRight = -1 - (parent.Size.X - 2) * (1 - fraction);
    }
}
