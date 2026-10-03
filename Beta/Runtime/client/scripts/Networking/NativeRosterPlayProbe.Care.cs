using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client.Networking;

/// <summary>Exact care-library choices made through real widgets, with independent preparation and confirmation.</summary>
internal sealed partial class NativeRosterPlayProbe
{
    private FarmActionOffer? _preparedCare;
    private readonly List<object> _careLibraryCaptures = [];
    private FarmCarePanel CarePanel => _game.GetNode<FarmCarePanel>("FarmCarePanel");
    private FarmQuickWheel CareWheel => _game.GetNode<FarmQuickWheel>("FarmQuickWheel");
    private ulong _careOperationStarted;
    private double CareSecondsRemaining => 5 - (Time.GetTicksMsec() - _careOperationStarted) / 1000.0;
    private Task CareUntil(Func<bool> predicate, string failure) => _driver.Until(predicate, failure, CareSecondsRemaining);
    private async Task CareOperation(Func<Task> action)
    {
        _careOperationStarted = Time.GetTicksMsec();
        await action();
        MarketProbeDriver.Require(CareSecondsRemaining > 0, "The Care operation exceeded its unchanged five-second limit.");
    }

    private async Task OpenCareLibrary()
    {
        await CloseSelectionPanel();
        MarketProbeDriver.Require(!_game.IsInventoryOpen && !_game.IsRecipesOpen && !_game.IsMarketOpen && !_game.HasPendingAction,
            "Close other inspections before opening Care.");
        var selection = Selected.Selection;
        int before = _submissions.Count;
        if (!_game.IsCareOpen)
        {
            MarketProbeDriver.Require(!_game.IsWheelOpen, "Close the prior wheel before opening Care.");
            _driver.Tap(Key.V);
            await CareUntil(() => _game.IsWheelOpen, "The actual wheel did not open for Care.");
            for (int pages = 0; CareWheel.Model?.PageName != "Care"; pages++)
            {
                MarketProbeDriver.Require(pages < 5, "The wheel has no Care page for this character.");
                _driver.Tap(Key.Q);
                await _driver.Frame();
            }
            _driver.Joy(JoyButton.DpadDown);
            await CareUntil(() => CareWheel.Model?.Selected?.Action == FarmSemanticAction.OpenCare,
                "Native directional input did not select the Care library.");
            _driver.Joy(JoyButton.A);
        }
        await CareUntil(() => _game.IsCareOpen && _game.SelectedCareLibrary is not null
            && _game.PresentedSelection?.Selection == selection, "The shared Care library did not present the selected character.");
        MarketProbeDriver.Require(Selected.Selection == selection && _submissions.Count == before && !_game.HasPendingAction,
            "Opening Care changed scope or committed work.");
    }

    private async Task ChooseCareLibrary(string kind, EntityId id)
    {
        MarketProbeDriver.Require(kind is "UseRemedy" or "WithdrawPermission", "Choose a specific remedy or assistance permission.");
        await OpenCareLibrary();
        var selection = Selected.Selection;
        int before = _submissions.Count;
        Button row = Descendants(CarePanel).OfType<Button>().Single(button => button.HasMeta("care_kind") && button.HasMeta("care_id")
            && button.GetMeta("care_kind").AsString() == kind && button.GetMeta("care_id").AsString() == id.ToString());
        await _driver.Click(row);
        await CareUntil(() => _game.SelectedCareLibrary.SelectedId is { } chosen && chosen.Kind.ToString() == kind && chosen.Id == id,
            "The actual Care row did not retain its exact kind and identity.");
        MarketProbeDriver.Require(Selected.Selection == selection && _submissions.Count == before && !_game.HasPendingAction
            && _game.SelectedCareLibrary.PreparedOffer is null, "Care browsing committed work or kept an unrelated preparation.");
    }

    private async Task PrepareCareLibrary()
    {
        MarketProbeDriver.Require(_game.IsCareOpen, "Open Care before preparing an action.");
        var selection = Selected.Selection;
        int before = _submissions.Count;
        await CareUntil(() => _game.SelectedCareLibrary.Selected?.Offer.CanExecute == true,
            "The exact selected care action is not available.");
        await _driver.Click(CarePanel.GetNode<Button>("%PrepareCare"));
        await CareUntil(() => _game.SelectedCareLibrary.PreparedOffer is not null,
            "The actual Prepare button captured no care action.");
        _preparedCare = _game.SelectedCareLibrary.PreparedOffer!;
        bool drawn = DisplayServer.GetName() == "headless";
        void OnDraw() => drawn = true;
        RenderingServer.FramePostDraw += OnDraw;
        try { await CareUntil(() => drawn, "The prepared care review was not drawn before its held read interval."); }
        finally { RenderingServer.FramePostDraw -= OnDraw; }
        await _driver.Delay(0.3);
        MarketProbeDriver.Require(Selected.Selection == selection && _preparedCare.Scope.Selection == selection
            && _game.SelectedCareLibrary.PreparedOffer is { CanExecute: true } current && current.Scope == _preparedCare.Scope
            && current.Intent == _preparedCare.Intent && !CarePanel.GetNode<Button>("%ConfirmCare").Disabled
            && _submissions.Count == before && !_game.HasPendingAction,
            "Reading care terms changed the exact dose, permission, scope or submitted before confirmation.");
    }

    private async Task ConfirmCareLibrary(string expected)
    {
        var prepared = _preparedCare ?? throw new InvalidOperationException("No native care action was prepared.");
        int before = _submissions.Count;
        var confirm = CarePanel.GetNode<Button>("%ConfirmCare");
        if (expected == "blocked")
        {
            MarketProbeDriver.Require(!_game.IsCareOpen || _game.SelectedCareLibrary.PreparedOffer is null || confirm.Disabled,
                "The expected stale care capture is still executable.");
            if (_game.IsCareOpen && confirm.IsVisibleInTree()) await _driver.Click(confirm, requireEnabled: false);
            else _driver.Tap(Key.Enter);
            await _driver.Frame();
            await _driver.Frame();
            MarketProbeDriver.Require(_submissions.Count == before && !_game.HasPendingAction,
                "An invalidated care capture submitted work.");
            return;
        }
        MarketProbeDriver.Require(expected == "Applied" && _game.IsCareOpen && !_game.IsLocallyPaused
            && _game.SelectedCareLibrary.PreparedOffer is { CanExecute: true } current && current.Scope == prepared.Scope
            && current.Intent == prepared.Intent && Selected.Selection == prepared.Scope.Selection
            && _game.PresentedSelection?.Selection == Selected.Selection,
            "The original care review changed; it will not be replaced or retried.");
        await _driver.Click(confirm);
        await CareUntil(() => _submissions.Count == before + 1 && _submissions[^1].Receipt.IsCompleted && !_game.HasPendingAction
            && CareOutcomeObserved(_game.CurrentSelection, prepared) && CareOutcomeObserved(_game.PresentedSelection, prepared),
            "The single care confirmation did not return its receipt and both observed outcomes.");
        var submission = _submissions[^1];
        var command = WorldActionCodec.Decode(submission.CanonicalCommandBytes());
        var original = new WorldActionCommand(command.CommandId, prepared.Scope.PrincipalId, prepared.Scope.SessionId,
            prepared.Scope.ActorId, prepared.Scope.WorldId, prepared.Scope.BranchId, prepared.Scope.ExpectedActorRevision,
            prepared.Intent!, prepared.Scope.Selection?.ControlWitness);
        MarketProbeDriver.Require(!submission.IsRetry && submission.Receipt.IsCompletedSuccessfully
            && submission.Receipt.Result.Status == CommandReceiptStatus.Applied && submission.Receipt.Result.Rejection == WorldActionRejection.None
            && WorldActionCodec.Encode(original).AsSpan().SequenceEqual(WorldActionCodec.Encode(command)),
            "Care confirmation changed its exact captured request, truthful operator or receipt.");
    }

    private static bool CareOutcomeObserved(LocalSelectedPlayerObservation? observed, FarmActionOffer prepared)
    {
        if (observed is null || observed.Selection != prepared.Scope.Selection) return false;
        if (prepared.Intent is UseRestorativeItemIntent use)
        {
            var offers = observed.ControlledView is { } controlled ? controlled.Recovery?.SelfUseOffers : observed.HomeView.Restorative?.SelfUseOffers;
            return offers?.Any(offer => offer.DefinitionId == use.UseDefinitionId
                && (offer.Intent is null || offer.Intent.ExpectedPatientProgressionRevision > use.ExpectedPatientProgressionRevision)) == true;
        }
        if (prepared.Intent is RevokeCarePermissionIntent revoke)
        {
            var offers = observed.ControlledView is { } controlled ? controlled.CareDisposition?.PermissionOffers : observed.HomeView.Restorative?.PermissionOffers;
            // The delegated facet only discloses active permissions; removal is its lawful observed result.
            return observed.ControlledView is not null
                ? offers is not null && offers.All(offer => offer.PermissionId != revoke.PermissionId)
                : offers?.Any(offer => offer.PermissionId == revoke.PermissionId && !offer.IsActive) == true;
        }
        return false;
    }

    private async Task CloseCareLibrary()
    {
        int before = _submissions.Count;
        if (_game.IsCareOpen) await _driver.Click(CarePanel.GetNode<Button>("%CloseCare"));
        await CareUntil(() => !_game.IsCareOpen, "The actual Close Care button did not close its panel.");
        MarketProbeDriver.Require(_submissions.Count == before && !_game.HasPendingAction,
            "Closing Care committed an action.");
    }

    private async Task CaptureCareLibrary(string label)
    {
        MarketProbeDriver.Require(_game.IsCareOpen && label.Length is > 0 and <= 64 && _careLibraryCaptures.Count < 64,
            "Care capture requires an open library and bounded label.");
        await Capture(CareSecondsRemaining, "-care-library");
        Control root = CarePanel.GetNode<Control>("%CareRoot");
        MarketProbeDriver.Require(GetViewport().GetVisibleRect().Encloses(root.GetGlobalRect()), "The Care panel extends outside the viewport.");
        var labels = Descendants(CarePanel).OfType<Label>().Where(value => value.IsVisibleInTree()).ToArray();
        var buttons = Descendants(CarePanel).OfType<Button>().Where(value => value.IsVisibleInTree()).ToArray();
        foreach (Control control in labels.Cast<Control>().Concat(buttons))
        {
            MarketProbeDriver.Require(GetViewport().GetVisibleRect().Encloses(control.GetGlobalRect()), "A Care control is clipped.");
            RequireRecipeScrollVisible(control);
        }
        foreach (var text in labels)
            MarketProbeDriver.Require(text.GetLineCount() == text.GetVisibleLineCount(), "A Care explanation is clipped.");
        _careLibraryCaptures.Add(new { Sequence = _sequence, Label = label, Image = $"{_sequence:D4}-care-library.png",
            Bounds = Bounds(root.GetGlobalRect()), Care = CareLibraryEvidence(),
            Text = labels.Select(value => new { Name = value.Name.ToString(), value.Text, Bounds = Bounds(value.GetGlobalRect()),
                Lines = value.GetLineCount(), VisibleLines = value.GetVisibleLineCount() }).ToArray(),
            Controls = buttons.Select(value => new { Name = value.Name.ToString(), value.Text, value.Disabled,
                Bounds = Bounds(value.GetGlobalRect()) }).ToArray() });
    }

    private object? CareLibraryEvidence()
    {
        if (_game.CurrentSelection is not { } observed) return null;
        var library = _game.SelectedCareLibrary;
        var uses = observed.ControlledView is { } controlled ? controlled.Recovery?.SelfUseOffers : observed.HomeView.Restorative?.SelfUseOffers;
        var permissions = observed.ControlledView is { } selected ? selected.CareDisposition?.PermissionOffers : observed.HomeView.Restorative?.PermissionOffers;
        return new
        {
            Actor = observed.Selection.ActorId.ToString(), observed.Selection.Generation, Open = _game.IsCareOpen,
            RecoveryDisclosed = observed.ControlledView is null || observed.ControlledView.Recovery is not null,
            DispositionDisclosed = observed.ControlledView is null || observed.ControlledView.CareDisposition is not null,
            InventoryDisclosed = observed.ControlledView is null || observed.ControlledView.Inventory is not null,
            CombatDisclosed = observed.ControlledView is null || observed.ControlledView.Combat is not null,
            Hp = observed.ControlledView is { } scoped ? scoped.Recovery?.Health?.Hp : observed.HomeView.Progression?.Hp,
            Entries = library.Entries.Select(entry => new { Kind = entry.Key.Kind.ToString(), Id = entry.Key.Id.ToString(), entry.Label,
                Offer = CareOfferEvidence(entry.Offer) }).ToArray(),
            Selected = library.SelectedId is { } choice ? new { Kind = choice.Kind.ToString(), Id = choice.Id.ToString() } : null,
            Prepared = CareOfferEvidence(library.PreparedOffer), Captured = CareOfferEvidence(_preparedCare),
            Uses = uses?.Select(use => new { Id = use.DefinitionId.ToString(), use.ItemName, use.HealHitPoints,
                use.ExpectedHealingHp, use.CanUse, Reason = use.Reason.ToString(), Ingredient = use.Intent?.Ingredient }).ToArray() ?? [],
            Permissions = permissions?.Select(permission => new { Id = permission.PermissionId.ToString(),
                Helper = permission.HelperActorId.ToString(), permission.HelperName, permission.IsActive, permission.CanRevoke }).ToArray() ?? []
        };
    }

    private static object? CareOfferEvidence(FarmActionOffer? offer) => offer is null ? null : new
    {
        Action = offer.Action.ToString(), offer.CanExecute, offer.BlockingReason, offer.CostText, offer.DetailText,
        Actor = offer.Scope.ActorId.ToString(), offer.Scope.ExpectedActorRevision, Generation = offer.Scope.Selection?.Generation,
        Witness = WitnessBytes(offer.Scope.Selection?.ControlWitness),
        Use = offer.Intent as UseRestorativeItemIntent, Withdrawal = offer.Intent as RevokeCarePermissionIntent
    };
}
