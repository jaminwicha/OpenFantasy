using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Explicit consent and selection controls. Rows and reviews capture the values actually presented.</summary>
internal sealed partial class FarmControlPanel : CanvasLayer
{
    internal const string ActionMetadata = "control_action";
    internal const string ActorMetadata = "control_actor";
    internal const string ScopeMetadata = "control_scope";
    private PanelContainer _panel = null!;
    private VBoxContainer _rows = null!;
    private Label _status = null!;
    private OptionButton _recipient = null!;
    private GridContainer _scopes = null!;
    private readonly Dictionary<WorldControlCapabilities, CheckBox> _checks = [];
    private readonly List<EntityId> _recipients = [];
    private readonly List<Button> _actionButtons = [];
    private LocalControlPermissionBoard? _board;
    private EntityId? _recipientActor;
    private EntityId? _draftHome;
    private ulong _rowsGeneration;
    private bool _enabled;
    private bool _busy;
    internal bool IsOpen => _panel.Visible;
    internal LocalControlPermissionBoard? CurrentBoard => _board;
    internal event Action? OpenRequested;
    internal event Action? CloseRequested;
    internal event Action? RefreshRequested;
    internal event Action<LocalControlPermissionBoard, ControlPermissionIntent>? PermissionRequested;
    internal event Action<LocalControlAcquisition>? AcquisitionRequested;
    internal event Action<WorldControlWitness>? ReleaseRequested;
    internal event Action<LocalSelectedPlayerObservation, EntityId>? SelectionRequested;
    internal event Action? MarketRequested;

    public override void _Ready()
    {
        _panel = GetNode<PanelContainer>("Root/Panel");
        _rows = GetNode<VBoxContainer>("%Rows");
        _status = GetNode<Label>("%Status");
        _recipient = GetNode<OptionButton>("%Recipient");
        _scopes = GetNode<GridContainer>("%Scopes");
        GetNode<SpinBox>("%Units").Hide();
        GetNode<Button>("%Characters").Pressed += () => OpenRequested?.Invoke();
        GetNode<Button>("%Close").Pressed += () => CloseRequested?.Invoke();
        GetNode<Button>("%Refresh").Pressed += () => RefreshRequested?.Invoke();
        GetNode<Button>("%Grant").Pressed += Grant;
        _recipient.ItemSelected += index =>
        {
            _recipientActor = index >= 0 && index < _recipients.Count ? _recipients[(int)index] : null;
        };
        foreach (var (name, action) in new[] { ("Characters", "open"), ("Refresh", "refresh"), ("Close", "close"), ("Grant", "offer") })
            GetNode<Button>("%" + name).SetMeta(ActionMetadata, action);
        foreach (var scope in Enum.GetValues<WorldControlCapabilities>().Where(scope => scope != 0
            && ((ulong)scope & ((ulong)scope - 1)) == 0))
        {
            var check = new CheckBox { Text = ScopeName(scope), TooltipText = scope.ToString() };
            check.SetMeta(ScopeMetadata, scope.ToString());
            check.AddThemeFontSizeOverride("font_size", 14);
            _checks.Add(scope, check);
            _scopes.AddChild(check);
        }
        _panel.Hide();
    }

    internal void Enable(bool enabled)
    {
        _enabled = enabled;
        GetNode<Button>("%Characters").Visible = enabled && !IsOpen;
    }
    internal void Open()
    {
        _panel.Show();
        GetNode<Button>("%Characters").Hide();
        Notice("Reading control permissions…");
        GetNode<Button>("%Close").GrabFocus();
    }
    internal void Close()
    {
        _panel.Hide();
        _board = null;
        ClearRows();
        GetNode<Button>("%Characters").Visible = _enabled;
        if (_enabled) GetNode<Button>("%Characters").GrabFocus();
    }
    internal void Notice(string text)
    {
        _status.Text = text;
        _status.TooltipText = text;
    }

    internal void SetBusy(bool busy)
    {
        _busy = busy;
        GetNode<Button>("%Refresh").Disabled = busy;
        GetNode<Button>("%Grant").Disabled = busy || _board is null;
        _recipient.Disabled = busy;
        foreach (var check in _checks.Values) check.Disabled = busy;
        foreach (var button in _actionButtons) if (IsInstanceValid(button)) button.Disabled = busy;
    }

    internal void Present(LocalControlPermissionBoard board, LocalSelectedPlayerObservation? selected, FarmClientSession session)
    {
        if (!IsOpen) return;
        if (_draftHome != board.HomeActorId)
        {
            _recipientActor = null;
            foreach (var check in _checks.Values) check.SetPressedNoSignal(false);
            _draftHome = board.HomeActorId;
        }
        _board = board;
        _recipients.Clear();
        _recipients.AddRange(board.HumanActorIds.Where(actor => actor != board.HomeActorId));
        _recipient.Clear();
        foreach (var actor in _recipients)
        {
            _recipient.AddItem(Name(actor));
            _recipient.SetItemMetadata(_recipient.ItemCount - 1, actor.ToString());
        }
        int selectedRecipient = _recipientActor is { } chosen ? _recipients.IndexOf(chosen) : -1;
        _recipient.Select(selectedRecipient);
        if (selectedRecipient < 0) { _recipientActor = null; _recipient.Text = "Choose a person"; }
        ClearRows();
        foreach (var slot in board.Slots)
        {
            AddText($"{Name(slot.OwnerActorId)} → {Name(slot.GranteeActorId)}: {slot.Status}\n{slot.Capabilities}");
            if (slot.GranteeActorId == board.HomeActorId && slot.Status == WorldControlPermissionStatus.Offered)
                AddButton("Accept these scopes", () => PermissionRequested?.Invoke(board,
                    new AcceptControlPermissionIntent(slot.OwnerActorId, slot.Revision, slot.GrantCommandId!.Value, slot.TermsHash!)),
                    "accept", slot.OwnerActorId);
            if (slot.OwnerActorId == board.HomeActorId && slot.Status != WorldControlPermissionStatus.Revoked)
                AddButton("Revoke " + Name(slot.GranteeActorId), () => PermissionRequested?.Invoke(board,
                    new RevokeControlPermissionIntent(slot.GranteeActorId, slot.Revision)), "revoke", slot.GranteeActorId);
            if (slot.Acquisition is { } acquisition)
                AddButton("Acquire " + Name(slot.OwnerActorId), () => AcquisitionRequested?.Invoke(acquisition), "acquire", slot.OwnerActorId);
        }
        if (selected is not null)
        {
            AddText("Operating: " + Name(selected.Selection.ActorId) + ". Changing character requires Resume.");
            if (selected.Selection.ActorId != board.HomeActorId)
                AddButton("Select home character", () => SelectionRequested?.Invoke(selected, board.HomeActorId), "home", board.HomeActorId);
            if (board.HeldWitness is { } witness)
            {
                if (selected.Selection.ActorId != witness.OwnerActorId)
                    AddButton("Select " + Name(witness.OwnerActorId), () => SelectionRequested?.Invoke(selected, witness.OwnerActorId), "select", witness.OwnerActorId);
                AddButton("Release borrowed control", () => ReleaseRequested?.Invoke(witness), "release", witness.OwnerActorId);
            }
            if (selected.ControlledView?.Trade is not null)
                AddButton("Open selected character’s stall", () => MarketRequested?.Invoke());
        }
        Notice("Consent changes no ownership. Only checked scopes are granted; acquisition requires the owner's expired disconnect allowance.");
        SetBusy(_busy);

        string Name(EntityId actor) => selected?.HomeView.Actors.FirstOrDefault(value => value.ActorId == actor)?.DisplayName
            ?? (actor == board.HomeActorId ? "Your home character" : actor.ToString());
    }

    private void Grant()
    {
        if (!IsOpen || _busy || _board is not { } board) return;
        if (_recipientActor is not { } recipient || !_recipients.Contains(recipient))
        { Notice("Choose the person who may accept these scopes."); return; }
        WorldControlCapabilities scopes = 0;
        foreach (var pair in _checks) if (pair.Value.ButtonPressed) scopes |= pair.Key;
        if (scopes == 0) { Notice("Choose the exact scopes to grant first."); return; }
        ulong revision = board.Slots.FirstOrDefault(slot => slot.OwnerActorId == board.HomeActorId
            && slot.GranteeActorId == recipient)?.Revision ?? 0;
        PermissionRequested?.Invoke(board, new OfferControlPermissionIntent(recipient, revision, scopes));
    }

    private void ClearRows()
    {
        _rowsGeneration++;
        _actionButtons.Clear();
        foreach (Node row in _rows.GetChildren()) { _rows.RemoveChild(row); row.QueueFree(); }
    }

    private void AddText(string text, VBoxContainer? parent = null) => (parent ?? _rows).AddChild(new Label { Text = text,
        AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
    private void AddButton(string text, Action action, string? controlAction = null, EntityId? actor = null,
        VBoxContainer? parent = null)
    {
        var button = new Button { Text = text, ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            TooltipText = text, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        if (controlAction is not null) button.SetMeta(ActionMetadata, controlAction);
        if (actor is { } id) button.SetMeta(ActorMetadata, id.ToString());
        ulong generation = _rowsGeneration;
        button.Pressed += () =>
        {
            if (IsOpen && !_busy && generation == _rowsGeneration && button.IsInsideTree()) action();
        };
        _actionButtons.Add(button);
        (parent ?? _rows).AddChild(button);
    }
    private static string ScopeName(WorldControlCapabilities scope) => scope switch
    {
        WorldControlCapabilities.ConversationAndQuests => "Conversation / quests",
        WorldControlCapabilities.CareDisposition => "Care decisions",
        WorldControlCapabilities.PrivateHistory => "Private history",
        _ => scope.ToString()
    };
}
