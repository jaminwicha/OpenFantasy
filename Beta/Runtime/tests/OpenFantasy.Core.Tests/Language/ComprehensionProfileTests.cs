using OpenFantasy.Core.Content;
using OpenFantasy.Core.Language;

namespace OpenFantasy.Core.Tests.Language;

public sealed class ComprehensionProfileTests
{
    private static readonly ContentHash _definitionVersion = ContentHash.Compute("qualification-channel"u8);
    private static readonly ContentHash _rulesVersion = ContentHash.Compute("qualification-rules"u8);

    [Fact]
    public void CanonicalProfilesMatchIndependentOracleAtInitialAndMaximumRevision()
    {
        foreach (var expected in LanguageFixture.Read().GetProperty("profiles").EnumerateArray())
        {
            ActorComprehensionProfile profile = LanguageFixture.Profile(expected);
            Assert.Equal(expected.GetProperty("canonicalHex").GetString(),
                Convert.ToHexString(LanguageContractEncodingV1.EncodeProfile(profile)).ToLowerInvariant());
            Assert.Equal(expected.GetProperty("sha256").GetString(), profile.ComputeContentHash().Value);
            var reversed = new ActorComprehensionProfile(profile.ActorId, profile.Revision, profile.RulesVersion,
                profile.Entries.Reverse());
            Assert.Equal(profile.ComputeContentHash(), reversed.ComputeContentHash());
        }
    }

    [Fact]
    public void ExactBandQueriesMatchOracleWithoutLanguageRegisterModeOrActorFallback()
    {
        foreach (var expected in LanguageFixture.Read().GetProperty("queries").EnumerateArray())
        {
            ActorComprehensionProfile profile = LanguageFixture.Profile(expected.GetProperty("profile").GetString()!);
            var requirement = new ComprehensionRequirement(LanguageFixture.Channel(expected.GetProperty("channel")),
                new ContentHash(expected.GetProperty("definitionVersion").GetString()!),
                (ComprehensionBand)expected.GetProperty("minimumBand").GetInt32());
            ComprehensionQueryResult result = profile.Query(profile.ActorId, profile.RulesVersion, requirement);
            Assert.Equal((ComprehensionBand)expected.GetProperty("expectedAvailableBand").GetInt32(), result.AvailableBand);
            Assert.Equal(expected.GetProperty("expectedHasRecordedEntry").GetBoolean(), result.HasRecordedEntry);
            Assert.Equal(expected.GetProperty("expectedMeetsBandRequirement").GetBoolean(), result.MeetsBandRequirement);
            Assert.Equal(profile.ActorId, result.ActorId);
            Assert.Equal(profile.Revision, result.ProfileRevision);
            Assert.Equal(profile.RulesVersion, result.RulesVersion);
            Assert.Same(requirement, result.Requirement);
        }
    }

    [Fact]
    public void EveryBandThresholdHasExplicitInclusiveComparison()
    {
        foreach (ComprehensionBand available in Enum.GetValues<ComprehensionBand>())
        {
            var profile = new ActorComprehensionProfile(default, 0, _rulesVersion, [Entry(1, available)]);
            foreach (ComprehensionBand required in Enum.GetValues<ComprehensionBand>())
            {
                ComprehensionQueryResult result = profile.Query(default, _rulesVersion,
                    new ComprehensionRequirement(Channel(1), _definitionVersion, required));
                Assert.Equal(available >= required, result.MeetsBandRequirement);
                Assert.Equal(available, result.AvailableBand);
                Assert.True(result.HasRecordedEntry);
            }
        }
    }

    [Fact]
    public void WrongActorRulesAndRecordedDefinitionPinsRejectBeforeComparison()
    {
        ActorComprehensionProfile profile = LanguageFixture.Profile("actor-a");
        LanguageComprehensionEntry entry = profile.Entries[0];
        var requirement = new ComprehensionRequirement(entry.Channel, entry.DefinitionVersion, ComprehensionBand.Practical);
        Assert.Throws<ArgumentException>(() => profile.Query(new EntityId(0, 999), profile.RulesVersion, requirement));
        ContentHash stale = ContentHash.Compute("different pin"u8);
        Assert.Throws<ArgumentException>(() => profile.Query(profile.ActorId, stale, requirement));
        Assert.Throws<ArgumentException>(() => profile.Query(profile.ActorId, profile.RulesVersion,
            new ComprehensionRequirement(entry.Channel, stale, ComprehensionBand.Unfamiliar)));
        LanguageComprehensionEntry unfamiliar = profile.Entries.Single(value => value.Band == ComprehensionBand.Unfamiliar);
        Assert.Throws<ArgumentException>(() => profile.Query(profile.ActorId, profile.RulesVersion,
            new ComprehensionRequirement(unfamiliar.Channel, stale, ComprehensionBand.Unfamiliar)));
        Assert.Throws<ArgumentNullException>(() => profile.Query(profile.ActorId, null!, requirement));
        Assert.Throws<ArgumentNullException>(() => profile.Query(profile.ActorId, profile.RulesVersion, null!));
    }

    [Fact]
    public void DuplicateChannelRejectsEvenWhenBandOrDefinitionPinDiffers()
    {
        LanguageComprehensionEntry original = Entry(1);
        Assert.Throws<ArgumentException>(() => new ActorComprehensionProfile(default, 0, _rulesVersion, [original, original]));
        Assert.Throws<ArgumentException>(() => new ActorComprehensionProfile(default, 0, _rulesVersion,
            [original, new LanguageComprehensionEntry(original.Channel, _definitionVersion, ComprehensionBand.Nuanced)]));
        Assert.Throws<ArgumentException>(() => new ActorComprehensionProfile(default, 0, _rulesVersion,
            [original, new LanguageComprehensionEntry(original.Channel, ContentHash.Compute("changed"u8), original.Band)]));
    }

    [Fact]
    public void StableOrderingIncludesEveryKeyDimensionAndCanonicalEntityBytes()
    {
        LanguageChannelKey[] keys =
        [
            new(new EntityId(1, 0), new EntityId(0, 1), CommunicationMode.Spoken),
            new(new EntityId(0, ulong.MaxValue), new EntityId(0, 1), CommunicationMode.Spoken),
            new(new EntityId(0, 1), new EntityId(0, 2), CommunicationMode.Spoken),
            new(new EntityId(0, 1), new EntityId(0, 1), CommunicationMode.Written),
            new(new EntityId(0, 1), new EntityId(0, 1), CommunicationMode.Signed),
            new(new EntityId(0, 1), new EntityId(0, 1), CommunicationMode.Spoken)
        ];
        var profile = new ActorComprehensionProfile(default, 0, _rulesVersion,
            keys.Select(key => new LanguageComprehensionEntry(key, _definitionVersion, ComprehensionBand.Practical)));
        Assert.Equal(keys.Reverse(), profile.Entries.Select(entry => entry.Channel));
        Assert.True(keys[0].CompareTo(null) > 0);
        Assert.Equal(0, Channel(1).CompareTo(Channel(1)));
    }

    [Fact]
    public void ProfileAndResultProtectCopiedStateAndEncodingBuffers()
    {
        LanguageComprehensionEntry[] source = [Entry(1)];
        var profile = new ActorComprehensionProfile(default, 4, _rulesVersion, source);
        var requirement = new ComprehensionRequirement(Channel(1), _definitionVersion, ComprehensionBand.Practical);
        ComprehensionQueryResult result = profile.Query(default, _rulesVersion, requirement);
        source[0] = Entry(1, ComprehensionBand.Nuanced);
        Assert.Equal(ComprehensionBand.Practical, Assert.Single(profile.Entries).Band);
        Assert.Equal(ComprehensionBand.Practical, result.AvailableBand);
        Assert.Throws<NotSupportedException>(() => ((IList<LanguageComprehensionEntry>)profile.Entries).Clear());
        byte[] bytes = LanguageContractEncodingV1.EncodeProfile(profile);
        bytes.AsSpan().Clear();
        Assert.NotEqual(bytes, LanguageContractEncodingV1.EncodeProfile(profile));
    }

    [Fact]
    public void ProfileIdentityBindsActorRevisionRulesDefinitionAndBand()
    {
        var profile = new ActorComprehensionProfile(default, 0, _rulesVersion, [Entry(1)]);
        ContentHash baseline = profile.ComputeContentHash();
        Assert.NotEqual(baseline, new ActorComprehensionProfile(new EntityId(0, 1), 0, _rulesVersion, profile.Entries).ComputeContentHash());
        Assert.NotEqual(baseline, new ActorComprehensionProfile(default, 1, _rulesVersion, profile.Entries).ComputeContentHash());
        Assert.NotEqual(baseline, new ActorComprehensionProfile(default, 0, ContentHash.Compute("changed"u8), profile.Entries).ComputeContentHash());
        Assert.NotEqual(baseline, new ActorComprehensionProfile(default, 0, _rulesVersion, [Entry(1, ComprehensionBand.Nuanced)]).ComputeContentHash());
        Assert.NotEqual(baseline, new ActorComprehensionProfile(default, 0, _rulesVersion,
            [new LanguageComprehensionEntry(Channel(1), ContentHash.Compute("changed"u8), ComprehensionBand.Practical)]).ComputeContentHash());
    }

    [Fact]
    public void MissingChannelHasNoLearnedBandButCanMeetAnExplicitZeroPrerequisite()
    {
        var profile = new ActorComprehensionProfile(default, 0, _rulesVersion, []);
        ComprehensionQueryResult result = profile.Query(default, _rulesVersion,
            new ComprehensionRequirement(Channel(1), _definitionVersion, ComprehensionBand.Unfamiliar));
        Assert.Equal(ComprehensionBand.Unfamiliar, result.AvailableBand);
        Assert.False(result.HasRecordedEntry);
        Assert.True(result.MeetsBandRequirement);
        Assert.Empty(profile.Entries);
    }

    [Fact]
    public void FiniteProfileLimitRejectsExcessInputWithoutConsumingAnEndlessSequence()
    {
        int yielded = 0;
        IEnumerable<LanguageComprehensionEntry> Endless()
        {
            while (true)
            {
                yielded++;
                yield return Entry((ulong)yielded);
            }
        }
        Assert.Throws<ArgumentException>(() => new ActorComprehensionProfile(default, 0, _rulesVersion, Endless()));
        Assert.Equal(257, yielded);
        var maximum = new ActorComprehensionProfile(default, 0, _rulesVersion,
            Enumerable.Range(1, ActorComprehensionProfile.MaximumEntryCount).Select(index => Entry((ulong)index)));
        Assert.Equal(256, maximum.Entries.Count);
        Assert.NotEmpty(LanguageContractEncodingV1.EncodeProfile(maximum));
    }

    [Fact]
    public void UnknownModesBandsAndNullInputsReject()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LanguageChannelKey(default, default, (CommunicationMode)0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LanguageChannelKey(default, default, (CommunicationMode)4));
        Assert.Throws<ArgumentOutOfRangeException>(() => Entry(1, (ComprehensionBand)(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Entry(1, (ComprehensionBand)4));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ComprehensionRequirement(Channel(1), _definitionVersion, (ComprehensionBand)4));
        Assert.Throws<ArgumentNullException>(() => new LanguageComprehensionEntry(null!, _definitionVersion, ComprehensionBand.Practical));
        Assert.Throws<ArgumentNullException>(() => new LanguageComprehensionEntry(Channel(1), null!, ComprehensionBand.Practical));
        Assert.Throws<ArgumentNullException>(() => new ComprehensionRequirement(null!, _definitionVersion, ComprehensionBand.Practical));
        Assert.Throws<ArgumentNullException>(() => new ActorComprehensionProfile(default, 0, null!, []));
        Assert.Throws<ArgumentNullException>(() => new ActorComprehensionProfile(default, 0, _rulesVersion, null!));
        Assert.Throws<ArgumentNullException>(() => new ActorComprehensionProfile(default, 0, _rulesVersion, [null!]));
    }

    private static LanguageChannelKey Channel(ulong language) => new(new EntityId(0, language), new EntityId(0, 10), CommunicationMode.Spoken);
    private static LanguageComprehensionEntry Entry(ulong language, ComprehensionBand band = ComprehensionBand.Practical) =>
        new(Channel(language), _definitionVersion, band);
}
