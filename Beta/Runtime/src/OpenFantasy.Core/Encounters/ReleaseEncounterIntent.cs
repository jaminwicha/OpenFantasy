using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Encounters;

/// <summary>Acknowledges an exact outcome before current conditions are checked for release.</summary>
public sealed record ReleaseEncounterIntent(EntityId EncounterId, ContentHash ExpectedActionHash) : WorldActionIntent;
