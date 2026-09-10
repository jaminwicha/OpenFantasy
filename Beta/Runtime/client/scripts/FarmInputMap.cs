using Godot;

namespace OpenFantasy.Client;

/// <summary>One place for initial device bindings; gameplay code consumes named actions.</summary>
internal static class FarmInputMap
{
    internal static void InstallDefaults()
    {
        Movement("walk_left", Key.A, Key.Left, JoyButton.DpadLeft, JoyAxis.LeftX, -1);
        Movement("walk_right", Key.D, Key.Right, JoyButton.DpadRight, JoyAxis.LeftX, 1);
        Movement("walk_up", Key.W, Key.Up, JoyButton.DpadUp, JoyAxis.LeftY, -1);
        Movement("walk_down", Key.S, Key.Down, JoyButton.DpadDown, JoyAxis.LeftY, 1);
        Button("talk", Key.E, JoyButton.A);
        Button("give_wood", Key.G, JoyButton.X);
        Button("gather", Key.F, JoyButton.Y);
        Button("craft_rations", Key.C, JoyButton.B);
        Button("pause_world", Key.Escape, JoyButton.Start);
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
}
