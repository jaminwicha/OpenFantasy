using Godot;

namespace OpenFantasy.Client;

/// <summary>One place for initial device bindings; gameplay code consumes named actions through the router.</summary>
internal static class FarmInputMap
{
    internal static void InstallDefaults()
    {
        Movement(FarmInputActions.WalkLeft, Key.A, Key.Left, JoyButton.DpadLeft, JoyAxis.LeftX, -1);
        Movement(FarmInputActions.WalkRight, Key.D, Key.Right, JoyButton.DpadRight, JoyAxis.LeftX, 1);
        Movement(FarmInputActions.WalkUp, Key.W, Key.Up, JoyButton.DpadUp, JoyAxis.LeftY, -1);
        Movement(FarmInputActions.WalkDown, Key.S, Key.Down, JoyButton.DpadDown, JoyAxis.LeftY, 1);
        Button(FarmInputActions.Interact, Key.E, JoyButton.A);
        Button(FarmInputActions.Give, Key.G, JoyButton.X);
        Button(FarmInputActions.Gather, Key.F, JoyButton.Y);
        Button(FarmInputActions.Craft, Key.C, JoyButton.B);
        Button(FarmInputActions.Pause, Key.Escape, JoyButton.Start);
        Button(FarmInputActions.NextTarget, Key.Q, JoyButton.RightShoulder);
        Button(FarmInputActions.ClearTarget, Key.R, JoyButton.LeftShoulder);
        Button(FarmInputActions.Inventory, Key.I, JoyButton.Back);
        Button(FarmInputActions.QuickWheel, Key.V, JoyButton.RightStick);
        // Controller B also names Cancel; the router lets it unwind a modal layer and craft in ordinary play.
        JoypadButton(FarmInputActions.Cancel, JoyButton.B);
        // Built-in UI defaults can be keyboard-only. Install the controller equivalent for every
        // native Control, including buttons; future saved binding profiles apply after defaults.
        InputEventJoypadButton accept = new() { ButtonIndex = JoyButton.A };
        if (!InputMap.ActionHasEvent("ui_accept", accept)) InputMap.ActionAddEvent("ui_accept", accept);
    }

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
