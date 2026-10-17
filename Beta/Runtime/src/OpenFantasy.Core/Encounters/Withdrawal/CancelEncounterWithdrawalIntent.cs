using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Encounters;

/// <summary>Ends only this actor's exact standing route; it grants no movement, readiness or replacement action.</summary>
public sealed record CancelEncounterWithdrawalIntent(EntityId EncounterId, ulong ExpectedTimingRevision,
    EntityId WithdrawalCommandId) : WorldActionIntent;
