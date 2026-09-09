using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Language;

/// <summary>
/// A narrow profile comparison result. It contains no other channels, translated text,
/// knowledge facts, membership or right to observe an utterance.
/// </summary>
public sealed class ComprehensionQueryResult
{
    internal ComprehensionQueryResult(EntityId actorId, ulong profileRevision, ContentHash rulesVersion,
        ComprehensionRequirement requirement, ComprehensionBand availableBand, bool hasRecordedEntry)
    {
        ActorId = actorId;
        ProfileRevision = profileRevision;
        RulesVersion = rulesVersion;
        Requirement = requirement;
        AvailableBand = availableBand;
        HasRecordedEntry = hasRecordedEntry;
    }

    public EntityId ActorId { get; }
    public ulong ProfileRevision { get; }
    public ContentHash RulesVersion { get; }
    public ComprehensionRequirement Requirement { get; }
    public ComprehensionBand AvailableBand { get; }
    public bool HasRecordedEntry { get; }
    public bool MeetsBandRequirement => AvailableBand >= Requirement.MinimumBand;
}
