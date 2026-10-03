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
    internal const string QuickWheel = "quick_wheel";

    /// <summary>
    /// Routing priority. One device event may match several names (controller B is both Cancel and
    /// Craft); the owning layer takes the first name it binds.
    /// </summary>
    internal static readonly IReadOnlyList<string> Routed = [Pause, Cancel, QuickWheel, Inventory, Interact, Give, Gather, Craft, NextTarget, ClearTarget];
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
    OpenWheel,
    CloseWheel,
    ConfirmWheel,
    Excavate,
    Traverse,
    EnterEncounter,
    Strike,
    Wait,
    ReleaseEncounter,
    AcknowledgeCare,
    UseRestorative,
    RevokeCarePermission,
    NextWheelPage,
    ProposeMaterial,
    AcceptMaterial,
    DeclineMaterial,
    WithdrawMaterial,
    /// <summary>Opens the stall panel to choose units to buy; it asks, buys and spends nothing.</summary>
    TradeBuy,
    /// <summary>Opens the stall panel to choose an own lot and units to sell; it shows and sells nothing.</summary>
    TradeSell,
    /// <summary>Opens the stall panel at the merchant's answers and saved outcomes.</summary>
    TradeOffers,
    /// <summary>Shows one exact own lot to the stall: a world command that sells, reserves and consents to nothing.</summary>
    ShowTradeLot,
    /// <summary>Accepts the one reviewed offer at its exact restated terms.</summary>
    ConfirmTrade,
    CloseMarket,
    /// <summary>Commits the inspected retreat route; each step is still walked and charged by the world.</summary>
    Retreat,
    CancelCare,
    DeclineCare,
    AdministerCare,
    OpenRecipes,
    CloseRecipes,
    PrepareRecipe,
    ConfirmRecipe,
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
    /// <summary>The quick wheel owns selection and confirmation without changing host timing.</summary>
    Wheel,
    /// <summary>Encounter choices own input; walking and ordinary farm actions are suppressed.</summary>
    Encounter,
    /// <summary>The stall panel owns navigation and its explicit commitments; opening it changes no host timing.</summary>
    Market,
    /// <summary>Accepted work is finishing before this player can explicitly resume control.</summary>
    ControlReturn,
    /// <summary>Known recipes and a separately prepared batch own input without changing world time.</summary>
    Recipes,
}

/// <summary>World facts the router does not own, read from the current projection at each decision.</summary>
internal readonly record struct FarmInputSituation(bool SessionOpen, bool ReadingHold, bool ActionPending)
{
    internal bool IsInEncounter { get; init; }
    internal bool HasEncounterEntry { get; init; }
    internal bool IsEncounterOutcome { get; init; }
    internal bool HasCareAcknowledgment { get; init; }
    /// <summary>
    /// The presented outcome choice is an outcome-completion reading (profile 35): accepting stays a deliberate choice
    /// beside admitted care, because it declines that care. Under earlier laws care is the only choice at a defeat.
    /// </summary>
    internal bool HasOutcomeReadingBesideCare { get; init; }
    internal bool HasWheelPages { get; init; }
    /// <summary>The wheel's selected choice only opens an inspection panel; it submits nothing.</summary>
    internal bool WheelChoiceOpensPanel { get; init; }
}

/// <summary>One discrete device event translated to the routed action names it matches.</summary>
internal readonly record struct FarmInputSignal(IReadOnlyList<string> Actions, bool Pressed, bool Echo)
{
    /// <summary>
    /// The event is also the native accept action for a visible focused Control, which received the
    /// press first. Native buttons complete activation only when the matching release reaches them.
    /// </summary>
    internal bool FocusedControlAccept { get; init; }

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
/// Each held press has one owner until its release: the router swallows the release of a press it
/// took, while a focused pack or wheel Control that took an accept press receives its release.
/// </summary>
internal sealed class FarmInputRouter
{
    /// <summary>Who receives the release of a held routed press.</summary>
    private enum PressOwner
    {
        /// <summary>The router took the press; its release is swallowed before the GUI.</summary>
        Router,
        /// <summary>A focused panel Control took the press; its release must reach that Control.</summary>
        Control,
    }

    private readonly Dictionary<string, PressOwner> _heldActions = new(StringComparer.Ordinal);
    private FarmDirections _heldWhileBlocked;
    private bool _unavailable;

    internal FarmInputRouter(bool paused = false)
    {
        Paused = paused;
    }

    internal bool Paused { get; private set; }
    internal bool Focused { get; private set; } = true;
    internal bool InventoryOpen { get; private set; }
    internal bool WheelOpen { get; private set; }
    internal bool MarketOpen { get; private set; }
    internal bool RecipesOpen { get; private set; }
    internal bool ControlPending { get; private set; }

    /// <summary>
    /// Local readiness request; inspection panels, including the stall, do not change the host's clock policy.
    /// A stall request is therefore answered while the panel stays open unless the player pauses explicitly.
    /// </summary>
    internal bool WorldShouldRun => !_unavailable && Focused && !Paused && !ControlPending;

    internal FarmInputLayer Layer(FarmInputSituation situation)
    {
        if (_unavailable) return FarmInputLayer.Unavailable;
        if (!Focused) return FarmInputLayer.Unfocused;
        if (ControlPending) return FarmInputLayer.ControlReturn;
        if (RecipesOpen) return FarmInputLayer.Recipes;
        if (InventoryOpen) return FarmInputLayer.Inventory;
        if (WheelOpen) return FarmInputLayer.Wheel;
        if (MarketOpen) return FarmInputLayer.Market;
        if (Paused) return FarmInputLayer.Paused;
        if (!situation.SessionOpen) return FarmInputLayer.Opening;
        if (situation.ReadingHold) return FarmInputLayer.Dialogue;
        if (situation.IsInEncounter) return FarmInputLayer.Encounter;
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
        if (_unavailable || ControlPending) return false;
        Paused = !Paused;
        return true;
    }

    /// <summary>A new control boundary requires a deliberate fresh Resume; focus alone cannot accept it.</summary>
    internal void RequireControlResume(bool pending)
    {
        ControlPending = pending;
        Paused = true;
    }

    /// <summary>A failed or closing farm accepts no further input.</summary>
    internal void MarkUnavailable() => _unavailable = true;

    /// <summary>Changes input ownership only, after the caller checks the semantic action.</summary>
    internal void SetInventoryOpen(bool open) => InventoryOpen = open;

    internal void SetWheelOpen(bool open) => WheelOpen = open;

    internal void SetMarketOpen(bool open) => MarketOpen = open;

    internal void SetRecipesOpen(bool open) => RecipesOpen = open;

    /// <summary>Whether the owning layer accepts this semantic action right now, from any source.</summary>
    internal bool Permits(FarmInputSituation situation, FarmSemanticAction action)
    {
        FarmInputLayer layer = Layer(situation);
        return action switch
        {
            FarmSemanticAction.None => false,
            FarmSemanticAction.TogglePause => layer is not (FarmInputLayer.Unavailable or FarmInputLayer.Unfocused
                or FarmInputLayer.Inventory or FarmInputLayer.Wheel or FarmInputLayer.Recipes or FarmInputLayer.ControlReturn),
            FarmSemanticAction.OpenRecipes => situation.SessionOpen && !situation.ReadingHold && !situation.ActionPending
                && layer is FarmInputLayer.World or FarmInputLayer.Encounter or FarmInputLayer.Paused,
            FarmSemanticAction.CloseRecipes => layer == FarmInputLayer.Recipes,
            FarmSemanticAction.PrepareRecipe => layer == FarmInputLayer.Recipes && !situation.ReadingHold && !situation.ActionPending,
            FarmSemanticAction.ConfirmRecipe => layer == FarmInputLayer.Recipes && !Paused
                && !situation.ReadingHold && !situation.ActionPending,
            FarmSemanticAction.OpenInventory => situation.SessionOpen && !situation.ReadingHold && !situation.ActionPending
                && layer is FarmInputLayer.World or FarmInputLayer.Encounter or FarmInputLayer.Paused,
            FarmSemanticAction.CloseInventory => layer == FarmInputLayer.Inventory,
            FarmSemanticAction.OpenWheel => situation.SessionOpen && !situation.ReadingHold && !situation.ActionPending
                && layer is FarmInputLayer.World or FarmInputLayer.Encounter or FarmInputLayer.Paused,
            FarmSemanticAction.CloseWheel => layer == FarmInputLayer.Wheel,
            FarmSemanticAction.NextWheelPage => layer == FarmInputLayer.Wheel && situation.HasWheelPages,
            // Opening an inspection panel is allowed while paused; a world action needs running local time.
            FarmSemanticAction.ConfirmWheel => layer == FarmInputLayer.Wheel && !situation.ReadingHold
                && (situation.WheelChoiceOpensPanel || !Paused && !situation.ActionPending),
            FarmSemanticAction.CloseMarket => layer == FarmInputLayer.Market,
            // Stall commitments are world commands: they need running local time, as the session requires.
            FarmSemanticAction.ShowTradeLot or FarmSemanticAction.ConfirmTrade => layer == FarmInputLayer.Market
                && !Paused && !situation.ActionPending && !situation.ReadingHold,
            FarmSemanticAction.TradeBuy or FarmSemanticAction.TradeSell or FarmSemanticAction.TradeOffers => false,
            FarmSemanticAction.CancelCare or FarmSemanticAction.DeclineCare or FarmSemanticAction.AdministerCare =>
                layer == FarmInputLayer.Encounter && !situation.ActionPending,
            FarmSemanticAction.AcknowledgeCare => layer == FarmInputLayer.Encounter
                && situation.HasCareAcknowledgment && !situation.ActionPending,
            FarmSemanticAction.ReleaseEncounter when situation.HasCareAcknowledgment && situation.HasOutcomeReadingBesideCare =>
                layer == FarmInputLayer.Encounter && !situation.ActionPending,
            FarmSemanticAction.Strike or FarmSemanticAction.Wait or FarmSemanticAction.ReleaseEncounter
                when situation.HasCareAcknowledgment => false,
            _ => !situation.ActionPending && LayerAccepts(layer, action),
        };
    }

    /// <summary>
    /// Routes one device event. Every routed press is consumed after the GUI has seen it. A release is
    /// consumed only when the router owned its press; Control-owned and untracked releases propagate.
    /// </summary>
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
        FarmInputLayer layer = Layer(situation);
        if (signal.FocusedControlAccept && LeavesAcceptToControl(layer, signal.Actions)
            && !signal.Actions.Any(action => _heldActions.TryGetValue(action, out PressOwner owner)
                && owner == PressOwner.Router))
        {
            // The focused Control already has this press; it completes activation on the release.
            foreach (string action in signal.Actions)
            {
                _heldActions[action] = PressOwner.Control;
            }
            return new FarmInputDecision(Consumed: true, FarmSemanticAction.None);
        }
        if (signal.Actions.Any(_heldActions.ContainsKey))
        {
            // A press without an intervening release duplicates the tracked one. The router keeps its
            // release, so a Control release that went missing cannot activate the next focus owner.
            foreach (string action in signal.Actions.Where(_heldActions.ContainsKey))
            {
                _heldActions[action] = PressOwner.Router;
            }
            return new FarmInputDecision(Consumed: true, FarmSemanticAction.None);
        }
        foreach (string action in signal.Actions)
        {
            _heldActions[action] = PressOwner.Router;
        }
        FarmSemanticAction requested = Resolve(layer, signal.Actions);
        if (requested == FarmSemanticAction.Speak && situation.HasEncounterEntry)
            requested = FarmSemanticAction.EnterEncounter;
        if (requested == FarmSemanticAction.Strike && situation.HasCareAcknowledgment)
            requested = FarmSemanticAction.AcknowledgeCare;
        else if (requested == FarmSemanticAction.Strike && situation.IsEncounterOutcome)
            requested = FarmSemanticAction.ReleaseEncounter;
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

    /// <summary>
    /// Recover from releases delivered outside the window; held actions still require a fresh press.
    /// The router owns every press held across the boundary, so its release activates no Control.
    /// </summary>
    internal void ReconcileHeldActions(IEnumerable<string> currentlyPressed)
    {
        _heldActions.Clear();
        foreach (string action in currentlyPressed)
        {
            _heldActions[action] = PressOwner.Router;
        }
    }

    /// <summary>Clears every released name; only the release of a router-owned press is swallowed.</summary>
    private bool ReleaseTracked(IReadOnlyList<string> actions)
    {
        bool routerOwned = false;
        foreach (string action in actions)
        {
            if (_heldActions.Remove(action, out PressOwner owner) && owner == PressOwner.Router) routerOwned = true;
        }
        return routerOwned;
    }

    /// <summary>
    /// Pack and wheel Controls own a native accept press their layer binds to no command, such as
    /// controller A, which is Talk in ordinary play. A layer command always keeps its press.
    /// </summary>
    private static bool LeavesAcceptToControl(FarmInputLayer layer, IReadOnlyList<string> actions) =>
        (layer is FarmInputLayer.Inventory or FarmInputLayer.Wheel or FarmInputLayer.Market or FarmInputLayer.Recipes)
        && Resolve(layer, actions) == FarmSemanticAction.None;

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
        (FarmInputLayer.Recipes, FarmInputActions.Pause or FarmInputActions.Cancel
            or FarmInputActions.Inventory or FarmInputActions.QuickWheel) => FarmSemanticAction.CloseRecipes,
        (FarmInputLayer.Wheel, FarmInputActions.Pause or FarmInputActions.Cancel or FarmInputActions.QuickWheel) => FarmSemanticAction.CloseWheel,
        (FarmInputLayer.Wheel, FarmInputActions.NextTarget) => FarmSemanticAction.NextWheelPage,
        (FarmInputLayer.Market, FarmInputActions.Pause or FarmInputActions.Cancel or FarmInputActions.QuickWheel) => FarmSemanticAction.CloseMarket,
        (FarmInputLayer.World or FarmInputLayer.Encounter or FarmInputLayer.Paused, FarmInputActions.QuickWheel) => FarmSemanticAction.OpenWheel,
        (FarmInputLayer.Inventory, FarmInputActions.Pause or FarmInputActions.Cancel or FarmInputActions.Inventory) => FarmSemanticAction.CloseInventory,
        (FarmInputLayer.World or FarmInputLayer.Encounter or FarmInputLayer.Paused, FarmInputActions.Inventory) => FarmSemanticAction.OpenInventory,
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
        (FarmInputLayer.Encounter, FarmInputActions.Interact) => FarmSemanticAction.Strike,
        (FarmInputLayer.Encounter, FarmInputActions.Give) => FarmSemanticAction.Wait,
        (FarmInputLayer.Encounter, FarmInputActions.NextTarget) => FarmSemanticAction.NextTarget,
        (FarmInputLayer.Encounter, FarmInputActions.ClearTarget) => FarmSemanticAction.ClearTarget,
        _ => FarmSemanticAction.None,
    };

    private static bool LayerAccepts(FarmInputLayer layer, FarmSemanticAction action) => layer switch
    {
        FarmInputLayer.World => action is FarmSemanticAction.Speak or FarmSemanticAction.Give
            or FarmSemanticAction.Gather or FarmSemanticAction.Craft
            or FarmSemanticAction.Excavate or FarmSemanticAction.Traverse or FarmSemanticAction.EnterEncounter
            or FarmSemanticAction.UseRestorative or FarmSemanticAction.RevokeCarePermission
            or FarmSemanticAction.ProposeMaterial or FarmSemanticAction.AcceptMaterial
            or FarmSemanticAction.DeclineMaterial or FarmSemanticAction.WithdrawMaterial
            or FarmSemanticAction.NextTarget or FarmSemanticAction.ClearTarget,
        FarmInputLayer.Encounter => action is FarmSemanticAction.Strike or FarmSemanticAction.Wait
            or FarmSemanticAction.ReleaseEncounter or FarmSemanticAction.NextTarget or FarmSemanticAction.ClearTarget,
        FarmInputLayer.Dialogue => action is FarmSemanticAction.Give or FarmSemanticAction.CloseDialogue,
        _ => false,
    };
}
