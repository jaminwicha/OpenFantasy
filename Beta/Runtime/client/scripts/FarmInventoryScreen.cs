using Godot;
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
    private Button _close = null!;
    private Control? _returnFocus;
    private FarmInventoryModel? _model;
    private IReadOnlyList<FarmInventoryEntry> _shown = Array.Empty<FarmInventoryEntry>();
    private readonly Dictionary<FarmInventoryKey, Button> _buttons = [];

    internal event Action? CloseRequested;
    internal bool IsOpen => _root.Visible;

    public override void _Ready()
    {
        _root = GetNode<Control>("%InventoryRoot");
        _grid = GetNode<GridContainer>("%Items");
        _scroll = GetNode<ScrollContainer>("%ItemScroll");
        _title = GetNode<Label>("%ItemName");
        _details = GetNode<Label>("%ItemDetails");
        _summary = GetNode<Label>("%PackSummary");
        _time = GetNode<Label>("%PackTime");
        _close = GetNode<Button>("%ClosePack");
        _close.Pressed += () => CloseRequested?.Invoke();
    }

    internal void Open(FarmInventoryModel model)
    {
        _returnFocus = GetViewport().GuiGetFocusOwner();
        _model = model;
        _root.Show();
    }

    internal void Close()
    {
        _root.Hide();
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

    internal void Present(LocalWorldView view, LocalWorldRunState runState)
    {
        if (!IsOpen || _model is null) return;
        _model.Refresh(view);
        _time.Text = runState switch
        {
            LocalWorldRunState.Running => "World continuing",
            LocalWorldRunState.Waiting => "World waiting",
            LocalWorldRunState.Paused => "World paused",
            _ => "World unavailable"
        };
        int hour = view.Calendar.MillisecondsWithinDay / 3_600_000;
        int minute = view.Calendar.MillisecondsWithinDay / 60_000 % 60;
        _time.Text += $" · Day {view.Calendar.Days + 1} · {hour:D2}:{minute:D2}";
        _summary.Text = $"{view.PlayerInventory.Lots.Count(lot => lot.Quantity > 0)} stacks · {view.PlayerInventory.Quantity} units";
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
        Button[] chain = [.. _buttons.Values, _close];
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
