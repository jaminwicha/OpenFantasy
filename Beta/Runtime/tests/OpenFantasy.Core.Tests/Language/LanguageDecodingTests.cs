using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Language;

namespace OpenFantasy.Core.Tests.Language;

public sealed class LanguageDecodingTests
{
    [Fact]
    public void All_independent_profiles_restore_exact_bytes_hashes_and_query_results()
    {
        Dictionary<string, ActorComprehensionProfile> profiles = [];
        foreach (JsonElement record in LanguageFixture.Read().GetProperty("profiles").EnumerateArray())
        {
            byte[] encoded = Convert.FromHexString(record.GetProperty("canonicalHex").GetString()!);
            ActorComprehensionProfile restored = LanguageContractDecodingV1.DecodeProfile(encoded);
            Assert.Equal(encoded, LanguageContractEncodingV1.EncodeProfile(restored));
            Assert.Equal(record.GetProperty("sha256").GetString(), restored.ComputeContentHash().Value);
            Assert.Equal(LanguageFixture.Profile(record).Revision, restored.Revision);
            profiles.Add(record.GetProperty("name").GetString()!, restored);
        }
        foreach (JsonElement query in LanguageFixture.Read().GetProperty("queries").EnumerateArray())
        {
            ActorComprehensionProfile profile = profiles[query.GetProperty("profile").GetString()!];
            var requirement = new ComprehensionRequirement(LanguageFixture.Channel(query.GetProperty("channel")),
                new ContentHash(query.GetProperty("definitionVersion").GetString()!),
                (ComprehensionBand)query.GetProperty("minimumBand").GetInt32());
            ComprehensionQueryResult result = profile.Query(profile.ActorId, profile.RulesVersion, requirement);
            Assert.Equal(query.GetProperty("expectedHasRecordedEntry").GetBoolean(), result.HasRecordedEntry);
            Assert.Equal((ComprehensionBand)query.GetProperty("expectedAvailableBand").GetInt32(), result.AvailableBand);
            Assert.Equal(query.GetProperty("expectedMeetsBandRequirement").GetBoolean(), result.MeetsBandRequirement);
        }
    }

    [Fact]
    public void Maximum_256_entry_profile_exactly_fits_the_published_bound()
    {
        ContentHash version = ContentHash.Compute("language-decoding-qualification"u8);
        var profile = new ActorComprehensionProfile(new EntityId(0, 1), ulong.MaxValue, version,
            Enumerable.Range(0, 256).Select(index => new LanguageComprehensionEntry(
                new LanguageChannelKey(new EntityId(0, (ulong)index), new EntityId(0, 99), CommunicationMode.Spoken),
                version, ComprehensionBand.Practical)));
        byte[] encoded = LanguageContractEncodingV1.EncodeProfile(profile);
        Assert.Equal(LanguageContractDecodingV1.MaximumProfileByteLength, encoded.Length);
        ActorComprehensionProfile restored = LanguageContractDecodingV1.DecodeProfile(encoded);
        Assert.Equal(256, restored.Entries.Count);
        Assert.Equal(ulong.MaxValue, restored.Revision);
        Assert.Equal(encoded, LanguageContractEncodingV1.EncodeProfile(restored));
        List<byte[]> entries = Fields(Fields(encoded)[5]);
        AssertBad(Replace(encoded, 5, Frame([.. entries, entries[^1]])));
    }

    [Fact]
    public void Duplicate_and_reordered_channels_reject_even_when_bands_or_pins_change()
    {
        byte[] original = Original();
        List<byte[]> entries = Fields(Fields(original)[5]);
        AssertBad(Replace(original, 5, Frame([entries[0], .. entries.Skip(1).Reverse()])));
        foreach (byte[] duplicate in new[] { entries[1], Replace(entries[1], 2, new byte[32]), Replace(entries[1], 3, Number(0)) })
        {
            AssertBad(Replace(original, 5, Frame([entries[0], entries[1], duplicate])));
        }
    }

    [Fact]
    public void Exact_hash_widths_modes_bands_and_scalar_widths_are_enforced()
    {
        byte[] original = Original();
        AssertBad(Replace(original, 4, new byte[31]));
        AssertBad(Replace(original, 4, new byte[33]));
        AssertBad(Replace(original, 2, new byte[15]));
        AssertBad(Replace(original, 3, new byte[7]));
        byte[] entry = Fields(Fields(original)[5])[1];
        foreach (byte[] malformed in new[] { Replace(entry, 2, new byte[31]), Replace(entry, 2, new byte[33]),
            Replace(entry, 3, Number(4)), Replace(entry, 3, Number(ulong.MaxValue)), Replace(entry, 3, new byte[7]) })
        {
            AssertBad(Replace(original, 5, Frame([Text("language.comprehension-entries"), malformed])));
        }
        byte[] channel = Fields(entry)[1];
        foreach (byte[] malformed in new[] { Replace(channel, 1, new byte[15]), Replace(channel, 2, new byte[17]),
            Replace(channel, 3, Number(0)), Replace(channel, 3, Number(4)), Replace(channel, 3, Number(ulong.MaxValue)) })
        {
            AssertBad(Replace(original, 5, Frame([Text("language.comprehension-entries"), Replace(entry, 1, malformed)])));
        }
    }

    [Fact]
    public void Nested_tags_trailing_bytes_field_counts_and_versions_reject()
    {
        byte[] original = Original();
        List<byte[]> entries = Fields(Fields(original)[5]);
        AssertBad(Replace(original, 5, [.. Fields(original)[5], 0]));
        AssertBad(Replace(original, 5, Frame([Text("language.unknown"), .. entries.Skip(1)])));
        byte[] entry = entries[1];
        byte[] channel = Fields(entry)[1];
        foreach (byte[] malformed in new[] { [.. entry, 0], Frame([.. Fields(entry), Array.Empty<byte>()]),
            Replace(entry, 0, Text("language.unknown")), Replace(entry, 1, [.. channel, 0]),
            Replace(entry, 1, Replace(channel, 0, Text("language.unknown"))) })
        {
            AssertBad(Replace(original, 5, Frame([entries[0], malformed])));
        }
        byte[] tooManyFields = (byte[])Fields(original)[5].Clone();
        BinaryPrimitives.WriteUInt32BigEndian(tooManyFields.AsSpan(8), 258);
        AssertBad(Replace(original, 5, tooManyFields));
    }

    [Fact]
    public void All_prefixes_and_invalid_top_frames_fail_closed()
    {
        byte[] original = Original();
        for (int length = 0; length < original.Length; length++)
        {
            AssertBad(original[..length]);
        }
        AssertBad(new byte[LanguageContractDecodingV1.MaximumProfileByteLength + 1]);
        AssertBad([.. original, 0]);
        AssertBad(Replace(original, 1, Number(2)));
        AssertBad(Replace(original, 0, Text("language.unknown")));
        AssertBad(Frame([.. Fields(original), Array.Empty<byte>()]));
        foreach (int offset in new[] { 0, 4, 8, 12 })
        {
            byte[] mutated = (byte[])original.Clone();
            BinaryPrimitives.WriteUInt32BigEndian(mutated.AsSpan(offset), uint.MaxValue);
            AssertBad(mutated);
        }
    }

    [Fact]
    public void Restored_profile_owns_bytes_preserves_explicit_unfamiliar_and_checks_pins_at_query()
    {
        byte[] original = Original();
        ActorComprehensionProfile restored = LanguageContractDecodingV1.DecodeProfile(original);
        byte[] retained = (byte[])original.Clone();
        Array.Fill(original, (byte)0);
        Assert.Equal(retained, LanguageContractEncodingV1.EncodeProfile(restored));
        Assert.Throws<NotSupportedException>(() => ((IList<LanguageComprehensionEntry>)restored.Entries).Clear());
        LanguageComprehensionEntry unfamiliar = restored.Entries.Single(entry => entry.Band == ComprehensionBand.Unfamiliar);
        ComprehensionQueryResult query = restored.Query(restored.ActorId, restored.RulesVersion,
            new ComprehensionRequirement(unfamiliar.Channel, unfamiliar.DefinitionVersion, ComprehensionBand.Unfamiliar));
        Assert.True(query.HasRecordedEntry);
        Assert.Equal(ComprehensionBand.Unfamiliar, query.AvailableBand);
        ContentHash different = ContentHash.Compute("different-admission-pin"u8);
        Assert.Throws<ArgumentException>(() => restored.Query(restored.ActorId, different,
            new ComprehensionRequirement(unfamiliar.Channel, unfamiliar.DefinitionVersion, ComprehensionBand.Practical)));
        Assert.Throws<ArgumentException>(() => restored.Query(restored.ActorId, restored.RulesVersion,
            new ComprehensionRequirement(unfamiliar.Channel, different, ComprehensionBand.Practical)));
    }

    private static byte[] Original() => Convert.FromHexString(LanguageFixture.Read().GetProperty("profiles")[0].GetProperty("canonicalHex").GetString()!);
    private static void AssertBad(byte[] bytes) => Assert.Throws<FormatException>(() => LanguageContractDecodingV1.DecodeProfile(bytes));
    private static byte[] Text(string value) => Encoding.UTF8.GetBytes(value);
    private static byte[] Number(ulong value)
    {
        byte[] result = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(result, value);
        return result;
    }
    private static List<byte[]> Fields(byte[] bytes)
    {
        uint count = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8));
        int offset = 12;
        List<byte[]> fields = [];
        for (uint index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
            offset += 4;
            fields.Add(bytes.AsSpan(offset, length).ToArray());
            offset += length;
        }
        Assert.Equal(bytes.Length, offset);
        return fields;
    }
    private static byte[] Frame(IEnumerable<byte[]> values)
    {
        byte[][] fields = values.ToArray();
        byte[] bytes = new byte[12 + fields.Sum(field => 4 + field.Length)];
        "OFC\0"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), (uint)fields.Length);
        int offset = 12;
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset), (uint)field.Length);
            offset += 4;
            field.CopyTo(bytes, offset);
            offset += field.Length;
        }
        return bytes;
    }
    private static byte[] Replace(byte[] bytes, int index, byte[] replacement)
    {
        List<byte[]> fields = Fields(bytes);
        fields[index] = replacement;
        return Frame(fields);
    }
}
