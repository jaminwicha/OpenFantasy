namespace OpenFantasy.Client;

/// <summary>
/// Display names of the player's current primary keys, for on-screen hints only. Routing always reads the input map;
/// this value changes no binding. The client replaces it whenever key bindings change, and it starts at the defaults.
/// </summary>
internal sealed record FarmKeyHints(string Interact, string Give, string Gather, string Craft, string NextTarget,
    string ClearTarget, string Inventory, string Wheel, string History, string Settings, string Walking, string Journal)
{
    internal static FarmKeyHints Default { get; } = new("E", "G", "F", "C", "Q", "R", "I", "V", "H", "O", "WASD", "J");

    /// <summary>The hints for the current bindings. Presentation-only state, read by display text.</summary>
    internal static FarmKeyHints Current { get; private set; } = Default;

    internal static void Apply(FarmKeyHints hints) => Current = hints ?? throw new ArgumentNullException(nameof(hints));
}
