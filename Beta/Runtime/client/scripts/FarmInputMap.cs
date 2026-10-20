using Godot;
using OpenFantasy.Client.Settings;

namespace OpenFantasy.Client;

/// <summary>One place for initial device bindings; gameplay code consumes named actions through the router.</summary>
internal static class FarmInputMap
{
    internal static void InstallDefaults()
    {
        Movement(FarmInputActions.WalkLeft, Primary(FarmInputActions.WalkLeft), Key.Left, JoyButton.DpadLeft, JoyAxis.LeftX, -1);
        Movement(FarmInputActions.WalkRight, Primary(FarmInputActions.WalkRight), Key.Right, JoyButton.DpadRight, JoyAxis.LeftX, 1);
        Movement(FarmInputActions.WalkUp, Primary(FarmInputActions.WalkUp), Key.Up, JoyButton.DpadUp, JoyAxis.LeftY, -1);
        Movement(FarmInputActions.WalkDown, Primary(FarmInputActions.WalkDown), Key.Down, JoyButton.DpadDown, JoyAxis.LeftY, 1);
        Button(FarmInputActions.Interact, Primary(FarmInputActions.Interact), JoyButton.A);
        Button(FarmInputActions.Give, Primary(FarmInputActions.Give), JoyButton.X);
        Button(FarmInputActions.Gather, Primary(FarmInputActions.Gather), JoyButton.Y);
        Button(FarmInputActions.Craft, Primary(FarmInputActions.Craft), JoyButton.B);
        Button(FarmInputActions.Pause, Key.Escape, JoyButton.Start);
        Button(FarmInputActions.NextTarget, Primary(FarmInputActions.NextTarget), JoyButton.RightShoulder);
        Button(FarmInputActions.ClearTarget, Primary(FarmInputActions.ClearTarget), JoyButton.LeftShoulder);
        Button(FarmInputActions.Inventory, Primary(FarmInputActions.Inventory), JoyButton.Back);
        Button(FarmInputActions.QuickWheel, Primary(FarmInputActions.QuickWheel), JoyButton.RightStick);
        Button(FarmInputActions.History, Primary(FarmInputActions.History), JoyButton.LeftStick);
        // Controllers without a share/capture button reach settings through the HUD's Settings control.
        Button(FarmInputActions.Settings, Primary(FarmInputActions.Settings), JoyButton.Misc1);
        // Controller B also names Cancel; the router lets it unwind a modal layer and craft in ordinary play.
        JoypadButton(FarmInputActions.Cancel, JoyButton.B);
        // Built-in UI defaults can be keyboard-only. Install the controller equivalent for every
        // native Control, including buttons; future saved binding profiles apply after defaults.
        InputEventJoypadButton accept = new() { ButtonIndex = JoyButton.A };
        if (!InputMap.ActionHasEvent("ui_accept", accept)) InputMap.ActionAddEvent("ui_accept", accept);
    }

    /// <summary>
    /// Replaces each remappable action's primary keyboard key with the player's choice. Arrow-key walking
    /// alternatives and every controller binding stay unchanged.
    /// </summary>
    internal static void ApplyBindings(KeyBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        var arrows = new HashSet<Key> { Key.Left, Key.Right, Key.Up, Key.Down };
        foreach (KeyBindingAction action in KeyBindings.Actions)
        {
            if (!InputMap.HasAction(action.Action)) continue;
            foreach (InputEvent existing in InputMap.ActionGetEvents(action.Action))
            {
                if (existing is InputEventKey key && !arrows.Contains(key.PhysicalKeycode)) InputMap.ActionEraseEvent(action.Action, existing);
            }
            InputMap.ActionAddEvent(action.Action, new InputEventKey { PhysicalKeycode = (Key)bindings[action.Action] });
        }
    }

    /// <summary>Default primary keys come from the one remappable binding table.</summary>
    private static Key Primary(string action) => (Key)KeyBindings.Default[action];

    private static void Movement(string name, Key letter, Key arrow, JoyButton button, JoyAxis axis, float sign)
    {
        if (InputMap.HasAction(name)) return;
        InputMap.AddAction(name, 0.25f);
        InputMap.ActionAddEvent(name, new InputEventKey { PhysicalKeycode = letter });
        InputMap.ActionAddEvent(name, new InputEventKey { PhysicalKeycode = arrow });
        InputMap.ActionAddEvent(name, new InputEventJoypadButton { ButtonIndex = button });
        InputMap.ActionAddEvent(name, new InputEventJoypadMotion { Axis = axis, AxisValue = sign });
    }

    private static void Button(string name, Key key, JoyButton button)
    {
        if (InputMap.HasAction(name)) return;
        InputMap.AddAction(name);
        InputMap.ActionAddEvent(name, new InputEventKey { PhysicalKeycode = key });
        InputMap.ActionAddEvent(name, new InputEventJoypadButton { ButtonIndex = button });
    }

    private static void JoypadButton(string name, JoyButton button)
    {
        if (InputMap.HasAction(name)) return;
        InputMap.AddAction(name);
        InputMap.ActionAddEvent(name, new InputEventJoypadButton { ButtonIndex = button });
    }
}
