using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.ReceiptFixture;

namespace OpenFantasy.Core.Tests.Worlds;

public sealed class ReceiptBatchTests
{
    [Fact]
    public void Full_batch_sorts_all_outcomes_and_rejects_the_first_excess_without_truncation()
    {
        WorldActionReceipt[] receipts = Enumerable.Range(0, 64).Select(index => Applied((ulong)index + 1000)).Reverse().ToArray();
        byte[] encoded = WorldReceiptCodecV1.EncodeBatch(receipts);
        IReadOnlyList<WorldActionReceipt> restored = WorldReceiptCodecV1.DecodeBatch(encoded);
        Assert.Equal(64, restored.Count);
        Assert.Equal(CommandFixture.Id(1000), restored[0].CommandId);
        Assert.Equal(CommandFixture.Id(1063), restored[^1].CommandId);
        Assert.Equal(encoded, WorldReceiptCodecV1.EncodeBatch(receipts.Reverse()));
        Assert.Throws<ArgumentException>(() => WorldReceiptCodecV1.EncodeBatch(receipts.Append(Applied(1064))));
        List<byte[]> fields = Fields(encoded);
        Assert.Throws<FormatException>(() => WorldReceiptCodecV1.DecodeBatch(Frame([.. fields, WorldReceiptCodecV1.Encode(Applied(1064))])));
        int consumed = 0;
        IEnumerable<WorldActionReceipt> Unlimited()
        {
            while (true)
            {
                consumed++;
                if (consumed > 65) { throw new InvalidOperationException("Enumerated beyond the receipt bound."); }
                yield return Applied((ulong)consumed + 1000);
            }
        }
        Assert.Throws<ArgumentException>(() => WorldReceiptCodecV1.EncodeBatch(Unlimited()));
        Assert.Equal(65, consumed);
        Assert.Equal(64, restored.Count);
    }

    [Fact]
    public void Repeated_command_identity_rejects_even_when_principal_or_outcome_differs()
    {
        WorldActionReceipt first = Applied();
        foreach (byte[] conflicting in new[] { AppliedBytes(), Replace(AppliedBytes(), 3, CommandFixture.Id(999).ToByteArray()),
            Replace(Replace(AppliedBytes(), 8, Number(2)), 9, Number(1)) })
        {
            WorldActionReceipt second = WorldReceiptCodecV1.Decode(conflicting);
            Assert.Throws<ArgumentException>(() => WorldReceiptCodecV1.EncodeBatch([first, second]));
            byte[] batch = WorldReceiptCodecV1.EncodeBatch([first]);
            Assert.Throws<FormatException>(() => WorldReceiptCodecV1.DecodeBatch(Frame([.. Fields(batch), conflicting])));
        }
    }

    [Fact]
    public void Empty_batches_are_canonical_and_missing_inputs_reject()
    {
        byte[] empty = WorldReceiptCodecV1.EncodeBatch([]);
        Assert.Empty(WorldReceiptCodecV1.DecodeBatch(empty));
        Assert.Equal(Bytes(Read().GetProperty("batches")[0]), empty);
        Assert.Throws<ArgumentNullException>(() => WorldReceiptCodecV1.Encode(null!));
        Assert.Throws<ArgumentNullException>(() => WorldReceiptCodecV1.EncodeBatch(null!));
        Assert.Throws<ArgumentNullException>(() => WorldReceiptCodecV1.EncodeBatch([null!]));
    }
}
