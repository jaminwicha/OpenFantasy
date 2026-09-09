namespace OpenFantasy.Core.Language;

/// <summary>One exact language, register and communication mode; no implicit related-channel fallback.</summary>
public sealed record LanguageChannelKey : IComparable<LanguageChannelKey>
{
    public LanguageChannelKey(EntityId languageId, EntityId registerId, CommunicationMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        LanguageId = languageId;
        RegisterId = registerId;
        Mode = mode;
    }

    public EntityId LanguageId { get; }
    public EntityId RegisterId { get; }
    public CommunicationMode Mode { get; }

    public int CompareTo(LanguageChannelKey? other)
    {
        if (other is null)
        {
            return 1;
        }

        int comparison = LanguageId.CompareTo(other.LanguageId);
        if (comparison == 0)
        {
            comparison = RegisterId.CompareTo(other.RegisterId);
        }
        if (comparison == 0)
        {
            comparison = Mode.CompareTo(other.Mode);
        }

        return comparison;
    }
}
