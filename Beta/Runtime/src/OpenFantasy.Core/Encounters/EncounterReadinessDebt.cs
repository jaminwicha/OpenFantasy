namespace OpenFantasy.Core.Encounters;

/// <summary>Retained tactical-unit debt for one actor, including bystanders and incapacitated actors.</summary>
public sealed record EncounterReadinessDebt(EntityId ActorId, ulong RemainingDelay);
