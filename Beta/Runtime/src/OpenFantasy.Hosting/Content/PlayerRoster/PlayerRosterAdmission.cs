using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.EncounterOutcome;

namespace OpenFantasy.Hosting.Content.PlayerRoster;

/// <summary>Fresh production cargo roster; the one-player path retains the existing solo genesis exactly.</summary>
public static class PlayerRosterAdmission
{
    public static WorldSnapshot CreateEmbedded(string seedText, IReadOnlyList<string> playerNames) =>
        CargoOutcomeWorldAdmission.CreateEmbedded(seedText, playerNames);
}
