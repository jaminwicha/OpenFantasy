using System.Collections.ObjectModel;
using System.Text;
using OpenFantasy.Core.Content;
using OpenFantasy.Hosting.Content.Cargo;

namespace OpenFantasy.Hosting.Content.PlayerRoster;

/// <summary>
/// Ordered fresh cooperative roster. Membership here authors the starting encounter catalogs; it is not
/// a mutable party service, controller handoff, item-sharing permission or independent encounter policy.
/// </summary>
public sealed class PlayerRosterDefinition
{
    public const int MaximumPlayers = 4;
    public const int ToolsPerAdditionalPlayer = 2;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public PlayerRosterDefinition(IReadOnlyList<string> playerNames)
    {
        ArgumentNullException.ThrowIfNull(playerNames);
        if (playerNames.Count is < 1 or > MaximumPlayers)
            throw new ArgumentException("A fresh player roster requires one to four ordered names.", nameof(playerNames));
        string[] names = new string[playerNames.Count];
        for (int index = 0; index < names.Length; index++)
        {
            string name = playerNames[index];
            if (string.IsNullOrWhiteSpace(name) || name.Length > FarmWorldAdmission.MaximumPlayerNameByteLength
                || name.Any(char.IsControl) || StrictUtf8.GetByteCount(name) > FarmWorldAdmission.MaximumPlayerNameByteLength)
                throw new ArgumentException("A roster name must be bounded exact UTF-8 without control characters.", nameof(playerNames));
            names[index] = name;
        }
        PlayerNames = Array.AsReadOnly(names);
        Version = CargoCanonicalFrame.Hash("cargo-player-roster.definition.v1",
            [CargoCanonicalFrame.Unsigned64((ulong)names.Length), .. names.Select(CargoCanonicalFrame.Text),
                CargoCanonicalFrame.Text("same-player-class-body-language;full-hp-mp;zero-xp-wallet;one-axe-one-sickle;shared-finite-quest-care;cooperative-full-catalog;spawn-square-rings-1-6;preserve-existing-cardinal-escape-capability-and-authored-access-anchors")]);
    }

    public ReadOnlyCollection<string> PlayerNames { get; }
    public ContentHash Version { get; }
}
