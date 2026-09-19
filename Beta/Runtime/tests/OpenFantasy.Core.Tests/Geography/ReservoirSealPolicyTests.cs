using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using static OpenFantasy.Core.Tests.Actors.BodyDecodingTestFrames;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class ReservoirSealPolicyTests
{
    private static CountedItemDefinition Item(ulong id) => new(new(90210, id), ContentHash.Compute(BitConverter.GetBytes(id)));

    [Fact]
    public void SealingPinsAreExplicitSortedAndCanonicalWithoutReinterpretingHardness()
    {
        var policy = new ReservoirSealPolicy(Item(1), [new(Item(3), 10), new(Item(2), 1000)]);
        Assert.Equal(1000U, policy.MinimumThicknessFor(Item(2)));
        Assert.Null(policy.MinimumThicknessFor(Item(4)));
        Assert.Null(policy.MinimumThicknessFor(new(Item(2).DefinitionId, Item(4).Version)));
        byte[] bytes = ReservoirSealPolicyCodecV1.Encode(policy);
        Assert.Equal(bytes, ReservoirSealPolicyCodecV1.Encode(new(Item(1), policy.Rules.Reverse())));
        Assert.Equal(bytes, ReservoirSealPolicyCodecV1.Encode(ReservoirSealPolicyCodecV1.Decode(bytes, TestContext.Current.CancellationToken)));
        foreach (var bad in new[] { bytes[..^1], bytes.Concat(new byte[] { 0 }).ToArray(), Replace(bytes, 0, Text("unknown")),
            Replace(bytes, 2, Fields(bytes)[2].Chunk(56).Reverse().SelectMany(row => row).ToArray()),
            new byte[ReservoirSealPolicyCodecV1.MaximumEncodedByteLength + 1] })
        {
            Assert.Throws<FormatException>(() => ReservoirSealPolicyCodecV1.Decode(bad, TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public void InvalidOrUnboundedMaterialRulesAndCanceledDecodeReject()
    {
        Assert.Throws<ArgumentException>(() => new ReservoirSealPolicy(Item(1), []));
        Assert.Throws<ArgumentException>(() => new ReservoirSealPolicy(Item(1), [new(Item(1), 1)]));
        Assert.Throws<ArgumentException>(() => new ReservoirSealPolicy(Item(1), [new(Item(2), 1), new(Item(2), 2)]));
        Assert.Throws<ArgumentException>(() => new ReservoirSealPolicy(Item(1), Enumerable.Range(2, 65).Select(i => new ReservoirSealRule(Item((ulong)i), 1))));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReservoirSealRule(Item(2), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReservoirSealRule(Item(2), 1001));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        byte[] bytes = ReservoirSealPolicyCodecV1.Encode(new(Item(1), [new(Item(2), 1)]));
        Assert.Throws<OperationCanceledException>(() => ReservoirSealPolicyCodecV1.Decode(bytes, cancellation.Token));
    }
}
