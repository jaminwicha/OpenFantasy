namespace OpenFantasy.Core.Quests;

public enum ActorQuestStatus { Accepted = 1, Completed = 2 }

/// <summary>
/// Persistent participation in one quest. Readiness is computed from current inventory; earlier
/// gifts are not credited. Completion does not imply a structure has been constructed.
/// </summary>
public sealed class ActorQuestState
{
    public ActorQuestState(EntityId actorId, EntityId questId, EntityId acceptanceCommandId,
        WorldInstant acceptedAt, EntityId? completionCommandId = null, WorldInstant? completedAt = null)
    {
        if (completionCommandId.HasValue != completedAt.HasValue
            || completedAt is { } finish && finish < acceptedAt
            || acceptanceCommandId == actorId || acceptanceCommandId == questId
            || completionCommandId == actorId || completionCommandId == questId
            || completionCommandId == acceptanceCommandId)
        {
            throw new ArgumentException("Quest participation requires distinct causes and ordered completion.");
        }
        ActorId = actorId;
        QuestId = questId;
        AcceptanceCommandId = acceptanceCommandId;
        AcceptedAt = acceptedAt;
        CompletionCommandId = completionCommandId;
        CompletedAt = completedAt;
    }

    public EntityId ActorId { get; }
    public EntityId QuestId { get; }
    public EntityId AcceptanceCommandId { get; }
    public WorldInstant AcceptedAt { get; }
    public EntityId? CompletionCommandId { get; }
    public WorldInstant? CompletedAt { get; }
    public ActorQuestStatus Status => CompletionCommandId.HasValue ? ActorQuestStatus.Completed : ActorQuestStatus.Accepted;
    public ulong Revision => (ulong)Status;

    internal ActorQuestState Complete(EntityId commandId, WorldInstant at)
    {
        if (Status != ActorQuestStatus.Accepted)
        {
            throw new InvalidOperationException("A completed quest cannot award another reward.");
        }
        return new ActorQuestState(ActorId, QuestId, AcceptanceCommandId, AcceptedAt, commandId, at);
    }
}
