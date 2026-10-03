using Godot;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Known recipe inspection and explicit batch review; events return to the ordinary input boundary.</summary>
internal sealed partial class FarmRecipePanel : CanvasLayer
{
    private Control _root = null!;
    private VBoxContainer _list = null!;
    private ScrollContainer _listScroll = null!;
    private Label _title = null!;
    private Label _inputs = null!;
    private Label _outputs = null!;
    private Label _availability = null!;
    private Label _time = null!;
    private Label _notice = null!;
    private Button _prepare = null!;
    private Button _confirm = null!;
    private Button _close = null!;
    private Control? _returnFocus;
    private readonly Dictionary<EntityId, Button> _rows = [];

    internal bool IsOpen => _root.Visible;
    internal FarmRecipeLibraryModel? CurrentModel { get; private set; }
    internal event Action<EntityId>? SelectionRequested;
    internal event Action<FarmSemanticAction>? ActionRequested;

    public override void _Ready()
    {
        _root = GetNode<Control>("%RecipeRoot");
        _list = GetNode<VBoxContainer>("%RecipeList");
        _listScroll = GetNode<ScrollContainer>("%RecipeListScroll");
        _title = GetNode<Label>("%RecipeTitle");
        _inputs = GetNode<Label>("%RecipeInputs");
        _outputs = GetNode<Label>("%RecipeOutputs");
        _availability = GetNode<Label>("%RecipeAvailability");
        _time = GetNode<Label>("%RecipeTime");
        _notice = GetNode<Label>("%RecipeNotice");
        _prepare = GetNode<Button>("%PrepareRecipe");
        _confirm = GetNode<Button>("%ConfirmRecipe");
        _close = GetNode<Button>("%CloseRecipes");
        _prepare.Pressed += () => ActionRequested?.Invoke(FarmSemanticAction.PrepareRecipe);
        _confirm.Pressed += () => ActionRequested?.Invoke(FarmSemanticAction.ConfirmRecipe);
        _close.Pressed += () => ActionRequested?.Invoke(FarmSemanticAction.CloseRecipes);
    }

    internal void Open(FarmRecipeLibraryModel model)
    {
        _returnFocus = GetViewport().GuiGetFocusOwner();
        CurrentModel = model;
        _root.Show();
    }

    internal void Close()
    {
        _root.Hide();
        CurrentModel = null;
        if (IsInstanceValid(_returnFocus) && _returnFocus!.IsVisibleInTree()) _returnFocus.GrabFocus();
        _returnFocus = null;
    }

    internal void FocusSelection()
    {
        if (CurrentModel?.SelectedId is { } selected && _rows.TryGetValue(selected, out var button))
        {
            button.GrabFocus();
            _listScroll.EnsureControlVisible(button);
        }
        else _close.GrabFocus();
    }

    internal void Present(FarmRecipeLibraryModel model, LocalWorldRunState runState, bool shared,
        bool paused, bool canPrepare, bool canConfirm)
    {
        if (!IsOpen) return;
        CurrentModel = model;
        if (!_rows.Keys.SequenceEqual(model.Entries.Select(entry => entry.RecipeId))) Rebuild(model);
        foreach (var entry in model.Entries)
        {
            var button = _rows[entry.RecipeId];
            button.Text = entry.Label;
            button.TooltipText = entry.Label + "\nUses: " + entry.InputsText + "\nProduces: " + entry.OutputsText
                + (entry.Offer.BlockingReason is { } blocker ? "\n" + blocker : "");
            button.SetPressedNoSignal(entry.RecipeId == model.SelectedId);
        }
        var selected = model.Selected;
        GetNode<Label>("RecipeRoot/Panel/Column/Header/Title").Text = "Recipes · "
            + (selected?.Offer.TargetName ?? "Selected character");
        _title.Text = selected?.Label ?? "No known recipes";
        _inputs.Text = "Uses: " + (selected?.InputsText ?? "None");
        _outputs.Text = "Produces: " + (selected?.OutputsText ?? "None");
        _availability.Text = selected?.Offer.BlockingReason ?? (selected is null
            ? "There are no crafting recipes available for this character."
            : "Ingredients are available. Preparing reserves nothing; confirmation checks the current world.");
        _notice.Text = model.Notice ?? "Choose a recipe, prepare its exact batch, then confirm to spend the ingredients.";
        _time.Text = paused ? shared ? "Your input is paused · others may continue. Close and Resume before confirming."
            : "World paused · close and Resume before confirming."
            : runState switch
            {
                LocalWorldRunState.Running => shared ? "Ready · shared world continuing" : "World continuing",
                LocalWorldRunState.Resolving => "Resolving accepted world changes",
                LocalWorldRunState.Waiting => "World waiting",
                LocalWorldRunState.Paused => "Input paused",
                _ => "World unavailable"
            };
        _prepare.Disabled = !canPrepare || selected?.Offer.CanExecute != true;
        _confirm.Disabled = !canConfirm || model.PreparedOffer is null;
        WireFocus();
    }

    private void Rebuild(FarmRecipeLibraryModel model)
    {
        bool restoreFocus = _rows.Values.Any(button => button.HasFocus());
        foreach (var button in _rows.Values)
        {
            _list.RemoveChild(button);
            button.QueueFree();
        }
        _rows.Clear();
        foreach (var entry in model.Entries)
        {
            var button = new Button
            {
                Text = entry.Label, ToggleMode = true, ClipText = true,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                CustomMinimumSize = new Vector2(0, 42), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            button.SetMeta("recipe_id", entry.RecipeId.ToString());
            button.Pressed += () => SelectionRequested?.Invoke(entry.RecipeId);
            button.FocusEntered += () => _listScroll.EnsureControlVisible(button);
            _list.AddChild(button);
            _rows.Add(entry.RecipeId, button);
        }
        if (restoreFocus) FocusSelection();
    }

    private void WireFocus()
    {
        Control[] chain = [.. _rows.Values, .. (_prepare.Disabled ? Array.Empty<Control>() : new Control[] { _prepare }),
            .. (_confirm.Disabled ? Array.Empty<Control>() : new Control[] { _confirm }), _close];
        for (int index = 0; index < chain.Length; index++)
        {
            var previous = chain[index].GetPathTo(chain[(index + chain.Length - 1) % chain.Length]);
            var next = chain[index].GetPathTo(chain[(index + 1) % chain.Length]);
            chain[index].FocusPrevious = chain[index].FocusNeighborTop = chain[index].FocusNeighborLeft = previous;
            chain[index].FocusNext = chain[index].FocusNeighborBottom = chain[index].FocusNeighborRight = next;
        }
    }
}
