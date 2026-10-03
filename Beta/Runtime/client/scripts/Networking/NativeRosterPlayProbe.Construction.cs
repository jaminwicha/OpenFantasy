using System.Diagnostics;
using System.Globalization;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client.Networking;

/// <summary>Separate native reviews and confirmations; a bounded batch is test input, not an automatic work feature.</summary>
internal sealed partial class NativeRosterPlayProbe
{
    private FarmActionOffer? _reviewedConstruction;
    private readonly List<object> _constructionCaptures = [];
    private static IReadOnlyList<LocalConstructionView> ConstructionProjects(LocalSelectedPlayerObservation observation) =>
        observation.ControlledView is { } controlled
            ? controlled.Production is { } production ? production.Construction : Array.Empty<LocalConstructionView>()
            : observation.HomeView.Construction;
    private static IReadOnlyList<LocalPublicStructureView> ConstructionStructures(LocalSelectedPlayerObservation observation) =>
        (observation.ControlledView is { } controlled ? controlled.Structures : observation.HomeView.Structures) is { } structures
            ? structures : Array.Empty<LocalPublicStructureView>();

    private async Task ConstructionTarget(EntityId project)
    {
        MarketProbeDriver.Require(!_game.IsInventoryOpen && !ControlPanel.IsOpen && !_game.IsWheelOpen
            && !_game.HasPendingAction, "Construction navigation requires the unobscured field.");
        var selection = Selected.Selection;
        var elapsed = Stopwatch.StartNew();
        _driver.Tap(Key.R);
        await _driver.Frame();
        for (int count = 0; _game.CurrentTarget?.Id != project; count++)
        {
            MarketProbeDriver.Require(count < 24 && elapsed.Elapsed.TotalSeconds < 5,
                "Native target navigation did not reach the construction project.");
            _driver.Tap(Key.Q);
            await _driver.Frame();
        }
        MarketProbeDriver.Require(elapsed.Elapsed.TotalSeconds < 5 && Selected.Selection == selection
            && _game.PresentedSelection?.Selection == selection, "Construction navigation changed selection or arrived late.");
    }

    private async Task ReviewConstruction(EntityId project)
    {
        MarketProbeDriver.Require(!_game.IsWheelOpen && !_game.HasPendingAction && !_game.IsLocallyPaused,
            "Construction review needs ready field input with no previous choice.");
        var selection = Selected.Selection;
        int before = _submissions.Count;
        await ProductionUntil(() => _game.CurrentOffers.GetValueOrDefault(FarmSemanticAction.Build) is { CanExecute: true } offer
            && offer.Intent is WorkConstructionIntent work && work.ProjectId == project
            && offer.Scope.Selection == selection && _game.PresentedSelection?.Selection == selection,
            "The exact executable building offer did not reach the presented frame.");
        _driver.Tap(Key.V);
        await ProductionUntil(() => _game.IsWheelOpen, "The construction wheel did not open.");
        int sector = Enumerable.Range(0, 4).Single(index => ProductionWheel.Model!.Slots[index]?.Action == FarmSemanticAction.Build);
        _driver.Joy(new[] { JoyButton.DpadUp, JoyButton.DpadRight, JoyButton.DpadDown, JoyButton.DpadLeft }[sector]);
        await ProductionUntil(() => ProductionWheel.Model?.Selected?.Action == FarmSemanticAction.Build,
            "Native direction did not review Build.");
        _reviewedConstruction = ProductionWheel.Model!.Selected!.Offer;
        MarketProbeDriver.Require(_reviewedConstruction.CanExecute && _reviewedConstruction.Intent is WorkConstructionIntent intent
            && intent.ProjectId == project && _reviewedConstruction.Scope.Selection == selection
            && _submissions.Count == before && !_game.HasPendingAction,
            "Construction review changed its scope or submitted before confirmation.");
    }

    private async Task ConfirmConstruction(string expected)
    {
        var reviewed = _reviewedConstruction ?? throw new InvalidOperationException("No construction choice was reviewed.");
        int before = _submissions.Count;
        if (expected == "blocked")
        {
            MarketProbeDriver.Require(!_game.IsWheelOpen || ProductionWheel.Model?.Selected?.Offer.CanExecute != true,
                "The old construction capture is unexpectedly executable.");
            _driver.Joy(JoyButton.A);
            await _driver.Frame();
            await _driver.Frame();
            MarketProbeDriver.Require(_submissions.Count == before && !_game.HasPendingAction,
                "An invalidated building capture submitted a command.");
            return;
        }
        MarketProbeDriver.Require(expected == "Applied" && !_game.IsLocallyPaused && _game.IsWheelOpen
            && ProductionWheel.Model?.Selected?.Offer is { CanExecute: true } current
            && current.Scope == reviewed.Scope && current.Intent == reviewed.Intent
            && reviewed.Scope.Selection == Selected.Selection && _game.PresentedSelection?.Selection == Selected.Selection,
            "The reviewed construction input is stale; it is neither replaced nor retried.");
        var work = (WorkConstructionIntent)reviewed.Intent!;
        _driver.Joy(JoyButton.A);
        await ProductionUntil(() => _submissions.Count == before + 1 && _submissions[^1].Receipt.IsCompleted
            && !_game.HasPendingAction && !_game.IsWheelOpen
            && ConstructionObserved(_game.CurrentSelection, reviewed, work, _submissions[^1])
            && ConstructionObserved(_game.PresentedSelection, reviewed, work, _submissions[^1]),
            "Construction confirmation did not yield its receipt and both observed work boundaries.");
        var submission = _submissions[^1];
        var command = WorldActionCodec.Decode(submission.CanonicalCommandBytes());
        MarketProbeDriver.Require(!submission.IsRetry && submission.Receipt.IsCompletedSuccessfully
            && submission.Receipt.Result.Status == CommandReceiptStatus.Applied
            && submission.Receipt.Result.Rejection == WorldActionRejection.None && command.Intent == work
            && command.ActorId == reviewed.Scope.ActorId && command.PrincipalId == reviewed.Scope.PrincipalId
            && command.OriginSessionId == reviewed.Scope.SessionId && command.WorldId == reviewed.Scope.WorldId
            && command.BranchId == reviewed.Scope.BranchId && command.ExpectedActorRevision == reviewed.Scope.ExpectedActorRevision
            && command.ControlWitness == reviewed.Scope.Selection!.ControlWitness,
            "The original work command, truthful submitter or receipt differs.");
    }

    private static bool ConstructionObserved(LocalSelectedPlayerObservation? observation, FarmActionOffer offer, WorkConstructionIntent work, FarmSubmission submission) =>
        observation is not null && observation.Selection == offer.Scope.Selection
        && observation.HomeView.WorldId == offer.Scope.WorldId && observation.HomeView.BranchId == offer.Scope.BranchId
        && (ConstructionProjects(observation).Any(project => project.ProjectId == work.ProjectId
            && project.CompletedWork == work.ExpectedProjectRevision + 1)
            || work.ExpectedProjectRevision == 199 && ConstructionStructures(observation)
                .Any(structure => structure.StructureId == EntityId.DeriveSpawned(
                    WorldActionCodec.Decode(submission.CanonicalCommandBytes()).CommandId, 3)));

    private async Task ConstructionBatch(EntityId project, string countText)
    {
        MarketProbeDriver.Require(int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out int count)
            && count is >= 1 and <= 20, "A native work batch contains one to twenty explicit choices.");
        var elapsed = Stopwatch.StartNew();
        for (int index = 0; index < count; index++)
        {
            await ReviewConstruction(project);
            await ConfirmConstruction("Applied");
            MarketProbeDriver.Require(elapsed.Elapsed.TotalSeconds < 20, "The native work-input batch exceeded its mailbox bound.");
        }
    }

    private async Task ConstructionSupplier(string operation)
    {
        FarmSemanticAction action = operation switch
        {
            "speak" => FarmSemanticAction.Speak, "accept" or "deliver" => FarmSemanticAction.Give,
            "close" => FarmSemanticAction.CloseDialogue, _ => throw new InvalidDataException("Unknown supplier choice.")
        };
        FarmActionOffer? captured = null;
        await ProductionUntil(() => _game.CurrentOffers.TryGetValue(action, out captured) && captured.CanExecute
            && (operation switch
            {
                "speak" => captured.Intent is SpeakToActorIntent,
                "accept" => captured.Intent is AcceptDeliveryQuestIntent,
                "deliver" => captured.Intent is CompleteDeliveryQuestIntent,
                _ => captured.Intent is CloseDialogueIntent
            }), "The exact native supplier choice is unavailable.");
        var originalOffer = captured ?? throw new InvalidOperationException("The supplier capture disappeared.");
        int before = _submissions.Count;
        _driver.Tap(action == FarmSemanticAction.Give ? Key.G : Key.E);
        await ProductionUntil(() => _submissions.Count == before + 1 && _submissions[^1].Receipt.IsCompleted
            && !_game.HasPendingAction && (operation switch
            {
                "speak" => View.Dialogue is not null,
                "accept" => View.Quests.Any(quest => quest.Status == ActorQuestStatus.Accepted),
                "deliver" => View.Quests.Any(quest => quest.Status == ActorQuestStatus.Completed),
                _ => View.Dialogue is null
            }), "The native supplier choice did not publish its exact outcome.");
        var submission = _submissions[^1];
        var command = WorldActionCodec.Decode(submission.CanonicalCommandBytes());
        MarketProbeDriver.Require(!submission.IsRetry && submission.Receipt.IsCompletedSuccessfully
            && submission.Receipt.Result.Status == CommandReceiptStatus.Applied
            && command.ActorId == originalOffer.Scope.ActorId && command.ControlWitness is null,
            "The supplier command was not its original own-actor choice.");
        var original = new WorldActionCommand(command.CommandId, originalOffer.Scope.PrincipalId, originalOffer.Scope.SessionId,
            originalOffer.Scope.ActorId, originalOffer.Scope.WorldId, originalOffer.Scope.BranchId, originalOffer.Scope.ExpectedActorRevision,
            originalOffer.Intent!, originalOffer.Scope.Selection?.ControlWitness);
        MarketProbeDriver.Require(WorldActionCodec.Encode(command).AsSpan().SequenceEqual(WorldActionCodec.Encode(original)),
            "Native supplier input changed the captured exact quest, stock or resource revisions.");
    }

    private async Task CaptureConstruction(string label, EntityId project)
    {
        MarketProbeDriver.Require(label.Length is > 0 and <= 64 && _constructionCaptures.Count < 128
            && (ConstructionProjects(Selected).Any(value => value.ProjectId == project)
                || ConstructionStructures(Selected).Count > 0),
            "Construction capture requires the named current project or its completed public geometry.");
        await Capture(5, "-construction");
        Rect2? actor = _game.Canvas.ActorSpriteViewportBounds(Selected.Selection.ActorId);
        MarketProbeDriver.Require(actor is { } actorBounds && Hud.ShowsFieldSprite(actorBounds),
            "Construction actor sprite is obscured by the HUD.");
        if (_game.IsWheelOpen)
        {
            MarketProbeDriver.Require(_reviewedConstruction is { } reviewed
                && ProductionWheel.Model?.Selected?.Offer is { } current && current.Scope == reviewed.Scope
                && current.Intent == reviewed.Intent && current.Scope.Selection == Selected.Selection,
                "The rendered construction review changed its original capture.");
            foreach (string name in new[] { "ChoiceName", "ChoiceDetails", "WheelTime" })
            {
                var text = ProductionWheel.GetNode<Label>("%" + name);
                MarketProbeDriver.Require(text.IsVisibleInTree() && text.GetLineCount() == text.GetVisibleLineCount()
                    && GetViewport().GetVisibleRect().Encloses(text.GetGlobalRect()), "Construction explanation is clipped.");
            }
        }
        var actual = ConstructionStructures(Selected).ToArray();
        var bounds = actual.Select(value => _game.Canvas.StructureSpriteViewportBounds(value.StructureId)).ToArray();
        MarketProbeDriver.Require(bounds.All(value => value is { } rectangle && Hud.ShowsFieldSprite(rectangle)),
            "A completed public structure's actual sprite is absent or obscured.");
        _constructionCaptures.Add(new { Sequence = _sequence, Label = label, Image = $"{_sequence:D4}-construction.png",
            Selection = SelectionEvidence(), ActorBounds = Bounds(actor!.Value), StructureBounds = bounds.Select(value => Bounds(value!.Value)).ToArray(),
            Construction = ConstructionEvidence() });
    }

    private object? ConstructionEvidence() => _game.CurrentSelection is not { } observation ? null : new
    {
        Actor = observation.Selection.ActorId.ToString(), observation.Selection.Generation,
        ProductionDisclosed = observation.ControlledView is null || observation.ControlledView.Production is not null,
        InventoryDisclosed = observation.ControlledView?.Inventory is not null,
        RecoveryDisclosed = observation.ControlledView?.Recovery is not null,
        Projects = ConstructionProjects(observation),
        Work = observation.ControlledView is { } scoped ? scoped.Production?.ConstructionWorkViews : observation.HomeView.ConstructionWork,
        Structures = ConstructionStructures(observation).Select(value => new
        { Id = value.StructureId.ToString(), value.Origin, value.Members }).ToArray(),
        Reviewed = _reviewedConstruction is { } offer ? new { Actor = offer.Scope.ActorId.ToString(),
            Port = offer.Scope.Selection?.PortId.ToString(), Generation = offer.Scope.Selection?.Generation,
            Witness = WitnessBytes(offer.Scope.Selection?.ControlWitness), offer.CostText, offer.DetailText,
            Intent = offer.Intent as WorkConstructionIntent } : null
    };
}
