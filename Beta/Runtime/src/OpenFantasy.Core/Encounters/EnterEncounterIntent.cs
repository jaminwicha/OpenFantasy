using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Encounters;

/// <summary>Requests one admitted confrontation without resetting actor recovery or earning time.</summary>
public sealed record EnterEncounterIntent(EntityId ScenarioId, EntityId EncounterId,
    ulong ExpectedEncounterCount, ulong ExpectedTargetRevision) : WorldActionIntent;
