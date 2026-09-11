using Godot;
using OpenFantasy.Core.Quests;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Readable local play controls; all displayed inventory belongs to the current player.</summary>
internal sealed partial class FarmHud : CanvasLayer
{
    private const string InspectHint = "Hover or focus an action to see its target and cost.";

    private Label _identity = null!;
    private Label _clock = null!;
    private Label _wood = null!;
    private Label _message = null!;
    private Label _project = null!;
    private Label _preview = null!;
    private Label _speech = null!;
    private PanelContainer _dialogue = null!;
    private Button _talk = null!;
    private Button _give = null!;
    private Button _gather = null!;
    private Button _craft = null!;
    private Button _close = null!;
    private Button _pause = null!;
    private Label _targetName = null!;
    private Button _nextTarget = null!;
    private Button _clearTarget = null!;
    private Button _openPack = null!;
    private Button _openWheel = null!;
    private readonly Dictionary<FarmSemanticAction, Button> _buttons = [];
    private IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> _offers =
        new Dictionary<FarmSemanticAction, FarmActionOffer>();
    private FarmSemanticAction _hovered;
    private FarmSemanticAction _focused;
    private bool _wasSpeaking;

    /// <summary>Every button names a semantic action; the game's input router decides whether the context accepts it.</summary>
    internal event Action<FarmSemanticAction>? ActionRequested;

    public override void _Ready()
    {
        _identity = GetNode<Label>("%Identity");
        _clock = GetNode<Label>("%Clock");
        _wood = GetNode<Label>("%Inventory");
        _message = GetNode<Label>("%Message");
        _project = GetNode<Label>("%ProjectStatus");
        _preview = GetNode<Label>("%ActionPreview");
        _speech = GetNode<Label>("%Speech");
        _dialogue = GetNode<PanelContainer>("%Dialogue");
        _talk = GetNode<Button>("%Talk");
        _give = GetNode<Button>("%Give");
        _gather = GetNode<Button>("%Gather");
        _craft = GetNode<Button>("%Craft");
        _close = GetNode<Button>("%Close");
        _pause = GetNode<Button>("%Pause");
        _targetName = GetNode<Label>("%TargetName");
        _nextTarget = GetNode<Button>("%NextTarget");
        _clearTarget = GetNode<Button>("%ClearTarget");
        _openPack = GetNode<Button>("%OpenPack");
        _openWheel = GetNode<Button>("%OpenWheel");
        _openWheel.Pressed += () => Request(FarmSemanticAction.OpenWheel);
        _openPack.Pressed += () => Request(FarmSemanticAction.OpenInventory);
        _nextTarget.Pressed += () => Request(FarmSemanticAction.NextTarget);
        _clearTarget.Pressed += () => Request(FarmSemanticAction.ClearTarget);
        BindOfferButton(FarmSemanticAction.Speak, _talk);
        BindOfferButton(FarmSemanticAction.Give, _give);
        BindOfferButton(FarmSemanticAction.Gather, _gather);
        BindOfferButton(FarmSemanticAction.Craft, _craft);
        BindOfferButton(FarmSemanticAction.CloseDialogue, _close);
        _pause.Pressed += () => Request(FarmSemanticAction.TogglePause);
        _preview.Text = InspectHint;
    }

    /// <summary>Pressing requests execution; hover and focus only select which offer the card explains.</summary>
    private void BindOfferButton(FarmSemanticAction action, Button button)
    {
        _buttons[action] = button;
        button.Pressed += () => Request(action);
        button.MouseEntered += () => _hovered = action;
        button.MouseExited += () => { if (_hovered == action) _hovered = FarmSemanticAction.None; };
        button.FocusEntered += () => _focused = action;
        button.FocusExited += () => { if (_focused == action) _focused = FarmSemanticAction.None; };
    }

    private void Request(FarmSemanticAction action) => ActionRequested?.Invoke(action);

    internal void Present(LocalWorldView view, ulong wood, ulong berries, bool pending, bool paused,
        IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> offers, FarmTargetSelection targets)
    {
        _offers = offers;
        int hour = view.Calendar.MillisecondsWithinDay / 3_600_000;
        int minute = view.Calendar.MillisecondsWithinDay / 60_000 % 60;
        _clock.Text = $"Day {view.Calendar.Days + 1} · {hour:D2}:{minute:D2}";
        _wood.Text = view.PlayerGatherEffort.HasValue ? $"Wood: {wood} · Berries: {berries}" : $"Wood: {wood}";
        if (view.Crafting is not null)
        {
            _wood.Text += $"\nRations: {FarmCrafting.Quantity(view, OpenFantasy.Core.Production.RecipeMaterialRole.Output)}"
                + $" · Offcuts: {FarmCrafting.Quantity(view, OpenFantasy.Core.Production.RecipeMaterialRole.Waste)}";
        }
        _identity.Text = "Wren's farm";
        if (view.Progression is { } progression)
        {
            _identity.Text = $"Level {progression.Level} · {progression.CurrencyBalance} {progression.CurrencyUnit}"
                + $"\nHP {progression.Hp}/{progression.MaxHp} · MP {progression.Mp}/{progression.MaxMp}";
        }
        bool speaking = view.Dialogue is not null;
        _openPack.Disabled = pending || speaking;
        _openWheel.Disabled = pending || speaking;
        _targetName.Text = targets.Selected is { } target
            ? $"Target: {target.Name}" + (target.Feet is null ? " · out of sight" : "") : "Target: automatic";
        _nextTarget.Disabled = pending || paused || speaking || !targets.CanCycle;
        _clearTarget.Disabled = pending || paused || speaking || targets.Selected is null;
        // Layer legality stays with the input router; these mirror it so a disabled control matches a refused key.
        PresentOffer(FarmSemanticAction.Speak, "E", blockedByLayer: pending || paused || speaking);
        PresentOffer(FarmSemanticAction.Give, "G", blockedByLayer: pending || paused);
        PresentOffer(FarmSemanticAction.Gather, "F", blockedByLayer: pending || paused || speaking);
        PresentOffer(FarmSemanticAction.Craft, "C", blockedByLayer: pending || paused || speaking);
        PresentOffer(FarmSemanticAction.CloseDialogue, "E", blockedByLayer: pending || paused || !speaking);
        _pause.Text = paused ? "Resume  [Esc]" : view.HasReadingHold ? "Pause" : "Pause  [Esc]";
        LocalConstructionView? project = view.Construction.FirstOrDefault();
        _project.Visible = project is not null;
        _project.Text = project is null ? "" : DescribeProject(view, project, paused);
        _dialogue.Visible = speaking;
        if (view.Dialogue is { } dialogue)
        {
            _speech.Text = DescribeSpeech(view, dialogue, project);
        }
        PresentPreview();
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

    /// <summary>A missing offer hides the control; a blocked offer keeps it visible and inspectable.</summary>
    private void PresentOffer(FarmSemanticAction action, string key, bool blockedByLayer)
    {
        Button button = _buttons[action];
        if (!_offers.TryGetValue(action, out FarmActionOffer? offer))
        {
            button.Visible = false;
            button.TooltipText = "";
            return;
        }
        button.Visible = true;
        button.Text = offer.SpendsSomething ? $"{offer.Label}  [{key}]\n{offer.CostText}" : $"{offer.Label}  [{key}]";
        button.Disabled = blockedByLayer || !offer.CanExecute;
        button.TooltipText = Explain(offer);
    }

    /// <summary>Keyboard/controller focus and pointer hover explain the same presented offer.</summary>
    private void PresentPreview()
    {
        FarmSemanticAction inspected = _hovered != FarmSemanticAction.None ? _hovered : _focused;
        _preview.Text = inspected != FarmSemanticAction.None && _offers.TryGetValue(inspected, out FarmActionOffer? offer)
            ? Explain(offer) : InspectHint;
    }

    private static string Explain(FarmActionOffer offer)
    {
        string text = $"{offer.Label}: {offer.TargetName} · Cost: {offer.CostText}";
        if (offer.DetailText is not null)
        {
            text += " · " + offer.DetailText;
        }
        if (offer.BlockingReason is not null)
        {
            text += "\nUnavailable: " + offer.BlockingReason;
        }
        return text;
    }

    private static string DescribeSpeech(LocalWorldView view, LocalDialogueView dialogue, LocalConstructionView? project)
    {
        LocalQuestView? quest = view.Quests.SingleOrDefault(value => value.GiverActorId == dialogue.SpeakerActorId);
        if (quest is null)
        {
            return dialogue.SpeakerName + "\n" + dialogue.Text;
        }
        string text = dialogue.SpeakerName + " · " + quest.Title + "\n";
        if (quest.Status == ActorQuestStatus.Completed)
        {
            return text + (project is not null && project.BuilderActorId == dialogue.SpeakerActorId
                ? project.IsCompleted ? "A small guard for the first sapling. That's a start."
                    : "I've set the wood aside. Let me get to work when we're done talking."
                : "The supplies are set aside for the orchard.");
        }
        return text + quest.Description + $"\nWood: {quest.EligibleQuantity}/{quest.RequiredQuantity} · Reward: "
            + $"{quest.RewardExperience} XP and {quest.RewardCurrency} {quest.CurrencyUnit}";
    }

    private static string DescribeProject(LocalWorldView view, LocalConstructionView project, bool paused)
    {
        if (project.IsCompleted)
        {
            return "Seedling guard built · Saved with the farm.";
        }
        LocalQuestView? quest = view.Quests.FirstOrDefault(value => value.GiverActorId == project.BuilderActorId);
        if (quest?.Status != ActorQuestStatus.Completed)
        {
            return "Orchard task · Bring Wren six wood. Ration crafting is optional.";
        }
        if (paused || view.HasReadingHold)
        {
            return $"Wren's project paused · {project.CompletedWork * 100 / project.RequiredWork}% fitted";
        }
        LocalActorView builder = view.Actors.Single(value => value.ActorId == project.BuilderActorId);
        return builder.Feet == project.WorkFeet
            ? $"Wren's project · {project.CompletedWork * 100 / project.RequiredWork}% fitted · Keep the plot clear."
            : "Wren needs a clear route to the crop plot.";
    }

    internal void Message(string text) => _message.Text = text;

    internal void SetCovered(bool covered) => GetNode<Control>("Root").Visible = !covered;
}
