using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Language;

/// <summary>One actor profile's recorded band under an exact channel-definition pin.</summary>
public sealed record LanguageComprehensionEntry
{
    public LanguageComprehensionEntry(LanguageChannelKey channel, ContentHash definitionVersion, ComprehensionBand band)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(definitionVersion);
        if (!Enum.IsDefined(band))
        {
            throw new ArgumentOutOfRangeException(nameof(band));
        }

        Channel = channel;
        DefinitionVersion = definitionVersion;
        Band = band;
    }

    public LanguageChannelKey Channel { get; }
    public ContentHash DefinitionVersion { get; }
    public ComprehensionBand Band { get; }
}
