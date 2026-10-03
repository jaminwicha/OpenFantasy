using Godot;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Native pack inspection; controls select details and never equip, consume or transfer items.</summary>
internal sealed partial class FarmInventoryScreen : CanvasLayer
{
    private Control _root = null!;
    private GridContainer _grid = null!;
    private ScrollContainer _scroll = null!;
    private Label _title = null!;
    private Label _details = null!;
    private Label _summary = null!;
    private Label _time = null!;
    private Label _subject = null!;
    private Button _close = null!;
    private Button _giftToggle = null!;
    private Control _body = null!;
    private FarmGiftPanel _gifts = null!;
    private Control? _returnFocus;
    private FarmInventoryModel? _model;
    private IReadOnlyList<FarmInventoryEntry> _shown = Array.Empty<FarmInventoryEntry>();
    private readonly Dictionary<FarmInventoryKey, Button> _buttons = [];

    internal event Action? CloseRequested;
    internal event Action? GiftsReviewRequested;
    internal event Action<EntityId>? GiftHistoryRequested;
    internal bool IsOpen => _root.Visible;
    internal FarmInventoryPresentation? CurrentPresentation { get; private set; }
    internal ulong PresentationLifetime { get; private set; }
    internal ulong GiftOfferSelectionVersion => _gifts.OfferSelectionVersion;

    public override void _Ready()
    {
        _root = GetNode<Control>("%InventoryRoot");
        _grid = GetNode<GridContainer>("%Items");
        _scroll = GetNode<ScrollContainer>("%ItemScroll");
        _title = GetNode<Label>("%ItemName");
        _details = GetNode<Label>("%ItemDetails");
        _summary = GetNode<Label>("%PackSummary");
        _time = GetNode<Label>("%PackTime");
        _subject = GetNode<Label>("InventoryRoot/Panel/Column/Header/Title");
        _close = GetNode<Button>("%ClosePack");
        _giftToggle = GetNode<Button>("%GiftToggle");
        _body = GetNode<Control>("InventoryRoot/Panel/Column/Body");
        _gifts = GetNode<FarmGiftPanel>("%Gifts");
        _giftToggle.Pressed += ToggleGifts;
        _gifts.ReviewRequested += () => GiftsReviewRequested?.Invoke();
        _gifts.HistoryRequested += identity => GiftHistoryRequested?.Invoke(identity);
        _close.Pressed += () => CloseRequested?.Invoke();
    }

    internal void Open(FarmInventoryModel model, FarmGiftSelection? gifts = null)
    {
        _returnFocus = GetViewport().GuiGetFocusOwner();
        PresentationLifetime++;
        CurrentPresentation = null;
        _gifts.Suspend();
        _model = model;
        _gifts.Selection = gifts;
        _gifts.Hide();
        _body.Show();
        _giftToggle.Text = "Gifts";
        _root.Show();
    }

    internal void Close()
    {
        _root.Hide();
        PresentationLifetime++;
        CurrentPresentation = null;
        _gifts.Suspend();
        if (IsInstanceValid(_returnFocus) && _returnFocus!.IsVisibleInTree())
        {
            _returnFocus.GrabFocus();
        }
        _returnFocus = null;
    }

    internal void FocusSelection()
    {
        if (_model?.SelectedKey is { } key && _buttons.TryGetValue(key, out Button? button))
        {
            button.GrabFocus();
            _scroll.EnsureControlVisible(button);
        }
        else
        {
            _close.GrabFocus();
        }
    }

    internal void Present(LocalWorldView view, LocalWorldRunState runState, bool shared = false) =>
        Present(FarmInventoryPresentation.From(view), runState, shared);

    internal void Present(FarmInventoryPresentation view, LocalWorldRunState runState, bool shared = false)
    {
        if (!IsOpen || _model is null) return;
        CurrentPresentation = view;
        _model.Refresh(view);
        bool hasGifts = view.MaterialProposals is not null;
        if (_giftToggle.Visible != hasGifts)
        {
            _giftToggle.Visible = hasGifts;
            WireFocus();
        }
        if (!hasGifts && _gifts.Visible) ToggleGifts();
        _gifts.Present(view, _model);
        _time.Text = runState switch
        {
            LocalWorldRunState.Running => shared ? "Ready · shared world" : "World continuing",
            LocalWorldRunState.Resolving => "Resolving world changes",
            LocalWorldRunState.Waiting => shared ? "Your character is waiting" : "World waiting",
            LocalWorldRunState.Paused => shared ? "Your input paused · others may continue" : "World paused",
            _ => "World unavailable"
        };
        int hour = view.Calendar.MillisecondsWithinDay / 3_600_000;
        int minute = view.Calendar.MillisecondsWithinDay / 60_000 % 60;
        _time.Text += $" · Day {view.Calendar.Days + 1} · {hour:D2}:{minute:D2}";
        bool borrowed = view.Selection?.ControlWitness is not null;
        string name = view.Actors.FirstOrDefault(actor => actor.ActorId == view.ActorId)?.DisplayName ?? "Selected character";
        _subject.Text = borrowed ? name + "'s pack" : "Your pack";
        _subject.TooltipText = borrowed ? "Operating " + name + ". Your home character keeps its own belongings and obligations." : "Your own pack";
        _summary.Text = $"{view.Inventory.Lots.Count(lot => lot.Quantity > 0)} stacks · {view.Inventory.Quantity} units";
        if (borrowed) _summary.Text += " · Current permissions limit the details shown.";
        if (!_shown.SequenceEqual(_model.Entries))
        {
            Rebuild();
        }
        PresentDetails();
    }

    private void Rebuild()
    {
        bool restoreItemFocus = _buttons.Values.Any(button => button.HasFocus());
        foreach (Button button in _buttons.Values)
        {
            _grid.RemoveChild(button);
            button.QueueFree();
        }
        _buttons.Clear();
        _shown = _model!.Entries;
        foreach (FarmInventoryEntry entry in _shown)
        {
            var button = new Button
            {
                Text = $"{entry.Name}\n× {entry.Quantity}",
                TooltipText = entry.Name + "\n" + entry.Details,
                CustomMinimumSize = new Vector2(180, 72),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                ToggleMode = true
            };
            button.FocusEntered += () => Inspect(entry.Key);
            button.MouseEntered += () => Inspect(entry.Key);
            button.Pressed += () => Inspect(entry.Key);
            _grid.AddChild(button);
            _buttons.Add(entry.Key, button);
        }
        WireFocus();
        if (restoreItemFocus) FocusSelection();
    }

    private void WireFocus()
    {
        if (_gifts.Visible)
        {
            _gifts.WireFocus();
            return;
        }
        Button[] chain = _giftToggle.Visible ? [.. _buttons.Values, _giftToggle, _close] : [.. _buttons.Values, _close];
        for (int index = 0; index < chain.Length; index++)
        {
            NodePath previous = chain[index].GetPathTo(chain[(index + chain.Length - 1) % chain.Length]);
            NodePath next = chain[index].GetPathTo(chain[(index + 1) % chain.Length]);
            chain[index].FocusPrevious = previous;
            chain[index].FocusNext = next;
            chain[index].FocusNeighborTop = previous;
            chain[index].FocusNeighborLeft = previous;
            chain[index].FocusNeighborBottom = next;
            chain[index].FocusNeighborRight = next;
        }
    }

    internal void ShowGiftHistory(EntityId identity, MaterialProposalView? result, string? message) =>
        _gifts.ShowHistory(identity, result, message);

    internal void InvalidateGiftHistory()
    {
        PresentationLifetime++;
        _gifts.ClearHistory();
    }

    private void ToggleGifts()
    {
        bool show = !_gifts.Visible;
        InvalidateGiftHistory();
        _gifts.Visible = show;
        _body.Visible = !show;
        _giftToggle.Text = show ? "Items" : "Gifts";
        if (show) _gifts.FocusFirst();
        else { WireFocus(); FocusSelection(); }
    }

    private void Inspect(FarmInventoryKey key)
    {
        if (_model?.Select(key) != true) return;
        PresentDetails();
    }

    private void PresentDetails()
    {
        FarmInventoryEntry? selected = _model?.Selected;
        _title.Text = selected?.Name ?? "Your pack is empty";
        _details.Text = selected?.Details ?? "Gather supplies in the field to fill your pack.";
        foreach ((FarmInventoryKey key, Button button) in _buttons)
        {
            button.SetPressedNoSignal(key == selected?.Key);
        }
    }
}
