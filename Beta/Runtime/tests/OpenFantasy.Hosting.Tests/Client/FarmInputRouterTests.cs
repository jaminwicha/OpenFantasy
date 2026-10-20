using OpenFantasy.Client;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.Client;

public sealed class FarmInputRouterTests
{
    private const long Step = WorldMovementInput.DisplacementPerStep;
    private static readonly FarmInputSituation World = new(SessionOpen: true, ReadingHold: false, ActionPending: false);
    private static readonly FarmInputSituation Dialogue = World with { ReadingHold = true };
    private static readonly FarmInputSituation Pending = World with { ActionPending = true };
    private static readonly FarmInputSituation Opening = new(SessionOpen: false, ReadingHold: false, ActionPending: false);
    private static readonly FarmDirectionalInput Right = new(0, 1, 0, 0);
    private static readonly FarmDirectionalInput Up = new(0, 0, 1, 0);

    [Fact]
    public void HeldDirectionBecomesOneCardinalHostSample()
    {
        var router = new FarmInputRouter();

        Assert.Equal(new FarmMovementIntent(Step, 0), router.Movement(World, Right));
        Assert.Equal(new FarmMovementIntent(-Step, 0), router.Movement(World, new FarmDirectionalInput(1, 0, 0, 0)));
        Assert.Equal(new FarmMovementIntent(0, -Step), router.Movement(World, Up));
        Assert.Equal(new FarmMovementIntent(0, Step), router.Movement(World, new FarmDirectionalInput(0, 0, 0, 1)));
        Assert.Equal(new FarmMovementIntent(Step, 0), router.Movement(World, new FarmDirectionalInput(0, 0.9f, 0.4f, 0)));
        Assert.Equal(new FarmMovementIntent(0, -Step), router.Movement(World, new FarmDirectionalInput(0, 1, 1, 0)));
        Assert.True(router.Movement(World, FarmDirectionalInput.Neutral).IsNeutral);
    }

    [Fact]
    public void OpposingDirectionsCancelInsteadOfChoosingOne()
    {
        var router = new FarmInputRouter();

        Assert.True(router.Movement(World, new FarmDirectionalInput(1, 1, 0, 0)).IsNeutral);
        Assert.True(router.Movement(World, new FarmDirectionalInput(0, 0, 1, 1)).IsNeutral);
        Assert.Equal(new FarmMovementIntent(0, -Step), router.Movement(World, new FarmDirectionalInput(1, 1, 1, 0)));
        Assert.Equal(new FarmMovementIntent(0, Step), router.Movement(World, new FarmDirectionalInput(0, 0.5f, 0.4f, 1)));
    }

    [Fact]
    public void ReadingHoldBlocksMovementAndHeldKeyStaysNeutralUntilReleased()
    {
        var router = new FarmInputRouter();
        Assert.Equal(new FarmMovementIntent(Step, 0), router.Movement(World, Right));

        Assert.Equal(FarmInputLayer.Dialogue, router.Layer(Dialogue));
        Assert.True(router.Movement(Dialogue, Right).IsNeutral);

        // The conversation closed while the key was still down: nothing moves until it is let go.
        Assert.True(router.Movement(World, Right).IsNeutral);
        Assert.True(router.Movement(World, new FarmDirectionalInput(0, 1, 1, 0)).IsNeutral);
        Assert.True(router.Movement(World, FarmDirectionalInput.Neutral).IsNeutral);
        Assert.Equal(new FarmMovementIntent(Step, 0), router.Movement(World, Right));
    }

    [Fact]
    public void FreshDirectionAfterTheBlockMovesImmediately()
    {
        var router = new FarmInputRouter();
        Assert.Equal(new FarmMovementIntent(Step, 0), router.Movement(World, Right));
        Assert.True(router.Movement(Dialogue, Right).IsNeutral);
        Assert.True(router.Movement(Dialogue, FarmDirectionalInput.Neutral).IsNeutral);

        // Released during the hold, pressed again after it: a deliberate new command.
        Assert.Equal(new FarmMovementIntent(0, -Step), router.Movement(World, Up));
    }

    [Fact]
    public void PauseBlocksWorldInputAndOnlyPauseOrCancelResumes()
    {
        var router = new FarmInputRouter();
        Assert.True(router.WorldShouldRun);

        Assert.Equal(FarmSemanticAction.TogglePause,
            router.Route(World, FarmInputSignal.Press(FarmInputActions.Pause)).Action);
        Assert.True(router.TogglePause());
        Assert.True(router.Paused);
        Assert.False(router.WorldShouldRun);
        Assert.Equal(FarmInputLayer.Paused, router.Layer(World));
        Assert.True(router.Movement(World, Right).IsNeutral);

        Assert.Equal(new FarmInputDecision(Consumed: true, FarmSemanticAction.None),
            router.Route(World, FarmInputSignal.Press(FarmInputActions.Interact)));
        Assert.False(router.Permits(World, FarmSemanticAction.Gather));
        Assert.False(router.Permits(Dialogue, FarmSemanticAction.CloseDialogue));

        Assert.Equal(FarmSemanticAction.TogglePause,
            router.Route(Dialogue, FarmInputSignal.Press(FarmInputActions.Cancel, FarmInputActions.Craft)).Action);
        Assert.True(router.TogglePause());
        Assert.True(router.WorldShouldRun);
    }

    [Fact]
    public void EscapeClosesDialogueWhileExplicitHudPauseStaysReachable()
    {
        var router = new FarmInputRouter();

        Assert.Equal(FarmSemanticAction.CloseDialogue,
            router.Route(Dialogue, FarmInputSignal.Press(FarmInputActions.Pause)).Action);
        Assert.True(router.Permits(Dialogue, FarmSemanticAction.TogglePause));
    }

    [Fact]
    public void FocusLossBlocksWorldInputAndReturnRequiresNeutralInput()
    {
        var router = new FarmInputRouter();
        Assert.Equal(new FarmMovementIntent(Step, 0), router.Movement(World, Right));

        Assert.True(router.SetFocused(false));
        Assert.False(router.WorldShouldRun);
        Assert.Equal(FarmInputLayer.Unfocused, router.Layer(World));
        Assert.True(router.Movement(World, Right).IsNeutral);
        Assert.Equal(FarmSemanticAction.None, router.Route(World, FarmInputSignal.Press(FarmInputActions.Gather)).Action);
        Assert.Equal(FarmSemanticAction.None, router.Route(World, FarmInputSignal.Press(FarmInputActions.Pause)).Action);

        Assert.True(router.SetFocused(true));
        Assert.True(router.WorldShouldRun);
        Assert.True(router.Movement(World, Right).IsNeutral);
        Assert.True(router.Movement(World, FarmDirectionalInput.Neutral).IsNeutral);
        Assert.Equal(new FarmMovementIntent(Step, 0), router.Movement(World, Right));
    }

    [Fact]
    public void CancelUnwindsTheTopLayerAndCraftsOnlyInOrdinaryPlay()
    {
        var router = new FarmInputRouter();
        FarmInputSignal controllerB = FarmInputSignal.Press(FarmInputActions.Cancel, FarmInputActions.Craft);

        Assert.Equal(FarmSemanticAction.Craft, router.Route(World, controllerB).Action);
        Assert.True(router.Route(World, FarmInputSignal.Release(FarmInputActions.Cancel, FarmInputActions.Craft)).Consumed);

        Assert.Equal(FarmSemanticAction.CloseDialogue, router.Route(Dialogue, controllerB).Action);
        Assert.True(router.Route(Dialogue, FarmInputSignal.Release(FarmInputActions.Cancel, FarmInputActions.Craft)).Consumed);

        Assert.Equal(FarmSemanticAction.None, router.Route(World, FarmInputSignal.Press(FarmInputActions.Cancel)).Action);
        Assert.Equal(FarmSemanticAction.TogglePause, router.Route(World, FarmInputSignal.Press(FarmInputActions.Pause)).Action);
    }

    [Fact]
    public void ClosingPressDoesNotLeakItsRepeatOrReleaseIntoTheWorld()
    {
        var router = new FarmInputRouter();

        FarmInputDecision closing = router.Route(Dialogue, FarmInputSignal.Press(FarmInputActions.Interact));
        Assert.Equal(new FarmInputDecision(Consumed: true, FarmSemanticAction.CloseDialogue), closing);

        // The conversation closes while the key is still held: repeats and the release are swallowed.
        Assert.Equal(new FarmInputDecision(Consumed: true, FarmSemanticAction.None),
            router.Route(Dialogue, FarmInputSignal.Repeat(FarmInputActions.Interact)));
        Assert.Equal(new FarmInputDecision(Consumed: true, FarmSemanticAction.None),
            router.Route(World, FarmInputSignal.Repeat(FarmInputActions.Interact)));
        Assert.Equal(new FarmInputDecision(Consumed: true, FarmSemanticAction.None),
            router.Route(World, FarmInputSignal.Press(FarmInputActions.Interact)));
        Assert.Equal(new FarmInputDecision(Consumed: true, FarmSemanticAction.None),
            router.Route(World, FarmInputSignal.Release(FarmInputActions.Interact)));

        Assert.Equal(FarmSemanticAction.Speak, router.Route(World, FarmInputSignal.Press(FarmInputActions.Interact)).Action);
    }

    [Fact]
    public void KeyAutoRepeatNeverDuplicatesAnAction()
    {
        var router = new FarmInputRouter();

        Assert.Equal(FarmSemanticAction.Gather, router.Route(World, FarmInputSignal.Press(FarmInputActions.Gather)).Action);
        for (int repeat = 0; repeat < 3; repeat++)
        {
            Assert.Equal(new FarmInputDecision(Consumed: true, FarmSemanticAction.None),
                router.Route(World, FarmInputSignal.Repeat(FarmInputActions.Gather)));
        }
        Assert.True(router.Route(World, FarmInputSignal.Release(FarmInputActions.Gather)).Consumed);
        Assert.Equal(FarmSemanticAction.Gather, router.Route(World, FarmInputSignal.Press(FarmInputActions.Gather)).Action);
    }

    [Fact]
    public void DialogueAllowsItsOwnActionsAndBlocksWorldActions()
    {
        var router = new FarmInputRouter();

        Assert.Equal(FarmSemanticAction.Give, router.Route(Dialogue, FarmInputSignal.Press(FarmInputActions.Give)).Action);
        Assert.Equal(FarmSemanticAction.CloseDialogue, router.Route(Dialogue, FarmInputSignal.Press(FarmInputActions.Interact)).Action);
        Assert.Equal(new FarmInputDecision(Consumed: true, FarmSemanticAction.None),
            router.Route(Dialogue, FarmInputSignal.Press(FarmInputActions.Gather)));
        Assert.Equal(new FarmInputDecision(Consumed: true, FarmSemanticAction.None),
            router.Route(Dialogue, FarmInputSignal.Press(FarmInputActions.Craft)));

        Assert.True(router.Permits(Dialogue, FarmSemanticAction.Give));
        Assert.True(router.Permits(Dialogue, FarmSemanticAction.CloseDialogue));
        Assert.False(router.Permits(Dialogue, FarmSemanticAction.Speak));
        Assert.False(router.Permits(Dialogue, FarmSemanticAction.Gather));
        Assert.False(router.Permits(Dialogue, FarmSemanticAction.Craft));
        Assert.False(router.Permits(World, FarmSemanticAction.CloseDialogue));
        Assert.True(router.Permits(World, FarmSemanticAction.Speak));
    }

    [Fact]
    public void PendingActionBlocksNewSubmissionsButNotPauseOrMovement()
    {
        var router = new FarmInputRouter();

        Assert.Equal(new FarmInputDecision(Consumed: true, FarmSemanticAction.None),
            router.Route(Pending, FarmInputSignal.Press(FarmInputActions.Interact)));
        Assert.False(router.Permits(Pending, FarmSemanticAction.Give));
        Assert.False(router.Permits(Pending with { ReadingHold = true }, FarmSemanticAction.CloseDialogue));
        Assert.Equal(FarmSemanticAction.TogglePause, router.Route(Pending, FarmInputSignal.Press(FarmInputActions.Pause)).Action);
        Assert.Equal(new FarmMovementIntent(Step, 0), router.Movement(Pending, Right));
    }

    [Fact]
    public void OpeningSessionAcceptsOnlyPauseAndUnavailableFarmAcceptsNothing()
    {
        var router = new FarmInputRouter();

        Assert.Equal(FarmInputLayer.Opening, router.Layer(Opening));
        Assert.True(router.Movement(Opening, Right).IsNeutral);
        Assert.Equal(new FarmInputDecision(Consumed: true, FarmSemanticAction.None),
            router.Route(Opening, FarmInputSignal.Press(FarmInputActions.Interact)));
        Assert.Equal(FarmSemanticAction.TogglePause, router.Route(Opening, FarmInputSignal.Press(FarmInputActions.Pause)).Action);

        router.MarkUnavailable();
        Assert.Equal(FarmInputLayer.Unavailable, router.Layer(World));
        Assert.False(router.WorldShouldRun);
        Assert.False(router.TogglePause());
        Assert.False(router.SetFocused(true));
        Assert.Equal(new FarmInputDecision(Consumed: true, FarmSemanticAction.None),
            router.Route(World, FarmInputSignal.Press(FarmInputActions.Pause)));
        Assert.False(router.Permits(World, FarmSemanticAction.TogglePause));
        Assert.True(router.Movement(World, Right).IsNeutral);
    }

    [Fact]
    public void StartingPausedHoldsTheWorldUntilResumed()
    {
        var router = new FarmInputRouter(paused: true);

        Assert.True(router.Paused);
        Assert.False(router.WorldShouldRun);
        Assert.True(router.Movement(World, Right).IsNeutral);
        Assert.True(router.TogglePause());
        Assert.True(router.WorldShouldRun);
    }

    [Fact]
    public void SettingsOpenFromPlayPauseConversationAndOpeningAndOwnInputWithoutChangingTiming()
    {
        foreach (FarmInputSituation situation in new[] { World, Dialogue, Opening })
        {
            var router = new FarmInputRouter();
            Assert.Equal(FarmSemanticAction.OpenSettings, router.Route(situation, FarmInputSignal.Press(FarmInputActions.Settings)).Action);
            Assert.True(router.Permits(situation, FarmSemanticAction.OpenSettings));
        }
        var paused = new FarmInputRouter(paused: true);
        Assert.True(paused.Permits(World, FarmSemanticAction.OpenSettings));

        var open = new FarmInputRouter();
        Assert.True(open.WorldShouldRun);
        open.SetSettingsOpen(true);
        Assert.Equal(FarmInputLayer.Settings, open.Layer(World));
        // Settings never pause or stop the world; they only own input while open.
        Assert.True(open.WorldShouldRun);
        Assert.True(open.Movement(World, Right).IsNeutral);
        Assert.False(open.Permits(World, FarmSemanticAction.TogglePause));
        Assert.False(open.Permits(World, FarmSemanticAction.Speak));
        Assert.False(open.Permits(World, FarmSemanticAction.OpenSettings));
        Assert.True(open.Permits(World, FarmSemanticAction.CloseSettings));
        Assert.Equal(FarmSemanticAction.CloseSettings, open.Route(World, FarmInputSignal.Press(FarmInputActions.Cancel)).Action);
    }

    [Fact]
    public void SettingsCloseOnTheirOwnKeyOrPauseAndOtherLayersCannotCloseThem()
    {
        foreach (string key in new[] { FarmInputActions.Settings, FarmInputActions.Pause, FarmInputActions.Cancel })
        {
            var router = new FarmInputRouter();
            router.SetSettingsOpen(true);
            Assert.Equal(FarmSemanticAction.CloseSettings, router.Route(World, FarmInputSignal.Press(key)).Action);
        }
        var history = new FarmInputRouter();
        history.SetHistoryOpen(true);
        Assert.False(history.Permits(World, FarmSemanticAction.CloseSettings));
        Assert.False(history.Permits(World, FarmSemanticAction.OpenSettings));
        history.SetSettingsOpen(true);
        // Settings sit above other panels until closed.
        Assert.Equal(FarmInputLayer.Settings, history.Layer(World));
    }

    [Fact]
    public void UnroutedEventsAndUntrackedReleasesPropagate()
    {
        var router = new FarmInputRouter();

        Assert.Equal(new FarmInputDecision(Consumed: false, FarmSemanticAction.None),
            router.Route(World, new FarmInputSignal(Array.Empty<string>(), Pressed: true, Echo: false)));
        Assert.Equal(new FarmInputDecision(Consumed: false, FarmSemanticAction.None),
            router.Route(World, FarmInputSignal.Release(FarmInputActions.Interact)));
    }

    [Fact]
    public void BoundaryCapturesOpposingKeysWithoutAnInterveningPhysicsSample()
    {
        var router = new FarmInputRouter(paused: true);
        router.CaptureBoundary(World, new FarmDirectionalInput(1, 1, 0, 0));
        router.TogglePause();

        Assert.True(router.Movement(World, Right).IsNeutral);
        Assert.True(router.Movement(World, FarmDirectionalInput.Neutral).IsNeutral);
        Assert.Equal(new FarmMovementIntent(Step, 0), router.Movement(World, Right));
    }

    [Fact]
    public void BoundaryCapturesDirectionsPressedDuringBriefFocusLoss()
    {
        var router = new FarmInputRouter();
        router.SetFocused(false);
        router.CaptureBoundary(World, Right);
        router.SetFocused(true);

        Assert.True(router.Movement(World, Right).IsNeutral);
        Assert.True(router.Movement(World, FarmDirectionalInput.Neutral).IsNeutral);
        Assert.Equal(new FarmMovementIntent(Step, 0), router.Movement(World, Right));
    }

    [Fact]
    public void LostFocusOverridesPauseAndBlocksHudAsWellAsDeviceRequests()
    {
        var router = new FarmInputRouter(paused: true);
        router.SetFocused(false);
        Assert.Equal(FarmInputLayer.Unfocused, router.Layer(World));
        Assert.False(router.Permits(World, FarmSemanticAction.TogglePause));
        Assert.Equal(FarmSemanticAction.None,
            router.Route(World, FarmInputSignal.Press(FarmInputActions.Cancel, FarmInputActions.Craft)).Action);
    }

    [Fact]
    public void NativeDeadzoneIntentDoesNotEraseTheHeldButtonMask()
    {
        var router = new FarmInputRouter(paused: true);
        var opposed = new FarmDirectionalInput(1, 1, 0, 0)
        {
            ResolvedIntent = FarmMovementIntent.Neutral
        };
        router.CaptureBoundary(World, opposed);
        router.TogglePause();
        Assert.True(router.Movement(World, Right).IsNeutral);
        router.Movement(World, FarmDirectionalInput.Neutral);
        Assert.True(router.Movement(World, Right with { ResolvedIntent = FarmMovementIntent.Neutral }).IsNeutral);
    }

    [Fact]
    public void FocusReconciliationRecoversMissingReleasesButDoesNotReplayHeldActions()
    {
        var router = new FarmInputRouter();
        router.Route(World, FarmInputSignal.Press(FarmInputActions.Interact));
        router.SetFocused(false);
        router.ReconcileHeldActions([FarmInputActions.Gather]);
        router.SetFocused(true);

        Assert.Equal(FarmSemanticAction.Speak,
            router.Route(World, FarmInputSignal.Press(FarmInputActions.Interact)).Action);
        Assert.Equal(FarmSemanticAction.None,
            router.Route(World, FarmInputSignal.Press(FarmInputActions.Gather)).Action);
        router.Route(World, FarmInputSignal.Release(FarmInputActions.Gather));
        Assert.Equal(FarmSemanticAction.Gather,
            router.Route(World, FarmInputSignal.Press(FarmInputActions.Gather)).Action);
    }
}
