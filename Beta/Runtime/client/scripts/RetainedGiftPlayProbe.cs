using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Care;
using OpenFantasy.Hosting.Simulation;
using static OpenFantasy.Client.RetainedGiftNativeDriver;

namespace OpenFantasy.Client;

/// <summary>
/// Native continuation of an explicitly transitioned retained world. The configured helper offers its
/// own dose; the player's own native offer is the step whose automatic cut carries that open gift; the
/// player accepts it through the Gifts wheel and later spends it with ordinary Care self-use. A separate
/// process reopens the paused result. Recorded boundaries are authenticated from the saved journal.
/// </summary>
internal sealed partial class RetainedGiftPlayProbe(FarmGame game, string reportPath, string metadataPath, string mode) : Node
{
    private const double OpeningSeconds = 30;
    private const double JourneySeconds = 150;
    private readonly List<string> _checks = [];
    private readonly List<object> _observations = [];
    private RetainedGiftNativeDriver _driver = null!;
    private string? _initial;
    private EntityId? _helperOffer;
    private EntityId? _playerOffer;
    private long _openingMilliseconds;
    private bool _observedNativeFocus;
    private LocalWorldView View => game.CurrentView ?? throw new InvalidOperationException("No public world.");
    private FarmQuickWheel Wheel => game.GetNode<FarmQuickWheel>("FarmQuickWheel");
    private FarmInventoryScreen Pack => game.GetNode<FarmInventoryScreen>("FarmInventoryScreen");
    private FarmGiftPanel Gifts => Pack.GetNode<FarmGiftPanel>("%Gifts");
    private Label HealthDelta => game.GetChildren().OfType<FarmCanvas>().Single()
        .GetNode<Label>("Actors/Actor_" + View.PlayerActorId + "/HealthDelta");

    public override void _Process(double delta)
    {
        if (DisplayServer.GetName() != "headless" && GetWindow().HasFocus()) _observedNativeFocus = true;
    }

    public override async void _Ready()
    {
        _driver = new RetainedGiftNativeDriver(this, game, JourneySeconds);
        try
        {
            using var metadata = JsonDocument.Parse(File.ReadAllText(metadataPath));
            JsonElement expected = metadata.RootElement;
            await _driver.Until(() => game.CurrentView?.MaterialProposals is not null && game.CurrentOffers.Count > 0,
                "Retained gift world did not open.", OpeningSeconds);
            _openingMilliseconds = _driver.ElapsedMilliseconds;
            game.InjectFixtureFocus(true);
            Require(game.IsLocallyPaused && !View.IsInEncounter && View.Care is null,
                "The retained gift world must open paused in exploration without pending aftermath care.");
            Require(View.PlayerActorId.ToString() == Text(expected, "playerId"), "The retained world opened another player.");
            _initial = PublicHash();
            Observe("opened");
            if (mode == "reopen") await Reopen();
            else await Journey(expected);
            await PauseAndSettle();
            Observe("final");
            await _driver.Capture(Output(mode == "reopen" ? "reopen" : "final", "png"));
            _driver.Deadline();
            Report("passed", null);
            game.RequestClose();
        }
        catch (Exception error)
        {
            Report("failed", error.ToString());
            GD.PushError("Native retained gift probe failed: " + error);
            GetTree().Quit(1);
        }
    }

    private async Task Journey(JsonElement expected)
    {
        EntityId helper = EntityId.Parse(Text(expected, "helperId"));
        int hp = expected.GetProperty("beforeHp").GetInt32();
        int mp = expected.GetProperty("beforeMp").GetInt32();
        Require(View.Progression!.Hp == hp && View.Progression.Mp == mp, "Prepared retained resources differ.");
        Require(View.MaterialProposals!.Offers.All(row => row.Status != MaterialProposalStatus.Open),
            "The prepared retained world must open with no open gift.");
        Require(View.Restorative!.PermissionOffers.All(permission => !permission.IsActive),
            "The prepared patient permission must already be withdrawn so helper care cannot stand in for own use.");

        _driver.Stage = "helper-offer";
        _driver.Tap(Key.Escape);
        await _driver.Until(() => Incoming(helper) is not null, "The configured helper did not offer its own dose.");
        await PauseAndSettle();
        MaterialProposalView offered = Incoming(helper)!;
        _helperOffer = offered.ProposalId;
        Require(offered.Terms.Quantity == 1 && View.Progression!.Hp == hp, "The helper's gift changed its terms or health.");
        Observe("helper-offer");
        _checks.Add("The running session's configured helper policy offers exactly one of its own doses; nothing heals.");

        _driver.Stage = "player-offer";
        await OpenPack();
        await Prepare(Text(expected, "playerLotId"), Text(expected, "playerLotKind"), helper);
        await _driver.Capture(Output("pack", "png"));
        await ClosePack();
        _driver.Tap(Key.Escape);
        await Commit(FarmSemanticAction.ProposeMaterial);
        await _driver.Until(() => !game.HasPendingAction && Outgoing() is { Status: MaterialProposalStatus.Declined },
            "The player's native offer was not created and answered.");
        await PauseAndSettle();
        _playerOffer = Outgoing()!.ProposalId;
        Require(Status(_helperOffer.Value) == MaterialProposalStatus.Open && View.Progression!.Hp == hp,
            "The helper's gift did not stay open across the player's offer.");
        Observe("player-offer");
        _checks.Add("A native player offer is created through the pack and Gifts wheel; the helper's policy declines it.");

        _driver.Stage = "accept";
        await OpenPack();
        await SelectOffer(_helperOffer.Value);
        await ClosePack();
        _driver.Tap(Key.Escape);
        await Commit(FarmSemanticAction.AcceptMaterial);
        await _driver.Until(() => !game.HasPendingAction && Status(_helperOffer.Value) == MaterialProposalStatus.Accepted,
            "The selected helper gift was not accepted.");
        await PauseAndSettle();
        Require(View.Progression!.Hp == hp && View.Progression.Mp == mp && !HealthDelta.Visible,
            "Accepting the gift restored health or spent magic.");
        Observe("accepted");
        _checks.Add("Native Gifts wheel acceptance commits the exact captured offer without healing.");

        _driver.Stage = "self-use";
        int expectedHp = expected.GetProperty("expectedHp").GetInt32();
        _driver.Tap(Key.Escape);
        await OpenCare();
        _driver.Tap(Key.Enter);
        await _driver.Until(() => !game.HasPendingAction && View.Progression!.Hp == expectedHp,
            "Ordinary self-use did not restore the earned injury.");
        // Pause at once: the recorded journal, not this frame, proves the exact committed use.
        _driver.Tap(Key.Escape);
        await _driver.Until(() => game.IsLocallyPaused, "The world did not pause after self-use.");
        string cue = "+" + expected.GetProperty("restoredHp").GetInt32();
        await _driver.Until(() => HealthDelta.Visible && HealthDelta.Text == cue, "Self-use did not reach its health cue.");
        await _driver.Capture(Output("restoration", "png"));
        Require(View.Progression!.Mp == mp && View.Care is null && !View.IsInEncounter,
            "Ordinary self-use spent magic or entered another control boundary.");
        Observe("restored");
        _checks.Add("Ordinary Care self-use through the wheel spends the received dose on the player's own injury.");
    }

    private async Task Reopen()
    {
        using var prior = JsonDocument.Parse(File.ReadAllText(Output("active", "json")));
        Require(prior.RootElement.GetProperty("Status").GetString() == "passed"
            && prior.RootElement.GetProperty("FinalHash").GetString() == _initial,
            "The separate process did not restore the exact final public state.");
        _helperOffer = EntityId.Parse(prior.RootElement.GetProperty("HelperProposalId").GetString());
        _playerOffer = EntityId.Parse(prior.RootElement.GetProperty("PlayerProposalId").GetString());
        Require(Status(_helperOffer.Value) == MaterialProposalStatus.Accepted
            && Status(_playerOffer.Value) == MaterialProposalStatus.Declined,
            "Reopened participant outcomes differ from the native journey.");
        await _driver.Delay(0.5);
        Require(PublicHash() == _initial && !game.IsWheelOpen && !game.IsInventoryOpen && game.CurrentTarget is null
            && !HealthDelta.Visible && !game.HasPendingAction
            && game.CurrentOffers.Where(pair => MaterialProposalActionOffers.IsGiftAction(pair.Key)).All(pair => !pair.Value.CanExecute),
            "Reopening replayed a selection or health cue or changed the paused world.");
        _checks.Add("A separate process restores the exact paused state with no selected action or health cue.");

        _driver.Stage = "history";
        await OpenPack();
        await SelectOffer(_helperOffer.Value);
        await Activate(Gifts.GetNode<Button>("%GiftHistory"));
        await _driver.Until(() => Gifts.GetNode<Label>("%GiftOfferDetails").Text.Contains("Recorded: Accepted", StringComparison.Ordinal),
            "Participant history of the carried gift did not complete.");
        await _driver.Capture(Output("history", "png"));
        await ClosePack();
        Require(PublicHash() == _initial, "Participant history inspection changed the paused world.");
        _checks.Add("Explicit participant history inspection reports the accepted carried gift without changing the world.");
    }

    private MaterialProposalView? Incoming(EntityId helper) => View.MaterialProposals!.Offers.SingleOrDefault(row =>
        !row.ViewerIsSender && row.Terms.SenderActorId == helper && row.Status == MaterialProposalStatus.Open);

    private MaterialProposalView? Outgoing() => View.MaterialProposals!.Offers.SingleOrDefault(row => row.ViewerIsSender);

    private MaterialProposalStatus? Status(EntityId id) =>
        View.MaterialProposals!.Offers.FirstOrDefault(row => row.ProposalId == id)?.Status;

    private async Task Prepare(string lotId, string lotKind, EntityId recipient)
    {
        var preparation = View.MaterialProposals!.Preparation
            ?? throw new InvalidOperationException("Gift preparation is unavailable.");
        var kind = Enum.Parse<ConsentedMaterialLotKind>(lotKind);
        int lot = preparation.Lots.ToList().FindIndex(value => value.LotId.ToString() == lotId && value.Kind == kind);
        int person = preparation.RecipientActorIds.ToList().IndexOf(recipient);
        Require(lot >= 0 && person >= 0, "The fixture's exact player lot or helper recipient is unavailable.");
        await Choose(Gifts.GetNode<OptionButton>("%GiftLot"), lot + 1);
        await Choose(Gifts.GetNode<OptionButton>("%GiftRecipient"), person + 1);
        await Activate(Gifts.GetNode<Button>("%PrepareGift"));
        await _driver.Until(() => Gifts.Selection?.PreparedGift is not null, "Explicit gift preparation failed.");
        var terms = Gifts.Selection!.PreparedGift!.Terms;
        Require(terms.LotId.ToString() == lotId && terms.RecipientActorId == recipient && terms.Quantity == 1,
            "Preparation substituted a lot, recipient or quantity.");
    }

    private async Task SelectOffer(EntityId id)
    {
        var rows = View.MaterialProposals!.Offers.OrderBy(value => value.ProposalId).ToArray();
        int index = Array.FindIndex(rows, value => value.ProposalId == id);
        Require(index >= 0, "The selected gift is absent.");
        await Choose(Gifts.GetNode<OptionButton>("%GiftOffer"), index + 1);
        Require(Gifts.Selection?.SelectedOffer?.ProposalId == id, "The offer list selected another gift.");
    }

    private async Task OpenPack()
    {
        _driver.Tap(Key.I);
        await _driver.Until(() => game.IsInventoryOpen, "Pack did not open.");
        await Activate(Pack.GetNode<Button>("%GiftToggle"));
        await _driver.Until(() => Gifts.IsVisibleInTree(), "Gift panel did not open.");
        await _driver.Frame();
    }

    private async Task ClosePack()
    {
        await Activate(Pack.GetNode<Button>("%ClosePack"));
        await _driver.Until(() => !game.IsInventoryOpen, "Pack did not close.");
    }

    private async Task Choose(OptionButton control, int index)
    {
        Require(control.IsVisibleInTree() && index < control.ItemCount, "Dropdown choice unavailable.");
        control.GrabFocus();
        _driver.Tap(Key.Enter);
        await _driver.Until(() => control.GetPopup().Visible, "Dropdown did not open for explicit input.");
        for (int step = 0; control.GetPopup().GetFocusedItem() != index && step < control.ItemCount; step++)
        {
            _driver.Tap(Key.Down);
            await _driver.Frame();
        }
        Require(control.GetPopup().GetFocusedItem() == index, "Dropdown focus did not reach the selected row.");
        _driver.Tap(Key.Enter);
        await _driver.Until(() => !control.GetPopup().Visible && control.Selected == index, "Dropdown input did not select the exact row.");
        await _driver.Frame();
    }

    private async Task Activate(Button button)
    {
        Require(button.IsVisibleInTree() && !button.Disabled, "Gift control is unavailable: " + button.Name);
        button.GrabFocus();
        await _driver.Frame();
        _driver.Tap(Key.Enter);
        await _driver.Frame();
    }

    /// <summary>Opens the wheel, pages to Gifts and confirms the exact presented action by keyboard.</summary>
    private async Task Commit(FarmSemanticAction action)
    {
        _driver.Tap(Key.V);
        await _driver.Until(() => game.IsWheelOpen && Wheel.Model is not null, "Wheel did not open.");
        Require(Wheel.Model!.Selected is null, "Opening the wheel preselected an action.");
        for (int page = 0; Wheel.Model.PageName != "Gifts"; page++)
        {
            Require(page < Wheel.Model.PageCount, "The Gifts page is absent.");
            _driver.Tap(Key.Q);
            await _driver.Frame();
        }
        await SelectSector(action);
        Require(Wheel.Model.Selected!.Offer.CanExecute && Wheel.Model.Selected.Offer.Intent == game.CurrentOffers[action].Intent,
            "The captured gift action became unavailable or changed.");
        _driver.Tap(Key.Enter);
    }

    private async Task OpenCare()
    {
        _driver.Tap(Key.V);
        await _driver.Until(() => game.IsWheelOpen && Wheel.Model is not null, "Wheel did not open.");
        Require(Wheel.Model!.PageName == "Actions" && Wheel.Model.Selected is null, "Opening silently preselected an action.");
        _driver.Tap(Key.Q);
        await _driver.Until(() => Wheel.Model?.PageName == "Care", "Care page navigation failed.");
        await SelectSector(FarmSemanticAction.UseRestorative);
        var use = Wheel.Model!.Selected!.Offer.Intent as UseRestorativeItemIntent;
        Require(Wheel.Model.Selected.Offer.CanExecute && use is { PermissionId: null } && use.RecipientActorId == View.PlayerActorId
            && Wheel.Model.Selected.Offer.Intent == game.CurrentOffers[FarmSemanticAction.UseRestorative].Intent,
            "The Care page did not capture the player's own restorative use.");
    }

    private async Task SelectSector(FarmSemanticAction action)
    {
        int index = Array.FindIndex(Wheel.Model!.Slots.ToArray(), slot => slot?.Action == action);
        Require(index >= 0, "Wheel sector absent: " + action);
        _driver.Tap(new[] { Key.Up, Key.Right, Key.Down, Key.Left }[index]);
        await _driver.Until(() => Wheel.Model?.Selected?.Action == action, "Wheel selection failed: " + action);
    }

    private async Task PauseAndSettle()
    {
        if (game.IsWheelOpen) _driver.Tap(Key.Escape);
        if (!game.IsLocallyPaused) _driver.Tap(Key.Escape);
        await _driver.Until(() => game.IsLocallyPaused && !game.HasPendingAction, "Retained gift world did not settle paused.");
        await _driver.Delay(0.25);
        string held = PublicHash();
        await _driver.Delay(0.25);
        Require(PublicHash() == held, "The paused retained world changed.");
    }

    private string PublicHash() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(View))));

    private void Observe(string label) => _observations.Add(new { Label = label, View, game.IsLocallyPaused,
        game.IsWindowFocused, game.HasPendingAction, game.IsInventoryOpen, game.IsWheelOpen,
        HealthCue = new { HealthDelta.Text, HealthDelta.Visible } });

    private string Output(string stage, string extension) => Path.ChangeExtension(reportPath, null) + "." + stage + "." + extension;

    private static string Text(JsonElement value, string name) => value.GetProperty(name).GetString()
        ?? throw new InvalidDataException("Retained gift metadata requires " + name + ".");

    private void Report(string status, string? error) => File.WriteAllText(Output(mode == "reopen" ? "reopen" : "active",
        status == "passed" ? "json" : "failed.json"), JsonSerializer.Serialize(new
        {
            Kind = "native-retained-gifts-v1", Mode = mode, Status = status, Error = error, Stage = _driver.Stage,
            InitialHash = _initial, FinalHash = game.CurrentView is null ? null : PublicHash(),
            HelperProposalId = _helperOffer?.ToString(), PlayerProposalId = _playerOffer?.ToString(),
            Checks = _checks, Inputs = _driver.Inputs, Waits = _driver.Waits, Observations = _observations,
            Display = DisplayServer.GetName(), FocusRequests = 0, Unfocusable = GetWindow().Unfocusable,
            NativeFocused = GetWindow().HasFocus(), ObservedNativeFocus = _observedNativeFocus,
            StartupNoFocus = ProjectSettings.GetSetting("display/window/size/no_focus").AsBool(),
            game.IsLocallyPaused, game.IsWindowFocused, OpeningMilliseconds = _openingMilliseconds,
            DurationMilliseconds = _driver.ElapsedMilliseconds,
            Limit = "Managed labeled preparation precedes injected keyboard input with explicit Control focus. The saved journal, not this report, authenticates custody, the cut and the use. Physical devices, OS focus, art and production gates remain open."
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
}
