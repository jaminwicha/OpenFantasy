using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.StepFixture;

namespace OpenFantasy.Core.Tests.Worlds;

public sealed class SnapshotHashTests
{
    [Fact]
    public async Task ConcurrentReadersObserveTheCanonicalDigestWithoutSharingMutableEncodingBytes()
    {
        WorldSnapshot world = Create();
        byte[] canonical = WorldSnapshotCodec.Encode(world);
        ContentHash expected = ContentHash.Compute(canonical);
        ContentHash[] observed = await Task.WhenAll(Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => world.ContentHash)));
        Assert.All(observed, hash => Assert.Equal(expected, hash));
        canonical[0] ^= 0xff;
        Assert.Equal(expected, world.ContentHash);
        Assert.Equal(expected, ContentHash.Compute(WorldSnapshotCodec.Encode(world)));
    }

    [Fact]
    public void MovementSuccessorAndRestoredRootKeepTheirOwnExactCanonicalHashes()
    {
        WorldSnapshot original = Create();
        byte[] before = WorldSnapshotCodec.Encode(original);
        ContentHash beforeHash = original.ContentHash;
        WorldSnapshot moved = new WorldStepProcessor().Decide(original,
            Batch(original, 1000, movement: [Move(original, 10)])).Candidate;
        byte[] after = WorldSnapshotCodec.Encode(moved);
        Assert.NotEqual(beforeHash, moved.ContentHash);
        Assert.Equal(ContentHash.Compute(after), moved.ContentHash);
        Assert.Equal(beforeHash, original.ContentHash);
        Assert.Equal(before, WorldSnapshotCodec.Encode(original));
        WorldSnapshot restored = WorldSnapshotCodec.Decode(after);
        Assert.Equal(moved.ContentHash, restored.ContentHash);
        Assert.Equal(after, WorldSnapshotCodec.Encode(restored));
    }
}
