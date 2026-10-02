using Godot;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Quests;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.EncounterOutcome;

namespace OpenFantasy.Client;

/// <summary>Readable local play controls; all displayed inventory belongs to the current player.</summary>
internal sealed partial class FarmHud : CanvasLayer
{
    private const string InspectHint = "Hover or focus an action to see its target and cost.";

    private Control _header = null!;
    private Control _targetBar = null!;
    private float _targetBarGap;
    private float _targetBarAuthoredHeight;
    private Label _identity = null!;
    private Label _clock = null!;
    private Label _wood = null!;
    private Label _message = null!;
    private Label _project = null!;
    private Label _preview = null!;
    private Label _speech = null!;
    private FarmEncounterStatus _encounter = null!;
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
    /// <summary>An unfinalized profile-35 outcome: its choices preview in one line; the card and tooltips explain fully.</summary>
    private bool _outcomeChoices;

    /// <summary>Every button names a semantic action; the game's input router decides whether the context accepts it.</summary>
    internal event Action<FarmSemanticAction>? ActionRequested;

    /// <summary>The field between the laid-out status header and action footer, in viewport coordinates.</summary>
    internal Rect2 FieldViewportBounds
    {
        get
        {
            Rect2 viewport = GetViewport().GetVisibleRect();
            float top = _header.GetGlobalRect().End.Y;
            float bottom = GetNode<Control>("Root/Footer").GetGlobalRect().Position.Y;
            return new Rect2(viewport.Position.X, top, viewport.Size.X, Math.Max(1, bottom - top));
        }
    }

    /// <summary>Whether a whole field sprite clears both the main panels and the overlaid target controls.</summary>
    internal bool ShowsFieldSprite(Rect2 sprite)
    {
        return FieldViewportBounds.Encloses(sprite)
            && (!_targetBar.IsVisibleInTree() || !_targetBar.GetGlobalRect().Intersects(sprite));
    }

    public override void _Ready()
    {
        _header = GetNode<Control>("Root/Header");
        _targetBar = GetNode<Control>("Root/TargetBar");
        // Both top anchors share Root's origin. Capture the authored gap and offset span, not the transient
        // actual size: initial wrapping can temporarily raise a container's minimum height before its width settles.
        _targetBarGap = _targetBar.OffsetTop - _header.OffsetBottom;
        _targetBarAuthoredHeight = _targetBar.OffsetBottom - _targetBar.OffsetTop;
        _header.Resized += PlaceTargetBarBelowHeader;
        PlaceTargetBarBelowHeader();
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
        _encounter = new FarmEncounterStatus(GetNode<Label>("%EncounterStatus"));
        BindOfferButton(FarmSemanticAction.EnterEncounter, GetNode<Button>("%EnterEncounter"));
        BindOfferButton(FarmSemanticAction.Strike, GetNode<Button>("%Strike"));
        BindOfferButton(FarmSemanticAction.Wait, GetNode<Button>("%Wait"));
        BindOfferButton(FarmSemanticAction.ReleaseEncounter, GetNode<Button>("%ReleaseEncounter"));
        BindOfferButton(FarmSemanticAction.AcknowledgeCare, GetNode<Button>("%AcknowledgeCare"));
        BindOfferButton(FarmSemanticAction.UseRestorative, GetNode<Button>("%UseRestorative"));
        _pause.Pressed += () => Request(FarmSemanticAction.TogglePause);
        _preview.Text = InspectHint;
    }

    /// <summary>Follows container layout changes without allowing growing status text to cover the target controls.</summary>
    private void PlaceTargetBarBelowHeader()
    {
        float top = _header.Position.Y + _header.Size.Y + _targetBarGap;
        // Position would preserve the current size by baking it into both offsets, including a temporary
        // oversized minimum. Keep the scene's requested span; Godot can still grow to the current text minimum
        // and shrink back when wrapping settles. Neither the text nor the natural minimum is clamped.
        _targetBar.OffsetTop = top;
        _targetBar.OffsetBottom = top + _targetBarAuthoredHeight;
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
        _outcomeChoices = view.EncounterOutcome is { Stage: not LocalOutcomeStage.Completed };
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
        PresentOffer(FarmSemanticAction.UseRestorative, "", blockedByLayer: pending || paused || speaking);
        foreach (FarmSemanticAction action in new[] { FarmSemanticAction.EnterEncounter, FarmSemanticAction.Strike,
            FarmSemanticAction.Wait, FarmSemanticAction.ReleaseEncounter, FarmSemanticAction.AcknowledgeCare })
            PresentOffer(action, action == FarmSemanticAction.Wait ? "G" : "E", pending || paused || speaking);
        _encounter.Present(view, paused);
        GetNode<Label>("Root/Footer/Column/Actions/Movement").Text = view.Encounter?.IsOutcome == true
            ? view.EncounterOutcome is { } outcome ? EncounterOutcomeText.Hint(outcome, view.Care)
                : view.Care is { } care ? CareHint(care.Status)
                : view.EncounterLifecycle?.ReleaseActionHash is not null ? "Encounter complete\nE / A: continue"
                : "Incapacitated\nAid unavailable"
            : view.Progression is { Hp: 0 } ? EncounterOutcomeText.TerminalHint
            : view.IsInEncounter ? "Choose · Inspect · Confirm\nE / A: act · G / X: wait" : "Move\nWASD / arrows";
        _pause.Text = paused ? "Resume  [Esc]" : view.HasReadingHold ? "Pause" : "Pause  [Esc]";
        LocalConstructionView? project = view.Construction.FirstOrDefault(value => !value.IsCompleted)
            ?? view.Construction.FirstOrDefault();
        _project.Visible = project is not null;
        _project.Text = project is null ? "" : DescribeProject(view, project, paused);
        if (view.Worksite is { } worksite)
        {
            long elevation = view.Actors.Single(actor => actor.ActorId == view.PlayerActorId).Feet.Z;
            _clock.Text += $" · Height {elevation / 256.0:0.##} m";
            _project.Visible = true;
            string activity = project is not null ? _project.Text
                : worksite.HasPhysicalConstruction ? "Ask Wren about worksite supplies."
                : worksite.ExcavationTargets.Count == 1
                    ? $"Excavation {worksite.ExcavationTargets[0].CompletedWork}/{worksite.ExcavationTargets[0].RequiredWork}"
                    : "Select a work face to inspect excavation.";
            _project.Text = (view.IsResolvingConsequences ? "Finishing the work…" : activity)
                + "\nQ: choose work face · V: " + (worksite.SupportsAssistedTraversal ? "Mine / Climb" : "Mine");
        }
        if (view.IsInEncounter || view.EncounterLifecycle is not null
            && targets.Selected?.Kind != FarmTargetKind.Excavation && !speaking) _project.Visible = false;
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
        // Beside offered care, E / A asks for care; the separate reading names its choice and no key it does not have.
        bool readingBesideCare = action == FarmSemanticAction.ReleaseEncounter && EncounterOutcomeText.ReadingBesideCare(_offers);
        button.Text = action == FarmSemanticAction.UseRestorative ? "" : readingBesideCare ? offer.Label
            : action is FarmSemanticAction.EnterEncounter or FarmSemanticAction.Strike
            or FarmSemanticAction.Wait or FarmSemanticAction.ReleaseEncounter or FarmSemanticAction.AcknowledgeCare ? $"[{key}]"
            : offer.SpendsSomething ? $"{offer.Label}  [{key}]\n{offer.CostText}" : $"{offer.Label}  [{key}]";
        button.Disabled = blockedByLayer || !offer.CanExecute;
        button.TooltipText = readingBesideCare ? Explain(offer) + "\n" + EncounterOutcomeText.ReadingBesideCareRoutes : Explain(offer);
    }

    /// <summary>
    /// Keyboard/controller focus and pointer hover explain the same presented offer. Outcome choices preview in one line,
    /// so the footer never outgrows the field; their tooltips, the wheel inspector and the outcome card explain fully.
    /// </summary>
    private void PresentPreview()
    {
        FarmSemanticAction inspected = _hovered != FarmSemanticAction.None ? _hovered : _focused;
        _preview.Text = inspected != FarmSemanticAction.None && _offers.TryGetValue(inspected, out FarmActionOffer? offer)
            ? _outcomeChoices && inspected is FarmSemanticAction.ReleaseEncounter or FarmSemanticAction.AcknowledgeCare
                ? EncounterOutcomeText.InlinePreview(offer) : Explain(offer)
            : InspectHint;
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

    private static string CareHint(LocalCareStatus status) => status switch
    {
        LocalCareStatus.Available => "Help is available\nE / A: care · V: inspect",
        LocalCareStatus.Pending => "Care pending\nV: inspect",
        LocalCareStatus.NeedsAttention => "Care interrupted\nV: inspect",
        _ => "Aid unavailable\nV: inspect"
    };

    private static string DescribeSpeech(LocalWorldView view, LocalDialogueView dialogue, LocalConstructionView? project)
    {
        LocalQuestView? quest = FarmQuestActions.AddressedQuest(view);
        if (quest is null)
        {
            return dialogue.SpeakerName + "\n" + dialogue.Text;
        }
        string text = dialogue.SpeakerName + " · " + quest.Title + "\n";
        if (quest.Status == ActorQuestStatus.Completed)
        {
            if (view.Worksite?.HasPhysicalConstruction == true) return text + "The supplies are set aside. Clear the work area so I can build.";
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
        if (view.Worksite?.HasPhysicalConstruction == true)
        {
            return project.IsCompleted ? project.DisplayName + " built · Saved with the worksite."
                : $"{project.DisplayName} · {project.CompletedWork * 100 / project.RequiredWork}% fitted"
                    + (paused || view.HasReadingHold ? " · World waiting" : " · Supply Wren and keep the route clear.");
        }
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
