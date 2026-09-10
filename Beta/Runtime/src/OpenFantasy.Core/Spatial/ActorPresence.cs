using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Spatial;

/// <summary>Immutable presence of the same actor and current form; no control, inventory, time or placement grant.</summary>
public sealed class ActorPresence
{
    public ActorPresence(ActorFormBinding binding, BodyConditionSnapshot condition, TerrainScope scope,
        SubunitPosition feet, ulong revision, EntityId homePlaceId)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(scope);
        binding.ValidateCondition(condition);
        if (homePlaceId == binding.ActorId || homePlaceId == binding.FormInstanceId
            || homePlaceId == binding.PhysicalBodyId || homePlaceId == binding.TetherAnchorId)
        {
            throw new ArgumentException("A home place cannot alias this actor's instance identities.", nameof(homePlaceId));
        }
        Binding = binding;
        Condition = condition;
        Scope = scope;
        Feet = feet;
        Revision = revision;
        HomePlaceId = homePlaceId;
    }

    public EntityId ActorId => Binding.ActorId;
    public ActorFormBinding Binding { get; }
    public BodyConditionSnapshot Condition { get; }
    public TerrainScope Scope { get; }
    public SubunitPosition Feet { get; }
    public ulong Revision { get; }
    public EntityId HomePlaceId { get; }

    internal ActorPresence At(SubunitPosition feet) =>
        new(Binding, Condition, Scope, feet, checked(Revision + 1), HomePlaceId);
}
