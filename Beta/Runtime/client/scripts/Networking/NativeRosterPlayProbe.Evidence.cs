using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking.ObservationViews;

namespace OpenFantasy.Client.Networking;

internal sealed partial class NativeRosterPlayProbe
{
    private async Task Capture(double seconds = 10, string suffix = "")
    {
        MarketProbeDriver.Require(DisplayServer.GetName() != "headless", "The capture request requires the rendered roster process.");
        bool drawn = false;
        void OnDraw() => drawn = true;
        RenderingServer.FramePostDraw += OnDraw;
        try { await _driver.Until(() => drawn, "The roster frame did not render.", seconds); }
        finally { RenderingServer.FramePostDraw -= OnDraw; }
        using Image image = GetViewport().GetTexture().GetImage();
        MarketProbeDriver.Require(image.SavePng(Path.Combine(_directory, $"{_sequence:D4}{suffix}.png")) == Error.Ok,
            "The rendered roster frame could not be saved.");
    }

    private void Respond(string status, string? failure)
    {
        CapturePermissionSubmission();
        Directory.CreateDirectory(_directory);
        WriteAtomic(Path.Combine(_directory, $"{_sequence:D4}.response.json"), new
        {
            Sequence = _sequence, Status = status, Failure = failure, Stage = _stage,
            View = _game.CurrentView is { } view ? Convert.ToBase64String(PlayerViewCodec.Encode(view)) : null,
            LocallyPaused = _game.IsLocallyPaused, RunState = _game.ParticipantRunState.ToString(),
            ControlState = _game.ParticipantControl.State.ToString(),
            ControlPort = _game.ParticipantControl.PortId.ToString(),
            ControlGeneration = _game.ParticipantControl.Generation,
            NativeFocused = GetWindow().HasFocus(), ObservedNativeFocus = _nativeFocus,
            InitialFocusGuard = _initialFocusGuard, Submissions = Submissions(),
            Selection = SelectionEvidence(), ControlBoard = ControlBoardEvidence(),
            ControlOperationPending = _game.ControlOperationPending, ControlOperationStatus = _game.LastControlOperationStatus,
            PermissionSubmissions = PermissionSubmissions(), SelectionCaptures = _selectionCaptures
        });
    }

    private object[] Submissions() => _submissions.Select(value => (object)new
    {
        Command = Convert.ToHexString(value.CanonicalCommandBytes()), IsRetry = value.IsRetry,
        Receipt = value.Receipt.IsCompletedSuccessfully
            ? Convert.ToHexString(WorldReceiptCodecV1.Encode(value.Receipt.Result)) : null
    }).ToArray();

    private void WriteReport(string status, string? failure) => WriteAtomic(Path.Combine(_directory, "report.json"), new
    {
        Kind = "native-four-player-roster-v1", Status = status, Failure = failure, Stage = _stage,
        World = _game.CurrentView?.WorldId.ToString(), Actor = _game.CurrentView?.PlayerActorId.ToString(),
        Inputs = _driver.Inputs, Submissions = Submissions(), PermissionSubmissions = PermissionSubmissions(),
        Selection = SelectionEvidence(), ControlBoard = ControlBoardEvidence(), SelectionCaptures = _selectionCaptures,
        ElapsedMilliseconds = _driver.ElapsedMilliseconds,
        NativeFocused = GetWindow().HasFocus(), ObservedNativeFocus = _nativeFocus,
        InitialFocusGuard = _initialFocusGuard, Unfocusable = GetWindow().Unfocusable, Display = DisplayServer.GetName()
    });

    private object? SelectionEvidence()
    {
        if (_game.CurrentSelection is not { } selected) return null;
        var control = selected.ControlledView;
        var actors = control?.Actors ?? selected.HomeView.Actors;
        var encounter = control is null ? selected.HomeView.Encounter : control.Combat?.Encounter;
        var lifecycle = control is null ? selected.HomeView.EncounterLifecycle : control.Combat?.Lifecycle;
        var actions = control is null ? selected.HomeView.EncounterActions : control.Combat?.Actions;
        var combatants = control is null ? selected.HomeView.EncounterActors : control.Combat?.Actors;
        var selection = selected.Selection;
        return new
        {
            World = selected.HomeView.WorldId.ToString(), Branch = selected.HomeView.BranchId.ToString(),
            Port = selection.PortId.ToString(), HomeActor = selection.HomeActorId.ToString(), Actor = selection.ActorId.ToString(),
            selection.Generation, Witness = WitnessBytes(selection.ControlWitness),
            Capabilities = selection.ControlWitness?.Capabilities.ToString(), Ready = selected.IsReady,
            PresentedGeneration = _game.PresentedSelection?.Selection.Generation,
            PresentedMatches = _game.PresentedSelection?.Selection == selection,
            Feet = actors.Single(actor => actor.ActorId == selection.ActorId).Feet,
            HomeFeet = selected.HomeView.Actors.Single(actor => actor.ActorId == selection.HomeActorId).Feet,
            Phase = lifecycle?.Phase.ToString(), EncounterId = actions?.EncounterId.ToString(),
            TimingRevision = actions?.TimingRevision, CanWait = actions?.CanWait ?? false,
            CanQueue = actions?.AbilityOffers.Any(offer => offer.CanQueue) ?? false,
            PlayerIsDue = encounter?.PlayerIsDue ?? false,
            Hp = control is null ? selected.HomeView.Progression?.Hp : control.Combat?.Resources?.Hp,
            Mp = control is null ? selected.HomeView.Progression?.Mp : control.Combat?.Resources?.Mp,
            HomeCanWait = selected.HomeView.EncounterActions?.CanWait ?? false,
            HomeTimingRevision = selected.HomeView.EncounterActions?.TimingRevision,
            EncounterActors = combatants?.Select(actor => new { Actor = actor.ActorId.ToString(), Hp = actor.CurrentHp }).ToArray() ?? []
        };
    }

    private object? ControlBoardEvidence()
    {
        if (_game.GetNodeOrNull<FarmControlPanel>("FarmControlPanel")?.CurrentBoard is not { } board) return null;
        return new
        {
            World = board.WorldId.ToString(), Branch = board.BranchId.ToString(), HomeActor = board.HomeActorId.ToString(),
            board.WorldRevision, board.PermissionRevision, HeldWitness = WitnessBytes(board.HeldWitness),
            Slots = board.Slots.Select(slot => new
            {
                Owner = slot.OwnerActorId.ToString(), Grantee = slot.GranteeActorId.ToString(), slot.Revision,
                Status = slot.Status.ToString(), GrantCommandId = slot.GrantCommandId?.ToString(),
                AcceptanceCommandId = slot.AcceptanceCommandId?.ToString(), Capabilities = slot.Capabilities?.ToString(),
                TermsHash = slot.TermsHash?.ToString(), AcquisitionAvailable = slot.Acquisition is not null
            }).ToArray()
        };
    }

    private object[] PermissionSubmissions() => _permissionSubmissions.Select(value => (object)new
    {
        Command = Convert.ToHexString(WorldActionCodec.Encode(value.Command)),
        Receipt = value.Receipt.IsCompletedSuccessfully ? Convert.ToHexString(WorldReceiptCodecV1.Encode(value.Receipt.Result)) : null
    }).ToArray();

    private static string? WitnessBytes(WorldControlWitness? witness) => witness is null
        ? null : Convert.ToBase64String(WorldControlWitnessCodecV1.Encode(witness));

    private static void WriteAtomic(string path, object value)
    {
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, overwrite: true);
    }
}
