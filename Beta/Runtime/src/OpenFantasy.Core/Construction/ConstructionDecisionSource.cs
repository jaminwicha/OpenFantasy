using OpenFantasy.Core.Content;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Construction;

internal abstract class ConstructionDecisionSource
{
    internal abstract EntityId WorldId { get; }
    internal abstract EntityId BranchId { get; }
    internal abstract bool HasReadingHold { get; }
    internal abstract bool HasProgressionAndQuests { get; }
    internal abstract bool Authorizes(WorldActionCommand command);
    internal abstract int CurrentHp(EntityId actorId);
    internal abstract EntityId InventoryCustodian(EntityId actorId);
    internal abstract bool QuestCompleted(EntityId questId);
    internal abstract bool ContainsIdentity(EntityId identity);
}

internal sealed class LiveConstructionDecisionSource(WorldSnapshot world) : ConstructionDecisionSource
{
    internal override EntityId WorldId => world.WorldId;
    internal override EntityId BranchId => world.BranchId;
    internal override bool HasReadingHold => world.HasReadingHold;
    internal override bool HasProgressionAndQuests => world.Progression is not null && world.Quests is not null;
    internal override bool Authorizes(WorldActionCommand command) => WorldActorCommandAuthority.AuthorizeSelection(world, command) is not null;
    internal override int CurrentHp(EntityId actorId) => world.Progression!.GetActor(actorId).CurrentHp;
    internal override EntityId InventoryCustodian(EntityId actorId) => world.GetResident(actorId).InventoryCustodianId;
    internal override bool QuestCompleted(EntityId questId) => world.Quests!.Participations.Any(value =>
        value.QuestId == questId && value.Status == ActorQuestStatus.Completed);
    internal override bool ContainsIdentity(EntityId identity) => world.IdentityIndex.Contains(identity);
}

/// <summary>Copied decision facts, not a world/index reference or an independently advancing authority.</summary>
internal sealed class DetachedConstructionDecisionSource : ConstructionDecisionSource
{
    private readonly ContentHash _commandHash;
    private readonly EntityId _actorId;
    private readonly EntityId _custodianId;
    private readonly int _hitPoints;
    private readonly bool _authorized;
    private readonly HashSet<EntityId> _completedQuests;
    private readonly HashSet<EntityId> _identities;
    private readonly HashSet<ulong> _families;

    internal DetachedConstructionDecisionSource(WorldSnapshot world, WorldActionCommand command)
    {
        WorldId = world.WorldId;
        BranchId = world.BranchId;
        HasReadingHold = world.HasReadingHold;
        HasProgressionAndQuests = world.Progression is not null && world.Quests is not null;
        _commandHash = command.ContentHash;
        _actorId = command.ActorId;
        _authorized = WorldActorCommandAuthority.AuthorizeSelection(world, command) is not null;
        _custodianId = world.GetResident(command.ActorId).InventoryCustodianId;
        _hitPoints = world.Progression?.GetActor(command.ActorId).CurrentHp ?? 0;
        _completedQuests = world.Quests?.Participations.Where(value => value.Status == ActorQuestStatus.Completed)
            .Select(value => value.QuestId).ToHashSet() ?? [];
        var reservations = world.IdentityIndex.CaptureReservations();
        _identities = reservations.Identities.ToHashSet();
        _families = reservations.Families.ToHashSet();
    }

    internal override EntityId WorldId { get; }
    internal override EntityId BranchId { get; }
    internal override bool HasReadingHold { get; }
    internal override bool HasProgressionAndQuests { get; }
    internal override bool Authorizes(WorldActionCommand command) => _authorized && command.ContentHash == _commandHash;
    internal override int CurrentHp(EntityId actorId) => actorId == _actorId ? _hitPoints
        : throw new ArgumentException("The actor is outside this construction decision.", nameof(actorId));
    internal override EntityId InventoryCustodian(EntityId actorId) => actorId == _actorId ? _custodianId
        : throw new ArgumentException("The actor is outside this construction decision.", nameof(actorId));
    internal override bool QuestCompleted(EntityId questId) => _completedQuests.Contains(questId);
    internal override bool ContainsIdentity(EntityId identity) => _identities.Contains(identity) || _families.Contains(identity.High);
}
