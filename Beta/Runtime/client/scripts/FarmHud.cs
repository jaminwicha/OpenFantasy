using Godot;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Quests;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Readable local play controls; all displayed inventory belongs to the current player.</summary>
internal sealed partial class FarmHud : CanvasLayer
{
    private Label _identity = null!;
    private Label _clock = null!;
    private Label _wood = null!;
    private Label _message = null!;
    private Label _speech = null!;
    private PanelContainer _dialogue = null!;
    private Button _talk = null!;
    private Button _give = null!;
    private Button _gather = null!;
    private Button _craft = null!;
    private Button _close = null!;
    private Button _pause = null!;
    private bool _wasSpeaking;

    internal event Action? TalkRequested;
    internal event Action? GiveRequested;
    internal event Action? GatherRequested;
    internal event Action? CraftRequested;
    internal event Action? CloseRequested;
    internal event Action? PauseRequested;

    public override void _Ready()
    {
        _identity = GetNode<Label>("%Identity");
        _clock = GetNode<Label>("%Clock");
        _wood = GetNode<Label>("%Inventory");
        _message = GetNode<Label>("%Message");
        _speech = GetNode<Label>("%Speech");
        _dialogue = GetNode<PanelContainer>("%Dialogue");
        _talk = GetNode<Button>("%Talk");
        _give = GetNode<Button>("%Give");
        _gather = GetNode<Button>("%Gather");
        _craft = GetNode<Button>("%Craft");
        _close = GetNode<Button>("%Close");
        _pause = GetNode<Button>("%Pause");
        _talk.Pressed += () => TalkRequested?.Invoke();
        _give.Pressed += () => GiveRequested?.Invoke();
        _gather.Pressed += () => GatherRequested?.Invoke();
        _craft.Pressed += () => CraftRequested?.Invoke();
        _close.Pressed += () => CloseRequested?.Invoke();
        _pause.Pressed += () => PauseRequested?.Invoke();
    }

    internal void Present(LocalWorldView view, ulong wood, ulong berries, bool pending, bool paused)
    {
        int hour = view.Calendar.MillisecondsWithinDay / 3_600_000;
        int minute = view.Calendar.MillisecondsWithinDay / 60_000 % 60;
        _clock.Text = $"Day {view.Calendar.Days + 1} · {hour:D2}:{minute:D2}";
        _wood.Text = view.PlayerGatherEffort.HasValue ? $"Wood: {wood} · Berries: {berries}" : $"Wood: {wood}";
        if (view.Crafting is not null)
        {
            _wood.Text += $"\nRations: {FarmCrafting.Quantity(view, OpenFantasy.Core.Production.RecipeMaterialRole.Output)}"
                + $" · Offcuts: {FarmCrafting.Quantity(view, OpenFantasy.Core.Production.RecipeMaterialRole.Waste)}";
        }
        _craft.Visible = view.Crafting is not null;
        _craft.Disabled = pending || paused || view.HasReadingHold || !FarmCrafting.HasIngredients(view);
        _talk.Disabled = pending || paused || view.HasReadingHold;
        _give.Disabled = pending || paused || wood == 0;
        _give.Text = "Give wood  [G]";
        _identity.Text = "Wren's farm";
        if (view.Progression is { } progression)
        {
            _identity.Text = $"Level {progression.Level} · {progression.CurrencyBalance} {progression.CurrencyUnit}"
                + $"\nHP {progression.Hp}/{progression.MaxHp} · MP {progression.Mp}/{progression.MaxMp}";
        }
        _gather.Visible = view.PlayerGatherEffort.HasValue;
        var target = FarmClientSession.NearestGatherNode(view);
        _gather.Text = target?.RequiredTool == GatherToolKind.Axe ? "Chop oak  [F]" : "Pick berries  [F]";
        _gather.Disabled = pending || paused || view.HasReadingHold || view.PlayerGatherEffort < 400
            || target is null || !FarmClientSession.CanReachGatherNode(view, target);
        _close.Disabled = pending || paused;
        _pause.Text = paused ? "Resume  [Esc]" : "Pause  [Esc]";
        bool speaking = view.Dialogue is not null;
        _dialogue.Visible = speaking;
        if (view.Dialogue is { } dialogue)
        {
            _speech.Text = dialogue.SpeakerName + "\n" + dialogue.Text;
            LocalQuestView? quest = view.Quests.SingleOrDefault(value => value.GiverActorId == dialogue.SpeakerActorId);
            if (quest is not null)
            {
                _speech.Text = dialogue.SpeakerName + " · " + quest.Title + "\n";
                _speech.Text += quest.Status == ActorQuestStatus.Completed
                    ? "The supplies are set aside for the orchard."
                    : quest.Description + $"\nWood: {quest.EligibleQuantity}/{quest.RequiredQuantity} · Reward: "
                        + $"{quest.RewardExperience} XP and {quest.RewardCurrency} {quest.CurrencyUnit}";
                _give.Text = quest.Status is null ? "Accept task  [G]" : "Deliver wood  [G]";
                _give.Disabled = pending || paused || (!quest.CanAccept && !quest.CanComplete);
            }
        }
        if (speaking && !_wasSpeaking)
        {
            _close.GrabFocus();
        }
        else if (!speaking && _wasSpeaking)
        {
            _close.ReleaseFocus();
        }
        _wasSpeaking = speaking;
    }

    internal void Message(string text) => _message.Text = text;

}
