using Godot;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Icon-led quick inspection with explicit commitment; the game owns input and host submission.</summary>
internal sealed partial class FarmQuickWheel : CanvasLayer
{
    private Control _root = null!;
    private FarmWheelDisc _disc = null!;
    private Label _name = null!;
    private Label _details = null!;
    private Label _time = null!;
    private Button _confirm = null!;
    private Button _close = null!;
    private Button _nextPage = null!;
    private Control? _returnFocus;
    private bool _canConfirm;
    internal FarmQuickWheelModel? Model { get; private set; }
    internal event Action<FarmSemanticAction>? ActionRequested;

    public override void _Ready()
    {
        _root = GetNode<Control>("%WheelRoot");
        _disc = GetNode<FarmWheelDisc>("%Disc");
        _name = GetNode<Label>("%ChoiceName");
        _details = GetNode<Label>("%ChoiceDetails");
        _time = GetNode<Label>("%WheelTime");
        _confirm = GetNode<Button>("%ConfirmChoice");
        _close = GetNode<Button>("%CloseWheel");
        _nextPage = GetNode<Button>("%NextWheelPage");
        _disc.SelectionChanged += RefreshDetails;
        _disc.ConfirmRequested += RequestConfirmation;
        _confirm.Pressed += RequestConfirmation;
        _close.Pressed += () => ActionRequested?.Invoke(FarmSemanticAction.CloseWheel);
        _nextPage.Pressed += () => ActionRequested?.Invoke(FarmSemanticAction.NextWheelPage);
        // Cardinal input chooses a sector. Godot's fallback spatial focus navigation must stay here;
        // Tab deliberately leaves the disc for the explicit confirmation and close controls.
        NodePath discPath = _disc.GetPathTo(_disc);
        _disc.FocusNeighborTop = discPath;
        _disc.FocusNeighborBottom = discPath;
        _disc.FocusNeighborLeft = discPath;
        _disc.FocusNeighborRight = discPath;
        SetFocusChain(false);
    }

    private void SetFocusChain(bool hasPages)
    {
        Control[] controls = hasPages ? [_disc, _confirm, _nextPage, _close] : [_disc, _confirm, _close];
        for (int i = 0; i < controls.Length; i++)
        {
            controls[i].FocusNext = controls[i].GetPathTo(controls[(i + 1) % controls.Length]);
            controls[i].FocusPrevious = controls[i].GetPathTo(controls[(i + controls.Length - 1) % controls.Length]);
        }
    }

    internal void Open(IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> offers)
    {
        _returnFocus = GetViewport().GuiGetFocusOwner();
        Model = new FarmQuickWheelModel(offers);
        _nextPage.Visible = Model.PageCount > 1;
        SetFocusChain(Model.PageCount > 1);
        _disc.ShowModel(Model);
        _root.Show();
        _disc.GrabFocus();
    }

    internal void NextPage()
    {
        if (Model is null) return;
        Model.NextPage();
        _disc.ShowModel(Model);
        _disc.GrabFocus();
        RefreshDetails();
    }

    internal void Close()
    {
        _root.Hide();
        Model = null;
        if (IsInstanceValid(_returnFocus) && _returnFocus!.IsVisibleInTree()) _returnFocus.GrabFocus();
        _returnFocus = null;
    }

    internal void Present(LocalWorldView view, LocalWorldRunState runState, bool canConfirm, bool shared = false)
    {
        if (Model is null) return;
        _canConfirm = canConfirm;
        _time.Text = runState switch
        {
            LocalWorldRunState.Running => shared ? "Ready · shared world" : "World continuing",
            LocalWorldRunState.Resolving => "Resolving world changes",
            LocalWorldRunState.Waiting => shared ? "Your character is waiting" : "World waiting",
            LocalWorldRunState.Paused => shared ? "Your input paused · others may continue" : "World paused · Inspection only",
            _ => "World unavailable"
        };
        _time.Text += $" · {view.Calendar.MillisecondsWithinDay / 3_600_000:D2}:{view.Calendar.MillisecondsWithinDay / 60_000 % 60:D2}";
        RefreshDetails();
        _disc.QueueRedraw();
    }

    private void RefreshDetails()
    {
        _nextPage.Text = (Model?.NextPageName ?? "Actions") + " [Q]";
        FarmActionOffer? offer = Model?.Selected?.Offer;
        _name.Text = offer?.Label ?? "Choose an action";
        _details.Text = offer is null ? "Point to a symbol, press a direction, or use the left stick.\n\nSelecting only shows details."
            : $"Target: {offer.TargetName}\nCost: {offer.CostText}\n\n" + offer.DetailText
                + (offer.BlockingReason is { } reason ? "\nUnavailable: " + reason : "");
        _confirm.Disabled = !_canConfirm || offer?.CanExecute != true;
        _confirm.TooltipText = _details.Text;
    }

    private void RequestConfirmation()
    {
        if (_canConfirm && Model?.Selected?.Offer.CanExecute == true)
            ActionRequested?.Invoke(FarmSemanticAction.ConfirmWheel);
    }
}
