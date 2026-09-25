using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Encounters;

/// <summary>A request to retain a costed Wait choice; its receipt confirms queuing, not resolution.</summary>
public sealed record QueueEncounterWaitIntent(EntityId EncounterId, ulong ExpectedTimingRevision) : WorldActionIntent;
