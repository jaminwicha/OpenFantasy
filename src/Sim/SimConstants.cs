namespace OpenFantasy.Sim;

/// <summary>
/// Global simulation constants. All gameplay time is ticks; all gameplay
/// space is integer world units (32 units = 1 tile).
/// </summary>
public static class SimConstants
{
    public const int TicksPerSecond = 20;
    public const int MsPerTick = 1000 / TicksPerSecond;

    public const int UnitsPerTile = 32;

    /// <summary>ATB gauge maximum (fixed-point, 0..1000).</summary>
    public const int AtbMax = 1000;

    /// <summary>Ticks a dead player waits before respawning at the crystal.</summary>
    public const int PlayerRespawnTicks = 100;

    /// <summary>Max ability targeting distance clamp, in units (sanity cap).</summary>
    public const int MaxAbilityRange = 12 * UnitsPerTile;

    public const int MaxLevel = 10;

    public const int InventorySlots = 24;
    public const int MaxStack = 99;

    /// <summary>Party members within this distance of a kill share XP/credit.</summary>
    public const int PartyShareRange = 20 * UnitsPerTile;

    /// <summary>Max distance for talking to an NPC.</summary>
    public const int InteractRange = 3 * UnitsPerTile;

    /// <summary>Ticks per in-game day (10 real minutes) — presentation clock only.</summary>
    public const int TicksPerDay = 600 * TicksPerSecond;

    public const int ProtocolVersion = 2;
}
