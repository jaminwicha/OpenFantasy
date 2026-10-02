using OpenFantasy.Core;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Tests.Networking.ControlReturn;

public sealed class PlayerControlCodecTests
{
    [Theory]
    [InlineData(PlayerControlState.Available, 0UL)]
    [InlineData(PlayerControlState.Pending, 0UL)]
    [InlineData(PlayerControlState.Restored, 1UL)]
    [InlineData(PlayerControlState.Available, 1UL)]
    public void Status_and_bound_readiness_preserve_exact_identity(PlayerControlState state, ulong generation)
    {
        var status = new PlayerControlStatus(state, new(1, 2), new(3, 4), generation, new(5, 6), 7);
        byte[] bytes = PlayerControlCodec.Encode(status);
        Assert.Equal(PlayerControlCodec.ByteLength, bytes.Length);
        Assert.Equal(status, PlayerControlCodec.Decode(bytes));
        var input = PlayerCommandInput.Decode(PlayerMessageCodec.Encode(PlayerGameplayCodec.Readiness(10, true, status)), 9);
        Assert.True(input.Ready);
        Assert.Equal(status, input.Control);
        Assert.Equal(bytes, PlayerControlCodec.Encode(input.Control!));
    }

    [Fact]
    public void Legacy_boolean_bytes_are_exact_and_malformed_control_is_refused()
    {
        Assert.Equal(new byte[] { 1 }, PlayerGameplayCodec.Readiness(1, true).Payload.ToArray());
        Assert.Equal(new byte[] { 0 }, PlayerGameplayCodec.Readiness(1, false).Payload.ToArray());
        var status = new PlayerControlStatus(PlayerControlState.Restored, new(1, 2), new(3, 4), 1, new(5, 6), 7);
        byte[] bytes = PlayerControlCodec.Encode(status);
        foreach (int offset in new[] { 0, 1 })
        {
            byte[] bad = bytes.ToArray(); bad[offset] = 255;
            Assert.Throws<FormatException>(() => PlayerControlCodec.Decode(bad));
        }
        Assert.Throws<FormatException>(() => PlayerControlCodec.Decode(bytes[..^1]));
        Assert.Throws<FormatException>(() => PlayerControlCodec.Decode([.. bytes, 0]));
        Assert.Throws<FormatException>(() => PlayerControlCodec.Encode(status with { ObservationRevision = 0 }));
        Assert.Throws<FormatException>(() => PlayerControlCodec.Encode(status with { Generation = 0 }));
        Assert.Throws<FormatException>(() => PlayerControlCodec.Encode(status with { PortId = default }));
    }
}
