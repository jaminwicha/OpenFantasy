using System.Buffers.Binary;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Markets;
using static OpenFantasy.Core.Tests.Markets.SpotExchangeFixture;

namespace OpenFantasy.Core.Tests.Markets;

public sealed class SpotExchangeCodecBoundaryTests
{
    [Theory]
    [InlineData(1, 2, AuthorityFormatFailure.UnsupportedVersion)]
    [InlineData(9, 9, AuthorityFormatFailure.UnknownType)]
    [InlineData(10, 9, AuthorityFormatFailure.UnknownType)]
    [InlineData(22, 1_000_001, AuthorityFormatFailure.InvalidValue)]
    [InlineData(26, 5, AuthorityFormatFailure.LimitExceeded)]
    public void Quote_decoder_refuses_unknown_versions_types_and_limits(int field, int value, AuthorityFormatFailure failure)
    {
        var bytes = SpotExchangeCodecV1.Encode(new SpotExchangeFixture().BerryOffer());
        Assert.Equal(failure, Assert.Throws<AuthorityFormatException>(() => SpotExchangeCodecV1.DecodeQuote(
            Rewrite(bytes, [field], Number((ulong)value)))).Failure);
        Assert.Equal(AuthorityFormatFailure.LimitExceeded, Assert.Throws<AuthorityFormatException>(() =>
            SpotExchangeCodecV1.DecodeQuote(new byte[SpotExchangeCodecV1.MaximumQuoteByteLength + 1])).Failure);
    }

    [Fact]
    public void Noncanonical_fee_issuer_order_and_open_fill_shape_are_refused()
    {
        var market = new SpotExchangeFixture();
        var fees = new List<SpotFee> { new(GuildAccount, 1), new(AAccount, 1) };
        var quote = SpotExchangeBoundaryTests.Copy(market.BerryOffer(), fees: fees);
        var bytes = SpotExchangeCodecV1.Encode(quote);
        fees.Clear();
        Assert.Equal(bytes, SpotExchangeCodecV1.Encode(quote));
        var reversed = Rewrite(Rewrite(bytes, [27], AAccount.ToByteArray()), [29], GuildAccount.ToByteArray());
        Assert.Equal(AuthorityFormatFailure.NonCanonical,
            Assert.Throws<AuthorityFormatException>(() => SpotExchangeCodecV1.DecodeQuote(reversed)).Failure);
        market.Issue(quote, MerchantADelivery);
        var open = SpotExchangeCodecV1.Encode(market.Book);
        Assert.Equal(AuthorityFormatFailure.NonCanonical, Assert.Throws<AuthorityFormatException>(() =>
            SpotExchangeCodecV1.DecodeBook(Rewrite(open, [7, 8], Number(1)))).Failure);
        Assert.Equal(AuthorityFormatFailure.InvalidValue, Assert.Throws<AuthorityFormatException>(() =>
            SpotExchangeCodecV1.DecodeBook(Rewrite(open, [7, 3], TraderDelivery.PrincipalId.ToByteArray()))).Failure);
        Assert.Equal(AuthorityFormatFailure.LimitExceeded, Assert.Throws<AuthorityFormatException>(() =>
            SpotExchangeCodecV1.DecodeBook(Rewrite(open, [5], Number(257)))).Failure);

        market.Commit(market.Decide(Accept(quote, 1)));
        var rations = market.CraftRations();
        var bid = market.RationBid(rations);
        market.Issue(bid, MerchantBDelivery);
        market.Commit(market.Decide(Accept(bid, 1, 951)));
        var closed = SpotExchangeCodecV1.Encode(market.Book);
        var swapped = Rewrite(Rewrite(closed, [6], MerchantB.ToByteArray()), [8], MerchantA.ToByteArray());
        Assert.Equal(AuthorityFormatFailure.NonCanonical,
            Assert.Throws<AuthorityFormatException>(() => SpotExchangeCodecV1.DecodeBook(swapped)).Failure);
        Assert.Equal(AuthorityFormatFailure.InvalidValue, Assert.Throws<AuthorityFormatException>(() =>
            SpotExchangeCodecV1.DecodeBook(Rewrite(closed, [11, 10], Number(999)))).Failure);
        Assert.Equal(AuthorityFormatFailure.InvalidValue, Assert.Throws<AuthorityFormatException>(() =>
            SpotExchangeCodecV1.DecodeBook(Rewrite(closed, [11, 9], Id(8000).ToByteArray()))).Failure);
        Assert.Equal(AuthorityFormatFailure.InvalidValue, Assert.Throws<AuthorityFormatException>(() =>
            SpotExchangeCodecV1.DecodeBook(Rewrite(closed, [4], Number(999)))).Failure);
        var copy = SpotExchangeCodecV1.Encode(market.Book);
        copy.AsSpan().Clear();
        Assert.Equal(closed, SpotExchangeCodecV1.Encode(market.Book));
        Assert.Equal(closed, SpotExchangeCodecV1.Encode(SpotExchangeCodecV1.DecodeBook(closed)));
    }

    private static byte[] Number(ulong value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    // Independent framing manipulation: all mutated payloads keep their outer byte shape valid.
    private static byte[] Rewrite(byte[] bytes, int[] path, byte[] replacement)
    {
        var fields = new List<byte[]>();
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8)));
        int position = 12;
        for (int i = 0; i < count; i++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position)));
            fields.Add(bytes.AsSpan(position + 4, length).ToArray());
            position += 4 + length;
        }
        fields[path[0]] = path.Length == 1 ? replacement : Rewrite(fields[path[0]], path[1..], replacement);
        using var output = new MemoryStream();
        output.Write(bytes.AsSpan(0, 12));
        Span<byte> size = stackalloc byte[4];
        foreach (var field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(size, (uint)field.Length);
            output.Write(size);
            output.Write(field);
        }
        return output.ToArray();
    }
}
