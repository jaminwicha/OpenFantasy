using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Actual native pack selection, wheel commitment and separate-process gift recovery.</summary>
internal sealed partial class MaterialGiftPlayProbe(
    FarmGame game, string reportPath, string genesisPath, string mode) : Node
{
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly List<string> _checks = [];
    private readonly List<object> _inputs = [];
    private readonly List<object> _observations = [];
    private string _stage = "opening";
    private string? _initial;
    private EntityId? _incoming;
    private EntityId? _outgoing;
    private MaterialProposalStatus? _recordedReply;
    private bool _observedNativeFocus;
    private bool _mouseEntered;
    private LocalWorldView View => game.CurrentView ?? throw new InvalidOperationException("No public world.");
    private FarmQuickWheel Wheel => game.GetNode<FarmQuickWheel>("FarmQuickWheel");
    private FarmInventoryScreen Pack => game.GetNode<FarmInventoryScreen>("FarmInventoryScreen");
    private FarmGiftPanel Gifts => Pack.GetNode<FarmGiftPanel>("%Gifts");
    private MaterialProposalStatus ReplyStatus => _recordedReply
        ?? (mode == "pointer" ? MaterialProposalStatus.Declined : MaterialProposalStatus.Accepted);

    public override void _Process(double delta)
    {
        if (DisplayServer.GetName() != "headless" && GetWindow().HasFocus()) _observedNativeFocus = true;
    }

    public override async void _Ready()
    {
        try
        {
            await Until(() => game.CurrentView?.MaterialProposals?.Preparation is not null && game.CurrentOffers.Count > 0,
                "Gift world did not open.", 30);
            game.InjectFixtureFocus(true);
            Require(game.IsLocallyPaused && !View.IsInEncounter, "Gift fixture must open paused in exploration.");
            _initial = PublicHash();
            Observe("opened");
            if (mode == "reopen")
            {
                using var prior = JsonDocument.Parse(File.ReadAllText(Output("active", "json")));
                Require(prior.RootElement.GetProperty("Status").GetString() == "passed"
                    && prior.RootElement.GetProperty("FinalHash").GetString() == _initial,
                    "Separate process did not restore the exact final public state.");
                _incoming = EntityId.Parse(prior.RootElement.GetProperty("IncomingProposalId").GetString());
                _outgoing = EntityId.Parse(prior.RootElement.GetProperty("OutgoingProposalId").GetString());
                _recordedReply = Enum.Parse<MaterialProposalStatus>(prior.RootElement.GetProperty("ReplyStatus").GetString()!);
                Require(Status(_incoming.Value) == ReplyStatus && Status(_outgoing.Value) == MaterialProposalStatus.Withdrawn,
                    "Reopened participant outcomes differ from the actual native journey.");
                await Delay(0.5);
                Require(PublicHash() == _initial && !game.IsWheelOpen && !game.IsInventoryOpen
                    && game.CurrentOffers.Where(pair => MaterialProposalActionOffers.IsGiftAction(pair.Key))
                        .All(pair => !pair.Value.CanExecute), "Reopening replayed a selection or changed the paused world.");
                _checks.Add("Separate process preserves exact paused public state and has no restored draft or selected offer.");
            }
            else await Exercise();
            await PauseAndSettle();
            Observe("final");
            await Capture(mode == "reopen" ? "reopen" : "final");
            Deadline();
            Report("passed", null);
            game.RequestClose();
        }
        catch (Exception error)
        {
            Report("failed", error.ToString());
            GD.PushError("Native gift probe failed: " + error);
            GetTree().Quit(1);
        }
    }

    private async Task Exercise()
    {
        using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(genesisPath)!, "metadata.json")));
        string rawLot = metadata.RootElement.GetProperty("rawLotId").GetString()!;
        string recipient = metadata.RootElement.GetProperty("recipientId").GetString()!;
        int hp = View.Progression!.Hp;
        _stage = "inspection";
        await OpenPack();
        Require(Gifts.Selection is { LotId: null, RecipientActorId: null, SelectedOffer: null, PreparedGift: null },
            "Opening preselected gift terms.");
        for (int index = 0; index < 12; index++)
        {
            Tap(Key.Tab);
            await Frame();
            var focused = GetViewport().GuiGetFocusOwner();
            Require(focused is not null && focused.IsVisibleInTree() && Pack.IsAncestorOf(focused),
                "Gift navigation escaped to a hidden control or the world.");
        }
        await Prepare(rawLot, recipient);
        string held = PublicHash();
        await Activate(Gifts.GetNode<Button>("%ReviewGifts"));
        await Until(() => game.IsWheelOpen && Wheel.Model?.PageName == "Gifts", "Pack did not open Gifts wheel.");
        Require(Wheel.Model!.Selected is null, "Pack review silently selected a wheel sector.");
        await SelectSector(FarmSemanticAction.ProposeMaterial);
        Tap(Key.Enter);
        Joy(JoyButton.A);
        await Delay(0.25);
        Require(game.IsWheelOpen && !game.HasPendingAction && PublicHash() == held,
            "Paused gift inspection committed an action.");
        await Capture("inspection");
        game.InjectFixtureFocus(false);
        await Until(() => !game.IsWheelOpen && !game.IsWindowFocused, "Focus loss retained a gift wheel.");
        Require(PublicHash() == held, "Focus loss changed the paused gift state.");
        game.InjectFixtureFocus(true);
        _checks.Add("Explicit stack, recipient and quantity preparation, paused confirmation and controlled focus loss publish no action.");

        _stage = "npc-offer";
        Tap(Key.Escape);
        await Until(() => View.MaterialProposals!.Offers.Any(row => !row.ViewerIsSender && row.Status == MaterialProposalStatus.Open),
            "Configured helper did not offer its own crafted medicine.");
        await PauseAndSettle();
        _incoming = View.MaterialProposals!.Offers.Single(row => !row.ViewerIsSender && row.Status == MaterialProposalStatus.Open).ProposalId;
        await OpenPack();
        await SelectOffer(_incoming.Value);
        await Activate(Gifts.GetNode<Button>("%GiftHistory"));
        await Until(() => Gifts.GetNode<Label>("%GiftOfferDetails").Text.Contains("Recorded: Open", StringComparison.Ordinal),
            "Explicit history inspection did not complete.");
        await Capture("pack");
        await ClosePack();
        Tap(Key.Escape);
        _stage = "reply-" + mode;
        await Commit(mode == "pointer" ? FarmSemanticAction.DeclineMaterial : FarmSemanticAction.AcceptMaterial);
        await Until(() => !game.HasPendingAction && Status(_incoming.Value) == ReplyStatus, "Selected incoming gift reply did not commit.");
        await PauseAndSettle();
        Require(View.Progression!.Hp == hp, "Gift reply healed the player or granted care permission.");
        Observe("replied");
        _checks.Add("Requested native wheel control commits one exact incoming gift reply without healing.");

        _stage = "create-" + mode;
        await OpenPack();
        await Prepare(rawLot, recipient);
        await ClosePack();
        Tap(Key.Escape);
        await Commit(FarmSemanticAction.ProposeMaterial);
        await Until(() => !game.HasPendingAction && View.MaterialProposals!.Offers.Any(row => row.ViewerIsSender
            && row.Status == MaterialProposalStatus.Open), "Exact outgoing gift was not created.");
        await PauseAndSettle();
        _outgoing = View.MaterialProposals!.Offers.Single(row => row.ViewerIsSender && row.Status == MaterialProposalStatus.Open).ProposalId;
        Observe("created");
        await OpenPack();
        await SelectOffer(_outgoing.Value);
        await ClosePack();
        Tap(Key.Escape);
        _stage = "withdraw-" + mode;
        await Commit(FarmSemanticAction.WithdrawMaterial);
        await Until(() => !game.HasPendingAction && Status(_outgoing.Value) == MaterialProposalStatus.Withdrawn,
            "Outgoing gift withdrawal did not commit.");
        await PauseAndSettle();
        await OpenPack();
        await SelectOffer(_outgoing.Value);
        await Activate(Gifts.GetNode<Button>("%GiftHistory"));
        await Until(() => Gifts.GetNode<Label>("%GiftOfferDetails").Text.Contains("Recorded: Withdrawn", StringComparison.Ordinal),
            "Recorded terminal outcome did not reach the pack.");
        await Capture("history");
        await ClosePack();
        Require(View.Progression!.Hp == hp, "Gifting changed health.");
        _checks.Add("Native creation and explicit withdrawal retain exact source/recipient; terminal history is inspectable.");
    }

    private MaterialProposalStatus? Status(EntityId id) => View.MaterialProposals!.Offers.FirstOrDefault(row => row.ProposalId == id)?.Status;

    private async Task Prepare(string lotId, string recipientId)
    {
        var preparation = View.MaterialProposals!.Preparation!;
        int lot = preparation.Lots.ToList().FindIndex(value => value.LotId.ToString() == lotId);
        int recipient = preparation.RecipientActorIds.ToList().FindIndex(value => value.ToString() == recipientId);
        Require(lot >= 0 && recipient >= 0, "The fixture's exact source or named recipient is unavailable.");
        await Choose(Gifts.GetNode<OptionButton>("%GiftLot"), lot + 1);
        await Choose(Gifts.GetNode<OptionButton>("%GiftRecipient"), recipient + 1);
        if (preparation.Lots[lot].Quantity > 1)
        {
            await Activate(Gifts.GetNode<Button>("%GiftMore"));
            Require(Gifts.Selection!.Quantity == 2, "Quantity increment failed.");
            await Activate(Gifts.GetNode<Button>("%GiftLess"));
        }
        await Activate(Gifts.GetNode<Button>("%PrepareGift"));
        await Until(() => Gifts.Selection?.PreparedGift is not null, "Explicit gift preparation failed.");
        Require(Gifts.Selection!.PreparedGift!.Terms.LotId.ToString() == lotId
            && Gifts.Selection.PreparedGift.Terms.RecipientActorId.ToString() == recipientId
            && Gifts.Selection.PreparedGift.Terms.Quantity == 1, "Preparation substituted a source, recipient or quantity.");
    }

    private async Task SelectOffer(EntityId id)
    {
        var rows = View.MaterialProposals!.Offers.OrderBy(value => value.ProposalId).ToArray();
        int index = Array.FindIndex(rows, value => value.ProposalId == id);
        Require(index >= 0, "Selected offer is absent.");
        await Choose(Gifts.GetNode<OptionButton>("%GiftOffer"), index + 1);
        Require(Gifts.Selection?.SelectedOffer?.ProposalId == id, "Offer dropdown selected a different proposal.");
        var control = Gifts.GetNode<OptionButton>("%GiftOffer");
        Require(control.AllowReselect, "Same-offer reselection is disabled.");
        int reselections = 0;
        void Selected(long _) => reselections++;
        control.ItemSelected += Selected;
        try { await Choose(control, index + 1); }
        finally { control.ItemSelected -= Selected; }
        Require(reselections == 1, $"Explicit same-offer reselection emitted {reselections} selection events.");
    }

    private async Task OpenPack()
    {
        Tap(Key.I);
        await Until(() => game.IsInventoryOpen, "Pack did not open.");
        await Activate(Pack.GetNode<Button>("%GiftToggle"));
        await Until(() => Gifts.IsVisibleInTree(), "Gift panel did not open.");
        await Frame();
    }

    private async Task ClosePack()
    {
        await Activate(Pack.GetNode<Button>("%ClosePack"));
        await Until(() => !game.IsInventoryOpen, "Pack did not close.");
    }

    private async Task Choose(OptionButton control, int index)
    {
        Require(control.IsVisibleInTree() && index < control.ItemCount, "Dropdown choice unavailable.");
        control.GrabFocus();
        Tap(Key.Enter);
        await Until(() => control.GetPopup().Visible, "Dropdown did not open for explicit input.");
        // PopupMenu uses its actual focused row; do not assume Home is a supported navigation key.
        for (int step = 0; control.GetPopup().GetFocusedItem() != index && step < control.ItemCount; step++)
        {
            Tap(Key.Down);
            await Frame();
        }
        Require(control.GetPopup().GetFocusedItem() == index, "Dropdown focus did not reach the selected row.");
        Tap(Key.Enter);
        await Until(() => !control.GetPopup().Visible && control.Selected == index, "Dropdown input did not select the exact row.");
        await Frame();
    }

    private async Task Commit(FarmSemanticAction action)
    {
        Tap(Key.V);
        await Until(() => game.IsWheelOpen, "Wheel did not open.");
        Require(Wheel.Model!.Selected is null, "Wheel opening preselected an action.");
        while (Wheel.Model.PageName != "Gifts")
        {
            if (mode == "controller") Joy(JoyButton.RightShoulder); else Tap(Key.Q);
            await Frame();
        }
        await SelectSector(action);
        Require(Wheel.Model.Selected!.Offer.CanExecute && Wheel.Model.Selected.Offer.Intent == game.CurrentOffers[action].Intent,
            "Captured gift action became unavailable or changed.");
        if (mode == "pointer") await Click(Wheel.GetNode<Button>("%ConfirmChoice"));
        else if (mode == "controller") Joy(JoyButton.A);
        else Tap(Key.Enter);
    }

    private async Task SelectSector(FarmSemanticAction action)
    {
        int index = Array.FindIndex(Wheel.Model!.Slots.ToArray(), slot => slot?.Action == action);
        Require(index >= 0, "Gift wheel sector absent.");
        if (mode == "controller") Joy(new[] { JoyButton.DpadUp, JoyButton.DpadRight, JoyButton.DpadDown, JoyButton.DpadLeft }[index]);
        else Tap(new[] { Key.Up, Key.Right, Key.Down, Key.Left }[index]);
        await Until(() => Wheel.Model?.Selected?.Action == action, "Gift wheel selection failed.");
    }

    private async Task Activate(Button button)
    {
        Require(button.IsVisibleInTree() && !button.Disabled, "Gift control is unavailable: " + button.Name);
        button.GrabFocus();
        await Frame();
        void Trace(InputEvent input) => _inputs.Add(new { Stage = _stage, Control = button.Name.ToString(),
            Event = input.AsText(), Pressed = input.IsPressed(), Accept = input.IsAction("ui_accept"), Focused = button.HasFocus() });
        button.GuiInput += Trace;
        try
        {
            if (mode == "pointer") await Click(button);
            else if (mode == "controller") Joy(JoyButton.A);
            else Tap(Key.Enter);
            await Frame();
        }
        finally { button.GuiInput -= Trace; }
    }

    private async Task PauseAndSettle()
    {
        if (game.IsWheelOpen) Tap(Key.Escape);
        if (!game.IsLocallyPaused) Tap(Key.Escape);
        await Until(() => game.IsLocallyPaused && !game.HasPendingAction, "Gift world did not settle paused.");
        await Delay(0.25);
        string held = PublicHash();
        await Delay(0.25);
        Require(PublicHash() == held, "Paused gift world changed.");
    }

    private string PublicHash() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(View))));
    private void Observe(string label) => _observations.Add(new { Label = label, View, game.IsLocallyPaused,
        game.HasPendingAction, game.IsInventoryOpen, game.IsWheelOpen });
    private void Tap(Key key)
    {
        _inputs.Add(new { Stage = _stage, Key = key.ToString() });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
        Input.FlushBufferedEvents();
    }
    private void Joy(JoyButton button)
    {
        _inputs.Add(new { Stage = _stage, Controller = button.ToString() });
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = true });
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = false });
        Input.FlushBufferedEvents();
    }
    private async Task Click(Button button)
    {
        Require(button.IsVisibleInTree() && !button.Disabled, "Pointer gift control is unavailable.");
        if (!_mouseEntered) { GetViewport().NotifyMouseEntered(); _mouseEntered = true; }
        Vector2 at = button.GetGlobalRect().GetCenter();
        _inputs.Add(new { Stage = _stage, PointerControl = button.Name.ToString(), X = at.X, Y = at.Y });
        GetViewport().PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at }, true);
        await Frame();
        GetViewport().PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        await Frame();
        GetViewport().PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, Pressed = false }, true);
    }
    private async Task Until(Func<bool> predicate, string failure, double seconds = 10)
    {
        var watch = Stopwatch.StartNew();
        while (true)
        {
            bool satisfied = predicate();
            Deadline();
            Require(watch.Elapsed.TotalSeconds < seconds, failure);
            if (satisfied) return;
            await Frame();
        }
    }
    private async Task Delay(double seconds)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed.TotalSeconds < seconds) await Frame();
    }
    private async Task Frame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Deadline();
    }
    private void Deadline() => Require(!game.HasFailed && _elapsed.Elapsed.TotalSeconds < 150,
        "Gift continuation failed or exceeded its 150-second bound at " + _stage);
    private async Task Capture(string stage)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        if (game.IsInventoryOpen)
        {
            Rect2 viewport = GetViewport().GetVisibleRect();
            Require(viewport.Encloses(Pack.GetNode<Button>("%ClosePack").GetGlobalRect())
                && viewport.Encloses(Gifts.GetNode<Button>("%ReviewGifts").GetGlobalRect()),
                "Gift pack header or review control is clipped outside the viewport.");
        }
        using Image image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(Output(stage, "png")) == Error.Ok, "Gift image could not be saved.");
    }
    private string Output(string stage, string extension) => Path.ChangeExtension(reportPath, null) + "." + stage + "." + extension;
    private void Report(string status, string? error) => File.WriteAllText(Output(mode == "reopen" ? "reopen" : "active",
        status == "passed" ? "json" : "failed.json"), JsonSerializer.Serialize(new
        {
            Kind = "native-gifts-v1", Mode = mode, Status = status, Error = error, Stage = _stage,
            InitialHash = _initial, FinalHash = game.CurrentView is null ? null : PublicHash(),
            IncomingProposalId = _incoming?.ToString(), OutgoingProposalId = _outgoing?.ToString(), ReplyStatus = ReplyStatus.ToString(),
            Checks = _checks, Inputs = _inputs, Observations = _observations, Display = DisplayServer.GetName(), FocusRequests = 0,
            Unfocusable = GetWindow().Unfocusable, NativeFocused = GetWindow().HasFocus(), ObservedNativeFocus = _observedNativeFocus,
            StartupNoFocus = ProjectSettings.GetSetting("display/window/size/no_focus").AsBool(),
            game.IsLocallyPaused, game.IsWindowFocused, DurationMilliseconds = _elapsed.ElapsedMilliseconds,
            Limit = "Forms use injected keyboard dropdown input with explicit Control focus. Named mode qualifies wheel commitment. Physical devices, OS focus, art and production gates remain open."
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
