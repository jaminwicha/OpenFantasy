namespace OpenFantasy.Sim;

/// <summary>
/// One player's intent for a tick. Sent by clients, applied by the server
/// until replaced by a newer sequence number.
/// </summary>
public struct InputCommand
{
    public int Seq;
    /// <summary>-1, 0, or 1.</summary>
    public sbyte MoveX;
    /// <summary>-1, 0, or 1.</summary>
    public sbyte MoveY;
    /// <summary>Index into the class's ability list (0-based) or -1 for none. Consumed once.</summary>
    public sbyte AbilitySlot;
    /// <summary>Entity id of the current target, or -1.</summary>
    public int TargetId;

    public static readonly InputCommand Neutral = new()
    {
        Seq = -1, MoveX = 0, MoveY = 0, AbilitySlot = -1, TargetId = -1,
    };
}
