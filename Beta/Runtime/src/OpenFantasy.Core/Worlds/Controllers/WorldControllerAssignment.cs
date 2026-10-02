namespace OpenFantasy.Core.Worlds;

public enum WorldControllerMode { Human = 1, ReconnectAllowance = 2, Autonomous = 3 }

/// <summary>Current controller interpretation for one admitted human actor, without duplicating credentials or ownership.</summary>
public sealed record WorldControllerAssignment
{
    public WorldControllerAssignment(EntityId actorId, ulong revision, WorldControllerMode mode, EntityId? disconnectId)
    {
        if (!Enum.IsDefined(mode) || (mode == WorldControllerMode.Human) != (disconnectId is null)
            || disconnectId == actorId || revision == 0 && mode != WorldControllerMode.Human
            || revision == 1 && mode != WorldControllerMode.ReconnectAllowance)
        {
            throw new ArgumentException("Controller mode, revision and disconnect episode violate the assignment's structural requirements.");
        }
        ActorId = actorId;
        Revision = revision;
        Mode = mode;
        DisconnectId = disconnectId;
    }

    public EntityId ActorId { get; }
    public ulong Revision { get; }
    public WorldControllerMode Mode { get; }
    public EntityId? DisconnectId { get; }
}
