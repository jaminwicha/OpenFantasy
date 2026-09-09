using System.Globalization;
using System.Text;

namespace OpenFantasy.Core.Tests;

public sealed class EntityIdTests
{
    [Fact]
    public void GeneratedIdsMatchIndependentGoldenVectors()
    {
        var vectors = ReferenceVectors.Read();
        byte[] seed = Convert.FromHexString(vectors.GetProperty("seedHex").GetString()!);
        foreach (var value in vectors.GetProperty("generated").EnumerateArray())
        {
            EntityId actual = EntityId.DeriveGenerated(seed, value.GetProperty("version").GetString()!,
                value.GetProperty("address").GetString()!, value.GetProperty("role").GetString()!,
                ReferenceVectors.Unsigned(value.GetProperty("ordinal")));
            Assert.Equal(value.GetProperty("id").GetString(), actual.ToString());
        }
    }

    [Fact]
    public void SpawnedIdsMatchIndependentGoldenVector()
    {
        var vectors = ReferenceVectors.Read();
        EntityId cause = EntityId.Parse(vectors.GetProperty("responsibleId").GetString()!);
        var value = vectors.GetProperty("spawned");
        Assert.Equal(value.GetProperty("id").GetString(),
            EntityId.DeriveSpawned(cause, ReferenceVectors.Unsigned(value.GetProperty("ordinal"))).ToString());
    }

    [Fact]
    public void CanonicalBytesAndOrderingDoNotUseGuidMixedEndianOrder()
    {
        EntityId first = EntityId.Parse("00112233445566778899aabbccddeeff");
        Assert.Equal("00112233445566778899AABBCCDDEEFF", Convert.ToHexString(first.ToByteArray()));
        Assert.Equal(first, EntityId.FromBytes(first.ToByteArray()));
        EntityId low = EntityId.Parse("00000001000000000000000000000000");
        EntityId high = EntityId.Parse("00000100000000000000000000000000");
        Assert.True(low < high);
        Assert.True(new EntityId(0, ulong.MaxValue) < new EntityId(1, 0));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("00112233-4455-6677-8899-aabbccddeeff")]
    [InlineData("00112233445566778899AABBCCDDEEFF")]
    [InlineData("00112233445566778899aabbccddeefg")]
    [InlineData(" 00112233445566778899aabbccddeeff")]
    public void NoncanonicalTextIsRejected(string? value)
    {
        Assert.False(EntityId.TryParse(value, out _));
        Assert.Throws<FormatException>(() => EntityId.Parse(value!));
    }

    [Fact]
    public void EveryBitPatternRoundTripsIncludingZeroAndAllOnes()
    {
        foreach (var id in new[] { default(EntityId), new EntityId(ulong.MaxValue, ulong.MaxValue),
                     new EntityId(0x8000000000000000, 0x0102030405060708) })
            Assert.Equal(id, EntityId.Parse(id.ToString()));
    }

    [Fact]
    public void ByteLengthIsExact()
    {
        Assert.Throws<ArgumentException>(() => EntityId.FromBytes(new byte[15]));
        Assert.Throws<ArgumentException>(() => EntityId.FromBytes(new byte[17]));
        Assert.Throws<ArgumentException>(() => default(EntityId).WriteBytes(new byte[15]));
    }

    [Fact]
    public void InvalidUnicodeAndMissingDerivationFieldsAreRejected()
    {
        Assert.Throws<EncoderFallbackException>(() => EntityId.DeriveGenerated([1], "v1", "\ud800", "actor", 0));
        Assert.Throws<ArgumentException>(() => EntityId.DeriveGenerated([], "v1", "x", "actor", 0));
        Assert.Throws<ArgumentException>(() => EntityId.DeriveGenerated([1], "", "x", "actor", 0));
    }

    [Fact]
    public void TextAndDerivationAreCultureIndependent()
    {
        CultureInfo prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal("00112233445566778899aabbccddeeff", EntityId.Parse("00112233445566778899aabbccddeeff").ToString());
            GeneratedIdsMatchIndependentGoldenVectors();
        }
        finally { CultureInfo.CurrentCulture = prior; }
    }
}
