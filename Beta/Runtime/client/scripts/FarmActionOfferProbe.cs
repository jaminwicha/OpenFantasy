using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Exercises the actual HUD previews and captured actions against a disposable native world.</summary>
internal partial class FarmActionOfferProbe(FarmGame game, string reportPath) : Node
{
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly List<string> _checks = [];
    private FarmHud _hud = null!;

    public override async void _Ready()
    {
        try
        {
            await Until(() => game.CurrentView is not null && game.CurrentOffers.Count > 0, "Offers did not become ready.");
            _hud = game.GetChildren().OfType<FarmHud>().Single();
            await InspectBlockedCraft();
            Tap(Key.Escape);
            await Until(() => !game.IsLocallyPaused, "The ordinary pause key did not resume.");
            await SelectTargets();
            await Harvest(GatherNodeKind.Oak);
            await Harvest(GatherNodeKind.Berry);
            await Harvest(GatherNodeKind.Berry);
            await Craft();
            _checks.AddRange(await new FarmInventoryProbe(game, this, reportPath).RunAsync());
            await Speak();
            WriteReport("passed", null);
            game.RequestClose();
        }
        catch (Exception error)
        {
            ReleaseMovement();
            WriteReport("failed", error.Message);
            GD.PushError("Native action-offer qualification failed: " + error);
            GetTree().Quit(1);
        }
    }

    private async Task InspectBlockedCraft()
    {
        Require(game.IsLocallyPaused, "The preview test must begin paused.");
        FarmActionOffer offer = Offer(FarmSemanticAction.Craft);
        Require(!offer.CanExecute && !string.IsNullOrWhiteSpace(offer.BlockingReason),
            "Missing materials did not retain an inspectable crafting offer and blocker.");
        string before = JsonSerializer.Serialize(View());
        Button craft = _hud.GetNode<Button>("%Craft");
        craft.GrabFocus();
        await Delay(0.15);
        Require(craft.HasFocus(), "A known unavailable action could not receive keyboard/controller focus.");
        AssertDetails(craft, offer);
        await Capture("blocked-craft");
        craft.ReleaseFocus();
        craft.EmitSignal(Control.SignalName.MouseEntered);
        await Delay(0.1);
        AssertDetails(craft, offer);
        Require(JsonSerializer.Serialize(View()) == before && !game.HasPendingAction,
            "Reading an unavailable action changed the authoritative view or submitted an action.");
        craft.EmitSignal(Control.SignalName.MouseExited);
        _checks.Add("Unavailable crafting stayed inspectable through focus and hover without changing the paused world.");
    }

    private async Task SelectTargets()
    {
        ulong quantity = View().PlayerInventory.Quantity;
        var revisions = View().GatheringNodes.ToDictionary(node => node.NodeId, node => node.Revision);
        Tap(Key.Q);
        await Until(() => game.CurrentTarget?.Kind == FarmTargetKind.Person, "Q did not select the observed person.");
        Require(!Offer(FarmSemanticAction.Gather).CanExecute, "A selected person silently gathered from a different target.");
        Require(_hud.GetNode<Label>("%TargetName").Text.Contains(game.CurrentTarget!.Name, StringComparison.Ordinal),
            "The selected person's name was not shown.");
        Node2D marker = game.GetChildren().OfType<FarmCanvas>().Single().GetNode<Node2D>("TargetMarker");
        Require(marker.Visible && marker.Position == Presentation.FarmCanvasCoordinates.Position(game.CurrentTarget.Feet!.Value),
            "The field marker did not identify the selected person's projected position.");
        await Delay(0.1); // Let the selected marker and label reach the rendered viewport.
        await Capture("person-target");
        EntityId person = game.CurrentTarget.Id;
        Tap(Key.Escape);
        await Until(() => game.IsLocallyPaused, "Could not pause the target comparison.");
        Tap(Key.Q);
        Shoulder(JoyButton.LeftShoulder);
        await Delay(0.1);
        Require(game.CurrentTarget?.Id == person && _hud.GetNode<Button>("%NextTarget").Disabled,
            "Paused target input bypassed its context.");
        Tap(Key.Escape);
        await Until(() => !game.IsLocallyPaused, "Could not resume the target comparison.");

        Shoulder(JoyButton.RightShoulder);
        await Until(() => game.CurrentTarget?.Kind == FarmTargetKind.Harvestable,
            "The shoulder event did not advance target selection.");
        EntityId first = game.CurrentTarget!.Id;
        _hud.GetNode<Button>("%NextTarget").EmitSignal(Button.SignalName.Pressed);
        await Until(() => game.CurrentTarget?.Kind == FarmTargetKind.Harvestable && game.CurrentTarget.Id != first,
            "The native Next button did not choose the next plant.");
        Require(Offer(FarmSemanticAction.Gather).TargetId == game.CurrentTarget!.Id,
            "The displayed gathering offer ignored the selected identity.");
        Require(!Offer(FarmSemanticAction.Speak).CanExecute && !Offer(FarmSemanticAction.Give).CanExecute,
            "A selected plant silently redirected Talk or Give to a person.");
        Tap(Key.E);
        await Delay(0.1);
        Require(View().Dialogue is null && !game.HasPendingAction, "Talking to a plant opened an unrelated conversation.");
        Require(View().PlayerInventory.Quantity == quantity
            && View().GatheringNodes.All(node => node.Revision == revisions[node.NodeId]),
            "Target selection changed inventory or harvested a resource.");
        _checks.Add("Keyboard, shoulder events and the native Next button selected identities without spending or changing verbs.");

        EntityId oak = View().GatheringNodes.Single(node => node.Kind == GatherNodeKind.Oak).NodeId;
        for (int attempt = 0; game.CurrentTarget?.Id != oak && attempt < 4; attempt++)
        {
            Tap(Key.Q);
            await Delay(0.05);
        }
        Require(game.CurrentTarget?.Id == oak, "The observed oak was missing from the target cycle.");
        await Approach(() => View().GatheringNodes.Single(node => node.NodeId == oak).Feet, 300);
        await Delay(0.1);
        await Capture("plant-target");
        await Approach(() => View().GatheringNodes.Single(node => node.Kind == GatherNodeKind.Berry).Feet, 300);
        Require(FarmClientSession.NearestGatherNode(View())?.Kind == GatherNodeKind.Berry,
            "The target retention comparison did not approach a different plant.");
        Require(game.CurrentTarget?.Id == oak && Offer(FarmSemanticAction.Gather).TargetId == oak
            && !Offer(FarmSemanticAction.Gather).CanExecute, "Moving silently substituted a nearer plant.");
        Button gather = _hud.GetNode<Button>("%Gather");
        gather.GrabFocus();
        await Delay(0.1);
        AssertDetails(gather, Offer(FarmSemanticAction.Gather));
        await Capture("locked-target");
        Tap(Key.F);
        await Delay(0.1);
        Require(View().PlayerInventory.Quantity == quantity && !game.HasPendingAction,
            "The out-of-range locked target submitted or spent something.");
        gather.ReleaseFocus();
        Tap(Key.R);
        await Until(() => game.CurrentTarget is null, "Clear did not release the explicit target.");
        Require(Offer(FarmSemanticAction.Gather).TargetId == FarmClientSession.NearestGatherNode(View())!.NodeId,
            "Clear did not restore the displayed contextual action.");
        Require(!game.GetChildren().OfType<FarmCanvas>().Single().GetNode<Node2D>("TargetMarker").Visible,
            "Clearing selection left a misleading field marker.");
        Shoulder(JoyButton.RightShoulder);
        await Until(() => game.CurrentTarget is not null, "Shoulder selection did not work after clearing.");
        Shoulder(JoyButton.LeftShoulder);
        await Until(() => game.CurrentTarget is null, "Left shoulder did not clear the target in ordinary play.");
        _checks.Add("A locked oak remained selected while berries became nearer; unavailable F spent nothing and explicit Clear restored context.");
    }

    private static void Shoulder(JoyButton button)
    {
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = true });
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = false });
    }

    private async Task Harvest(GatherNodeKind kind)
    {
        EntityId targetId = View().GatheringNodes.Single(node => node.Kind == kind).NodeId;
        await Approach(() => View().GatheringNodes.Single(node => node.NodeId == targetId).Feet, 300);
        await Until(() => Offer(FarmSemanticAction.Gather).TargetId == targetId
            && Offer(FarmSemanticAction.Gather).CanExecute, "The approached plant did not produce an executable offer.");
        FarmActionOffer offer = Offer(FarmSemanticAction.Gather);
        Button button = _hud.GetNode<Button>("%Gather");
        button.GrabFocus();
        await Delay(0.1);
        AssertDetails(button, offer);
        await Capture(kind == GatherNodeKind.Oak ? "oak-offer" : "berry-offer");
        LocalGatherNodeView before = View().GatheringNodes.Single(node => node.NodeId == targetId);
        var others = View().GatheringNodes.Where(node => node.NodeId != targetId)
            .ToDictionary(node => node.NodeId, node => node.RemainingSwings);
        ulong quantity = View().PlayerInventory.Quantity;
        Tap(Key.F);
        await Until(() => View().GatheringNodes.Single(node => node.NodeId == targetId).Revision > before.Revision
            && !game.HasPendingAction, "The displayed gathering action did not commit.");
        Require(View().GatheringNodes.Single(node => node.NodeId == targetId).RemainingSwings == before.RemainingSwings - 1,
            "One input did not consume exactly one swing from the displayed plant.");
        Require(View().PlayerInventory.Quantity > quantity, "Gathering did not add actual owned material.");
        Require(View().GatheringNodes.Where(node => node.NodeId != targetId)
            .All(node => node.RemainingSwings == others[node.NodeId]), "Gathering altered an unselected plant.");
        button.ReleaseFocus();
        _checks.Add("Displayed " + kind + " target and cost matched one real harvest; other plants were unchanged.");
    }

    private async Task Craft()
    {
        await Until(() => Offer(FarmSemanticAction.Craft).CanExecute, "The harvested recipe remained unavailable.");
        LocalWorldView view = View();
        LocalCraftRecipeView recipe = view.Crafting!.Recipes.Single(value => value.Key == "craft_rations");
        var quantities = recipe.Terms.Select(term => term.DefinitionId).Distinct()
            .ToDictionary(id => id, id => Quantity(view, id));
        ulong revision = view.Crafting.Revision;
        Button craft = _hud.GetNode<Button>("%Craft");
        craft.GrabFocus();
        await Delay(0.1);
        AssertDetails(craft, Offer(FarmSemanticAction.Craft));
        await Capture("ready-craft");
        Tap(Key.C);
        await Until(() => View().Crafting!.Revision > revision && !game.HasPendingAction, "The displayed recipe did not commit.");
        foreach (EntityId id in quantities.Keys)
        {
            long change = recipe.Terms.Where(term => term.DefinitionId == id).Sum(term =>
                (term.Role == RecipeMaterialRole.Input ? -1L : 1L) * checked((long)term.Quantity));
            Require(checked((long)Quantity(View(), id)) == checked((long)quantities[id] + change),
                "The action's actual material change did not match its recipe.");
        }
        craft.ReleaseFocus();
        _checks.Add("Craft inspection and keyboard commitment used the same recipe with exact input/output/waste quantities.");
    }

    private async Task Speak()
    {
        EntityId wren = View().Actors.Single(actor => actor.ActorId != View().PlayerActorId).ActorId;
        await Approach(() => View().Actors.Single(actor => actor.ActorId == wren).Feet, 600);
        FarmActionOffer offer = Offer(FarmSemanticAction.Speak);
        Require(offer.TargetId == wren, "Talk did not name the displayed person.");
        Tap(Key.E);
        await Until(() => View().Dialogue is not null && !game.HasPendingAction, "The displayed Talk action did not open dialogue.");
        Require(View().Dialogue!.SpeakerActorId == wren, "Talking addressed a different person.");
        await Delay(0.1);
        EntityId? selected = game.CurrentTarget?.Id;
        Tap(Key.Q);
        Shoulder(JoyButton.LeftShoulder);
        await Delay(0.1);
        Require(game.CurrentTarget?.Id == selected && View().Dialogue is not null,
            "Target controls changed selection or ended the addressed conversation.");
        await Capture("dialogue");
        Tap(Key.Escape);
        await Until(() => View().Dialogue is null && !game.HasPendingAction, "Offer routing broke top-dialogue cancellation.");
        Require(!game.IsLocallyPaused, "Dialogue cancellation also paused ordinary play.");
        _checks.Add("Talk preserved the displayed speaker and Escape retained dialogue-first cancellation.");
    }

    private void AssertDetails(Button button, FarmActionOffer offer)
    {
        Require(!string.IsNullOrWhiteSpace(button.TooltipText), "The action has no hover explanation.");
        string visible = string.Join("\n", Descendants(_hud).OfType<Label>()
            .Where(label => label.IsVisibleInTree()).Select(label => label.Text));
        Require(!string.IsNullOrWhiteSpace(offer.CostText) && visible.Contains(offer.CostText, StringComparison.Ordinal),
            "The known cost is not visible when this action is focused or hovered.");
        Require(visible.Contains(offer.TargetName, StringComparison.Ordinal), "The inspected target is not visible.");
        if (!offer.CanExecute)
            Require(visible.Contains(offer.BlockingReason!, StringComparison.Ordinal), "The unavailable action's blocker is not visible.");
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            yield return child;
            foreach (Node descendant in Descendants(child)) yield return descendant;
        }
    }

    private async Task Approach(Func<OpenFantasy.Core.Spatial.SubunitPosition> target, long range)
    {
        Stopwatch travel = Stopwatch.StartNew();
        while (true)
        {
            Require(travel.Elapsed < TimeSpan.FromSeconds(20), "Could not reach the native action target.");
            var feet = View().Actors.Single(actor => actor.ActorId == View().PlayerActorId).Feet;
            var destination = target();
            long x = destination.X - feet.X;
            long y = destination.Y - feet.Y;
            ReleaseMovement();
            if ((Int128)x * x + (Int128)y * y <= (Int128)range * range)
            {
                await Delay(0.15);
                return;
            }
            Input.ActionPress(Math.Abs(x) > Math.Abs(y)
                ? x < 0 ? "walk_left" : "walk_right" : y < 0 ? "walk_up" : "walk_down");
            await Frame();
        }
    }

    private FarmActionOffer Offer(FarmSemanticAction action) => game.CurrentOffers[action];
    private LocalWorldView View() => game.CurrentView ?? throw new InvalidOperationException("The public view disappeared.");
    private static ulong Quantity(LocalWorldView view, EntityId id) => view.PlayerInventory.Lots
        .Where(lot => lot.DefinitionId == id).Aggregate(0UL, (sum, lot) => checked(sum + lot.Quantity));
    private async Task Until(Func<bool> condition, string failure)
    {
        Stopwatch wait = Stopwatch.StartNew();
        while (!condition())
        {
            Require(wait.Elapsed < TimeSpan.FromSeconds(8), failure);
            await Frame();
        }
    }
    private async Task Delay(double seconds)
    {
        Stopwatch wait = Stopwatch.StartNew();
        while (wait.Elapsed.TotalSeconds < seconds) await Frame();
    }
    private async Task Frame()
    {
        Require(!game.HasFailed && _elapsed.Elapsed < TimeSpan.FromSeconds(60), "The native offer probe failed or timed out.");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private static void Tap(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
    }
    private static void ReleaseMovement()
    {
        foreach (string action in new[] { "walk_left", "walk_right", "walk_up", "walk_down" }) Input.ActionRelease(action);
    }
    private async Task Capture(string suffix)
    {
        Rect2 viewport = GetViewport().GetVisibleRect();
        foreach (Control control in Descendants(_hud).OfType<Control>()
            .Where(control => control.IsVisibleInTree() && (control is Button || control.Name == "ActionPreview")))
        {
            Rect2 bounds = control.GetGlobalRect();
            Require(bounds.Position.X >= viewport.Position.X && bounds.Position.Y >= viewport.Position.Y
                && bounds.End.X <= viewport.End.X && bounds.End.Y <= viewport.End.Y,
                $"The visible {control.Name} control is clipped outside the viewport.");
        }
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(Path.ChangeExtension(reportPath, "." + suffix + ".png")) == Error.Ok,
            "Could not retain the native preview image.");
    }
    private void WriteReport(string status, string? failure)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            Kind = "native-action-offers-v1", Status = status, Failure = failure,
            Runtime = RuntimeInformation.FrameworkDescription, DisplayDriver = DisplayServer.GetName(),
            WindowFocused = game.IsWindowFocused, LocallyPaused = game.IsLocallyPaused,
            BackgroundRender = GetWindow().Unfocusable, NativeWindowFocused = GetWindow().HasFocus(),
            game.BindWindowFocus,
            InventoryOpen = game.IsInventoryOpen, ActionPending = game.HasPendingAction,
            FinalView = game.CurrentView,
            Checks = _checks, ElapsedMilliseconds = _elapsed.ElapsedMilliseconds
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
