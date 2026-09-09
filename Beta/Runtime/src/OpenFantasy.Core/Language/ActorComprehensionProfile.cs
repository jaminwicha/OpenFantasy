using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Language;

/// <summary>
/// Immutable comprehension metadata owned by one actor. Candidate construction does not
/// teach languages, validate learning causes, authenticate a viewer or commit knowledge.
/// </summary>
public sealed class ActorComprehensionProfile
{
    public const int MaximumEntryCount = 256;
    private readonly Dictionary<LanguageChannelKey, LanguageComprehensionEntry> _entriesByChannel;

    public ActorComprehensionProfile(EntityId actorId, ulong revision, ContentHash rulesVersion,
        IEnumerable<LanguageComprehensionEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(rulesVersion);
        ArgumentNullException.ThrowIfNull(entries);
        _entriesByChannel = [];
        foreach (LanguageComprehensionEntry entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry, nameof(entries));
            if (_entriesByChannel.Count == MaximumEntryCount || !_entriesByChannel.TryAdd(entry.Channel, entry))
            {
                throw new ArgumentException("A comprehension profile requires at most 256 unique channel entries.", nameof(entries));
            }
        }

        ActorId = actorId;
        Revision = revision;
        RulesVersion = rulesVersion;
        Entries = Array.AsReadOnly(_entriesByChannel.Values.OrderBy(entry => entry.Channel).ToArray());
    }

    public EntityId ActorId { get; }
    public ulong Revision { get; }
    public ContentHash RulesVersion { get; }
    public ReadOnlyCollection<LanguageComprehensionEntry> Entries { get; }

    /// <summary>
    /// Checks asserted subject and exact pins before comparing one channel. Subject matching
    /// is local consistency, not authenticated requester authorization or a visibility filter.
    /// </summary>
    public ComprehensionQueryResult Query(EntityId subjectActorId, ContentHash expectedRulesVersion,
        ComprehensionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(expectedRulesVersion);
        ArgumentNullException.ThrowIfNull(requirement);
        if (subjectActorId != ActorId)
        {
            throw new ArgumentException("The profile belongs to a different subject actor.", nameof(subjectActorId));
        }
        if (expectedRulesVersion != RulesVersion)
        {
            throw new ArgumentException("The comprehension rules do not match the profile's pinned version.", nameof(expectedRulesVersion));
        }

        bool recorded = _entriesByChannel.TryGetValue(requirement.Channel, out LanguageComprehensionEntry? entry);
        if (recorded && entry!.DefinitionVersion != requirement.DefinitionVersion)
        {
            throw new ArgumentException("The recorded channel definition does not match the requested pin.", nameof(requirement));
        }

        ComprehensionBand available = entry?.Band ?? ComprehensionBand.Unfamiliar;
        return new ComprehensionQueryResult(ActorId, Revision, RulesVersion, requirement, available, recorded);
    }

    public ContentHash ComputeContentHash() => ContentHash.Compute(LanguageContractEncodingV1.EncodeProfile(this));
}
