using System.Diagnostics;
using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client.Networking;

internal sealed partial class NativeRosterPlayProbe
{
    private readonly List<(WorldActionCommand Command, Task<WorldActionReceipt> Receipt)> _permissionSubmissions = [];
    private readonly List<object> _selectionCaptures = [];
    private FarmControlPanel ControlPanel => _game.GetNode<FarmControlPanel>("FarmControlPanel");
    private LocalSelectedPlayerObservation Selected => _game.CurrentSelection
        ?? throw new InvalidOperationException("The connected client has no selected observation.");
    private IReadOnlyList<LocalActorView> SelectedActors => Selected.ControlledView?.Actors ?? Selected.HomeView.Actors;
    private LocalEncounterActionView? SelectedActions => Selected.ControlledView is { } controlled
        ? controlled.Combat?.Actions : Selected.HomeView.EncounterActions;

    private static EntityId RequestActor(JsonElement request) => EntityId.Parse(request.GetProperty("Target").GetString()!);

    private Task SelectionUntil(Func<bool> predicate, string failure) => _driver.Until(predicate,
        failure + " Last control status: " + _game.LastControlOperationStatus, 5);

    private async Task OpenSelectionPanel()
    {
        if (!ControlPanel.IsOpen) await _driver.Click(ControlPanel.GetNode<Button>("%Characters"));
        await SelectionUntil(() => ControlPanel.IsOpen && ControlPanel.CurrentBoard is not null && !_game.ControlOperationPending,
            "The actual Characters panel did not present its consent board.");
    }

    private async Task RefreshSelectionPanel()
    {
        await OpenSelectionPanel();
        var before = ControlPanel.CurrentBoard;
        await _driver.Click(ControlPanel.GetNode<Button>("%Refresh"));
        await SelectionUntil(() => ControlPanel.CurrentBoard is not null
            && !ReferenceEquals(before, ControlPanel.CurrentBoard) && !_game.ControlOperationPending,
            "The actual Refresh button did not publish a fresh board.");
    }

    private async Task CloseSelectionPanel()
    {
        if (ControlPanel.IsOpen) await _driver.Click(ControlPanel.GetNode<Button>("%Close"));
        await SelectionUntil(() => !ControlPanel.IsOpen, "The actual Close button did not close Characters.");
    }

    private async Task OfferControl(EntityId target, string scopes)
    {
        await OpenSelectionPanel();
        MarketProbeDriver.Require(Enum.TryParse(scopes, out WorldControlCapabilities requested)
            && requested != 0 && (requested & ~WorldControlCapabilities.All) == 0, "Unknown requested consent scopes.");
        OptionButton recipient = ControlPanel.GetNode<OptionButton>("%Recipient");
        int row = Enumerable.Range(0, recipient.ItemCount).Single(index => recipient.GetItemMetadata(index).AsString() == target.ToString());
        await _driver.Click(recipient);
        PopupMenu popup = recipient.GetPopup();
        await SelectionUntil(() => popup.Visible, "The actual recipient dropdown did not open.");
        for (int moves = 0; popup.GetFocusedItem() != row; moves++)
        {
            MarketProbeDriver.Require(moves <= recipient.ItemCount, "Recipient keyboard navigation did not reach the exact actor.");
            _driver.Tap(Key.Down);
            await _driver.Frame();
        }
        _driver.Tap(Key.Enter);
        await SelectionUntil(() => !popup.Visible && recipient.Selected == row, "Recipient input did not select the exact actor.");
        WorldControlCapabilities observedScopes = 0;
        foreach (var check in Descendants(ControlPanel).OfType<CheckBox>().Where(value => value.HasMeta(FarmControlPanel.ScopeMetadata)))
        {
            var capability = Enum.Parse<WorldControlCapabilities>(check.GetMeta(FarmControlPanel.ScopeMetadata).AsString());
            bool expected = (requested & capability) != 0;
            if (check.ButtonPressed != expected) await _driver.Click(check);
            MarketProbeDriver.Require(check.ButtonPressed == expected, "The actual scope checkbox did not retain its choice.");
            if (check.ButtonPressed) observedScopes |= capability;
        }
        MarketProbeDriver.Require(observedScopes == requested, "The visible checkboxes do not express the exact requested scopes.");
        await SubmitPermissionButton(ControlPanel.GetNode<Button>("%Grant"));
        await SelectionUntil(() => ControlPanel.CurrentBoard?.Slots.Any(slot => slot.OwnerActorId == Selected.Selection.HomeActorId
            && slot.GranteeActorId == target && slot.Status == WorldControlPermissionStatus.Offered && slot.Capabilities == requested) == true,
            "The offered actor and exact checked scopes were not observed.");
    }

    private async Task PermissionControl(string action, EntityId target)
    {
        await OpenSelectionPanel();
        await SubmitPermissionButton(ControlButton(action, target));
        var expected = action == "accept" ? WorldControlPermissionStatus.Accepted : WorldControlPermissionStatus.Revoked;
        await SelectionUntil(() => ControlPanel.CurrentBoard?.Slots.Any(slot => slot.Status == expected
            && (action == "accept" ? slot.OwnerActorId == target && slot.GranteeActorId == Selected.Selection.HomeActorId
                : slot.OwnerActorId == Selected.Selection.HomeActorId && slot.GranteeActorId == target)) == true,
            "The actual consent action did not produce its exact participant row.");
    }

    private async Task SubmitPermissionButton(Button button)
    {
        EntityId? previous = _game.LastControlPermissionCommand?.CommandId;
        await _driver.Click(button);
        await SelectionUntil(() => _game.LastControlPermissionCommand is { } command && command.CommandId != previous
            && _game.LastControlPermissionReceipt is { IsCompleted: true } && !_game.ControlOperationPending,
            "One actual permission click did not complete its original receipt.");
        CapturePermissionSubmission();
        var receipt = _game.LastControlPermissionReceipt!;
        MarketProbeDriver.Require(receipt.IsCompletedSuccessfully && receipt.Result.Status == CommandReceiptStatus.Applied,
            "The actual permission choice was refused; no replacement was submitted.");
    }

    private async Task AssignmentControl(string action, EntityId? target)
    {
        await OpenSelectionPanel();
        EntityId owner = target ?? ControlPanel.CurrentBoard!.HeldWitness?.OwnerActorId
            ?? throw new InvalidOperationException("There is no displayed assignment to release.");
        await _driver.Click(ControlButton(action, owner));
        if (action == "acquire")
            await SelectionUntil(() => !_game.ControlOperationPending && ControlPanel.CurrentBoard?.HeldWitness?.OwnerActorId == owner,
                "Acquisition did not return the displayed owner's actual custody witness.");
        else
        {
            await SelectionUntil(() => !_game.ControlOperationPending && Selected.Selection.ControlWitness is null,
                "Release did not return the live selection to home.");
            await OpenSelectionPanel();
            await RefreshSelectionPanel();
            MarketProbeDriver.Require(ControlPanel.CurrentBoard!.HeldWitness is null, "Released custody remains on the fresh board.");
        }
    }

    private async Task SelectControl(EntityId target)
    {
        await OpenSelectionPanel();
        var previous = Selected.Selection;
        string action = target == previous.HomeActorId ? "home" : "select";
        await _driver.Click(ControlButton(action, target));
        await SelectionUntil(() => !_game.ControlOperationPending && Selected.Selection.ActorId == target
            && Selected.Selection.Generation > previous.Generation && _game.PresentedSelection?.Selection == Selected.Selection
            && _game.IsLocallyPaused && !Selected.IsReady, "The actual actor selection did not publish its new paused generation.");
    }

    private Button ControlButton(string action, EntityId actor) => Descendants(ControlPanel).OfType<Button>().Single(button =>
        button.HasMeta(FarmControlPanel.ActionMetadata) && button.GetMeta(FarmControlPanel.ActionMetadata).AsString() == action
        && button.HasMeta(FarmControlPanel.ActorMetadata) && button.GetMeta(FarmControlPanel.ActorMetadata).AsString() == actor.ToString());

    private async Task SelectedPause(bool paused)
    {
        await CloseSelectionPanel();
        var selection = Selected.Selection;
        if (_game.IsLocallyPaused != paused) _driver.Tap(Key.Escape);
        await SelectionUntil(() => Selected.Selection == selection && _game.IsLocallyPaused == paused && Selected.IsReady != paused,
            "Explicit native selected Pause/Resume did not reach the same selection.");
    }

    private async Task SelectedWalk(string direction)
    {
        MarketProbeDriver.Require((Selected.ControlledView?.Combat?.Encounter ?? Selected.HomeView.Encounter) is null,
            "Selected walking was requested inside an encounter.");
        Key key = direction switch { "left" => Key.Left, "right" => Key.Right, "up" => Key.Up, "down" => Key.Down,
            _ => throw new InvalidDataException("Unknown selected walking direction.") };
        await SelectedPause(false);
        var selection = Selected.Selection;
        var origin = SelectedActors.Single(actor => actor.ActorId == selection.ActorId).Feet;
        var elapsed = Stopwatch.StartNew();
        _driver.Inputs.Add(new { Stage = _stage, SelectedActor = selection.ActorId.ToString(), Direction = direction,
            Origin = origin, HeldUntil = "first observed step", Retries = 0 });
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        Input.FlushBufferedEvents();
        try
        {
            await _driver.Until(() => Selected.Selection == selection
                && SelectedActors.Single(actor => actor.ActorId == selection.ActorId).Feet != origin,
                "Held native input did not produce selected movement within five seconds.", 5);
        }
        finally
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
            Input.FlushBufferedEvents();
        }
        MarketProbeDriver.Require(elapsed.Elapsed.TotalSeconds < 5, "Selected movement arrived after its original five-second deadline.");
        await SelectedPause(true);
    }

    private async Task SelectedEnter(EntityId target)
    {
        await SelectedPause(false);
        for (int count = 0; _game.CurrentTarget?.Id != target; count++)
        {
            MarketProbeDriver.Require(count < 24, "Native target input did not reach the exact selected actor's opponent.");
            _driver.Tap(Key.Q);
            await _driver.Frame();
        }
        await SelectionUntil(() => !_game.HasPendingAction
            && _game.CurrentOffers.GetValueOrDefault(FarmSemanticAction.EnterEncounter) is { CanExecute: true } offer
            && offer.TargetId == target && MatchesSelectedOffer(offer), "No matching selected confrontation offer is presented.");
        int before = _submissions.Count;
        _driver.Tap(Key.E);
        await SelectedApplied(before);
        await SelectionUntil(() => SelectedActions?.CanWait == true, "Selected entry did not reach a genuine required decision.");
    }

    private async Task SelectedWheel(FarmSemanticAction action)
    {
        await SelectedPause(false);
        await SelectionUntil(() => !_game.HasPendingAction
            && _game.CurrentOffers.GetValueOrDefault(action) is { CanExecute: true } offer && MatchesSelectedOffer(offer),
            "A matching selected " + action + " offer is unavailable.");
        int before = _submissions.Count;
        _driver.Tap(Key.V);
        await SelectionUntil(() => _game.IsWheelOpen, "The real selected wheel did not open.");
        var wheel = _game.GetNode<FarmQuickWheel>("FarmQuickWheel");
        int sector = Enumerable.Range(0, 4).Single(index => wheel.Model!.Slots[index]?.Action == action);
        _driver.Joy(new[] { JoyButton.DpadUp, JoyButton.DpadRight, JoyButton.DpadDown, JoyButton.DpadLeft }[sector]);
        await SelectionUntil(() => wheel.Model?.Selected?.Action == action, "Controller input did not select the requested wheel action.");
        MarketProbeDriver.Require(wheel.Model!.Selected!.Offer.CanExecute && MatchesSelectedOffer(wheel.Model.Selected.Offer),
            "The chosen wheel capture became stale; nothing was retried.");
        _driver.Joy(JoyButton.A);
        await SelectedApplied(before);
    }

    private bool MatchesSelectedOffer(FarmActionOffer offer)
    {
        var selected = Selected;
        if (offer.Scope.Selection != selected.Selection || _game.PresentedSelection?.Selection != selected.Selection
            || offer.Scope.ActorId != selected.Selection.ActorId || offer.Scope.ExpectedActorRevision
                != SelectedActors.Single(actor => actor.ActorId == selected.Selection.ActorId).PresenceRevision) return false;
        return offer.Intent switch
        {
            QueueEncounterWaitIntent wait => SelectedActions is { } actions
                && wait.EncounterId == actions.EncounterId && wait.ExpectedTimingRevision == actions.TimingRevision,
            QueueEncounterAbilityIntent ability => SelectedActions is { } actions
                && ability.EncounterId == actions.EncounterId && ability.ExpectedTimingRevision == actions.TimingRevision,
            EnterEncounterIntent entry => (selected.ControlledView?.Combat?.Lifecycle ?? selected.HomeView.EncounterLifecycle)
                ?.Entries.Any(value => value.ScenarioId == entry.ScenarioId && value.EncounterId == entry.EncounterId
                    && value.TargetRevision == entry.ExpectedTargetRevision && value.CanEnter) == true,
            _ => false
        };
    }

    private async Task SelectedApplied(int previous)
    {
        await SelectionUntil(() => _submissions.Count == previous + 1 && _submissions[^1].Receipt.IsCompleted && !_game.HasPendingAction,
            "The single selected native command did not return its receipt.");
        var submission = _submissions[^1];
        MarketProbeDriver.Require(!submission.IsRetry && submission.Receipt.IsCompletedSuccessfully
            && submission.Receipt.Result.Status == CommandReceiptStatus.Applied,
            "The selected native command failed; no replacement was submitted.");
    }

    private void CapturePermissionSubmission()
    {
        if (_game.LastControlPermissionCommand is { } command && _game.LastControlPermissionReceipt is { } receipt
            && _permissionSubmissions.All(value => value.Command.CommandId != command.CommandId))
            _permissionSubmissions.Add((command, receipt));
    }

    private static IEnumerable<Node> Descendants(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            yield return child;
            foreach (Node descendant in Descendants(child)) yield return descendant;
        }
    }

    private async Task CaptureSelection()
    {
        if (ControlPanel.IsOpen)
        {
            int index = 0;
            foreach (Control control in Descendants(ControlPanel).OfType<Control>().Where(value => value.IsVisibleInTree()
                && (value is Button && value.HasMeta(FarmControlPanel.ActionMetadata) || value is CheckBox || value is OptionButton)))
            {
                await _driver.Hover(control); // Ordinary wheel scrolling proves an offscreen row is reachable.
                string suffix = $"-control-{index++:D2}";
                await Capture(5, suffix);
                Rect2 bounds = control.GetGlobalRect();
                MarketProbeDriver.Require(GetViewport().GetVisibleRect().Encloses(bounds), "A consent control is clipped outside the viewport.");
                for (Node? parent = control.GetParent(); parent is not null; parent = parent.GetParent())
                    if (parent is ScrollContainer scroll)
                        MarketProbeDriver.Require(scroll.GetGlobalRect().Encloses(bounds), "The scrolled consent control remains clipped.");
                _selectionCaptures.Add(new { Sequence = _sequence, Control = control.Name.ToString(), Bounds = Bounds(bounds),
                    Image = $"{_sequence:D4}{suffix}.png",
                    Action = control.HasMeta(FarmControlPanel.ActionMetadata) ? control.GetMeta(FarmControlPanel.ActionMetadata).AsString() : null });
            }
            MarketProbeDriver.Require(GetViewport().GetVisibleRect().Encloses(ControlPanel.GetNode<Control>("Root/Panel").GetGlobalRect()),
                "The actual Characters panel does not fit the rendered viewport.");
            await Capture(5);
        }
        else
        {
            await Capture(5);
            EntityId actor = Selected.Selection.ActorId;
            Rect2? sprite = _game.Canvas.ActorSpriteViewportBounds(actor);
            MarketProbeDriver.Require(sprite is { } rectangle && Hud.ShowsFieldSprite(rectangle)
                && !rectangle.Intersects(ControlPanel.GetNode<Button>("%Characters").GetGlobalRect()),
                "The operated character's complete rendered sprite does not clear the HUD and Characters control.");
            _selectionCaptures.Add(new { Sequence = _sequence, Actor = actor.ToString(), Sprite = Bounds(sprite!.Value), PanelOpen = false });
        }
        Rect2 header = Hud.GetNode<Control>("Root/Header").GetGlobalRect();
        foreach (string name in new[] { "Identity", "Clock", "Inventory" })
        {
            Label label = Hud.GetNode<Label>("%" + name);
            if (!label.IsVisibleInTree() || label.Text.Length == 0) continue;
            MarketProbeDriver.Require(header.Encloses(label.GetGlobalRect())
                && label.GetLineCount() == label.GetVisibleLineCount(),
                "The rendered " + name + " header text is clipped.");
            _selectionCaptures.Add(new { Sequence = _sequence, Header = name, label.Text,
                Lines = label.GetLineCount(), VisibleLines = label.GetVisibleLineCount(), Bounds = Bounds(label.GetGlobalRect()) });
        }
    }

    private static object Bounds(Rect2 value) => new { X = value.Position.X, Y = value.Position.Y, Width = value.Size.X, Height = value.Size.Y };
}
