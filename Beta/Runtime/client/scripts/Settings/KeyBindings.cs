using System.Collections.ObjectModel;
using System.Globalization;

namespace OpenFantasy.Client.Settings;

/// <summary>One remappable action: its input-map name, player-facing label and default physical key code.</summary>
internal sealed record KeyBindingAction(string Action, string Label, long DefaultKey);

/// <summary>
/// The player's primary keyboard keys for remappable actions, as physical key codes. Escape, the arrow keys,
/// Enter, Space and Tab stay reserved, so pause, panel closing, walking alternatives and interface navigation
/// always work. Assigning a key already used by another action swaps the two, so no action is ever unbound.
/// Controller bindings are not remapped here.
/// </summary>
internal sealed class KeyBindings
{
    // Physical key codes match Godot's Key enumeration: letters use their ASCII codes.
    internal const long Escape = 4194305, Tab = 4194306, Enter = 4194309, KeypadEnter = 4194310,
        Left = 4194319, Up = 4194320, Right = 4194321, Down = 4194322, Space = 32;

    internal static ReadOnlyCollection<KeyBindingAction> Actions { get; } = new List<KeyBindingAction>
    {
        new("walk_up", "Walk up", 'W'),
        new("walk_left", "Walk left", 'A'),
        new("walk_down", "Walk down", 'S'),
        new("walk_right", "Walk right", 'D'),
        new("talk", "Talk / close", 'E'),
        new("give_wood", "Give / quest", 'G'),
        new("gather", "Gather", 'F'),
        new("craft_rations", "Craft", 'C'),
        new("next_target", "Next target", 'Q'),
        new("clear_target", "Clear target", 'R'),
        new("inventory", "Pack", 'I'),
        new("quick_wheel", "Action wheel", 'V'),
        new("history", "History", 'H'),
        new("settings", "Settings", 'O'),
    }.AsReadOnly();

    internal static IReadOnlySet<long> Reserved { get; } = new HashSet<long> { Escape, Tab, Enter, KeypadEnter, Left, Up, Right, Down, Space };

    internal static KeyBindings Default { get; } = new(Actions.ToDictionary(action => action.Action, action => action.DefaultKey));

    private readonly Dictionary<string, long> _keys;

    private KeyBindings(Dictionary<string, long> keys) => _keys = keys;

    internal long this[string action] => _keys.TryGetValue(action, out long key) ? key
        : throw new ArgumentOutOfRangeException(nameof(action), action, "Not a remappable action.");

    internal bool IsDefault => Actions.All(action => _keys[action.Action] == action.DefaultKey);

    internal static bool IsAssignable(long key) => key > 0 && !Reserved.Contains(key);

    /// <summary>Assigns a key, swapping with any action that already uses it; refuses reserved or unknown keys.</summary>
    internal KeyBindings Assign(string action, long key)
    {
        long previous = this[action];
        if (!IsAssignable(key)) throw new ArgumentException("That key is reserved for pausing, closing or navigation.", nameof(key));
        var keys = new Dictionary<string, long>(_keys);
        string? holder = keys.FirstOrDefault(pair => pair.Value == key && pair.Key != action).Key;
        if (holder is not null) keys[holder] = previous;
        keys[action] = key;
        return new KeyBindings(keys);
    }

    /// <summary>Saved entries apply only when every key is assignable and unique; otherwise the defaults apply.</summary>
    internal static KeyBindings Parse(IReadOnlyDictionary<string, string> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var keys = new Dictionary<string, long>();
        foreach (KeyBindingAction action in Actions)
        {
            keys[action.Action] = entries.TryGetValue("keys." + action.Action, out string? text)
                && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long key) && IsAssignable(key)
                ? key : action.DefaultKey;
        }
        return keys.Values.Distinct().Count() == keys.Count ? new KeyBindings(keys) : Default;
    }

    internal IEnumerable<KeyValuePair<string, string>> ToEntries() =>
        Actions.Select(action => KeyValuePair.Create("keys." + action.Action, _keys[action.Action].ToString(CultureInfo.InvariantCulture)));

    public override bool Equals(object? other) => other is KeyBindings bindings
        && Actions.All(action => bindings._keys[action.Action] == _keys[action.Action]);

    public override int GetHashCode() => Actions.Aggregate(17, (hash, action) => HashCode.Combine(hash, _keys[action.Action]));
}
