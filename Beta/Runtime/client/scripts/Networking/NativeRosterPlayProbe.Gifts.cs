using System.Globalization;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client.Networking;

/// <summary>Actual pack widgets and a separately confirmed wheel choice; captured evidence never submits work.</summary>
internal sealed partial class NativeRosterPlayProbe
{
    private readonly List<object> _giftSnapshots = [];
    private readonly List<object> _giftCaptures = [];
    private FarmActionOffer? _reviewedGift;
    private object? _giftOpeningLayout;
    private FarmInventoryScreen GiftPack => _game.GetNode<FarmInventoryScreen>("FarmInventoryScreen");
    private FarmGiftPanel GiftPanel => GiftPack.GetNode<FarmGiftPanel>("%Gifts");
    private FarmQuickWheel GiftWheel => _game.GetNode<FarmQuickWheel>("FarmQuickWheel");
    private LocalMaterialProposalView? GiftView => Selected.ControlledView is { } controlled
        ? controlled.MaterialProposals : Selected.HomeView.MaterialProposals;

    private Task GiftUntil(Func<bool> predicate, string failure) => _driver.Until(predicate, failure, 5);

    private async Task OpenGifts()
    {
        _giftOpeningLayout = null;
        await CloseSelectionPanel();
        MarketProbeDriver.Require(!_game.IsWheelOpen, "Cancel the current wheel before opening the gift pack.");
        var selection = Selected.Selection;
        MarketProbeDriver.Require(GiftView is not null, "The selected character has no disclosed Inventory gift view.");
        var opening = System.Diagnostics.Stopwatch.StartNew();
        Task OpeningUntil(Func<bool> predicate, string failure) => _driver.Until(predicate, failure, 5 - opening.Elapsed.TotalSeconds);
        if (!_game.IsInventoryOpen) _driver.Tap(Key.I);
        await OpeningUntil(() => _game.IsInventoryOpen && GiftPack.IsOpen, "The actual selected pack did not open.");
        if (!GiftPanel.IsVisibleInTree())
        {
            Button toggle = GiftPack.GetNode<Button>("%GiftToggle");
            Rect2? previous = null;
            bool drawn = DisplayServer.GetName() == "headless";
            void OnDraw() => drawn = true;
            RenderingServer.FramePostDraw += OnDraw;
            // Opening makes the pack visible synchronously; container layout settles on later frames.
            // Wait before the one click, which otherwise could capture the hidden panel's old coordinates.
            try
            {
                await OpeningUntil(() =>
                {
                    Rect2 bounds = toggle.GetGlobalRect();
                    bool ready = drawn && toggle.IsVisibleInTree() && !toggle.Disabled && bounds.Size.X > 0 && bounds.Size.Y > 0
                        && GetViewport().GetVisibleRect().Encloses(bounds) && previous == bounds;
                    _giftOpeningLayout = new
                    {
                        Drawn = drawn, Visible = toggle.IsVisibleInTree(), toggle.Disabled,
                        Bounds = Bounds(bounds), Previous = previous is { } last ? Bounds(last) : null,
                        Viewport = Bounds(GetViewport().GetVisibleRect()), Stable = previous == bounds,
                        Minimum = toggle.GetCombinedMinimumSize(), Ready = ready
                    };
                    previous = bounds;
                    return ready;
                }, "The actual Gifts button did not settle inside the viewport before input.");
            }
            finally { RenderingServer.FramePostDraw -= OnDraw; }
            await _driver.Click(toggle);
        }
        await OpeningUntil(() => GiftPanel.IsVisibleInTree() && GiftPanel.Selection is not null
            && _game.PresentedSelection?.Selection == selection && GiftPanel.CurrentPresentation?.Selection == selection,
            "The actual Gifts panel did not present this selection.");
        MarketProbeDriver.Require(Selected.Selection == selection, "Selection changed while opening the gift pack.");
    }

    private async Task CloseGifts()
    {
        if (_game.IsWheelOpen) _driver.Joy(JoyButton.B);
        await GiftUntil(() => !_game.IsWheelOpen, "The actual Cancel input did not close the gift wheel.");
        if (_game.IsInventoryOpen) await _driver.Click(GiftPack.GetNode<Button>("%ClosePack"));
        await GiftUntil(() => !_game.IsInventoryOpen, "The actual Close pack button did not close the gift pack.");
    }

    private async Task ChooseGiftLot(string kindText, EntityId lotId)
    {
        MarketProbeDriver.Require(Enum.TryParse(kindText, out ConsentedMaterialLotKind kind)
            && Enum.IsDefined(kind), "Unknown explicit gift lot kind.");
        await OpenGifts();
        await ChooseGiftRow(GiftPanel.GetNode<OptionButton>("%GiftLot"), $"{kind}:{lotId}");
        MarketProbeDriver.Require(GiftPanel.Selection!.LotId == lotId && GiftPanel.Selection.LotKind == kind,
            "The actual dropdown selected different gift stock.");
    }

    private async Task ChooseGiftRecipient(EntityId actor)
    {
        await OpenGifts();
        await ChooseGiftRow(GiftPanel.GetNode<OptionButton>("%GiftRecipient"), actor.ToString());
        MarketProbeDriver.Require(GiftPanel.Selection!.RecipientActorId == actor,
            "The actual dropdown selected another recipient.");
    }

    private async Task ChooseGiftQuantity(string text)
    {
        MarketProbeDriver.Require(ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong quantity)
            && quantity is > 0 and <= 32, "This finite input probe supports an explicit quantity from one to thirty-two.");
        await OpenGifts();
        for (int clicks = 0; GiftPanel.Selection!.Quantity != quantity; clicks++)
        {
            MarketProbeDriver.Require(clicks < 32, "Quantity input did not reach its exact bounded choice.");
            bool more = GiftPanel.Selection.Quantity < quantity;
            ulong before = GiftPanel.Selection.Quantity;
            await _driver.Click(GiftPanel.GetNode<Button>(more ? "%GiftMore" : "%GiftLess"));
            await GiftUntil(() => GiftPanel.Selection.Quantity == (more ? before + 1 : before - 1),
                "The actual quantity button did not change one unit.");
        }
    }

    private async Task PrepareGift()
    {
        await OpenGifts();
        int before = _submissions.Count;
        var selection = Selected.Selection;
        await _driver.Click(GiftPanel.GetNode<Button>("%PrepareGift"));
        await GiftUntil(() => GiftPanel.Selection?.PreparedGift is not null, "The actual Prepare button captured no gift.");
        var draft = GiftPanel.Selection!;
        var intent = draft.PreparedGift!;
        MarketProbeDriver.Require(Selected.Selection == selection && intent.Terms.SenderActorId == selection.ActorId
            && intent.Terms.LotId == draft.LotId && intent.Terms.LotKind == draft.LotKind
            && intent.Terms.RecipientActorId == draft.RecipientActorId && intent.Terms.Quantity == draft.Quantity
            && _submissions.Count == before && !_game.HasPendingAction,
            "Preparation changed scope, substituted terms or submitted an action.");
    }

    private async Task ChooseGiftOffer(EntityId proposal)
    {
        await OpenGifts();
        await ChooseGiftRow(GiftPanel.GetNode<OptionButton>("%GiftOffer"), proposal.ToString());
        MarketProbeDriver.Require(GiftPanel.Selection!.SelectedOffer?.ProposalId == proposal,
            "The actual offer dropdown selected a different saved proposal.");
    }

    private async Task ChooseGiftRow(OptionButton control, string identity)
    {
        var choosing = System.Diagnostics.Stopwatch.StartNew();
        Task ChoiceUntil(Func<bool> predicate, string failure) => _driver.Until(predicate, failure, 5 - choosing.Elapsed.TotalSeconds);
        await _driver.Frame();
        int row = Enumerable.Range(1, control.ItemCount - 1)
            .Single(index => control.GetItemMetadata(index).AsString() == identity);
        var selection = Selected.Selection;
        PopupMenu popup = control.GetPopup();
        MarketProbeDriver.Require(!popup.Visible, "A previous gift dropdown choice is still open.");
        // A tall pointer-opened popup can cover the button and consume the same release as a row
        // choice. Follow the panel's real focus chain and open with one key before choosing a row.
        await _driver.Reach(control);
        MarketProbeDriver.Require(choosing.Elapsed.TotalSeconds < 5, "Gift dropdown focus navigation exceeded five seconds.");
        _driver.Tap(Key.Enter);
        await ChoiceUntil(() => popup.Visible, "The actual gift dropdown did not open.");
        for (int moves = 0; popup.GetFocusedItem() != row; moves++)
        {
            MarketProbeDriver.Require(moves <= control.ItemCount && choosing.Elapsed.TotalSeconds < 5,
                "Gift dropdown keyboard input did not reach its exact row within five seconds.");
            _driver.Tap(Key.Down);
            await _driver.Frame();
        }
        _driver.Tap(Key.Enter);
        await ChoiceUntil(() => !popup.Visible && control.Selected == row, "Gift dropdown input did not retain its exact row.");
        MarketProbeDriver.Require(Selected.Selection == selection && control.GetItemMetadata(row).AsString() == identity,
            "Gift dropdown scope or captured row identity changed during input.");
    }

    private async Task ReviewGift(string actionText, bool blocked)
    {
        MarketProbeDriver.Require(Enum.TryParse(actionText, out FarmSemanticAction action)
            && MaterialProposalActionOffers.IsGiftAction(action), "Unknown explicit gift wheel action.");
        await OpenGifts();
        var selection = Selected.Selection;
        int before = _submissions.Count;
        await GiftUntil(() => _game.CurrentOffers.GetValueOrDefault(action) is { } offer
            && offer.Scope.Selection == selection && offer.CanExecute != blocked,
            "The exact prepared gift action has not reached its requested availability.");
        await _driver.Click(GiftPanel.GetNode<Button>("%ReviewGifts"));
        await GiftUntil(() => _game.IsWheelOpen && GiftWheel.Model?.PageName == "Gifts", "The actual Review button did not open the Gifts wheel.");
        MarketProbeDriver.Require(GiftWheel.Model!.Selected is null, "Review silently selected a gift action.");
        int sector = Enumerable.Range(0, 4).Single(index => GiftWheel.Model.Slots[index]?.Action == action);
        _driver.Joy(new[] { JoyButton.DpadUp, JoyButton.DpadRight, JoyButton.DpadDown, JoyButton.DpadLeft }[sector]);
        await GiftUntil(() => GiftWheel.Model?.Selected?.Action == action, "Controller input did not select the exact gift action.");
        _reviewedGift = GiftWheel.Model!.Selected!.Offer;
        MarketProbeDriver.Require(_reviewedGift.Scope.Selection == selection && Selected.Selection == selection
            && _reviewedGift.CanExecute != blocked && _submissions.Count == before && !_game.HasPendingAction,
            "Gift review changed scope, availability or submitted a command before confirmation.");
    }

    private async Task ConfirmGift(string expected)
    {
        FarmActionOffer reviewed = _reviewedGift ?? throw new InvalidOperationException("No actual gift wheel choice was reviewed.");
        int before = _submissions.Count;
        if (expected == "blocked")
        {
            MarketProbeDriver.Require(!_game.IsWheelOpen || _game.IsLocallyPaused
                || GiftWheel.Model?.Selected?.Offer.CanExecute != true,
                "The expected blocked confirmation is still executable.");
            _driver.Joy(JoyButton.A);
            await _driver.Frame();
            await _driver.Frame();
            MarketProbeDriver.Require(_submissions.Count == before && !_game.HasPendingAction,
                "Blocked gift confirmation submitted an action.");
            return;
        }
        WorldActionRejection rejection = WorldActionRejection.None;
        MarketProbeDriver.Require(expected == "Applied" || Enum.TryParse(expected, out rejection)
            && Enum.IsDefined(rejection) && rejection != WorldActionRejection.None, "Unknown expected gift receipt.");
        MarketProbeDriver.Require(!_game.IsLocallyPaused && _game.IsWheelOpen
            && GiftWheel.Model?.Selected?.Offer is { CanExecute: true } current
            && current.Action == reviewed.Action && current.Intent == reviewed.Intent
            && current.Scope == reviewed.Scope && reviewed.Scope.Selection == Selected.Selection
            && _game.PresentedSelection?.Selection == Selected.Selection,
            "The original reviewed gift is unavailable or stale; it will not be refreshed or retried.");
        _driver.Joy(JoyButton.A);
        await GiftUntil(() => _submissions.Count == before + 1 && _submissions[^1].Receipt.IsCompleted
            && !_game.HasPendingAction && !_game.IsWheelOpen, "One actual gift confirmation did not complete its original receipt.");
        var submission = _submissions[^1];
        WorldActionCommand command = WorldActionCodec.Decode(submission.CanonicalCommandBytes());
        MarketProbeDriver.Require(!submission.IsRetry && submission.Receipt.IsCompletedSuccessfully
            && submission.Receipt.Result.Rejection == rejection
            && (rejection != WorldActionRejection.None || submission.Receipt.Result.Status == CommandReceiptStatus.Applied)
            && command.Intent == reviewed.Intent && command.ActorId == reviewed.Scope.ActorId
            && command.PrincipalId == reviewed.Scope.PrincipalId && command.OriginSessionId == reviewed.Scope.SessionId
            && command.WorldId == reviewed.Scope.WorldId && command.BranchId == reviewed.Scope.BranchId
            && command.ExpectedActorRevision == reviewed.Scope.ExpectedActorRevision
            && command.ControlWitness == reviewed.Scope.Selection!.ControlWitness,
            "The original native gift receipt, terms or truthful operator differ; nothing was retried.");
    }

    private async Task CancelGift()
    {
        int before = _submissions.Count;
        foreach (var dropdown in new[] { "%GiftLot", "%GiftRecipient", "%GiftOffer" })
            if (GiftPanel.GetNode<OptionButton>(dropdown).GetPopup().Visible)
            {
                _driver.Tap(Key.Escape);
                await GiftUntil(() => !GiftPanel.GetNode<OptionButton>(dropdown).GetPopup().Visible,
                    "The actual cancel key did not close the gift dropdown.");
            }
        await CloseGifts();
        MarketProbeDriver.Require(_submissions.Count == before && !_game.HasPendingAction, "Cancelling gift inspection submitted an action.");
    }

    private void SnapshotGift(string name)
    {
        MarketProbeDriver.Require(name.Length is > 0 and <= 64 && _giftSnapshots.Count < 128,
            "Gift snapshot labels and count are bounded.");
        _giftSnapshots.Add(new { Sequence = _sequence, Name = name, Submissions = _submissions.Count,
            Selection = SelectionEvidence(), Gifts = GiftEvidence() });
    }

    private async Task CaptureGifts()
    {
        MarketProbeDriver.Require(GiftPanel.IsVisibleInTree(), "Open the actual gift panel before its rendered capture.");
        foreach (string name in new[] { "GiftLot", "GiftRecipient", "GiftLess", "GiftMore", "PrepareGift", "GiftOffer", "GiftHistory", "ReviewGifts" })
        {
            Control control = GiftPanel.GetNode<Control>("%" + name);
            if (!control.IsVisibleInTree()) continue;
            await _driver.Hover(control);
            string suffix = "-gift-" + name;
            await Capture(5, suffix);
            Rect2 rectangle = control.GetGlobalRect();
            MarketProbeDriver.Require(GetViewport().GetVisibleRect().Encloses(rectangle), "A rendered gift control lies outside the viewport.");
            for (Node? ancestor = control.GetParent(); ancestor is not null; ancestor = ancestor.GetParent())
                if (ancestor is ScrollContainer scroll)
                    MarketProbeDriver.Require(scroll.GetGlobalRect().Encloses(rectangle), "A scrolled gift control is still clipped.");
            _giftCaptures.Add(new { Sequence = _sequence, Control = name, Bounds = Bounds(rectangle), Image = $"{_sequence:D4}{suffix}.png" });
        }
        foreach (string name in new[] { "GiftDraftDetails", "GiftOfferDetails" })
        {
            Label label = GiftPanel.GetNode<Label>("%" + name);
            await _driver.Hover(label);
            await Capture(5, "-gift-" + name);
            MarketProbeDriver.Require(label.GetLineCount() == label.GetVisibleLineCount()
                && GetViewport().GetVisibleRect().Encloses(label.GetGlobalRect())
                && GiftPanel.GetNode<ScrollContainer>("%GiftScroll").GetGlobalRect().Encloses(label.GetGlobalRect()),
                "The rendered gift explanation is clipped.");
            _giftCaptures.Add(new { Sequence = _sequence, Control = name, label.Text,
                Lines = label.GetLineCount(), VisibleLines = label.GetVisibleLineCount(), Bounds = Bounds(label.GetGlobalRect()),
                Image = $"{_sequence:D4}-gift-{name}.png" });
        }
        await Capture(5);
    }

    private object? GiftEvidence()
    {
        if (_game.CurrentSelection is not { } observed) return null;
        var view = observed.ControlledView is { } controlled ? controlled.MaterialProposals : observed.HomeView.MaterialProposals;
        var draft = _game.GetNodeOrNull<FarmInventoryScreen>("FarmInventoryScreen")?.GetNodeOrNull<FarmGiftPanel>("%Gifts")?.Selection;
        return new
        {
            Actor = observed.Selection.ActorId.ToString(), observed.Selection.Generation,
            InventoryDisclosed = observed.ControlledView is null || observed.ControlledView.Inventory is not null,
            PackOpen = _game.IsInventoryOpen, PanelVisible = GiftPanel.IsVisibleInTree(), WheelOpen = _game.IsWheelOpen,
            PanelActor = GiftPanel.CurrentPresentation?.ActorId.ToString(),
            PanelGeneration = GiftPanel.CurrentPresentation?.Selection?.Generation,
            PanelLifetime = GiftPack.PresentationLifetime, OfferSelectionVersion = GiftPack.GiftOfferSelectionVersion,
            HistoryVisible = GiftPanel.GetNode<Button>("%GiftHistory").IsVisibleInTree(),
            Lots = view?.Preparation?.Lots.Select(lot => new { Kind = lot.Kind.ToString(), Lot = lot.LotId.ToString(),
                lot.Quantity, lot.Revision, Definition = lot.Definition.DefinitionId.ToString() }).ToArray() ?? [],
            Offers = view?.Offers.Select(offer => new { Proposal = offer.ProposalId.ToString(), Status = offer.Status.ToString(),
                Sender = offer.Terms.SenderActorId.ToString(), Recipient = offer.Terms.RecipientActorId.ToString(),
                Lot = offer.Terms.LotId.ToString(), offer.Terms.Quantity, Kind = offer.Terms.LotKind.ToString(),
                GrantHash = offer.GrantHash.ToString(), offer.SenderPresenceRevision, offer.ViewerIsSender,
                offer.ClosedAtSequence, Terms = GiftTerms(offer.Terms) }).ToArray() ?? [],
            Draft = draft is null ? null : new { Lot = draft.LotId?.ToString(), Kind = draft.LotKind?.ToString(),
                Recipient = draft.RecipientActorId?.ToString(), draft.Quantity, SelectedOffer = draft.SelectedOffer?.ProposalId.ToString(),
                PreparedTerms = draft.PreparedGift is { } prepared ? GiftTerms(prepared.Terms) : null,
                Expiry = draft.PreparedGift?.ExpiresAtAdvancingStep },
            Reviewed = GiftOfferEvidence(_reviewedGift),
            CurrentWheel = _game.IsWheelOpen ? GiftOfferEvidence(GiftWheel.Model?.Selected?.Offer) : null,
            Actions = _game.CurrentOffers.Where(pair => MaterialProposalActionOffers.IsGiftAction(pair.Key))
                .Select(pair => GiftOfferEvidence(pair.Value)).ToArray()
        };
    }

    private static object? GiftOfferEvidence(FarmActionOffer? offer) => offer is null ? null : new
    {
        Action = offer.Action.ToString(), offer.CanExecute, offer.BlockingReason, offer.DetailText, offer.CostText,
        Actor = offer.Scope.ActorId.ToString(), Principal = offer.Scope.PrincipalId.ToString(), Session = offer.Scope.SessionId.ToString(),
        Generation = offer.Scope.Selection?.Generation, Witness = WitnessBytes(offer.Scope.Selection?.ControlWitness),
        offer.Scope.ExpectedActorRevision, Intent = offer.Intent?.GetType().Name,
        Proposal = offer.Intent switch { AcceptMaterialProposalIntent value => value.ProposalId.ToString(),
            DeclineMaterialProposalIntent value => value.ProposalId.ToString(), WithdrawMaterialProposalIntent value => value.ProposalId.ToString(), _ => null },
        Terms = offer.Intent switch { ProposeMaterialIntent value => GiftTerms(value.Terms), AcceptMaterialProposalIntent value => GiftTerms(value.Terms), _ => null }
    };

    private static object GiftTerms(ConsentedMaterialTerms terms) => new
    {
        World = terms.WorldId.ToString(), Branch = terms.BranchId.ToString(), Sender = terms.SenderActorId.ToString(),
        Recipient = terms.RecipientActorId.ToString(), Kind = terms.LotKind.ToString(), Lot = terms.LotId.ToString(),
        Definition = terms.Definition.DefinitionId.ToString(), Version = terms.Definition.Version.Value,
        Source = terms.SourceCustodianId.ToString(), Destination = terms.DestinationCustodianId.ToString(),
        terms.Quantity, terms.ExpectedLotRevision
    };
}
