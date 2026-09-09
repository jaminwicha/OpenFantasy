using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Language;

/// <summary>
/// A declared band prerequisite, not an utterance or a promise of successful understanding.
/// Unfamiliar declares no learned-comprehension prerequisite.
/// </summary>
public sealed record ComprehensionRequirement
{
    public ComprehensionRequirement(LanguageChannelKey channel, ContentHash definitionVersion,
        ComprehensionBand minimumBand)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(definitionVersion);
        if (!Enum.IsDefined(minimumBand))
        {
            throw new ArgumentOutOfRangeException(nameof(minimumBand));
        }

        Channel = channel;
        DefinitionVersion = definitionVersion;
        MinimumBand = minimumBand;
    }

    public LanguageChannelKey Channel { get; }
    public ContentHash DefinitionVersion { get; }
    public ComprehensionBand MinimumBand { get; }
}
