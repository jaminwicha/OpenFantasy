using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.Recovery;

public sealed class RecoveryRootCodecTests
{
    [Fact]
    public void Fresh_recovery_root_roundtrips_without_an_archive_context()
    {
        var world = new RecoveryJournalFixture().Genesis;
        byte[] bytes = WorldSnapshotCodec.Encode(world);
        var restored = WorldSnapshotCodec.Decode(bytes);
        Assert.Equal(WorldRecoveryAdmission.ProfileVersion, restored.ProfileVersion);
        Assert.NotNull(restored.Recovery);
        Assert.Equal(world.ContentHash, restored.ContentHash);
        Assert.Equal(bytes, WorldSnapshotCodecV37.Encode(restored));
    }

    [Theory]
    [InlineData("policy-pin")]
    [InlineData("truncated")]
    [InlineData("trailing")]
    public void Recovery_interpretation_requires_the_complete_exact_policy_pin(string mutation)
    {
        byte[] bytes = WorldSnapshotCodec.Encode(new RecoveryJournalFixture().Genesis);
        Assert.Equal(WorldRecoveryPolicy.RulesPin.ToByteArray(), bytes[^32..]);
        switch (mutation)
        {
            case "policy-pin": bytes[^1] ^= 1; break;
            case "truncated": bytes = bytes[..^1]; break;
            case "trailing": bytes = [.. bytes, 0]; break;
        }
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(bytes));
        Assert.Throws<FormatException>(() => WorldSnapshotCodecV37.Decode(bytes));
    }
}
