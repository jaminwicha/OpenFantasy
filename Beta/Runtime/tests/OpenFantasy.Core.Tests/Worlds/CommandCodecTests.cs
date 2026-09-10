using System.Buffers.Binary;
using System.Text.Json;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Worlds;

public sealed class CommandCodecTests
{
    [Fact]
    public void Independent_action_vectors_match_authored_fields_bytes_hashes_and_restoration()
    {
        foreach (JsonElement value in CommandFixture.Read().GetProperty("actions").EnumerateArray())
        {
            WorldActionCommand authored = CommandFixture.Action(value);
            byte[] bytes = CommandFixture.Bytes(value);
            Assert.Equal(bytes, WorldActionCodecV1.Encode(authored));
            WorldActionCommand restored = WorldActionCodecV1.Decode(bytes);
            Assert.Equal(bytes, WorldActionCodecV1.Encode(restored));
            Assert.Equal(value.GetProperty("sha256").GetString(), restored.ContentHash.Value);
            Assert.Equal(authored.CommandId, restored.CommandId);
            Assert.Equal(authored.PrincipalId, restored.PrincipalId);
            Assert.Equal(authored.OriginSessionId, restored.OriginSessionId);
            Assert.Equal(authored.ActorId, restored.ActorId);
            Assert.Equal(authored.WorldId, restored.WorldId);
            Assert.Equal(authored.BranchId, restored.BranchId);
            Assert.Equal(ulong.MaxValue, restored.ExpectedActorRevision);
            Assert.Equal(authored.Intent, restored.Intent);
        }
    }

    [Fact]
    public void Independent_step_vectors_match_signed_samples_counters_order_and_actions()
    {
        foreach (JsonElement value in CommandFixture.Read().GetProperty("batches").EnumerateArray())
        {
            WorldStepBatch authored = CommandFixture.Batch(value);
            byte[] bytes = CommandFixture.Bytes(value);
            Assert.Equal(bytes, WorldStepCodecV1.Encode(authored));
            WorldStepBatch restored = WorldStepCodecV1.Decode(bytes);
            Assert.Equal(bytes, WorldStepCodecV1.Encode(restored));
            Assert.Equal(value.GetProperty("sha256").GetString(), ContentHash.Compute(bytes).Value);
            Assert.Equal(authored.CauseId, restored.CauseId);
            Assert.Equal(authored.WorldId, restored.WorldId);
            Assert.Equal(authored.BranchId, restored.BranchId);
            Assert.Equal(authored.ExpectedRevision, restored.ExpectedRevision);
            Assert.Equal(authored.ExpectedAdvancingSteps, restored.ExpectedAdvancingSteps);
            Assert.Equal(authored.Kind, restored.Kind);
            Assert.Equal(authored.Movement.ToArray(), restored.Movement.ToArray());
            Assert.Equal(authored.Actions.Select(action => action.ContentHash), restored.Actions.Select(action => action.ContentHash));
        }
    }

    [Fact]
    public void Independent_malformed_vectors_fail_with_format_errors()
    {
        int count = 0;
        foreach (JsonElement value in CommandFixture.Read().GetProperty("malformed").EnumerateArray())
        {
            byte[] bytes = CommandFixture.Bytes(value);
            if (value.GetProperty("kind").GetString() == "action")
            {
                Assert.Throws<FormatException>(() => WorldActionCodecV1.Decode(bytes));
            }
            else
            {
                Assert.Throws<FormatException>(() => WorldStepCodecV1.Decode(bytes));
            }
            count++;
        }
        Assert.Equal(64, count);
    }

    [Fact]
    public void Every_truncated_prefix_and_over_limit_frame_fails_closed()
    {
        foreach (JsonElement value in CommandFixture.Read().GetProperty("actions").EnumerateArray())
        {
            byte[] bytes = CommandFixture.Bytes(value);
            for (int length = 0; length < bytes.Length; length++)
            {
                Assert.Throws<FormatException>(() => WorldActionCodecV1.Decode(bytes.AsSpan(0, length)));
            }
        }
        byte[] batch = CommandFixture.Bytes(CommandFixture.Read().GetProperty("batches")[1]);
        for (int length = 0; length < batch.Length; length++)
        {
            Assert.Throws<FormatException>(() => WorldStepCodecV1.Decode(batch.AsSpan(0, length)));
        }
        Assert.Throws<FormatException>(() => WorldActionCodecV1.Decode(new byte[WorldActionCodecV1.MaximumCommandByteLength + 1]));
        Assert.Throws<FormatException>(() => WorldStepCodecV1.Decode(new byte[WorldStepCodecV1.MaximumBatchByteLength + 1]));
    }

    [Fact]
    public void Decoding_owns_bytes_and_mutating_an_encoding_cannot_change_the_command_or_batch()
    {
        byte[] original = CommandFixture.Bytes(CommandFixture.Read().GetProperty("batches")[1]);
        byte[] retained = (byte[])original.Clone();
        WorldStepBatch batch = WorldStepCodecV1.Decode(original);
        Array.Fill(original, (byte)0);
        Assert.Equal(retained, WorldStepCodecV1.Encode(batch));
        WorldActionCommand action = batch.Actions[0];
        ContentHash hash = action.ContentHash;
        byte[] actionBytes = WorldActionCodecV1.Encode(action);
        Array.Fill(actionBytes, (byte)255);
        Assert.Equal(hash, action.ContentHash);
        Assert.Throws<NotSupportedException>(() => ((IList<WorldMovementInput>)batch.Movement).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<WorldActionCommand>)batch.Actions).Clear());
    }

    [Fact]
    public void Command_hash_binds_every_envelope_field_and_each_payload_field()
    {
        byte[] original = CommandFixture.Bytes(CommandFixture.Read().GetProperty("actions")[1]);
        WorldActionCommand command = WorldActionCodecV1.Decode(original);
        for (int field = 2; field <= 8; field++)
        {
            byte[] changed = ChangeLastByte(original, field);
            Assert.NotEqual(command.ContentHash, WorldActionCodecV1.Decode(changed).ContentHash);
        }
        byte[] payload = Fields(original)[9];
        for (int field = 2; field <= 8; field++)
        {
            byte[] changed = Replace(original, 9, ChangeLastByte(payload, field));
            Assert.NotEqual(command.ContentHash, WorldActionCodecV1.Decode(changed).ContentHash);
        }
    }

    [Fact]
    public void Single_bit_changes_either_reject_or_reencode_exact_canonical_content()
    {
        foreach (JsonElement value in CommandFixture.Read().GetProperty("actions").EnumerateArray())
        {
            byte[] bytes = CommandFixture.Bytes(value);
            for (int index = 0; index < bytes.Length; index++)
            {
                for (int bit = 0; bit < 8; bit++)
                {
                    byte[] changed = (byte[])bytes.Clone();
                    changed[index] ^= (byte)(1 << bit);
                    WorldActionCommand restored;
                    try { restored = WorldActionCodecV1.Decode(changed); }
                    catch (FormatException) { continue; }
                    Assert.Equal(changed, WorldActionCodecV1.Encode(restored));
                }
            }
        }
    }

    [Fact]
    public void Unsupported_runtime_intents_and_null_encodes_do_not_silently_emit_other_actions()
    {
        WorldActionCommand original = CommandFixture.Speak();
        var unknown = new WorldActionCommand(original.CommandId, original.PrincipalId, original.OriginSessionId,
            original.ActorId, original.WorldId, original.BranchId, original.ExpectedActorRevision, new UnsupportedIntent());
        Assert.Throws<ArgumentException>(() => WorldActionCodecV1.Encode(unknown));
        Assert.Throws<ArgumentException>(() => unknown.ContentHash);
        Assert.Throws<ArgumentNullException>(() => WorldActionCodecV1.Encode(null!));
        Assert.Throws<ArgumentNullException>(() => WorldStepCodecV1.Encode(null!));
    }

    private sealed record UnsupportedIntent : WorldActionIntent;
    private static byte[] ChangeLastByte(byte[] frame, int field)
    {
        byte[] value = (byte[])Fields(frame)[field].Clone();
        value[^1] ^= 0x80;
        return Replace(frame, field, value);
    }
    private static List<byte[]> Fields(byte[] bytes)
    {
        uint count = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8));
        int offset = 12;
        List<byte[]> values = [];
        for (uint index = 0; index < count; index++)
        {
            int size = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
            offset += 4;
            values.Add(bytes.AsSpan(offset, size).ToArray());
            offset += size;
        }
        Assert.Equal(bytes.Length, offset);
        return values;
    }
    private static byte[] Replace(byte[] frame, int field, byte[] value)
    {
        List<byte[]> values = Fields(frame);
        values[field] = value;
        byte[] result = new byte[12 + values.Sum(row => row.Length + 4)];
        "OFC\0"u8.CopyTo(result);
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(8), (uint)values.Count);
        int offset = 12;
        foreach (byte[] row in values)
        {
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(offset), (uint)row.Length);
            offset += 4;
            row.CopyTo(result, offset);
            offset += row.Length;
        }
        return result;
    }
}
