namespace OpenFantasy.Core.Cognition;

/// <summary>
/// The cognition facet of one world branch: every actor's typed cognition, keyed by world, branch, role cognition
/// and actor identity. Actors are held once each in canonical identity order. The facet is bounded like the local
/// spatial snapshot it accompanies.
/// </summary>
public sealed class CognitionSnapshot
{
    public const int MaximumActors = 64;

    public CognitionSnapshot(EntityId worldId, EntityId branchId, ulong revision, IEnumerable<ActorCognition> actors)
    {
        ArgumentNullException.ThrowIfNull(actors);
        if (worldId == branchId) throw new ArgumentException("A world and its branch have distinct identities.");
        WorldId = worldId;
        BranchId = branchId;
        Revision = revision;
        Actors = actors.OrderBy(actor => actor.ActorId).ToList().AsReadOnly();
        if (Actors.Count > MaximumActors) throw new ArgumentException("The cognition facet exceeds its actor bound.");
        if (Actors.Select(actor => actor.ActorId).Distinct().Count() != Actors.Count)
            throw new ArgumentException("Each actor has one cognition owner.");
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public ulong Revision { get; }
    public IReadOnlyList<ActorCognition> Actors { get; }

    public ActorCognition For(EntityId actorId) =>
        Actors.FirstOrDefault(actor => actor.ActorId == actorId) ?? ActorCognition.Empty(actorId);

    /// <summary>Replaces one actor's cognition and advances the facet revision.</summary>
    public CognitionSnapshot With(ActorCognition cognition)
    {
        ArgumentNullException.ThrowIfNull(cognition);
        return new CognitionSnapshot(WorldId, BranchId, checked(Revision + 1),
            Actors.Where(actor => actor.ActorId != cognition.ActorId).Append(cognition));
    }
}
