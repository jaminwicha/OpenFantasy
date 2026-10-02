using OpenFantasy.Hosting.Networking;

namespace OpenFantasy.Hosting.Tests.Networking;

public sealed class PlayerMessageTests
{
    [Fact]
    public void Envelope_has_an_independent_byte_vector_and_owns_its_payload()
    {
        byte[] payload = [0xab, 0xcd];
        var message = new PlayerMessage(PlayerMessageKind.Command, 0x0102030405060708UL, payload);
        payload[0] = 0;
        byte[] expected = Convert.FromHexString("4F46575000010005010203040506070800000002ABCD");
        Assert.Equal(expected, PlayerMessageCodec.Encode(message));
        PlayerMessage decoded = PlayerMessageCodec.Decode(expected);
        expected[^1] = 0;
        Assert.Equal(PlayerMessageKind.Command, decoded.Kind);
        Assert.Equal(0x0102030405060708UL, decoded.RequestId);
        Assert.Equal(new byte[] { 0xab, 0xcd }, decoded.Payload.ToArray());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 2)]
    [InlineData(7, 0)]
    [InlineData(16, 255)]
    [InlineData(19, 1)]
    public void Unsupported_or_inconsistent_headers_are_refused(int offset, byte value)
    {
        byte[] encoded = PlayerMessageCodec.Encode(new PlayerMessage(PlayerMessageKind.Ping, 1, []));
        encoded[offset] = value;
        Assert.Throws<FormatException>(() => PlayerMessageCodec.Decode(encoded));
    }

    [Fact]
    public void Truncation_trailing_data_and_oversize_are_refused_before_payload_admission()
    {
        byte[] encoded = PlayerMessageCodec.Encode(new PlayerMessage(PlayerMessageKind.Ping, 1, []));
        for (int length = 0; length < encoded.Length; length++)
            Assert.Throws<FormatException>(() => PlayerMessageCodec.Decode(encoded.AsSpan(0, length)));
        Assert.Throws<FormatException>(() => PlayerMessageCodec.Decode([.. encoded, 0]));
        Assert.Throws<FormatException>(() => PlayerMessageCodec.Decode(new byte[PlayerMessageCodec.MaximumMessageBytes + 1]));
        Assert.Throws<ArgumentException>(() => new PlayerMessage(PlayerMessageKind.Command, 1,
            new byte[PlayerMessageCodec.MaximumPayloadBytes + 1]));
    }
}
