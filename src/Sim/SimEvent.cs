namespace OpenFantasy.Sim;

public enum SimEventKind : byte
{
    Damage = 0,
    Heal = 1,
    Death = 2,
    LevelUp = 3,
    AbilityUsed = 4,
    Respawn = 5,
    /// <summary>A player looted an item (AbilityId carries the item id).</summary>
    Loot = 6,
    /// <summary>Quest progress changed for a player (AbilityId carries the quest id; Value = kills or -1 on completion-ready).</summary>
    QuestProgress = 7,
}

/// <summary>
/// Presentation-facing fact emitted by the sim during a tick (damage numbers,
/// deaths, level-ups). The server forwards these to clients; they never feed
/// back into simulation state.
/// </summary>
public readonly struct SimEvent
{
    public readonly SimEventKind Kind;
    public readonly int SourceId;
    public readonly int TargetId;
    /// <summary>Damage/heal amount, new level, or ability slot depending on Kind.</summary>
    public readonly int Value;
    public readonly string AbilityId;

    public SimEvent(SimEventKind kind, int sourceId, int targetId, int value, string abilityId = "")
    {
        Kind = kind;
        SourceId = sourceId;
        TargetId = targetId;
        Value = value;
        AbilityId = abilityId;
    }
}
