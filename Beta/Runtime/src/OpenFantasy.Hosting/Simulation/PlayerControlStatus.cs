using OpenFantasy.Core;

namespace OpenFantasy.Hosting.Simulation;

public enum PlayerControlState : byte { Available, Pending, Restored }

/// <summary>
/// Transient port permission, separate from durable controller assignment and gameplay readiness.
/// A restored remote value binds the first playable observation, not the continually advancing world.
/// </summary>
public sealed record PlayerControlStatus(PlayerControlState State, EntityId PortId, EntityId ActorId,
    ulong Generation, EntityId ObservationStreamId = default, ulong ObservationRevision = 0)
{
    public static PlayerControlStatus Available { get; } = new(PlayerControlState.Available, default, default, 0);
}
