using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Progression.ResourceHistory;
using static OpenFantasy.Core.Tests.Progression.ProgressionFixture;
using static OpenFantasy.Core.Tests.Progression.ResourceHistory.ActorResourceHistoryTests;

namespace OpenFantasy.Core.Tests.Progression.ResourceHistory;

public sealed class ResourceBatchEncodingTests
{
    [Fact]
    public void Public_encoded_buffers_cannot_change_retained_history_or_later_exports()
    {
        var basis = Create();
        var history = History(basis, 5);
        byte[] expected = ActorResourceHistoryCodecV1.Encode(history);
        byte[] exported = ActorResourceHistoryCodecV1.Encode(history);
        exported.AsSpan().Clear();

        Assert.Equal(expected, ActorResourceHistoryCodecV1.Encode(history));
        var restored = ActorResourceHistoryCodecV1.Decode(expected, basis, 0);
        Assert.Equal(55, restored.Batches[0].Changes[0].After.CurrentHp);
        Assert.Equal(expected, ActorResourceHistoryCodecV1.Encode(restored));
    }

    [Fact]
    public void Equal_command_id_does_not_share_encoding_between_distinct_batches()
    {
        var basis = Create();
        var first = History(basis, 5);
        var other = History(basis, 7);
        byte[] firstBytes = ActorResourceHistoryCodecV1.Encode(first);
        byte[] otherBytes = ActorResourceHistoryCodecV1.Encode(other);

        Assert.Equal(first.Batches[0].CommandId, other.Batches[0].CommandId);
        Assert.False(firstBytes.AsSpan().SequenceEqual(otherBytes));
        Assert.Equal(53, ActorResourceHistoryCodecV1.Decode(otherBytes, basis, 0)
            .Batches[0].Changes[0].After.CurrentHp);
        Assert.Equal(firstBytes, ActorResourceHistoryCodecV1.Encode(first));
    }

    [Fact]
    public void Shared_batch_preserves_earlier_history_when_successor_and_input_list_change()
    {
        var basis = Create();
        List<ActorResourceChange> input = [ActorResourceChange.FromTransition(0, Cause(),
            basis.GetActor(Id(10)).TakeDamage(5))];
        var batch = new ResourceChangeBatch(1, 0, Id(1000), input);
        var earlier = new ActorResourceHistory(basis, 0, [batch]);
        byte[] original = ActorResourceHistoryCodecV1.Encode(History(basis, 5));
        input.Clear();
        Assert.Single(batch.Changes);
        Assert.Equal(original, ActorResourceHistoryCodecV1.Encode(earlier));
        var successor = earlier.Append(new(2, 0, Id(1001),
            [ActorResourceChange.FromTransition(0, Cause(), batch.Changes[0].After.RestoreHealth(3))]));

        Assert.Equal(original, ActorResourceHistoryCodecV1.Encode(earlier));
        byte[] continued = ActorResourceHistoryCodecV1.Encode(successor);
        var decoded = ActorResourceHistoryCodecV1.Decode(continued, basis, 0);
        Assert.Equal(2, decoded.ChangeCount);
        Assert.Equal(58, decoded.Batches[1].Changes[0].After.CurrentHp);
        Assert.Equal(continued, ActorResourceHistoryCodecV1.Encode(decoded));
    }

    [Fact]
    public void Concurrent_first_encoding_and_mutated_exports_preserve_exact_bytes()
    {
        var basis = Create();
        // Exercises concurrent cold callers; no particular CAS interleaving is assumed or required.
        // Obtain the expectation from a separate owner, leaving the shared instance cold.
        byte[] expected = ActorResourceHistoryCodecV1.Encode(History(basis, 5));
        var shared = History(basis, 5);
        Parallel.For(0, 64, _ =>
        {
            byte[] exported = ActorResourceHistoryCodecV1.Encode(shared);
            Assert.Equal(expected, exported);
            exported.AsSpan().Clear();
        });
        Assert.Equal(expected, ActorResourceHistoryCodecV1.Encode(shared));
    }

    private static ActorResourceHistory History(ProgressionSnapshot basis, uint damage) => new(basis, 0,
        [new(1, 0, Id(1000), [ActorResourceChange.FromTransition(0, Cause(), basis.GetActor(Id(10)).TakeDamage(damage))])]);
}
