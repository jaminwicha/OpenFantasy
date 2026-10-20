using Godot;

namespace OpenFantasy.Client.Presentation.Audio;

/// <summary>Scene-owned caption lines for notable sounds; presentation only and empty when captions are off.</summary>
internal sealed partial class FarmCaptions : CanvasLayer
{
    private VBoxContainer _lines = null!;
    private readonly StyleBoxFlat _lineStyle = new()
    {
        BgColor = new Color(0.03f, 0.05f, 0.06f, 0.82f),
        ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 4, ContentMarginBottom = 4,
    };

    internal IReadOnlyList<string> Shown => _lines.GetChildren().OfType<Label>().Where(label => label.Visible)
        .Select(label => label.Text).ToList();

    public override void _Ready() => _lines = GetNode<VBoxContainer>("%Lines");

    internal void Present(IReadOnlyList<CaptionLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        while (_lines.GetChildCount() < lines.Count)
        {
            var label = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
            label.AddThemeStyleboxOverride("normal", _lineStyle);
            _lines.AddChild(label);
        }
        Label[] labels = _lines.GetChildren().OfType<Label>().ToArray();
        for (int index = 0; index < labels.Length; index++)
        {
            bool shown = index < lines.Count;
            labels[index].Visible = shown;
            if (shown) labels[index].Text = lines[index].Text;
        }
    }
}
