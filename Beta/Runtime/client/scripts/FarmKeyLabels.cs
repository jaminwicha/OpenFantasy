using Godot;

namespace OpenFantasy.Client;

/// <summary>Reads each action's primary keyboard key from the input map to build accurate on-screen hints.</summary>
internal static class FarmKeyLabels
{
    private static readonly HashSet<Key> Arrows = [Key.Left, Key.Right, Key.Up, Key.Down];

    internal static FarmKeyHints FromInputMap() => new(
        Primary(FarmInputActions.Interact), Primary(FarmInputActions.Give), Primary(FarmInputActions.Gather),
        Primary(FarmInputActions.Craft), Primary(FarmInputActions.NextTarget), Primary(FarmInputActions.ClearTarget),
        Primary(FarmInputActions.Inventory), Primary(FarmInputActions.QuickWheel), Primary(FarmInputActions.History),
        Primary(FarmInputActions.Settings),
        Primary(FarmInputActions.WalkUp) + Primary(FarmInputActions.WalkLeft) + Primary(FarmInputActions.WalkDown)
            + Primary(FarmInputActions.WalkRight),
        Primary(FarmInputActions.Journal));

    private static string Primary(string action)
    {
        if (!InputMap.HasAction(action)) return "?";
        foreach (InputEvent bound in InputMap.ActionGetEvents(action))
        {
            if (bound is InputEventKey key && !Arrows.Contains(key.PhysicalKeycode)) return OS.GetKeycodeString(key.PhysicalKeycode);
        }
        return "?";
    }
}
