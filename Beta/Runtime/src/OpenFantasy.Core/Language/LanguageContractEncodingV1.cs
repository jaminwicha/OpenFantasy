namespace OpenFantasy.Core.Language;

/// <summary>Canonical identity bytes for an already validated actor comprehension profile.</summary>
public static class LanguageContractEncodingV1
{
    public const ulong ContractVersion = 1;

    public static byte[] EncodeProfile(ActorComprehensionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return CanonicalEncodingV1.Frame("language.comprehension-profile", Number(ContractVersion),
            profile.ActorId.ToByteArray(), Number(profile.Revision), profile.RulesVersion.ToByteArray(),
            CanonicalEncodingV1.Frame("language.comprehension-entries", profile.Entries.Select(entry =>
                (ReadOnlyMemory<byte>)EncodeEntry(entry)).ToArray()));
    }

    private static byte[] EncodeEntry(LanguageComprehensionEntry entry) =>
        CanonicalEncodingV1.Frame("language.comprehension-entry", EncodeChannel(entry.Channel),
            entry.DefinitionVersion.ToByteArray(), Number((ulong)entry.Band));

    private static byte[] EncodeChannel(LanguageChannelKey channel) =>
        CanonicalEncodingV1.Frame("language.channel", channel.LanguageId.ToByteArray(),
            channel.RegisterId.ToByteArray(), Number((ulong)channel.Mode));

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
}
