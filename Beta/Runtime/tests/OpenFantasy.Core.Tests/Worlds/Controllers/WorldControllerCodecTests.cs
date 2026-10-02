using System.Buffers.Binary;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.Controllers.WorldControllerStateTests;

namespace OpenFantasy.Core.Tests.Worlds.Controllers;

public sealed class WorldControllerCodecTests
{
    [Fact]
    public void State_encoding_is_deterministic_bounded_and_retains_every_assignment_field()
    {
        WorldControllerAssignment[] rows = [new(Id(1), 0, WorldControllerMode.Human, null),
            new(Id(2), 1, WorldControllerMode.ReconnectAllowance, Id(20)),
            new(Id(3), 2, WorldControllerMode.Autonomous, Id(30)),
            new(Id(4), ulong.MaxValue, WorldControllerMode.Human, null)];
        var state = new WorldControllerState(rows);
        byte[] encoded = WorldControllerCodecV1.Encode(state);
        Assert.True(encoded.Length <= WorldControllerCodecV1.MaximumStateByteLength);
        Assert.Equal(encoded, WorldControllerCodecV1.Encode(new(rows.Reverse())));
        var restored = WorldControllerCodecV1.Decode(encoded);
        Assert.Equal(rows, restored.Assignments);
        Assert.Equal(state.ContentHash, restored.ContentHash);
        Assert.Equal(encoded, WorldControllerCodecV1.Encode(restored));
        Assert.Equal(32, WorldControllerState.RulesPin.ToByteArray().Length);
    }

    [Fact]
    public void Every_event_kind_has_exact_canonical_roundtrip_including_maximum_numeric_values()
    {
        var events = Enum.GetValues<WorldControllerDisconnectReason>().Select(reason =>
            new WorldControllerEvent(Id(1), WorldControllerEventKind.Disconnected, ulong.MaxValue, reason: reason)).Concat([
                new(Id(1), WorldControllerEventKind.AllowanceExpired, ulong.MaxValue, Id(10), 30_000),
                new(Id(1), WorldControllerEventKind.AllowanceExpired, ulong.MaxValue, Id(10), ulong.MaxValue),
                new(Id(1), WorldControllerEventKind.Reconnected, ulong.MaxValue, Id(10))]);
        foreach (WorldControllerEvent input in events)
        {
            byte[] encoded = WorldControllerCodecV1.EncodeEvent(input);
            Assert.True(encoded.Length <= WorldControllerCodecV1.MaximumEventByteLength);
            var restored = WorldControllerCodecV1.DecodeEvent(encoded);
            Assert.Equal(input, restored);
            Assert.Equal(encoded, WorldControllerCodecV1.EncodeEvent(restored));
        }
    }

    [Fact]
    public void Raw_state_rejects_reordered_duplicate_excess_empty_rows_bad_pin_and_unreachable_modes()
    {
        byte[] encoded = WorldControllerCodecV1.Encode(WorldControllerState.Fresh([Id(1), Id(2)]));
        var fields = Fields(encoded);
        var rows = Fields(fields[3]);
        var reversed = rows.ToList();
        (reversed[1], reversed[2]) = (reversed[2], reversed[1]);
        RejectState(fields, 3, Frame(reversed));
        RejectState(fields, 3, Frame([rows[0], rows[1], rows[1]]));
        RejectState(fields, 3, Frame([rows[0]]));
        RejectState(fields, 3, Frame([rows[0], rows[1], rows[2], rows[1], rows[2], rows[1]]));
        byte[] pin = (byte[])fields[2].Clone();
        pin[0] ^= 1;
        RejectState(fields, 2, pin);
        RejectState(fields, 1, Number(2));
        foreach (ulong invalidMode in new[] { 0UL, 4UL, (ulong)int.MaxValue + 1, ulong.MaxValue })
        {
            var assignment = Fields(rows[1]);
            assignment[3] = Number(invalidMode);
            RejectState(fields, 3, Frame([rows[0], Frame(assignment), rows[2]]));
        }
        var nonHumanGenesis = Fields(rows[1]);
        nonHumanGenesis[3] = Number((ulong)WorldControllerMode.ReconnectAllowance);
        nonHumanGenesis[4] = Id(10).ToByteArray();
        RejectState(fields, 3, Frame([rows[0], Frame(nonHumanGenesis), rows[2]]));
        var malformedIdentity = Fields(rows[1]);
        malformedIdentity[1] = new byte[15];
        RejectState(fields, 3, Frame([rows[0], Frame(malformedIdentity), rows[2]]));
    }

    [Fact]
    public void Raw_event_rejects_bad_enums_threshold_pairing_lengths_and_extra_fields()
    {
        byte[] disconnected = WorldControllerCodecV1.EncodeEvent(new(Id(1), WorldControllerEventKind.Disconnected, 0,
            reason: WorldControllerDisconnectReason.PeerClosed));
        var fields = Fields(disconnected);
        foreach (ulong invalid in new[] { 0UL, 4UL, (ulong)int.MaxValue + 1, ulong.MaxValue })
        {
            RejectEvent(fields, 3, Number(invalid));
            RejectEvent(fields, 7, Number(invalid));
        }
        RejectEvent(fields, 1, Number(2));
        RejectEvent(fields, 5, Id(10).ToByteArray());
        RejectEvent(fields, 6, Number(1));
        RejectEvent(fields, 7, []);
        RejectEvent(fields, 7, new byte[7]);
        RejectEvent(fields, 4, new byte[9]);
        Assert.Throws<FormatException>(() => WorldControllerCodecV1.DecodeEvent(Frame([.. fields, []])));
        var expired = Fields(WorldControllerCodecV1.EncodeEvent(new(Id(1), WorldControllerEventKind.AllowanceExpired, 1, Id(10), 30_000)));
        RejectEvent(expired, 6, Number(29_999));
        RejectEvent(expired, 5, []);
        RejectEvent(expired, 5, new byte[15]);
        RejectEvent(expired, 5, new byte[17]);
        RejectEvent(expired, 7, Number((ulong)WorldControllerDisconnectReason.PeerClosed));
        var reconnected = Fields(WorldControllerCodecV1.EncodeEvent(new(Id(1), WorldControllerEventKind.Reconnected, 1, Id(10))));
        RejectEvent(reconnected, 6, Number(1));
        RejectEvent(reconnected, 7, Number((ulong)WorldControllerDisconnectReason.TransportLost));
    }

    [Fact]
    public void Both_formats_reject_raw_truncation_trailing_bytes_wrong_headers_and_oversized_input()
    {
        byte[] state = WorldControllerCodecV1.Encode(WorldControllerState.Fresh([Id(1)]));
        byte[] input = WorldControllerCodecV1.EncodeEvent(new(Id(1), WorldControllerEventKind.Disconnected, 0,
            reason: WorldControllerDisconnectReason.ProtocolFailure));
        Check(state, bytes => WorldControllerCodecV1.Decode(bytes), WorldControllerCodecV1.MaximumStateByteLength);
        Check(input, bytes => WorldControllerCodecV1.DecodeEvent(bytes), WorldControllerCodecV1.MaximumEventByteLength);
        Assert.Throws<FormatException>(() => WorldControllerCodecV1.DecodeEvent(state));
        Assert.Throws<FormatException>(() => WorldControllerCodecV1.Decode(input));
    }

    private static void Check(byte[] valid, Action<byte[]> decode, int maximum)
    {
        foreach (int length in new[] { 0, 1, 11, valid.Length - 1 })
            Assert.Throws<FormatException>(() => decode(valid[..length]));
        Assert.Throws<FormatException>(() => decode([.. valid, 0]));
        Assert.Throws<FormatException>(() => decode(new byte[maximum + 1]));
        foreach (int offset in new[] { 0, 7, 11 })
        {
            byte[] corrupt = (byte[])valid.Clone();
            corrupt[offset] ^= 0xff;
            Assert.Throws<FormatException>(() => decode(corrupt));
        }
        byte[] enormousField = (byte[])valid.Clone();
        BinaryPrimitives.WriteUInt32BigEndian(enormousField.AsSpan(12), uint.MaxValue);
        Assert.Throws<FormatException>(() => decode(enormousField));
    }

    private static void RejectState(List<byte[]> fields, int index, byte[] replacement)
    {
        var changed = fields.ToList();
        changed[index] = replacement;
        Assert.Throws<FormatException>(() => WorldControllerCodecV1.Decode(Frame(changed)));
    }

    private static void RejectEvent(List<byte[]> fields, int index, byte[] replacement)
    {
        var changed = fields.ToList();
        changed[index] = replacement;
        Assert.Throws<FormatException>(() => WorldControllerCodecV1.DecodeEvent(Frame(changed)));
    }

    private static byte[] Number(ulong value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }
    private static byte[] Frame(IReadOnlyList<byte[]> fields)
    {
        byte[] bytes = new byte[12 + fields.Sum(field => 4 + field.Length)];
        "OFC\0"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), checked((uint)fields.Count));
        int offset = 12;
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset), checked((uint)field.Length));
            field.CopyTo(bytes, offset + 4);
            offset += 4 + field.Length;
        }
        return bytes;
    }
    private static List<byte[]> Fields(byte[] bytes)
    {
        List<byte[]> fields = [];
        for (int offset = 12; offset < bytes.Length;)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
            fields.Add(bytes.AsSpan(offset + 4, length).ToArray());
            offset += 4 + length;
        }
        return fields;
    }
}
