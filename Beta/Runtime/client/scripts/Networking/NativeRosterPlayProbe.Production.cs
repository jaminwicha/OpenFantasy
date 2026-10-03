using System.Diagnostics;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client.Networking;

/// <summary>Reviews actual field offers through native input and confirms the same captured request once.</summary>
internal sealed partial class NativeRosterPlayProbe
{
    private FarmActionOffer? _reviewedProduction;
    private readonly List<object> _productionCaptures = [];
    private FarmQuickWheel ProductionWheel => _game.GetNode<FarmQuickWheel>("FarmQuickWheel");
    private LocalControlledProductionView? ProductionView => Selected.ControlledView?.Production;
    private Task ProductionUntil(Func<bool> predicate, string failure) => _driver.Until(predicate, failure, 5);

    private async Task SelectProductionTarget(EntityId target)
    {
        RequireProductionField();
        var selection = Selected.Selection;
        var elapsed = Stopwatch.StartNew();
        _driver.Tap(Key.R);
        await _driver.Frame();
        for (int count = 0; _game.CurrentTarget?.Id != target; count++)
        {
            MarketProbeDriver.Require(count < 24 && elapsed.Elapsed.TotalSeconds < 5,
                "Ordinary target navigation did not reach the exact production target within five seconds.");
            _driver.Tap(Key.Q);
            await _driver.Frame();
        }
        MarketProbeDriver.Require(elapsed.Elapsed.TotalSeconds < 5 && Selected.Selection == selection
            && _game.PresentedSelection?.Selection == selection,
            "The selected production target arrived late or under another selection.");
    }

    private void RequireProductionField()
    {
        MarketProbeDriver.Require(ProductionView is not null && !_game.IsInventoryOpen && !ControlPanel.IsOpen
            && !_game.IsWheelOpen && !_game.HasPendingAction,
            "Production input requires the permitted selected field with panels and previous choices closed.");
    }

    private async Task ReviewProduction(string name, EntityId target)
    {
        RequireProductionField();
        MarketProbeDriver.Require(Enum.TryParse(name, out FarmSemanticAction action) && IsProductionAction(action),
            "Unknown explicit production action.");
        var selection = Selected.Selection;
        int before = _submissions.Count;
        await ProductionUntil(() => _game.CurrentOffers.GetValueOrDefault(action) is { CanExecute: true } offer
            && ProductionSubject(offer.Intent) == target && MatchesProductionOffer(offer),
            "The exact executable selected production offer did not reach the presented frame.");
        _driver.Tap(Key.V);
        await ProductionUntil(() => _game.IsWheelOpen, "The actual field wheel did not open.");
        MarketProbeDriver.Require(ProductionWheel.Model!.Selected is null, "Opening production review selected an action.");
        int sector = Enumerable.Range(0, 4).Single(index => ProductionWheel.Model.Slots[index]?.Action == action);
        _driver.Joy(new[] { JoyButton.DpadUp, JoyButton.DpadRight, JoyButton.DpadDown, JoyButton.DpadLeft }[sector]);
        await ProductionUntil(() => ProductionWheel.Model?.Selected?.Action == action,
            "Controller input did not select the explicit production action.");
        _reviewedProduction = ProductionWheel.Model!.Selected!.Offer;
        MarketProbeDriver.Require(_reviewedProduction.CanExecute && MatchesProductionOffer(_reviewedProduction)
            && ProductionSubject(_reviewedProduction.Intent) == target && Selected.Selection == selection
            && _submissions.Count == before && !_game.HasPendingAction,
            "Review changed the production target or scope, or submitted work before confirmation.");
        await ObserveProductionPresentation("review", saveImage: false);
    }

    private async Task ConfirmProduction(string expected)
    {
        FarmActionOffer reviewed = _reviewedProduction
            ?? throw new InvalidOperationException("No actual production wheel choice was reviewed.");
        int before = _submissions.Count;
        if (expected == "blocked")
        {
            MarketProbeDriver.Require(!_game.IsWheelOpen || _game.IsLocallyPaused
                || ProductionWheel.Model?.Selected?.Offer.CanExecute != true,
                "The expected blocked production confirmation is still executable.");
            _driver.Joy(JoyButton.A);
            await _driver.Frame();
            await _driver.Frame();
            MarketProbeDriver.Require(_submissions.Count == before && !_game.HasPendingAction,
                "Blocked production confirmation submitted work.");
            return;
        }
        WorldActionRejection rejection = WorldActionRejection.None;
        MarketProbeDriver.Require(expected == "Applied" || expected == "Unavailable"
            && Enum.TryParse(expected, out rejection), "Unknown expected production receipt.");
        MarketProbeDriver.Require(!_game.IsLocallyPaused && _game.IsWheelOpen
            && ProductionWheel.Model?.Selected?.Offer is { CanExecute: true } current
            && current.Scope == reviewed.Scope && current.Action == reviewed.Action
            && SameProductionIntent(current.Intent, reviewed.Intent)
            && reviewed.Scope.Selection == Selected.Selection
            && _game.PresentedSelection?.Selection == Selected.Selection,
            "The original reviewed production capture is stale or unavailable; it will not be refreshed or retried.");
        _driver.Joy(JoyButton.A);
        await ProductionUntil(() => _submissions.Count == before + 1 && _submissions[^1].Receipt.IsCompleted
            && !_game.HasPendingAction && !_game.IsWheelOpen
            && (rejection != WorldActionRejection.None
                || ProductionOutcomeObserved(_game.CurrentSelection, reviewed)
                    && ProductionOutcomeObserved(_game.PresentedSelection, reviewed)),
            "One actual production confirmation did not complete its original receipt and observed outcome.");
        FarmSubmission submission = _submissions[^1];
        WorldActionCommand command = WorldActionCodec.Decode(submission.CanonicalCommandBytes());
        MarketProbeDriver.Require(!submission.IsRetry && submission.Receipt.IsCompletedSuccessfully
            && submission.Receipt.Result.Rejection == rejection
            && (rejection != WorldActionRejection.None || submission.Receipt.Result.Status == CommandReceiptStatus.Applied)
            && SameProductionIntent(command.Intent, reviewed.Intent)
            && command.ActorId == reviewed.Scope.ActorId && command.PrincipalId == reviewed.Scope.PrincipalId
            && command.OriginSessionId == reviewed.Scope.SessionId && command.WorldId == reviewed.Scope.WorldId
            && command.BranchId == reviewed.Scope.BranchId && command.ExpectedActorRevision == reviewed.Scope.ExpectedActorRevision
            && command.ControlWitness == reviewed.Scope.Selection!.ControlWitness,
            "Production receipt, captured inputs or truthful operator differ; nothing was retried.");
    }

    // A receipt can precede the next observation frame. A new review must not capture that old
    // frame merely because its selection token still matches. These are visible effect revisions,
    // not a claim that the observation stream revision equals the authority's commit sequence.
    private static bool ProductionOutcomeObserved(LocalSelectedPlayerObservation? observation, FarmActionOffer reviewed)
    {
        if (observation is null || observation.Selection != reviewed.Scope.Selection
            || observation.HomeView.WorldId != reviewed.Scope.WorldId || observation.HomeView.BranchId != reviewed.Scope.BranchId
            || observation.ControlledView?.Production is not { } production) return false;
        return reviewed.Intent switch
        {
            GatherIntent gather => production.GatheringNodes.Any(node => node.NodeId == gather.NodeId
                && node.Revision > gather.ExpectedNodeRevision),
            ExcavateIntent mine => production.Worksite?.ExcavationTargets.Any(site => site.SiteId == mine.SiteId
                && site.SiteRevision > mine.ExpectedSiteRevision) == true,
            CraftIntent craft => production.Crafting?.Revision > craft.Request.ExpectedProductionRevision,
            _ => false
        };
    }

    private async Task CancelProduction()
    {
        MarketProbeDriver.Require(_game.IsWheelOpen, "Production cancellation requires an actual open wheel.");
        int before = _submissions.Count;
        _driver.Joy(JoyButton.B);
        await ProductionUntil(() => !_game.IsWheelOpen, "Actual cancellation did not close the production wheel.");
        MarketProbeDriver.Require(_submissions.Count == before && !_game.HasPendingAction,
            "Cancelling production review submitted an action.");
    }

    private bool MatchesProductionOffer(FarmActionOffer offer)
    {
        var selected = Selected;
        var production = selected.ControlledView?.Production;
        if (production is null || offer.Scope.Selection != selected.Selection
            || _game.PresentedSelection?.Selection != selected.Selection
            || offer.Scope.ActorId != selected.Selection.ActorId || offer.Scope.WorldId != selected.HomeView.WorldId
            || offer.Scope.BranchId != selected.HomeView.BranchId || offer.Scope.ExpectedActorRevision
                != SelectedActors.Single(actor => actor.ActorId == selected.Selection.ActorId).PresenceRevision) return false;
        return offer.Intent switch
        {
            GatherIntent gather => production.GatheringNodes.Any(node => node.NodeId == gather.NodeId
                && node.Revision == gather.ExpectedNodeRevision && node.RemainingSwings > 0)
                && production.GatheringTools.Any(tool => tool.LotId == gather.ToolLotId
                    && tool.Revision == gather.ExpectedToolLotRevision),
            ExcavateIntent mine => production.Worksite is { } work
                && work.ExcavationTargets.Any(site => site.SiteId == mine.SiteId && site.SiteRevision == mine.ExpectedSiteRevision
                    && site.TerrainRevision == mine.ExpectedTerrainRevision && site.DestinationRevision == mine.ExpectedDestinationRevision)
                && work.Tools.Any(tool => tool.LotId == mine.ToolLotId && tool.Revision == mine.ExpectedToolLotRevision),
            CraftIntent craft => production.CraftPreparation is { } preparation
                && preparation.ProductionRevision == craft.Request.ExpectedProductionRevision
                && preparation.StorageRevision == craft.Request.ExpectedStorageRevision
                && preparation.CustodianId == craft.Request.CustodianId
                && preparation.Recipes.Any(recipe => recipe.RecipeId == craft.Request.RecipeId
                    && recipe.Version == craft.Request.RecipeVersion)
                && craft.Request.Ingredients.All(input => preparation.Ingredients.Any(lot => lot.LotId == input.LotId
                    && lot.Revision == input.ExpectedRevision && lot.Provenance == input.Kind && lot.Quantity >= input.Quantity)),
            _ => false
        };
    }

    private static bool IsProductionAction(FarmSemanticAction action) =>
        action is FarmSemanticAction.Gather or FarmSemanticAction.Craft or FarmSemanticAction.Excavate;

    private static EntityId? ProductionSubject(WorldActionIntent? intent) => intent switch
    {
        GatherIntent gather => gather.NodeId, CraftIntent craft => craft.Request.RecipeId,
        ExcavateIntent mine => mine.SiteId, _ => null
    };

    private static bool SameProductionIntent(WorldActionIntent? left, WorldActionIntent? right) => (left, right) switch
    {
        (CraftIntent a, CraftIntent b) => a.Request.RecipeId == b.Request.RecipeId
            && a.Request.RecipeVersion == b.Request.RecipeVersion
            && a.Request.ExpectedProductionRevision == b.Request.ExpectedProductionRevision
            && a.Request.CustodianId == b.Request.CustodianId && a.Request.ExpectedStorageRevision == b.Request.ExpectedStorageRevision
            && a.Request.Ingredients.SequenceEqual(b.Request.Ingredients),
        (GatherIntent a, GatherIntent b) => a == b,
        (ExcavateIntent a, ExcavateIntent b) => a == b,
        _ => false
    };

    private async Task ObserveProductionPresentation(string label, bool saveImage)
    {
        MarketProbeDriver.Require(label.Length is > 0 and <= 64 && _productionCaptures.Count < 128
            && _game.IsWheelOpen && ProductionWheel.Model?.Selected is not null,
            "Production capture requires one explicitly reviewed wheel choice and a bounded label.");
        bool rendered = DisplayServer.GetName() != "headless";
        if (saveImage) await Capture(5, "-production");
        else if (rendered)
        {
            bool drawn = false;
            void OnDraw() => drawn = true;
            RenderingServer.FramePostDraw += OnDraw;
            try { await ProductionUntil(() => drawn, "Production review did not reach a rendered frame."); }
            finally { RenderingServer.FramePostDraw -= OnDraw; }
        }
        var labels = new[] { "ChoiceName", "ChoiceDetails", "WheelTime" }
            .Select(name => ProductionWheel.GetNode<Label>("%" + name)).ToArray();
        foreach (Label text in labels)
            MarketProbeDriver.Require(text.IsVisibleInTree() && text.GetLineCount() == text.GetVisibleLineCount()
                && GetViewport().GetVisibleRect().Encloses(text.GetGlobalRect()),
                "The production cost or progress explanation is clipped.");
        Label details = labels[1];
        FarmActionOffer offer = ProductionWheel.Model!.Selected!.Offer;
        MarketProbeDriver.Require(_reviewedProduction is { } reviewed && offer.Scope == reviewed.Scope
            && SameProductionIntent(offer.Intent, reviewed.Intent) && offer.Scope.Selection == Selected.Selection
            && _game.PresentedSelection?.Selection == Selected.Selection,
            "The original production capture changed before its rendered inspection.");
        MarketProbeDriver.Require(details.Text.Contains(offer.CostText, StringComparison.Ordinal)
            && details.Text.Contains(offer.TargetName, StringComparison.Ordinal),
            "The actual production explanation omits its selected target or cost.");
        Rect2? actor = _game.Canvas.ActorSpriteViewportBounds(Selected.Selection.ActorId);
        MarketProbeDriver.Require(actor is { } actorBounds && Hud.ShowsFieldSprite(actorBounds),
            "The operated actor's actual sprite does not clear the HUD.");
        Rect2? cue = _game.Canvas.TargetCueViewportBounds;
        Rect2? resource = offer.Intent is GatherIntent gather
            ? _game.Canvas.GatheringSpriteViewportBounds(gather.NodeId) : null;
        if (offer.Intent is GatherIntent or ExcavateIntent)
            MarketProbeDriver.Require(cue is { } cueBounds && Hud.ShowsFieldSprite(cueBounds),
                "The actual production target cue does not clear the HUD.");
        if (offer.Intent is GatherIntent)
            MarketProbeDriver.Require(resource is { } resourceBounds && Hud.ShowsFieldSprite(resourceBounds),
                "The actual gathering sprite does not clear the HUD.");
        Button confirm = ProductionWheel.GetNode<Button>("%ConfirmChoice");
        MarketProbeDriver.Require(confirm.IsVisibleInTree() && GetViewport().GetVisibleRect().Encloses(confirm.GetGlobalRect()),
            "The actual production confirmation control is clipped.");
        _productionCaptures.Add(new { Sequence = _sequence, Label = label, Rendered = rendered,
            Image = saveImage ? $"{_sequence:D4}-production.png" : null, Selection = SelectionEvidence(),
            ActorBounds = Bounds(actor!.Value), ActorClearsHud = Hud.ShowsFieldSprite(actor.Value),
            Target = _game.CurrentTarget?.Id.ToString(),
            TargetCueBounds = cue is { } targetCue ? Bounds(targetCue) : null,
            GatheringSpriteBounds = resource is { } resourceSprite ? Bounds(resourceSprite) : null,
            Labels = labels.Select(text => new { Name = text.Name.ToString(), text.Text, Bounds = Bounds(text.GetGlobalRect()),
                Lines = text.GetLineCount(), VisibleLines = text.GetVisibleLineCount() }).ToArray(),
            ConfirmBounds = Bounds(confirm.GetGlobalRect()),
            Offer = ProductionOfferEvidence(offer) });
    }

    private object? ProductionEvidence()
    {
        if (_game.CurrentSelection is not { } observed) return null;
        var production = observed.ControlledView?.Production;
        return new
        {
            Actor = observed.Selection.ActorId.ToString(), observed.Selection.Generation,
            Disclosed = production is not null, InventoryDisclosed = observed.ControlledView?.Inventory is not null,
            Effort = production?.GatherEffort, CraftingRevision = production?.Crafting?.Revision,
            Nodes = production?.GatheringNodes.Select(node => new { Id = node.NodeId.ToString(), node.DisplayName,
                node.Feet, node.Revision, node.RemainingSwings, Kind = node.Kind.ToString(), Visibility = node.Visibility.ToString() }).ToArray() ?? [],
            Tools = production?.GatheringTools.Select(tool => new { Lot = tool.LotId.ToString(), tool.Revision,
                Kind = tool.Kind.ToString(), tool.Power }).ToArray() ?? [],
            Recipes = production?.Crafting?.Recipes.Select(recipe => new { Id = recipe.RecipeId.ToString(), recipe.Key,
                Version = recipe.Version.ToString(), Terms = recipe.Terms.Select(term => new { Definition = term.DefinitionId.ToString(),
                    term.Quantity, Role = term.Role.ToString() }).ToArray() }).ToArray() ?? [],
            Ingredients = production?.CraftPreparation?.Ingredients.Select(lot => new { Lot = lot.LotId.ToString(),
                Definition = lot.DefinitionId.ToString(), lot.Revision, lot.Quantity, Kind = lot.Provenance.ToString() }).ToArray() ?? [],
            Sites = production?.Worksite?.ExcavationTargets.Select(site => new { Id = site.SiteId.ToString(), site.Cell, site.Stance,
                site.SiteRevision, site.TerrainRevision, site.DestinationRevision, site.CompletedWork, site.RequiredWork }).ToArray() ?? [],
            Reviewed = ProductionOfferEvidence(_reviewedProduction),
            CurrentWheel = _game.IsWheelOpen ? ProductionOfferEvidence(ProductionWheel.Model?.Selected?.Offer) : null,
            Actions = _game.CurrentOffers.Where(pair => IsProductionAction(pair.Key))
                .Select(pair => ProductionOfferEvidence(pair.Value)).ToArray()
        };
    }

    private static object? ProductionOfferEvidence(FarmActionOffer? offer) => offer is null ? null : new
    {
        Action = offer.Action.ToString(), offer.Label, offer.TargetName, Target = ProductionSubject(offer.Intent)?.ToString(),
        offer.CanExecute, offer.CostText, offer.DetailText, offer.BlockingReason,
        Actor = offer.Scope.ActorId.ToString(), Principal = offer.Scope.PrincipalId.ToString(), Session = offer.Scope.SessionId.ToString(),
        World = offer.Scope.WorldId.ToString(), Branch = offer.Scope.BranchId.ToString(), offer.Scope.ExpectedActorRevision,
        Port = offer.Scope.Selection?.PortId.ToString(), Generation = offer.Scope.Selection?.Generation,
        Witness = WitnessBytes(offer.Scope.Selection?.ControlWitness), Intent = ProductionIntentEvidence(offer.Intent)
    };

    private static object? ProductionIntentEvidence(WorldActionIntent? intent) => intent switch
    {
        GatherIntent gather => new { Node = gather.NodeId.ToString(), gather.ExpectedNodeRevision,
            Tool = gather.ToolLotId.ToString(), gather.ExpectedToolLotRevision },
        ExcavateIntent mine => new { Site = mine.SiteId.ToString(), mine.ExpectedSiteRevision, mine.ExpectedTerrainRevision,
            Tool = mine.ToolLotId.ToString(), mine.ExpectedToolLotRevision, mine.ExpectedDestinationRevision },
        CraftIntent craft => new { Recipe = craft.Request.RecipeId.ToString(), Version = craft.Request.RecipeVersion.ToString(),
            craft.Request.ExpectedProductionRevision, Custodian = craft.Request.CustodianId.ToString(),
            craft.Request.ExpectedStorageRevision, Ingredients = craft.Request.Ingredients.Select(input => new
            { Kind = input.Kind.ToString(), Lot = input.LotId.ToString(), input.ExpectedRevision, input.Quantity }).ToArray() },
        _ => null
    };
}
