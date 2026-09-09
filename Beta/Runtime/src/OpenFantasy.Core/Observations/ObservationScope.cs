namespace OpenFantasy.Core.Observations;

/// <summary>The owner of one knowledge journal, independent of its controller.</summary>
public sealed record ObservationScope(EntityId WorldId, EntityId BranchId, EntityId ActorId);

/// <summary>
/// An exact host-supplied audience binding. Construction is not authentication or a control grant.
/// </summary>
public sealed record ObservationAudience
{
    public ObservationAudience(EntityId principalId, EntityId sessionId, ObservationScope scope,
        ulong controlRevision)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (controlRevision == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(controlRevision));
        }

        PrincipalId = principalId;
        SessionId = sessionId;
        Scope = scope;
        ControlRevision = controlRevision;
    }

    public EntityId PrincipalId { get; }
    public EntityId SessionId { get; }
    public ObservationScope Scope { get; }
    public ulong ControlRevision { get; }
}
