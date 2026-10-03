using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Worlds.ControllerPermissions;

public sealed class ControlPermissionCodecTests
{
    [Fact]
    public void Offered_accepted_and_revoked_slots_retain_exact_terms_and_monotonic_revision()
    {
        var terms = Terms();
        var offered = new WorldControlPermissionSlot(Id(3), Id(6), 1, 10, terms, 10);
        var accepted = new WorldControlPermissionSlot(Id(3), Id(6), 2, 12, terms, 10, Id(11), 12);
        var revoked = new WorldControlPermissionSlot(Id(3), Id(6), 3, 15);
        foreach (var slot in new[] { offered, accepted, revoked })
        {
            var state = new WorldControlPermissionState([slot]);
            byte[] bytes = WorldControlPermissionCodecV1.Encode(state);
            var restored = WorldControlPermissionCodecV1.Decode(bytes);
            Assert.Equal(slot, Assert.Single(restored.Slots));
            Assert.Equal(slot.Revision, restored.Revision);
            Assert.Equal(bytes, WorldControlPermissionCodecV1.Encode(restored));
        }
        Assert.Equal(WorldControlPermissionStatus.Offered, offered.Status);
        Assert.Equal(WorldControlPermissionStatus.Accepted, accepted.Status);
        Assert.Equal(WorldControlPermissionStatus.Revoked, revoked.Status);
        Assert.Null(revoked.Terms);
        Assert.Equal(terms.ContentHash, accepted.TermsHash);
        Assert.NotEqual(terms.ContentHash, Terms(WorldControlCapabilities.CombatAndSurvival | WorldControlCapabilities.PrivateHistory).ContentHash);
        Assert.Equal(0UL, WorldControlPermissionState.Empty.Revision);
        Assert.Empty(WorldControlPermissionState.Empty.Slots);
        Assert.False(WorldControlCapabilities.CombatAndSurvival.HasFlag(WorldControlCapabilities.CareDisposition));
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(512UL)]
    [InlineData(519UL)]
    public void Empty_and_unknown_scope_bits_never_enter_terms_or_commands(ulong value)
    {
        Assert.Throws<ArgumentException>(() => Terms((WorldControlCapabilities)value));
        Assert.Throws<ArgumentException>(() => new OfferControlPermissionIntent(Id(6), 0, (WorldControlCapabilities)value));
    }

    [Fact]
    public void Slot_structure_rejects_self_alias_future_acceptance_and_reused_causes()
    {
        var terms = Terms();
        Assert.Throws<ArgumentException>(() => new WorldControlPermissionSlot(Id(3), Id(3), 1, 1));
        Assert.Throws<ArgumentException>(() => new WorldControlPermissionSlot(Id(3), Id(6), 2, 1, terms, 1));
        Assert.Throws<ArgumentException>(() => new WorldControlPermissionSlot(Id(3), Id(6), 1, 2));
        Assert.Throws<ArgumentException>(() => new WorldControlPermissionSlot(Id(3), Id(6), 2, 4, terms, 3, Id(11), 5));
        Assert.Throws<ArgumentException>(() => new WorldControlPermissionSlot(Id(3), Id(6), 2, 4, terms, 3, terms.GrantCommandId, 4));
        var slot = new WorldControlPermissionSlot(Id(3), Id(6), 1, 1, terms, 1);
        Assert.Throws<ArgumentException>(() => new WorldControlPermissionState([slot, slot]));
        Assert.Throws<ArgumentException>(() => new WorldControlPermissionTerms(Id(1), Id(2), Id(3), Id(4), Id(5),
            Id(6), Id(4), Id(8), Id(9), WorldControlCapabilities.All));
    }

    [Fact]
    public void Strict_state_decoder_refuses_wrong_policy_trailing_truncation_and_unsorted_slots()
    {
        var first = new WorldControlPermissionSlot(Id(3), Id(6), 1, 1, Terms(), 1);
        var second = new WorldControlPermissionSlot(Id(6), Id(3), 2, 4);
        byte[] canonical = WorldControlPermissionCodecV1.Encode(new([second, first]));
        var fields = Fields(canonical);
        var rows = Fields(fields[2]);
        byte[] reordered = Frame(fields[0], fields[1], Frame(rows[0], rows[2], rows[1]));
        Assert.Throws<FormatException>(() => WorldControlPermissionCodecV1.Decode(reordered));
        byte[] wrongPolicy = Frame(fields[0], new byte[32], fields[2]);
        Assert.Throws<FormatException>(() => WorldControlPermissionCodecV1.Decode(wrongPolicy));
        Assert.Throws<FormatException>(() => WorldControlPermissionCodecV1.Decode(canonical[..^1]));
        Assert.Throws<FormatException>(() => WorldControlPermissionCodecV1.Decode(canonical.Concat(new byte[] { 0 }).ToArray()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Permission_action_and_isolated_profile39_batch_roundtrip_and_legacy_profiles_refuse(int kind)
    {
        ControlPermissionIntent intent = kind switch
        {
            0 => new OfferControlPermissionIntent(Id(6), 0, WorldControlCapabilities.All),
            1 => new AcceptControlPermissionIntent(Id(3), 1, Id(9), Terms().ContentHash),
            _ => new RevokeControlPermissionIntent(Id(6), 2)
        };
        var command = new WorldActionCommand(Id(20), Id(4), Id(5), Id(3), Id(1), Id(2), 0, intent);
        byte[] bytes = WorldActionCodec.Encode(command);
        Assert.Equal(intent, WorldActionCodec.Decode(bytes).Intent);
        Assert.Equal(bytes, WorldActionCodecV22.Encode(command));
        Assert.Throws<FormatException>(() => WorldActionCodecV21.Decode(bytes));
        Assert.Throws<FormatException>(() => WorldActionCodecV22.Decode(bytes[..^1]));
        var batch = new WorldStepBatch(Id(21), Id(1), Id(2), 3, 0, WorldBatchKind.ControlPermission, [], [command], 39);
        byte[] batchBytes = WorldStepCodec.Encode(batch);
        Assert.Equal(batchBytes, WorldStepCodecV39.Encode(WorldStepCodec.Decode(batchBytes)));
        Assert.Throws<FormatException>(() => WorldStepCodecV38.Decode(batchBytes));
        foreach (ushort profile in new ushort[] { 1, 26, 35, 36, 37, 38 })
            Assert.Throws<ArgumentException>(() => new WorldStepBatch(Id(21), Id(1), Id(2), 3, 0,
                WorldBatchKind.ControlPermission, [], [command], profile));
        Assert.Throws<ArgumentException>(() => new WorldStepBatch(Id(21), Id(1), Id(2), 3, 0,
            WorldBatchKind.AdvancingStep, [], [command], 39));
        Assert.Throws<ArgumentException>(() => new WorldStepBatch(Id(21), Id(1), Id(2), 3, 0,
            WorldBatchKind.ControlPermission, [], [], 39));
    }

    [Fact]
    public void Known_permission_milestones_require_distinct_sequences_and_sufficient_prior_commits()
    {
        Assert.Throws<ArgumentException>(() => new WorldControlPermissionSlot(Id(3), Id(6), 4, 4, Terms(), 1, Id(11), 4));
        var first = new WorldControlPermissionSlot(Id(3), Id(6), 1, 1, Terms(), 1);
        var reversedTerms = new WorldControlPermissionTerms(Id(1), Id(2), Id(6), Id(7), Id(8), Id(3), Id(4), Id(5), Id(10), WorldControlCapabilities.All);
        var sameSequence = new WorldControlPermissionSlot(Id(6), Id(3), 1, 1, reversedTerms, 1);
        Assert.Throws<ArgumentException>(() => new WorldControlPermissionState([first, sameSequence]));
        Assert.Throws<ArgumentException>(() => new WorldControlPermissionState([
            new(Id(3), Id(6), 3, 3), new(Id(6), Id(3), 3, 4)]));
        var lawful = new WorldControlPermissionState([new(Id(3), Id(6), 3, 3), new(Id(6), Id(3), 3, 6)]);
        Assert.Equal(6UL, lawful.Revision);
        var bytes = WorldControlPermissionCodecV1.Encode(lawful);
        var outer = Fields(bytes); var slots = Fields(outer[2]); var second = Fields(slots[2]);
        second[4] = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(second[4], 4);
        Assert.Throws<FormatException>(() => WorldControlPermissionCodecV1.Decode(
            Frame(outer[0], outer[1], Frame(slots[0], slots[1], Frame(second)))));
    }

    [Fact]
    public void Legacy_action_dispatch_keeps_exact_legacy_bytes()
    {
        var command = new WorldActionCommand(Id(20), Id(4), Id(5), Id(3), Id(1), Id(2), 0, new CloseDialogueIntent(Id(15)));
        Assert.Equal(WorldActionCodecV1.Encode(command), WorldActionCodec.Encode(command));
        Assert.Throws<ArgumentException>(() => WorldActionCodecV22.Encode(command));
    }

    private static WorldControlPermissionTerms Terms(WorldControlCapabilities capabilities = WorldControlCapabilities.CombatAndSurvival) =>
        new(Id(1), Id(2), Id(3), Id(4), Id(5), Id(6), Id(7), Id(8), Id(9), capabilities);
    private static EntityId Id(ulong value) => new(0xc039000000000000, value);
    private static byte[][] Fields(byte[] bytes)
    {
        int position = 12;
        var fields = new List<byte[]>();
        while (position < bytes.Length)
        {
            int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position)));
            position += 4; fields.Add(bytes.AsSpan(position, count).ToArray()); position += count;
        }
        return fields.ToArray();
    }
    private static byte[] Frame(params byte[][] fields)
    {
        using var stream = new MemoryStream();
        stream.Write("OFC\0"u8); Write(1); Write(checked((uint)fields.Length));
        foreach (var field in fields) { Write(checked((uint)field.Length)); stream.Write(field); }
        return stream.ToArray();
        void Write(uint value) { Span<byte> bytes = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(bytes, value); stream.Write(bytes); }
    }
}
