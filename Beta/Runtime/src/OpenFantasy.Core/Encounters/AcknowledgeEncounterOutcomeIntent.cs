using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Encounters;

/// <summary>
/// A required human's controller reads one exact unfinalized outcome (profile 35). Admitted even when the actor cannot
/// act, because reading is not a bodily action; it grants no movement, ability, care, reward or completion by itself.
/// </summary>
public sealed record AcknowledgeEncounterOutcomeIntent(EntityId EncounterId, ContentHash ExpectedActionHash) : WorldActionIntent;
