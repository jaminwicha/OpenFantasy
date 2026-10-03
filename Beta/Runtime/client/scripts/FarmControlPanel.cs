using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Explicit consent and selection controls. Rows and reviews capture the values actually presented.</summary>
internal sealed partial class FarmControlPanel : CanvasLayer
{
    private PanelContainer _panel = null!;
    private VBoxContainer _rows = null!;
    private Label _status = null!;
    private OptionButton _recipient = null!;
    private GridContainer _scopes = null!;
    private SpinBox _units = null!;
    private readonly Dictionary<WorldControlCapabilities, CheckBox> _checks = [];
    private LocalControlPermissionBoard? _board;
    internal bool IsOpen => _panel.Visible;
    internal event Action? OpenRequested;
    internal event Action? CloseRequested;
    internal event Action? RefreshRequested;
    internal event Action<LocalControlPermissionBoard, ControlPermissionIntent>? PermissionRequested;
    internal event Action<LocalControlAcquisition>? AcquisitionRequested;
    internal event Action<WorldControlWitness>? ReleaseRequested;
    internal event Action<LocalSelectedPlayerObservation, EntityId>? SelectionRequested;
    internal event Action<LocalSelectedPlayerObservation, LocalTradeInquiry>? InquiryRequested;
    internal event Action<FarmActionOffer>? ConfirmationRequested;

    public override void _Ready()
    {
        _panel = GetNode<PanelContainer>("Root/Panel");
        _rows = GetNode<VBoxContainer>("%Rows");
        _status = GetNode<Label>("%Status");
        _recipient = GetNode<OptionButton>("%Recipient");
        _scopes = GetNode<GridContainer>("%Scopes");
        _units = GetNode<SpinBox>("%Units");
        GetNode<Button>("%Characters").Pressed += () => OpenRequested?.Invoke();
        GetNode<Button>("%Close").Pressed += () => CloseRequested?.Invoke();
        GetNode<Button>("%Refresh").Pressed += () => RefreshRequested?.Invoke();
        GetNode<Button>("%Grant").Pressed += Grant;
        foreach (var scope in Enum.GetValues<WorldControlCapabilities>().Where(scope => scope != 0
            && ((ulong)scope & ((ulong)scope - 1)) == 0))
        {
            var check = new CheckBox { Text = ScopeName(scope), TooltipText = scope.ToString() };
            _checks.Add(scope, check);
            _scopes.AddChild(check);
        }
        _panel.Hide();
    }

    internal void Enable(bool enabled) => GetNode<Button>("%Characters").Visible = enabled;
    internal void Open() { _panel.Show(); GetNode<Button>("%Refresh").GrabFocus(); }
    internal void Close()
    {
        _panel.Hide();
        _board = null;
        foreach (Node row in _rows.GetChildren()) { _rows.RemoveChild(row); row.QueueFree(); }
    }
    internal void Notice(string text) => _status.Text = text;

    internal void Present(LocalControlPermissionBoard board, LocalSelectedPlayerObservation? selected, FarmClientSession session)
    {
        _board = board;
        _recipient.Clear();
        var recipients = board.HumanActorIds.Where(actor => actor != board.HomeActorId).ToArray();
        foreach (var actor in recipients) _recipient.AddItem(Name(actor));
        foreach (Node row in _rows.GetChildren()) { _rows.RemoveChild(row); row.QueueFree(); }
        foreach (var slot in board.Slots)
        {
            AddText($"{Name(slot.OwnerActorId)} → {Name(slot.GranteeActorId)}: {slot.Status}\n{slot.Capabilities}");
            if (slot.GranteeActorId == board.HomeActorId && slot.Status == WorldControlPermissionStatus.Offered)
                AddButton("Accept these scopes", () => PermissionRequested?.Invoke(board,
                    new AcceptControlPermissionIntent(slot.OwnerActorId, slot.Revision, slot.GrantCommandId!.Value, slot.TermsHash!)));
            if (slot.OwnerActorId == board.HomeActorId && slot.Status != WorldControlPermissionStatus.Revoked)
                AddButton("Revoke " + Name(slot.GranteeActorId), () => PermissionRequested?.Invoke(board,
                    new RevokeControlPermissionIntent(slot.GranteeActorId, slot.Revision)));
            if (slot.Acquisition is { } acquisition)
                AddButton("Acquire " + Name(slot.OwnerActorId), () => AcquisitionRequested?.Invoke(acquisition));
        }
        if (selected is not null)
        {
            AddText("Operating: " + Name(selected.Selection.ActorId) + ". Changing character requires Resume.");
            if (selected.Selection.ActorId != board.HomeActorId)
                AddButton("Select home character", () => SelectionRequested?.Invoke(selected, board.HomeActorId));
            if (board.HeldWitness is { } witness)
            {
                if (selected.Selection.ActorId != witness.OwnerActorId)
                    AddButton("Select " + Name(witness.OwnerActorId), () => SelectionRequested?.Invoke(selected, witness.OwnerActorId));
                AddButton("Release borrowed control", () => ReleaseRequested?.Invoke(witness));
            }
            if (selected.ControlledView?.Inventory is { } inventory)
            {
                AddText("Selected character's pack");
                foreach (var lot in inventory.Lots) AddText($"{lot.Quantity} × {lot.DefinitionId} · lot {lot.LotId}");
            }
            if (selected.ControlledView?.Trade is { } trade)
            {
                AddText("Selected character's trade · quantity is explicit below. Requests reserve nothing.");
                foreach (var stall in trade.Markets?.Stalls.Where(stall => stall.IsAvailableInPerson) ?? [])
                    AddButton("Ask to buy " + stall.CommodityName + " from " + Name(stall.MerchantActorId), () =>
                        InquiryRequested?.Invoke(selected, new LocalTradeInquiry(stall.MarketId, stall.Commodity,
                            LocalTradeDirection.Buy, checked((ulong)_units.Value))));
                foreach (var quote in trade.Quotes.Where(quote => !quote.ViewerIsIssuer && quote.ObservedIssuerRevision is not null))
                    AddButton("Review offer " + quote.QuoteId, () =>
                    {
                        try
                        {
                            FarmActionOffer offer = session.ReviewSelectedQuote(selected, quote, checked((ulong)_units.Value));
                            AddText(offer.Label + " · " + offer.CostText + "\n" + offer.DetailText);
                            AddButton("Confirm these exact terms", () => ConfirmationRequested?.Invoke(offer));
                        }
                        catch (InvalidOperationException error) { Notice(error.Message); }
                        catch (ArgumentException error) { Notice(error.Message); }
                    });
            }
        }
        Notice("Consent changes no ownership. Only checked scopes are granted; acquisition requires the owner's expired disconnect allowance.");

        string Name(EntityId actor) => selected?.HomeView.Actors.FirstOrDefault(value => value.ActorId == actor)?.DisplayName
            ?? (actor == board.HomeActorId ? "Your home character" : actor.ToString());
    }

    private void Grant()
    {
        if (_board is not { } board || _recipient.Selected < 0) return;
        var recipients = board.HumanActorIds.Where(actor => actor != board.HomeActorId).ToArray();
        if (_recipient.Selected >= recipients.Length) return;
        WorldControlCapabilities scopes = 0;
        foreach (var pair in _checks) if (pair.Value.ButtonPressed) scopes |= pair.Key;
        if (scopes == 0) { Notice("Choose the exact scopes to grant first."); return; }
        EntityId recipient = recipients[_recipient.Selected];
        ulong revision = board.Slots.FirstOrDefault(slot => slot.OwnerActorId == board.HomeActorId
            && slot.GranteeActorId == recipient)?.Revision ?? 0;
        PermissionRequested?.Invoke(board, new OfferControlPermissionIntent(recipient, revision, scopes));
    }

    private void AddText(string text) => _rows.AddChild(new Label { Text = text,
        AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
    private void AddButton(string text, Action action)
    {
        var button = new Button { Text = text, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            TooltipText = text, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        button.Pressed += action;
        _rows.AddChild(button);
    }
    private static string ScopeName(WorldControlCapabilities scope) => scope switch
    {
        WorldControlCapabilities.ConversationAndQuests => "Conversation / quests",
        WorldControlCapabilities.CareDisposition => "Care decisions",
        WorldControlCapabilities.PrivateHistory => "Private history",
        _ => scope.ToString()
    };
}
