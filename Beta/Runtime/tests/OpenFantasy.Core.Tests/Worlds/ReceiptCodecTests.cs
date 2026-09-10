using System.Text.Json;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.ReceiptFixture;

namespace OpenFantasy.Core.Tests.Worlds;

public sealed class ReceiptCodecTests
{
    [Fact]
    public void Independent_receipts_bind_exact_commands_principals_scope_hashes_and_commit_outcomes()
    {
        Dictionary<string, WorldActionCommand> commands = CommandFixture.Read().GetProperty("actions").EnumerateArray()
            .ToDictionary(row => row.GetProperty("name").GetString()!, CommandFixture.Action);
        foreach (JsonElement row in Read().GetProperty("receipts").EnumerateArray())
        {
            WorldActionCommand command = commands[row.GetProperty("commandName").GetString()!];
            byte[] encoded = Bytes(row);
            WorldActionReceipt receipt = WorldReceiptCodecV1.Decode(encoded);
            Assert.Equal(encoded, WorldReceiptCodecV1.Encode(receipt));
            Assert.Equal(command.CommandId, receipt.CommandId);
            Assert.Equal(command.PrincipalId, receipt.PrincipalId);
            Assert.Equal(command.WorldId, receipt.WorldId);
            Assert.Equal(command.BranchId, receipt.BranchId);
            Assert.Equal(command.ContentHash, receipt.CommandHash);
            Assert.Equal(CommandFixture.Number(row, "commitSequence"), receipt.CommitSequence);
            Assert.Equal((CommandReceiptStatus)row.GetProperty("status").GetInt32(), receipt.Status);
            Assert.Equal((WorldActionRejection)row.GetProperty("rejection").GetInt32(), receipt.Rejection);
            Assert.Equal(row.GetProperty("sha256").GetString(), ContentHash.Compute(encoded).Value);
            Assert.Equal(row.GetProperty("resultId").GetString(), receipt.ResultId.ToString());
        }
    }

    [Fact]
    public void Independent_outcome_batches_restore_exact_canonical_command_order()
    {
        Dictionary<string, WorldActionReceipt> receipts = Read().GetProperty("receipts").EnumerateArray()
            .ToDictionary(row => row.GetProperty("name").GetString()!, row => WorldReceiptCodecV1.Decode(Bytes(row)));
        foreach (JsonElement row in Read().GetProperty("batches").EnumerateArray())
        {
            WorldActionReceipt[] expected = row.GetProperty("receiptNames").EnumerateArray()
                .Select(name => receipts[name.GetString()!]).ToArray();
            byte[] bytes = Bytes(row);
            Assert.Equal(bytes, WorldReceiptCodecV1.EncodeBatch(expected.Reverse()));
            IReadOnlyList<WorldActionReceipt> restored = WorldReceiptCodecV1.DecodeBatch(bytes);
            Assert.Equal(expected.Select(value => value.ResultId), restored.Select(value => value.ResultId));
            Assert.Equal(bytes, WorldReceiptCodecV1.EncodeBatch(restored));
            Assert.Equal(row.GetProperty("sha256").GetString(), ContentHash.Compute(bytes).Value);
        }
    }

    [Fact]
    public void Independent_corruption_vectors_fail_with_typed_format_errors()
    {
        int count = 0;
        foreach (JsonElement row in Read().GetProperty("malformed").EnumerateArray())
        {
            byte[] encoded = Bytes(row);
            if (row.GetProperty("kind").GetString() == "receipt")
            {
                Assert.Throws<FormatException>(() => WorldReceiptCodecV1.Decode(encoded));
            }
            else
            {
                Assert.Throws<FormatException>(() => WorldReceiptCodecV1.DecodeBatch(encoded));
            }
            count++;
        }
        Assert.Equal(42, count);
    }

    [Fact]
    public void Every_prefix_and_over_limit_receipt_or_batch_rejects()
    {
        byte[] receipt = AppliedBytes();
        for (int length = 0; length < receipt.Length; length++)
        {
            Assert.Throws<FormatException>(() => WorldReceiptCodecV1.Decode(receipt.AsSpan(0, length)));
        }
        byte[] batch = Bytes(Read().GetProperty("batches")[1]);
        for (int length = 0; length < batch.Length; length++)
        {
            Assert.Throws<FormatException>(() => WorldReceiptCodecV1.DecodeBatch(batch.AsSpan(0, length)));
        }
        Assert.Throws<FormatException>(() => WorldReceiptCodecV1.Decode(new byte[WorldReceiptCodecV1.MaximumReceiptByteLength + 1]));
        Assert.Throws<FormatException>(() => WorldReceiptCodecV1.DecodeBatch(new byte[256 * 1024 + 1]));
    }

    [Fact]
    public void Only_applied_without_rejection_and_rejected_with_a_known_reason_are_consistent()
    {
        byte[] original = AppliedBytes();
        foreach (ulong status in new ulong[] { 1, 2 })
        {
            foreach (ulong rejection in Enumerable.Range(0, 6).Select(value => (ulong)value))
            {
                byte[] encoded = Replace(Replace(original, 8, Number(status)), 9, Number(rejection));
                bool valid = status == 1 ? rejection == 0 : rejection != 0;
                if (valid)
                {
                    WorldActionReceipt receipt = WorldReceiptCodecV1.Decode(encoded);
                    Assert.Equal((CommandReceiptStatus)status, receipt.Status);
                    Assert.Equal((WorldActionRejection)rejection, receipt.Rejection);
                    Assert.Equal(encoded, WorldReceiptCodecV1.Encode(receipt));
                }
                else
                {
                    Assert.Throws<FormatException>(() => WorldReceiptCodecV1.Decode(encoded));
                }
            }
        }
        Assert.Throws<FormatException>(() => WorldReceiptCodecV1.Decode(Replace(original, 7, Number(0))));
        Assert.Equal(ulong.MaxValue, WorldReceiptCodecV1.Decode(Replace(original, 7, Number(ulong.MaxValue))).CommitSequence);
    }

    [Fact]
    public void Result_identity_binds_every_receipt_field_including_request_hash_principal_and_scope()
    {
        byte[] original = AppliedBytes();
        EntityId result = WorldReceiptCodecV1.Decode(original).ResultId;
        for (int index = 2; index <= 7; index++)
        {
            byte[] field = Fields(original)[index];
            field[^1] ^= 0x80;
            WorldActionReceipt changed = WorldReceiptCodecV1.Decode(Replace(original, index, field));
            Assert.NotEqual(result, changed.ResultId);
        }
        byte[] rejected = Replace(Replace(original, 8, Number(2)), 9, Number(1));
        WorldActionReceipt denial = WorldReceiptCodecV1.Decode(rejected);
        Assert.NotEqual(result, denial.ResultId);
        Assert.NotEqual(denial.ResultId, WorldReceiptCodecV1.Decode(Replace(rejected, 9, Number(2))).ResultId);
        // A hash identifies supplied content. Decoding neither authenticates the claims nor proves a durable commit.
        WorldActionReceipt changedPrincipal = WorldReceiptCodecV1.Decode(Replace(original, 3, CommandFixture.Id(999).ToByteArray()));
        Assert.Equal(WorldReceiptCodecV1.Decode(original).CommandHash, changedPrincipal.CommandHash);
        Assert.NotEqual(WorldReceiptCodecV1.Decode(original).PrincipalId, changedPrincipal.PrincipalId);
    }

    [Fact]
    public void Receipt_and_batch_restore_do_not_retain_mutable_input_buffers()
    {
        byte[] receiptBytes = AppliedBytes();
        byte[] retained = (byte[])receiptBytes.Clone();
        WorldActionReceipt receipt = WorldReceiptCodecV1.Decode(receiptBytes);
        EntityId result = receipt.ResultId;
        Array.Fill(receiptBytes, (byte)0);
        Assert.Equal(retained, WorldReceiptCodecV1.Encode(receipt));
        Assert.Equal(result, receipt.ResultId);
        byte[] batchBytes = Bytes(Read().GetProperty("batches")[1]);
        byte[] batchRetained = (byte[])batchBytes.Clone();
        IReadOnlyList<WorldActionReceipt> batch = WorldReceiptCodecV1.DecodeBatch(batchBytes);
        Array.Fill(batchBytes, (byte)0);
        Assert.Equal(batchRetained, WorldReceiptCodecV1.EncodeBatch(batch));
        Assert.Throws<NotSupportedException>(() => ((IList<WorldActionReceipt>)batch).Clear());
    }

    [Fact]
    public void Single_bit_changes_either_reject_or_restore_exact_canonical_bytes()
    {
        byte[] original = AppliedBytes();
        for (int index = 0; index < original.Length; index++)
        {
            for (int bit = 0; bit < 8; bit++)
            {
                byte[] changed = (byte[])original.Clone();
                changed[index] ^= (byte)(1 << bit);
                WorldActionReceipt restored;
                try { restored = WorldReceiptCodecV1.Decode(changed); }
                catch (FormatException) { continue; }
                Assert.Equal(changed, WorldReceiptCodecV1.Encode(restored));
            }
        }
    }
}
