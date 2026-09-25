using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Encounters;

/// <summary>A captured action choice; queue acceptance does not spend resources or execute damage.</summary>
public sealed record QueueEncounterAbilityIntent(EntityId EncounterId, ulong ExpectedTimingRevision,
    EntityId AbilityDefinitionId, EntityId TargetActorId) : WorldActionIntent;
