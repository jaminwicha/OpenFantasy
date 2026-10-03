using Godot;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Explicit pack selections and explanations. Only the separate wheel can commit an action.</summary>
internal sealed partial class FarmGiftPanel : VBoxContainer
{
    private OptionButton _lots = null!;
    private OptionButton _recipients = null!;
    private OptionButton _offers = null!;
    private Button _less = null!;
    private Button _more = null!;
    private Button _prepare = null!;
    private Button _history = null!;
    private Label _quantity = null!;
    private Label _draft = null!;
    private Label _offer = null!;
    private Label _explanation = null!;
    private FarmInventoryPresentation? _view;
    private FarmInventoryModel? _inventory;
    private LocalMaterialProposalLot[] _lotRows = [];
    private LocalActorView[] _recipientRows = [];
    private MaterialProposalView[] _offerRows = [];
    private string[] _shownLots = [];
    private string[] _shownRecipients = [];
    private string[] _shownOffers = [];
    private string? _historyText;
    private bool _updating;
    internal FarmGiftSelection? Selection { get; set; }
    internal FarmInventoryPresentation? CurrentPresentation => _view;
    internal ulong OfferSelectionVersion { get; private set; }
    internal event Action? ReviewRequested;
    internal event Action<EntityId>? HistoryRequested;

    public override void _Ready()
    {
        _lots = GetNode<OptionButton>("%GiftLot");
        _recipients = GetNode<OptionButton>("%GiftRecipient");
        _offers = GetNode<OptionButton>("%GiftOffer");
        _less = GetNode<Button>("%GiftLess");
        _more = GetNode<Button>("%GiftMore");
        _prepare = GetNode<Button>("%PrepareGift");
        _history = GetNode<Button>("%GiftHistory");
        _quantity = GetNode<Label>("%GiftQuantity");
        _draft = GetNode<Label>("%GiftDraftDetails");
        _offer = GetNode<Label>("%GiftOfferDetails");
        _explanation = GetNode<Label>("GiftScroll/Content/Explanation");
        foreach (var picker in new[] { _lots, _recipients, _offers })
            picker.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _lots.ItemSelected += SelectLot;
        _recipients.ItemSelected += SelectRecipient;
        _offers.ItemSelected += SelectOffer;
        _less.Pressed += () => ChangeQuantity(false);
        _more.Pressed += () => ChangeQuantity(true);
        _prepare.Pressed += () => { if (CanEdit) Selection!.Prepare(_view!); RefreshDetails(); };
        _history.Pressed += () =>
        {
            if (CanEdit && _view!.CanReadHistory && Selection?.SelectedOffer is { } offer)
                HistoryRequested?.Invoke(offer.ProposalId);
        };
        var review = GetNode<Button>("%ReviewGifts");
        review.Pressed += () => { if (CanEdit) ReviewRequested?.Invoke(); };
        foreach (var (name, action) in new[] { ("GiftLot", "lot"), ("GiftRecipient", "recipient"),
            ("GiftOffer", "offer"), ("GiftLess", "less"), ("GiftMore", "more"),
            ("PrepareGift", "prepare"), ("GiftHistory", "history"), ("ReviewGifts", "review") })
            GetNode<Control>("%" + name).SetMeta("gift_action", action);
        var scroll = GetNode<ScrollContainer>("%GiftScroll");
        foreach (Control control in new Control[] { _lots, _recipients, _less, _more, _prepare, _offers, _history })
            control.FocusEntered += () => scroll.EnsureControlVisible(control);
    }

    internal void FocusFirst()
    {
        WireFocus();
        _lots.GrabFocus();
    }

    internal void WireFocus()
    {
        Control[] chain = [_lots, _recipients, _less, _more, _prepare, _offers,
            .. (_history.Visible ? new Control[] { _history } : []),
            GetNode<Button>("%ReviewGifts"), GetParent().GetNode<Button>("Header/GiftToggle"),
            GetParent().GetNode<Button>("Header/ClosePack")];
        for (int i = 0; i < chain.Length; i++)
        {
            chain[i].FocusPrevious = chain[i].GetPathTo(chain[(i + chain.Length - 1) % chain.Length]);
            chain[i].FocusNext = chain[i].GetPathTo(chain[(i + 1) % chain.Length]);
            chain[i].FocusNeighborTop = chain[i].FocusPrevious;
            chain[i].FocusNeighborLeft = chain[i].FocusPrevious;
            chain[i].FocusNeighborBottom = chain[i].FocusNext;
            chain[i].FocusNeighborRight = chain[i].FocusNext;
        }
    }

    internal void Present(LocalWorldView view, FarmInventoryModel inventory) =>
        Present(FarmInventoryPresentation.From(view), inventory);

    internal void Present(FarmInventoryPresentation view, FarmInventoryModel inventory)
    {
        _view = view;
        _inventory = inventory;
        Selection?.ObserveScope(view);
        if (Selection is null || !Visible) return;
        _explanation.Text = "Offers reserve no items. Acceptance moves items; healing and treatment permission are separate."
            + (view.IsSelected ? "\n" + view.AvailabilityNote : "")
            + (view.ContextBlocker is { } blocker ? "\n" + blocker : "");
        // Keep the visible popup's row identities stable until the user makes or cancels a choice.
        // The callback still validates that captured identity against the latest public world.
        if (_lots.GetPopup().Visible || _recipients.GetPopup().Visible || _offers.GetPopup().Visible) return;
        _updating = true;
        _lotRows = view.MaterialProposals?.Preparation?.Lots.ToArray() ?? [];
        _recipientRows = view.MaterialProposals?.Preparation?.RecipientActorIds.Select(id =>
            view.Actors.Single(actor => actor.ActorId == id)).ToArray() ?? [];
        _offerRows = view.MaterialProposals?.Offers.OrderBy(value => value.ProposalId).ToArray() ?? [];
        string lotPrompt = Selection.LotId is not null && !_lotRows.Any(lot => lot.LotId == Selection.LotId)
            ? "Selected stack unavailable" : "Choose an exact stack…";
        string[] lotLabels = [lotPrompt, .. _lotRows.Select((lot, index) =>
            $"{ItemName(lot.Definition.DefinitionId)} × {lot.Quantity} · stack {index + 1}")];
        Fill(_lots, lotLabels, ref _shownLots);
        _lots.Select(Array.FindIndex(_lotRows, lot => lot.LotId == Selection.LotId && lot.Kind == Selection.LotKind) + 1);
        Fill(_recipients, ["Choose a recipient…", .. _recipientRows.Select(actor => actor.DisplayName)], ref _shownRecipients);
        _recipients.Select(Array.FindIndex(_recipientRows, actor => actor.ActorId == Selection.RecipientActorId) + 1);
        Fill(_offers, ["Choose an offer…", .. _offerRows.Select(value =>
            $"{(value.ViewerIsSender ? "To" : "From")} {Partner(value)} · {value.Terms.Quantity} {ItemName(value.Terms.Definition.DefinitionId)} · {value.Status}")], ref _shownOffers);
        _offers.Select(Array.FindIndex(_offerRows, value => value.ProposalId == Selection.SelectedOffer?.ProposalId) + 1);
        SetItemIdentities(_lots, _lotRows.Select(lot => $"{lot.Kind}:{lot.LotId}"));
        SetItemIdentities(_recipients, _recipientRows.Select(actor => actor.ActorId.ToString()));
        SetItemIdentities(_offers, _offerRows.Select(offer => offer.ProposalId.ToString()));
        if (_history.Visible != view.CanReadHistory)
        {
            _history.Visible = view.CanReadHistory;
            WireFocus();
        }
        _updating = false;
        RefreshDetails();
    }

    internal void ClearHistory()
    {
        _historyText = null;
        OfferSelectionVersion++;
        RefreshDetails();
    }

    internal void Suspend()
    {
        foreach (var picker in new[] { _lots, _recipients, _offers }) picker.GetPopup().Hide();
        _view = null;
        _historyText = null;
        OfferSelectionVersion++;
    }

    private bool CanEdit => IsVisibleInTree() && _view is not null && Selection is not null;

    private static void SetItemIdentities(OptionButton control, IEnumerable<string> identities)
    {
        control.SetItemMetadata(0, "");
        int index = 1;
        foreach (string identity in identities) control.SetItemMetadata(index++, identity);
    }

    private static void Fill(OptionButton control, string[] labels, ref string[] shown)
    {
        if (shown.SequenceEqual(labels)) return;
        control.Clear();
        foreach (string label in labels)
        {
            control.AddItem(label);
            control.GetPopup().SetItemTooltip(control.ItemCount - 1, label);
        }
        shown = labels;
    }

    private void SelectLot(long index)
    {
        if (_updating || !IsVisibleInTree() || Selection is null || _view is null || index < 0) return;
        if (index == 0) Selection.ClearLot();
        else if (index <= _lotRows.Length) Selection.SelectLot(_view, _lotRows[index - 1].Kind, _lotRows[index - 1].LotId);
        RefreshDetails();
    }

    private void SelectRecipient(long index)
    {
        if (_updating || !IsVisibleInTree() || Selection is null || _view is null || index < 0) return;
        if (index == 0) Selection.ClearRecipient();
        else if (index <= _recipientRows.Length) Selection.SelectRecipient(_view, _recipientRows[index - 1].ActorId);
        RefreshDetails();
    }

    private void SelectOffer(long index)
    {
        if (_updating || !IsVisibleInTree() || Selection is null || _view is null || index < 0) return;
        if (index == 0) Selection.ClearOffer();
        else if (index <= _offerRows.Length) Selection.SelectOffer(_view, _offerRows[index - 1].ProposalId);
        _historyText = null;
        OfferSelectionVersion++;
        RefreshDetails();
    }

    private void ChangeQuantity(bool increase)
    {
        if (!IsVisibleInTree() || Selection is null || _view is null) return;
        ulong current = Selection.Quantity;
        if (increase && current < ulong.MaxValue) Selection.SetQuantity(current + 1);
        else if (!increase && current > 1) Selection.SetQuantity(current - 1);
        RefreshDetails();
    }

    internal void ShowHistory(EntityId identity, MaterialProposalView? result, string? message)
    {
        if (!CanEdit || !_view!.CanReadHistory || Selection?.SelectedOffer?.ProposalId != identity) return;
        _historyText = message ?? (result is null ? "No recorded offer is available to you."
            : $"Recorded: {result.Status}" + (result.ClosedAtSequence.HasValue ? " · terminal outcome" : ""));
        RefreshDetails();
    }

    private void RefreshDetails()
    {
        if (Selection is null || _view is null) return;
        foreach (var picker in new[] { _lots, _recipients, _offers })
            picker.TooltipText = picker.Selected >= 0 ? picker.GetItemText(picker.Selected) : "";
        _quantity.Text = Selection.Quantity.ToString();
        var lot = _lotRows.FirstOrDefault(value => value.LotId == Selection.LotId && value.Kind == Selection.LotKind);
        _less.Disabled = Selection.Quantity <= 1;
        _more.Disabled = lot is null || Selection.Quantity >= lot.Quantity;
        _prepare.Disabled = lot is null || Selection.RecipientActorId is null || Selection.Quantity > lot.Quantity;
        _draft.Text = Selection.PreparedGift is { } prepared
            ? (_view.MaterialProposals?.Preparation?.CanStillPropose(prepared) == true
                ? $"Prepared: {prepared.Terms.Quantity} {ItemName(prepared.Terms.Definition.DefinitionId)} for {ActorName(prepared.Terms.RecipientActorId)}.\nReview in the Gifts wheel to offer it."
                : "The prepared choice is no longer available. Select and prepare it again.")
            : "Choose one stack, a recipient and quantity, then prepare. Nothing is sent while browsing.";
        var selected = Selection.SelectedOffer;
        var current = _offerRows.FirstOrDefault(value => value.ProposalId == selected?.ProposalId);
        _history.Disabled = selected is null || !_view.CanReadHistory;
        _offer.Text = selected is null ? "Choose an incoming or outgoing offer to inspect it."
            : $"{(selected.ViewerIsSender ? "To" : "From")} {Partner(selected)} · {selected.Terms.Quantity} {ItemName(selected.Terms.Definition.DefinitionId)}\n"
                + (current is null ? _view.CanReadHistory ? "Outside the live list; inspect recorded history."
                    : "Outside the current offers. Recorded history is not included in this control permission."
                    : $"Status: {current.Status}")
                + "\n" + (_historyText ?? "Review the Gifts wheel to accept, decline or withdraw.");
        _draft.TooltipText = _draft.Text + "\nPrepared terms and expiry stay fixed until explicitly prepared again.";
        _offer.TooltipText = _offer.Text;
    }

    private string ActorName(EntityId id) => _view?.Actors.FirstOrDefault(actor => actor.ActorId == id)?.DisplayName ?? "Named traveler";
    private string Partner(MaterialProposalView offer) => ActorName(offer.ViewerIsSender ? offer.Terms.RecipientActorId : offer.Terms.SenderActorId);
    private string ItemName(EntityId definition) => _inventory?.Entries.FirstOrDefault(entry => entry.Key.DefinitionId == definition)?.Name
        ?? _view?.ItemName(definition) ?? "item";
}
