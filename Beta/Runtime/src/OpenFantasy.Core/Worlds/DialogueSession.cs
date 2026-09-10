namespace OpenFantasy.Core.Worlds;

/// <summary>A committed addressed utterance and shared reading hold; closing it does not erase its journal event.</summary>
public sealed record DialogueSession
{
    public DialogueSession(EntityId dialogueId, EntityId listenerActorId, EntityId speakerActorId,
        EntityId utteranceDefinitionId, EntityId causeId, WorldInstant startedAt)
    {
        if (listenerActorId == speakerActorId || dialogueId == listenerActorId || dialogueId == speakerActorId
            || dialogueId == utteranceDefinitionId || dialogueId == causeId)
        {
            throw new ArgumentException("Dialogue roles require distinct identities.");
        }
        DialogueId = dialogueId;
        ListenerActorId = listenerActorId;
        SpeakerActorId = speakerActorId;
        UtteranceDefinitionId = utteranceDefinitionId;
        CauseId = causeId;
        StartedAt = startedAt;
    }

    public EntityId DialogueId { get; }
    public EntityId ListenerActorId { get; }
    public EntityId SpeakerActorId { get; }
    public EntityId UtteranceDefinitionId { get; }
    public EntityId CauseId { get; }
    public WorldInstant StartedAt { get; }
}
