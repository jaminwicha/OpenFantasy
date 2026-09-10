using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Language;

/// <summary>Restores exact actor-owned language metadata; performs no learning or content admission.</summary>
public static class LanguageContractDecodingV1
{
    public const int MaximumProfileByteLength = 47_280;
    private const int MaximumEntriesByteLength = 47_150;
    private const int MaximumEntryByteLength = 180;
    private const int MaximumChannelByteLength = 84;

    public static ActorComprehensionProfile DecodeProfile(ReadOnlySpan<byte> bytes)
    {
        try
        {
            LanguageContractFrameReader reader = new(bytes, MaximumProfileByteLength, 6, "language.comprehension-profile"u8);
            if (reader.ReadUnsigned64() != LanguageContractEncodingV1.ContractVersion)
            {
                throw new FormatException("The language profile version is unsupported.");
            }

            EntityId actor = reader.ReadIdentity();
            ulong revision = reader.ReadUnsigned64();
            ContentHash rules = reader.ReadHash();
            List<LanguageComprehensionEntry> entries = ReadEntries(reader.ReadField(MaximumEntriesByteLength));
            reader.Complete();
            return new ActorComprehensionProfile(actor, revision, rules, entries);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException("The language profile violates channel or comprehension invariants.", exception);
        }
    }

    private static List<LanguageComprehensionEntry> ReadEntries(ReadOnlySpan<byte> bytes)
    {
        LanguageContractFrameReader reader = new(bytes, MaximumEntriesByteLength, ActorComprehensionProfile.MaximumEntryCount + 1,
            "language.comprehension-entries"u8);
        List<LanguageComprehensionEntry> entries = [];
        while (reader.RemainingFields > 0)
        {
            LanguageContractFrameReader entryReader = new(reader.ReadField(MaximumEntryByteLength), MaximumEntryByteLength, 4,
                "language.comprehension-entry"u8);
            LanguageChannelKey channel = ReadChannel(entryReader.ReadField(MaximumChannelByteLength));
            ContentHash version = entryReader.ReadHash();
            ulong band = entryReader.ReadUnsigned64();
            if (band > 3)
            {
                throw new FormatException("The comprehension band is unsupported.");
            }

            entryReader.Complete();
            if (entries.Count > 0 && entries[^1].Channel.CompareTo(channel) >= 0)
            {
                throw new FormatException("Language channels must be unique and canonically ordered.");
            }

            entries.Add(new LanguageComprehensionEntry(channel, version, (ComprehensionBand)band));
        }

        reader.Complete();
        return entries;
    }

    private static LanguageChannelKey ReadChannel(ReadOnlySpan<byte> bytes)
    {
        LanguageContractFrameReader reader = new(bytes, MaximumChannelByteLength, 4, "language.channel"u8);
        EntityId language = reader.ReadIdentity();
        EntityId register = reader.ReadIdentity();
        ulong mode = reader.ReadUnsigned64();
        if (mode is < 1 or > 3)
        {
            throw new FormatException("The communication mode is unsupported.");
        }

        reader.Complete();
        return new LanguageChannelKey(language, register, (CommunicationMode)mode);
    }
}
