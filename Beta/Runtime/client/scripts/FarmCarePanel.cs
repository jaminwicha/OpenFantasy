using Godot;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Shared ordinary-care inspection. Buttons emit explicit choices; the session alone submits commands.</summary>
internal sealed partial class FarmCarePanel : CanvasLayer
{
    private Control _root = null!;
    private VBoxContainer _list = null!;
    private ScrollContainer _listScroll = null!;
    private Label _title = null!;
    private Label _details = null!;
    private Label _cost = null!;
    private Label _availability = null!;
    private Label _time = null!;
    private Label _notice = null!;
    private Button _prepare = null!;
    private Button _confirm = null!;
    private Button _close = null!;
    private Control? _returnFocus;
    private Texture2D _remedyIcon = null!;
    private Texture2D _permissionIcon = null!;
    private readonly Dictionary<FarmCareChoiceId, Button> _rows = [];

    internal bool IsOpen => _root.Visible;
    internal ulong PresentationLifetime { get; private set; }
    internal FarmCareLibraryModel? CurrentModel { get; private set; }
    internal event Action<FarmCareChoiceId, FarmActionScope?, ulong>? SelectionRequested;
    internal event Action<FarmSemanticAction>? ActionRequested;

    public override void _Ready()
    {
        _root = GetNode<Control>("%CareRoot");
        _list = GetNode<VBoxContainer>("%CareList");
        _listScroll = GetNode<ScrollContainer>("%CareListScroll");
        _title = GetNode<Label>("%CareTitle");
        _details = GetNode<Label>("%CareDetails");
        _cost = GetNode<Label>("%CareCost");
        _availability = GetNode<Label>("%CareAvailability");
        _time = GetNode<Label>("%CareTime");
        _notice = GetNode<Label>("%CareNotice");
        _prepare = GetNode<Button>("%PrepareCare");
        _confirm = GetNode<Button>("%ConfirmCare");
        _close = GetNode<Button>("%CloseCare");
        _remedyIcon = GD.Load<Texture2D>("res://assets/ui/care.svg");
        _permissionIcon = GD.Load<Texture2D>("res://assets/ui/leave.svg");
        _prepare.Pressed += () => ActionRequested?.Invoke(FarmSemanticAction.PrepareCare);
        _confirm.Pressed += () => ActionRequested?.Invoke(FarmSemanticAction.ConfirmCare);
        _close.Pressed += () => ActionRequested?.Invoke(FarmSemanticAction.CloseCare);
    }

    internal void Open(FarmCareLibraryModel model)
    {
        PresentationLifetime++;
        _returnFocus = GetViewport().GuiGetFocusOwner();
        CurrentModel = model;
        _root.Show();
        Rebuild(model);
    }

    internal void Close()
    {
        PresentationLifetime++;
        _root.Hide();
        CurrentModel = null;
        if (IsInstanceValid(_returnFocus) && _returnFocus!.IsVisibleInTree()) _returnFocus.GrabFocus();
        _returnFocus = null;
    }

    internal void FocusSelection()
    {
        Button? selected = CurrentModel?.SelectedId is { } key ? _rows.GetValueOrDefault(key) : _rows.Values.FirstOrDefault();
        if (selected is null) { _close.GrabFocus(); return; }
        selected.GrabFocus();
        _listScroll.EnsureControlVisible(selected);
    }

    internal void Present(FarmCareLibraryModel model, LocalWorldRunState runState, bool shared,
        bool paused, bool canPrepare, bool canConfirm)
    {
        if (!IsOpen) return;
        CurrentModel = model;
        if (!_rows.Keys.SequenceEqual(model.Entries.Select(entry => entry.Key))) Rebuild(model);
        foreach (var entry in model.Entries)
        {
            var button = _rows[entry.Key];
            button.Text = entry.Label;
            button.TooltipText = entry.Offer.Label + "\n" + entry.Offer.CostText + "\n" + entry.Offer.DetailText
                + (entry.Offer.BlockingReason is { } blocker ? "\n" + blocker : "");
            button.SetPressedNoSignal(entry.Key == model.SelectedId);
        }
        GetNode<Label>("CareRoot/Panel/Column/Header/Title").Text = "Care · " + model.ActorName;
        var selected = model.Selected;
        var shown = model.PreparedOffer ?? selected?.Offer;
        _title.Text = selected?.Label ?? "Choose a medicine or aid permission";
        _details.Text = shown?.DetailText ?? "Select a disclosed choice to inspect it. No dose is consumed and no permission changes while browsing.";
        _cost.Text = shown is null ? "" : "Cost: " + shown.CostText;
        _availability.Text = selected?.Offer.BlockingReason ?? (selected is null ? "No choice selected."
            : "Available to review. Confirmation checks the current world.");
        _notice.Text = model.Notice ?? "Select one choice, review its exact terms, then confirm.";
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
        _confirm.Text = model.PreparedOffer?.Action == FarmSemanticAction.RevokeCarePermission ? "Confirm withdrawal" : "Confirm use";
        _confirm.TooltipText = shown is null ? "Review one exact choice first." : shown.Label + "\n" + shown.CostText + "\n" + shown.DetailText;
        WireFocus();
    }

    private void Rebuild(FarmCareLibraryModel model)
    {
        bool restoreFocus = _rows.Values.Any(button => button.HasFocus());
        foreach (Node child in _list.GetChildren()) { _list.RemoveChild(child); child.QueueFree(); }
        _rows.Clear();
        FarmCareChoiceKind? section = null;
        FarmActionScope? scope = model.Scope;
        ulong lifetime = PresentationLifetime;
        foreach (var entry in model.Entries)
        {
            if (section != entry.Key.Kind)
            {
                section = entry.Key.Kind;
                _list.AddChild(new Label { Text = section == FarmCareChoiceKind.UseRemedy ? "Medicines" : "Aid permissions" });
            }
            var button = new Button
            {
                Text = entry.Label, ToggleMode = true, ClipText = true,
                Icon = entry.Key.Kind == FarmCareChoiceKind.UseRemedy ? _remedyIcon : _permissionIcon,
                ExpandIcon = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                CustomMinimumSize = new Vector2(0, 42), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            button.AddThemeConstantOverride("icon_max_width", 24);
            button.SetMeta("care_kind", entry.Key.Kind.ToString());
            button.SetMeta("care_id", entry.Key.Id.ToString());
            button.Pressed += () => SelectionRequested?.Invoke(entry.Key, scope, lifetime);
            button.FocusEntered += () => _listScroll.EnsureControlVisible(button);
            _list.AddChild(button);
            _rows.Add(entry.Key, button);
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
            chain[index].FocusPrevious = previous;
            chain[index].FocusNext = next;
            chain[index].FocusNeighborTop = previous;
            chain[index].FocusNeighborLeft = previous;
            chain[index].FocusNeighborBottom = next;
            chain[index].FocusNeighborRight = next;
        }
    }
}
