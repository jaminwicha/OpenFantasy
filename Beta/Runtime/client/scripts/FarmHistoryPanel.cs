using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Markets;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Scene-owned, read-only history presentation. Every choice retains its displayed opening and page.</summary>
internal sealed partial class FarmHistoryPanel : CanvasLayer
{
    private Control _root = null!;
    private VBoxContainer _list = null!;
    private ScrollContainer _listScroll = null!;
    private ScrollContainer _detailsScroll = null!;
    private string _detailScrollHint = "";
    private Label _title = null!;
    private Label _details = null!;
    private Label _notice = null!;
    private Label _time = null!;
    private Label _page = null!;
    private Button _inspect = null!;
    private Button _next = null!;
    private Button _refresh = null!;
    private Button _close = null!;
    private Texture2D _exchangeIcon = null!;
    private Texture2D _giftIcon = null!;
    private Control? _returnFocus;
    private FarmHistoryModel? _model;
    private FarmHistoryCapture? _shownCapture;
    private FarmHistoryDetail? _shownDetail;
    private readonly Dictionary<(LocalPrivateHistoryKind Kind, EntityId Subject), Button> _rows = [];

    internal event Action? CloseRequested;
    internal event Action<FarmHistoryCapture, LocalPrivateHistoryKind, EntityId>? SelectionRequested;
    internal event Action<FarmSemanticAction, FarmHistoryCapture, LocalPrivateHistoryKind?, EntityId?>? NavigationRequested;

    public override void _Ready()
    {
        _root = GetNode<Control>("%HistoryRoot");
        _list = GetNode<VBoxContainer>("%HistoryList");
        _listScroll = GetNode<ScrollContainer>("%HistoryListScroll");
        _detailsScroll = GetNode<ScrollContainer>("%HistoryDetailsScroll");
        _detailScrollHint = _detailsScroll.TooltipText;
        _title = GetNode<Label>("%HistoryTitle");
        _details = GetNode<Label>("%HistoryDetails");
        _notice = GetNode<Label>("%HistoryNotice");
        _time = GetNode<Label>("%HistoryTime");
        _page = GetNode<Label>("%HistoryPage");
        _inspect = GetNode<Button>("%InspectHistory");
        _next = GetNode<Button>("%NextHistory");
        _refresh = GetNode<Button>("%RefreshHistory");
        _close = GetNode<Button>("%CloseHistory");
        _exchangeIcon = GD.Load<Texture2D>("res://assets/ui/market-offers.svg");
        _giftIcon = GD.Load<Texture2D>("res://assets/ui/give.svg");
        _close.Pressed += () => CloseRequested?.Invoke();
        BindNavigation(_inspect, FarmSemanticAction.InspectHistory);
        BindNavigation(_next, FarmSemanticAction.NextHistory);
        BindNavigation(_refresh, FarmSemanticAction.RefreshHistory);
        _detailsScroll.GuiInput += ScrollDetails;
    }

    private void BindNavigation(Button button, FarmSemanticAction action)
    {
        FarmHistoryCapture? pressedCapture = null;
        LocalPrivateHistoryEntry? pressedEntry = null;
        button.ButtonDown += () => { pressedCapture = _shownCapture; pressedEntry = _model?.SelectedEntry; };
        button.Pressed += () =>
        {
            if (pressedCapture is not { } capture) return;
            pressedCapture = null;
            NavigationRequested?.Invoke(action, capture, pressedEntry?.Kind, pressedEntry?.SubjectId);
            pressedEntry = null;
        };
    }

    internal void Open(FarmHistoryModel model)
    {
        _returnFocus = GetViewport().GuiGetFocusOwner();
        _model = model;
        _shownCapture = null;
        _shownDetail = null;
        _listScroll.ScrollVertical = 0;
        _detailsScroll.ScrollVertical = 0;
        _root.Show();
    }

    internal void Close()
    {
        _root.Hide();
        _model = null;
        _shownCapture = null;
        _shownDetail = null;
        _details.Text = "";
        _details.TooltipText = "";
        _detailsScroll.TooltipText = _detailScrollHint;
        _notice.Text = "";
        _title.Text = "History";
        _page.Text = "";
        ClearRows();
        if (IsInstanceValid(_returnFocus) && _returnFocus!.IsVisibleInTree()) _returnFocus.GrabFocus();
        _returnFocus = null;
    }

    internal void FocusSelection()
    {
        Button? button = _model?.SelectedEntry is { } selected
            ? _rows.GetValueOrDefault((selected.Kind, selected.SubjectId)) : _rows.Values.FirstOrDefault();
        if (button is null) { _close.GrabFocus(); return; }
        button.GrabFocus();
        _listScroll.EnsureControlVisible(button);
    }

    internal void Present(FarmHistoryModel model, LocalWorldRunState runState, bool shared, bool paused)
    {
        if (!_root.Visible) return;
        _model = model;
        if (_shownCapture != model.Capture || !_rows.Keys.SequenceEqual(model.Entries.Select(row => (row.Kind, row.SubjectId))))
            Rebuild(model);
        _shownCapture = model.Capture;
        _title.Text = string.IsNullOrWhiteSpace(model.ActorName) ? "History" : "History · " + model.ActorName;
        _notice.Text = model.Notice ?? model.State.ToString();
        _page.Text = model.Page is null ? "Completed exchanges and gifts"
            : $"{model.Entries.Count} recorded {(model.Entries.Count == 1 ? "entry" : "entries")} · "
                + (model.Page.Next is null ? "End of this history" : "More entries available");
        _page.TooltipText = "This page belongs to a fixed history snapshot. Next keeps that snapshot; Latest starts with the latest records.";
        foreach (var entry in model.Entries)
        {
            var row = _rows[(entry.Kind, entry.SubjectId)];
            row.SetPressedNoSignal(entry == model.SelectedEntry);
            row.Disabled = model.IsBusy;
        }
        _inspect.Disabled = !model.CanInspect;
        _next.Disabled = !model.CanNext;
        _refresh.Disabled = !model.CanRefresh;
        if (!Equals(_shownDetail, model.Detail))
        {
            _shownDetail = model.Detail;
            _detailsScroll.ScrollVertical = 0;
        }
        _details.Text = Describe(model);
        var definition = model.Detail?.MaterialProposal?.Terms.Definition ?? model.Detail?.PaidQuote?.Definition;
        _details.TooltipText = definition is null ? ""
            : $"Material definition: {definition.DefinitionId}\nVersion: {definition.Version}";
        _detailsScroll.TooltipText = _detailScrollHint
            + (definition is null ? "" : "\n" + _details.TooltipText);
        _time.Text = paused ? shared ? "Your input is paused · other players may continue" : "World paused"
            : runState switch
            {
                LocalWorldRunState.Running => shared ? "Shared world continuing" : "World continuing",
                LocalWorldRunState.Resolving => "Resolving accepted world changes",
                LocalWorldRunState.Waiting => "World waiting",
                LocalWorldRunState.Paused => shared ? "Your input is paused · other players may continue" : "World paused",
                _ => "World unavailable"
            };
        WireFocus();
    }

    private void Rebuild(FarmHistoryModel model)
    {
        bool rowFocused = _rows.Values.Any(button => button.HasFocus());
        ClearRows();
        FarmHistoryCapture capture = model.Capture;
        foreach (var entry in model.Entries)
        {
            string kind = entry.Kind == LocalPrivateHistoryKind.PaidQuote ? "Exchange" : "Gift";
            string status = entry.PaidQuoteStatus?.ToString() ?? entry.MaterialProposalStatus!.Value.ToString();
            var row = new Button
            {
                Text = $"{kind} · {status}\n{model.Name(entry.CounterpartyActorId)} · #{RecordReference(entry, model.Entries)}",
                TooltipText = $"{kind} with {model.Name(entry.CounterpartyActorId)} · {status}\nSelect, then Inspect to read its recorded terms.\nRecord: {entry.SubjectId}",
                ToggleMode = true, ClipText = true, ExpandIcon = true,
                Icon = entry.Kind == LocalPrivateHistoryKind.PaidQuote ? _exchangeIcon : _giftIcon,
                CustomMinimumSize = new Vector2(0, 60), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                Alignment = HorizontalAlignment.Left, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
            };
            row.AddThemeConstantOverride("icon_max_width", 28);
            row.SetMeta("history_kind", entry.Kind.ToString());
            row.SetMeta("history_id", entry.SubjectId.ToString());
            row.Pressed += () => SelectionRequested?.Invoke(capture, entry.Kind, entry.SubjectId);
            row.FocusEntered += () => _listScroll.EnsureControlVisible(row);
            _list.AddChild(row);
            _rows.Add((entry.Kind, entry.SubjectId), row);
        }
        if (rowFocused) FocusSelection();
    }

    private static string RecordReference(LocalPrivateHistoryEntry entry, IReadOnlyList<LocalPrivateHistoryEntry> entries)
    {
        string identity = entry.SubjectId.ToString();
        for (int length = 6; length < identity.Length; length += 2)
        {
            string suffix = identity[^length..];
            if (entries.Count(other => other.SubjectId.ToString().EndsWith(suffix, StringComparison.Ordinal)) == 1) return suffix;
        }
        return identity;
    }

    private void ClearRows()
    {
        foreach (Node child in _list.GetChildren()) { _list.RemoveChild(child); child.QueueFree(); }
        _rows.Clear();
    }

    private static string Describe(FarmHistoryModel model)
    {
        if (model.Detail?.MaterialProposal is { } gift)
        {
            var terms = gift.Terms;
            return $"Gift · {gift.Status}\n\nFrom {model.Name(terms.SenderActorId)}\nTo {model.Name(terms.RecipientActorId)}"
                + $"\n\n{terms.Quantity} × {model.ItemName(terms.Definition.DefinitionId)}";
        }
        if (model.Detail?.PaidQuote is { } quote)
        {
            string offer = quote.Side == SpotQuoteSide.IssuerSells ? "Offered to sell" : "Offered to buy";
            string settled = quote.FilledQuantity is { } units
                ? $"\n\nCompleted: {units} units\nPayment: {quote.FilledPayment} currency units\nFees: {quote.FilledFees} currency units"
                : "\n\nThis offer closed without a completed exchange.";
            return $"Exchange · {quote.Status}\n\nIssuer: {model.Name(quote.IssuerActorId)}"
                + $"\nCounterparty: {model.Name(quote.CounterpartyActorId)}\n\n{offer} up to {quote.QuantityAllowance} × {model.ItemName(quote.Definition.DefinitionId)}"
                + $"\nPrice per unit: {quote.UnitPrice} currency units\nFees per unit: {quote.UnitFees} currency units"
                + settled;
        }
        return model.SelectedEntry is { } selected
            ? $"{(selected.Kind == LocalPrivateHistoryKind.PaidQuote ? "Exchange" : "Gift")} with {model.Name(selected.CounterpartyActorId)}"
                + "\n\nChoose Inspect to retrieve this record's exact historical terms."
            : "Choose a completed exchange or gift, then Inspect.\n\nBrowsing history does not move goods, spend money or change permissions.";
    }

    private void ScrollDetails(InputEvent input)
    {
        if (!_detailsScroll.HasFocus() || !input.IsPressed()) return;
        int direction = input.IsAction("ui_down") ? 1 : input.IsAction("ui_up") ? -1 : 0;
        if (direction == 0) return;
        _detailsScroll.ScrollVertical += direction * 48;
        _detailsScroll.AcceptEvent();
    }

    private void WireFocus()
    {
        Control[] chain = [.. _rows.Values.Where(button => !button.Disabled), _detailsScroll,
            .. new[] { _inspect, _refresh, _next }.Where(button => !button.Disabled), _close];
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
