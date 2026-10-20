using Godot;

namespace OpenFantasy.Client;

/// <summary>Scene-owned, read-only journal of the player's own character and quests; it submits nothing.</summary>
internal sealed partial class FarmJournalPanel : CanvasLayer
{
    private Control _root = null!;
    private Label _character = null!;
    private VBoxContainer _quests = null!;
    private Button _close = null!;
    private Control? _returnFocus;

    internal event Action? CloseRequested;
    internal bool IsOpen => _root.Visible;
    internal string CharacterText => _character.Text;
    internal IReadOnlyList<string> QuestTexts => _quests.GetChildren().OfType<Label>().Select(label => label.Text).ToList();

    public override void _Ready()
    {
        _root = GetNode<Control>("%JournalRoot");
        _character = GetNode<Label>("%Character");
        _quests = GetNode<VBoxContainer>("%Quests");
        GetNode<Label>("%NotShown").Text = JournalPage.EquipmentAndSkills;
        _close = GetNode<Button>("%Close");
        _close.Pressed += () => CloseRequested?.Invoke();
    }

    internal void Open(JournalPage page)
    {
        Present(page);
        _returnFocus = GetViewport().GuiGetFocusOwner();
        _root.Visible = true;
        _close.GrabFocus();
    }

    internal void Present(JournalPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        _close.Text = $"Close  [{FarmKeyHints.Current.Journal}]";
        _character.Text = page.Character is { } who
            ? $"{who.Name} · {who.Profile} · Level {who.Level} · {who.Experience} XP\n"
                + $"HP {who.Hp}/{who.MaxHp} · MP {who.Mp}/{who.MaxMp} · {who.Currency} {who.CurrencyUnit}"
            : "This world records no character progression.";
        string[] texts = page.Quests.Count == 0
            ? ["No quests yet. People may offer work when you speak with them."]
            : page.Quests.Select(quest => $"{quest.Title} — {quest.StateText}\n{quest.ProgressText} · Reward {quest.RewardText}\n{quest.Description}").ToArray();
        while (_quests.GetChildCount() < texts.Length) _quests.AddChild(new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart });
        Label[] labels = _quests.GetChildren().OfType<Label>().ToArray();
        for (int index = 0; index < labels.Length; index++)
        {
            labels[index].Visible = index < texts.Length;
            if (index < texts.Length) labels[index].Text = texts[index];
        }
    }

    internal void Close()
    {
        _root.Visible = false;
        if (IsInstanceValid(_returnFocus) && _returnFocus!.IsVisibleInTree()) _returnFocus.GrabFocus();
        _returnFocus = null;
    }
}
