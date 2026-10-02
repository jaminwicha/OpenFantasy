namespace OpenFantasy.Core.Worlds;

public enum WorldControllerEventKind { Disconnected = 1, AllowanceExpired = 2, Reconnected = 3 }
public enum WorldControllerDisconnectReason { PeerClosed = 1, TransportLost = 2, ProtocolFailure = 3 }

/// <summary>A host-observed controller event. Its elapsed duration is evidence, never simulation time.</summary>
public sealed record WorldControllerEvent
{
    public WorldControllerEvent(EntityId actorId, WorldControllerEventKind kind, ulong expectedControlRevision,
        EntityId? disconnectId = null, ulong elapsedHostMilliseconds = 0, WorldControllerDisconnectReason? reason = null)
    {
        if (!Enum.IsDefined(kind) || reason is { } value && !Enum.IsDefined(value))
            throw new ArgumentException("A controller event requires known event and disconnect reason kinds.");
        bool valid = kind switch
        {
            WorldControllerEventKind.Disconnected => reason is not null && disconnectId is null && elapsedHostMilliseconds == 0,
            WorldControllerEventKind.AllowanceExpired => reason is null && disconnectId is not null
                && elapsedHostMilliseconds >= WorldControllerState.ReconnectAllowanceMilliseconds,
            WorldControllerEventKind.Reconnected => reason is null && disconnectId is not null && elapsedHostMilliseconds == 0,
            _ => false
        };
        if (!valid || disconnectId == actorId)
            throw new ArgumentException("Controller event fields do not match the event's bounded interpretation.");
        ActorId = actorId;
        Kind = kind;
        ExpectedControlRevision = expectedControlRevision;
        DisconnectId = disconnectId;
        ElapsedHostMilliseconds = elapsedHostMilliseconds;
        Reason = reason;
    }

    public EntityId ActorId { get; }
    public WorldControllerEventKind Kind { get; }
    public ulong ExpectedControlRevision { get; }
    public EntityId? DisconnectId { get; }
    public ulong ElapsedHostMilliseconds { get; }
    public WorldControllerDisconnectReason? Reason { get; }
}
