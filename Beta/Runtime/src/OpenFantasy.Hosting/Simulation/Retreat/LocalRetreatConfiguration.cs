using OpenFantasy.Core;

namespace OpenFantasy.Hosting.Simulation.Retreat;

/// <summary>
/// One host-controlled combatant's opt-in retreat preference: it commits the advised retreat when, at its own due
/// boundary, its current HP is at most <see cref="HpPermille"/> thousandths of its maximum. Host advice only.
/// </summary>
public sealed record LocalRetreatPreference
{
    public LocalRetreatPreference(EntityId actorId, int hpPermille)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(hpPermille, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(hpPermille, 1000);
        ActorId = actorId;
        HpPermille = hpPermille;
    }

    public EntityId ActorId { get; }
    public int HpPermille { get; }
}

/// <summary>
/// Opt-in NPC retreat advice, resupplied on every opening and never saved. Players and NPCs submit the same retreat
/// intent through the same admission, body and readiness rules; this only decides when an NPC asks.
/// </summary>
public sealed class LocalRetreatConfiguration
{
    public static LocalRetreatConfiguration None { get; } = new([]);

    public LocalRetreatConfiguration(IEnumerable<LocalRetreatPreference> preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        Dictionary<EntityId, LocalRetreatPreference> byActor = [];
        foreach (LocalRetreatPreference preference in preferences)
        {
            ArgumentNullException.ThrowIfNull(preference);
            if (!byActor.TryAdd(preference.ActorId, preference))
            {
                throw new ArgumentException("Each actor has at most one retreat preference.", nameof(preferences));
            }
        }
        Preferences = byActor;
    }

    public IReadOnlyDictionary<EntityId, LocalRetreatPreference> Preferences { get; }
}
