using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Client;

// Client input policy without engine types: which layer owns each event, which semantic action it
// requests, and when polled movement may reach the host. Legality, cost and timing of every
// submitted action remain host decisions. Godot code translates device events and polled strengths
// into the value types below and applies the returned decisions.

/// <summary>Named input-map actions the client routes semantically; walking actions are polled, not routed.</summary>
internal static class FarmInputActions
{
    internal const string WalkLeft = "walk_left";
    internal const string WalkRight = "walk_right";
    internal const string WalkUp = "walk_up";
    internal const string WalkDown = "walk_down";
    internal const string Interact = "talk";
    internal const string Give = "give_wood";
    internal const string Gather = "gather";
    internal const string Craft = "craft_rations";
    internal const string Pause = "pause_world";
    internal const string Cancel = "cancel";
    internal const string NextTarget = "next_target";
    internal const string ClearTarget = "clear_target";
    internal const string Inventory = "inventory";

    /// <summary>
    /// Routing priority. One device event may match several names (controller B is both Cancel and
    /// Craft); the owning layer takes the first name it binds.
    /// </summary>
    internal static readonly IReadOnlyList<string> Routed = [Pause, Cancel, Inventory, Interact, Give, Gather, Craft, NextTarget, ClearTarget];
}

/// <summary>Client intentions. The host still validates the actor, target, resources and revisions.</summary>
internal enum FarmSemanticAction
{
    None,
    Speak,
    Give,
    Gather,
    Craft,
    CloseDialogue,
    TogglePause,
    NextTarget,
    ClearTarget,
    OpenInventory,
    CloseInventory,
}

/// <summary>Who owns input, from the top of the stack down.</summary>
internal enum FarmInputLayer
{
    /// <summary>The farm failed or is closing; nothing is accepted.</summary>
    Unavailable,
    /// <summary>Local pause; Cancel or the pause command resumes.</summary>
    Paused,
    /// <summary>The window lost focus; world input waits for its return.</summary>
    Unfocused,
    /// <summary>The session is still opening.</summary>
    Opening,
    /// <summary>An addressed conversation holds time; only its own actions apply.</summary>
    Dialogue,
    /// <summary>Ordinary play.</summary>
    World,
    /// <summary>Pack inspection owns navigation, independently of the existing world pause.</summary>
    Inventory,
}

/// <summary>World facts the router does not own, read from the current projection at each decision.</summary>
internal readonly record struct FarmInputSituation(bool SessionOpen, bool ReadingHold, bool ActionPending);

/// <summary>One discrete device event translated to the routed action names it matches.</summary>
internal readonly record struct FarmInputSignal(IReadOnlyList<string> Actions, bool Pressed, bool Echo)
{
    internal static FarmInputSignal Press(params string[] actions) => new(actions, Pressed: true, Echo: false);
    internal static FarmInputSignal Repeat(params string[] actions) => new(actions, Pressed: true, Echo: true);
    internal static FarmInputSignal Release(params string[] actions) => new(actions, Pressed: false, Echo: false);
}

/// <summary>Whether the event stops propagating and which semantic action, if any, it requests.</summary>
internal readonly record struct FarmInputDecision(bool Consumed, FarmSemanticAction Action);

[Flags]
internal enum FarmDirections
{
    None = 0,
    Left = 1,
    Right = 2,
    Up = 4,
    Down = 8,
}

/// <summary>Polled walking strengths in [0, 1] per direction; opposing directions cancel.</summary>
internal readonly record struct FarmDirectionalInput(float Left, float Right, float Up, float Down)
{
    internal static readonly FarmDirectionalInput Neutral = new(0, 0, 0, 0);

    /// <summary>The native adapter may supply its circular-deadzone result independently of held buttons.</summary>
    internal FarmMovementIntent? ResolvedIntent { get; init; }

    internal FarmDirections Held =>
        (Left > 0 ? FarmDirections.Left : FarmDirections.None)
        | (Right > 0 ? FarmDirections.Right : FarmDirections.None)
        | (Up > 0 ? FarmDirections.Up : FarmDirections.None)
        | (Down > 0 ? FarmDirections.Down : FarmDirections.None);

    /// <summary>The dominant axis wins; a tie prefers vertical, matching the retained adapter rule.</summary>
    internal FarmMovementIntent ToIntent()
    {
        return ResolvedIntent ?? FarmMovementIntent.FromAxes(Right - Left, Down - Up);
    }
}

/// <summary>One fixed cardinal host sample, or zero to stop.</summary>
internal readonly record struct FarmMovementIntent(long DeltaX, long DeltaY)
{
    internal static readonly FarmMovementIntent Neutral = new(0, 0);
    internal bool IsNeutral => DeltaX == 0 && DeltaY == 0;

    internal static FarmMovementIntent FromAxes(float horizontal, float vertical)
    {
        if (Math.Abs(horizontal) > Math.Abs(vertical))
        {
            return new FarmMovementIntent(Math.Sign(horizontal) * WorldMovementInput.DisplacementPerStep, 0);
        }
        if (vertical != 0)
        {
            return new FarmMovementIntent(0, Math.Sign(vertical) * WorldMovementInput.DisplacementPerStep);
        }
        return FarmMovementIntent.Neutral;
    }
}

/// <summary>
/// Single owner of client input context. Tracks local pause, window focus and availability, decides
/// the owning layer from those plus the observed world, and enforces that a consumed press, its
/// release, key auto-repeat and directions held through a context change never reach the world.
/// </summary>
internal sealed class FarmInputRouter
{
    private readonly HashSet<string> _heldActions = new(StringComparer.Ordinal);
    private FarmDirections _heldWhileBlocked;
    private bool _unavailable;

    internal FarmInputRouter(bool paused = false)
    {
        Paused = paused;
    }

    internal bool Paused { get; private set; }
    internal bool Focused { get; private set; } = true;
    internal bool InventoryOpen { get; private set; }

    /// <summary>Local readiness request; inspection panels do not change the host's clock policy.</summary>
    internal bool WorldShouldRun => !_unavailable && Focused && !Paused;

    internal FarmInputLayer Layer(FarmInputSituation situation)
    {
        if (_unavailable) return FarmInputLayer.Unavailable;
        if (!Focused) return FarmInputLayer.Unfocused;
        if (InventoryOpen) return FarmInputLayer.Inventory;
        if (Paused) return FarmInputLayer.Paused;
        if (!situation.SessionOpen) return FarmInputLayer.Opening;
        if (situation.ReadingHold) return FarmInputLayer.Dialogue;
        return FarmInputLayer.World;
    }

    /// <summary>Records focus; returns false when the farm can no longer react to it.</summary>
    internal bool SetFocused(bool focused)
    {
        Focused = focused;
        return !_unavailable;
    }

    /// <summary>Flips local pause; returns false when the farm can no longer react to it.</summary>
    internal bool TogglePause()
    {
        if (_unavailable) return false;
        Paused = !Paused;
        return true;
    }

    /// <summary>A failed or closing farm accepts no further input.</summary>
    internal void MarkUnavailable() => _unavailable = true;

    /// <summary>Changes input ownership only, after the caller checks the semantic action.</summary>
    internal void SetInventoryOpen(bool open) => InventoryOpen = open;

    /// <summary>Whether the owning layer accepts this semantic action right now, from any source.</summary>
    internal bool Permits(FarmInputSituation situation, FarmSemanticAction action)
    {
        FarmInputLayer layer = Layer(situation);
        return action switch
        {
            FarmSemanticAction.None => false,
            FarmSemanticAction.TogglePause => layer is not (FarmInputLayer.Unavailable or FarmInputLayer.Unfocused or FarmInputLayer.Inventory),
            FarmSemanticAction.OpenInventory => situation.SessionOpen && !situation.ReadingHold && !situation.ActionPending
                && layer is FarmInputLayer.World or FarmInputLayer.Paused,
            FarmSemanticAction.CloseInventory => layer == FarmInputLayer.Inventory,
            _ => !situation.ActionPending && LayerAccepts(layer, action),
        };
    }

    /// <summary>Routes one device event. Every routed press is consumed, as is the release of a tracked press.</summary>
    internal FarmInputDecision Route(FarmInputSituation situation, FarmInputSignal signal)
    {
        if (signal.Actions.Count == 0)
        {
            return new FarmInputDecision(Consumed: false, FarmSemanticAction.None);
        }
        if (signal.Echo)
        {
            // Key auto-repeat is never a new intention.
            return new FarmInputDecision(Consumed: true, FarmSemanticAction.None);
        }
        if (!signal.Pressed)
        {
            return new FarmInputDecision(ReleaseTracked(signal.Actions), FarmSemanticAction.None);
        }
        if (signal.Actions.Any(_heldActions.Contains))
        {
            // A press without an intervening release duplicates the tracked one.
            return new FarmInputDecision(Consumed: true, FarmSemanticAction.None);
        }
        foreach (string action in signal.Actions)
        {
            _heldActions.Add(action);
        }
        FarmSemanticAction requested = Resolve(Layer(situation), signal.Actions);
        return new FarmInputDecision(Consumed: true, Permits(situation, requested) ? requested : FarmSemanticAction.None);
    }

    /// <summary>
    /// Converts polled walking input to the host sample. A blocking layer yields neutral and remembers
    /// what was held; after the block those directions stay neutral until physically released.
    /// </summary>
    internal FarmMovementIntent Movement(FarmInputSituation situation, FarmDirectionalInput input)
    {
        if (Layer(situation) != FarmInputLayer.World)
        {
            _heldWhileBlocked = input.Held;
            return FarmMovementIntent.Neutral;
        }
        if ((_heldWhileBlocked & input.Held) != FarmDirections.None)
        {
            return FarmMovementIntent.Neutral;
        }
        _heldWhileBlocked = FarmDirections.None;
        return input.ToIntent();
    }

    /// <summary>Sample before leaving a blocked layer, even when it opened and closed between physics frames.</summary>
    internal void CaptureBoundary(FarmInputSituation situation, FarmDirectionalInput input)
    {
        if (Layer(situation) != FarmInputLayer.World) _heldWhileBlocked = input.Held;
    }

    /// <summary>Recover from releases delivered outside the window; held actions still require a fresh press.</summary>
    internal void ReconcileHeldActions(IEnumerable<string> currentlyPressed)
    {
        _heldActions.Clear();
        _heldActions.UnionWith(currentlyPressed);
    }

    private bool ReleaseTracked(IReadOnlyList<string> actions)
    {
        bool tracked = false;
        foreach (string action in actions)
        {
            tracked |= _heldActions.Remove(action);
        }
        return tracked;
    }

    private static FarmSemanticAction Resolve(FarmInputLayer layer, IReadOnlyList<string> actions)
    {
        foreach (string name in FarmInputActions.Routed)
        {
            if (!actions.Contains(name)) continue;
            FarmSemanticAction bound = Bind(layer, name);
            if (bound != FarmSemanticAction.None) return bound;
        }
        return FarmSemanticAction.None;
    }

    /// <summary>
    /// Escape/Start and Cancel unwind the top layer. The HUD's explicit pause command remains
    /// available during dialogue without giving its keyboard cancellation a second meaning.
    /// </summary>
    private static FarmSemanticAction Bind(FarmInputLayer layer, string action) => (layer, action) switch
    {
        (FarmInputLayer.Inventory, FarmInputActions.Pause or FarmInputActions.Cancel or FarmInputActions.Inventory) => FarmSemanticAction.CloseInventory,
        (FarmInputLayer.World or FarmInputLayer.Paused, FarmInputActions.Inventory) => FarmSemanticAction.OpenInventory,
        (FarmInputLayer.Dialogue, FarmInputActions.Pause) => FarmSemanticAction.CloseDialogue,
        (_, FarmInputActions.Pause) => FarmSemanticAction.TogglePause,
        (FarmInputLayer.Paused, FarmInputActions.Cancel) => FarmSemanticAction.TogglePause,
        (FarmInputLayer.Dialogue, FarmInputActions.Cancel) => FarmSemanticAction.CloseDialogue,
        (FarmInputLayer.Dialogue, FarmInputActions.Interact) => FarmSemanticAction.CloseDialogue,
        (FarmInputLayer.Dialogue, FarmInputActions.Give) => FarmSemanticAction.Give,
        (FarmInputLayer.World, FarmInputActions.Interact) => FarmSemanticAction.Speak,
        (FarmInputLayer.World, FarmInputActions.Give) => FarmSemanticAction.Give,
        (FarmInputLayer.World, FarmInputActions.Gather) => FarmSemanticAction.Gather,
        (FarmInputLayer.World, FarmInputActions.Craft) => FarmSemanticAction.Craft,
        (FarmInputLayer.World, FarmInputActions.NextTarget) => FarmSemanticAction.NextTarget,
        (FarmInputLayer.World, FarmInputActions.ClearTarget) => FarmSemanticAction.ClearTarget,
        _ => FarmSemanticAction.None,
    };

    private static bool LayerAccepts(FarmInputLayer layer, FarmSemanticAction action) => layer switch
    {
        FarmInputLayer.World => action is FarmSemanticAction.Speak or FarmSemanticAction.Give
            or FarmSemanticAction.Gather or FarmSemanticAction.Craft
            or FarmSemanticAction.NextTarget or FarmSemanticAction.ClearTarget,
        FarmInputLayer.Dialogue => action is FarmSemanticAction.Give or FarmSemanticAction.CloseDialogue,
        _ => false,
    };
}
