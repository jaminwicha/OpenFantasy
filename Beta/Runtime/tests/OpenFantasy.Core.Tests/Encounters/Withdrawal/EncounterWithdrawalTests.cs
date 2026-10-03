using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Encounters.Withdrawal;

public sealed class EncounterWithdrawalTests(ITestOutputHelper output)
{
    private static EntityId Id(ulong value) => new(0x5749544844524157, value);
    private static readonly EncounterTimingScope Scope = new(Id(1), Id(2), Id(3));

    private static EncounterWithdrawalMember Member(ulong slot, int length = 3, uint speed = 100,
        int walked = 0, ulong? command = null)
    {
        var origin = new SubunitPosition(checked((long)slot * 256), 512, 256);
        var route = Enumerable.Repeat(EncounterRetreatDirection.North, length).ToArray();
        var intent = new QueueEncounterRetreatIntent(Scope.EncounterId, 7,
            QueueEncounterRetreatIntent.Endpoint(origin, route), route);
        return new(new WorldActionCommand(Id(command ?? 10_000 + slot), Id(20_000 + slot), Id(30_000 + slot),
            Id(100 + slot), Scope.WorldId, Scope.BranchId, 4, intent), origin, speed, walked);
    }

    private static EncounterWithdrawalParty Fresh(params EncounterWithdrawalMember[] members) =>
        EncounterWithdrawalParty.Fresh(Scope, 1, members.Select(member => member.ActorId));

    private static EncounterWithdrawalParty Step(EncounterWithdrawalParty party, EntityId actor)
    {
        var member = party.GetMember(actor);
        return party.RecordMoved(party.Revision, actor, member.Command.CommandId, member.ExpectedFeet, member.NextFeet);
    }

    [Fact]
    public void Every_member_keeps_their_own_choice_speed_route_and_progress_without_moving_an_ally()
    {
        var slow = Member(1, 2, 70);
        var fast = Member(2, 3, 130);
        var fresh = Fresh(slow, fast);
        var party = fresh.RecordAccepted(0, slow);
        Assert.Single(party.Members);
        Assert.False(party.AllArrived);
        Assert.Throws<KeyNotFoundException>(() => party.GetMember(fast.ActorId));
        party = party.RecordAccepted(1, fast);
        party = Step(party, slow.ActorId);
        Assert.Equal(18UL, party.GetMember(slow.ActorId).WalkedReadiness);
        Assert.Equal(0UL, party.GetMember(fast.ActorId).WalkedReadiness);
        Assert.Equal(fast.Origin, party.GetMember(fast.ActorId).ExpectedFeet);
        Assert.False(party.AllArrived);
        party = Step(party, slow.ActorId);
        Assert.True(party.GetMember(slow.ActorId).HasArrived);
        Assert.False(party.AllArrived);
        for (int i = 0; i < 3; i++) party = Step(party, fast.ActorId);
        Assert.True(party.AllArrived);
        Assert.Equal(30UL, party.GetMember(fast.ActorId).WalkedReadiness);
        Assert.Equal(WorldActionCodec.Encode(slow.Command), WorldActionCodec.Encode(party.GetMember(slow.ActorId).Command));
        Assert.Empty(fresh.Members);
        Assert.Equal(0UL, fresh.Revision);
    }

    [Fact]
    public void Ending_a_choice_never_removes_the_person_from_departure_obligations()
    {
        var first = Member(1, 1);
        var second = Member(2, 1);
        var party = Fresh(first, second).RecordAccepted(0, first).RecordAccepted(1, second);
        party = Step(Step(party, first.ActorId), second.ActorId);
        Assert.True(party.AllArrived);
        var stopped = party.RecordEnded(party.Revision, second.ActorId, second.Command.CommandId);
        Assert.False(stopped.AllArrived);
        Assert.Equal(party.RequiredActors, stopped.RequiredActors);
        Assert.Single(stopped.Members);
        var next = Member(2, 2, command: 90_002);
        var resumed = stopped.RecordAccepted(stopped.Revision, next);
        Assert.Equal(0, resumed.GetMember(second.ActorId).SamplesWalked);
        Assert.Equal(1, resumed.GetMember(first.ActorId).SamplesWalked);
    }

    [Fact]
    public void Stale_choices_teleports_and_extra_steps_are_refused_without_mutating_the_basis()
    {
        var member = Member(1, 1);
        var party = Fresh(member).RecordAccepted(0, member);
        byte[] before = EncounterWithdrawalCodecV1.Encode(party);
        Assert.Throws<InvalidOperationException>(() => party.RecordEnded(0, member.ActorId, member.Command.CommandId));
        Assert.Throws<ArgumentException>(() => party.RecordEnded(1, member.ActorId, Id(80_000)));
        Assert.Throws<ArgumentException>(() => party.RecordAccepted(1, member));
        Assert.Throws<ArgumentException>(() => party.RecordMoved(1, member.ActorId, member.Command.CommandId,
            member.Origin, member.Origin.Offset(0, -64, 0)));
        Assert.Throws<ArgumentException>(() => party.RecordMoved(1, member.ActorId, member.Command.CommandId,
            member.Origin.Offset(32, 0, 0), member.NextFeet));
        Assert.Equal(before, EncounterWithdrawalCodecV1.Encode(party));
        var arrived = Step(party, member.ActorId);
        Assert.Throws<InvalidOperationException>(() => _ = arrived.GetMember(member.ActorId).NextFeet);
        Assert.Throws<ArgumentException>(() => arrived.RecordMoved(arrived.Revision, member.ActorId,
            member.Command.CommandId, member.NextFeet, member.NextFeet.Offset(0, -32, 0)));
    }

    [Fact]
    public void Restore_requires_complete_distinct_membership_scope_and_enough_recorded_progress()
    {
        var member = Member(1);
        var other = Member(2);
        Assert.Throws<ArgumentException>(() => EncounterWithdrawalParty.Fresh(Scope, 1, []));
        Assert.Throws<ArgumentException>(() => Fresh(member, member));
        Assert.Throws<ArgumentException>(() => EncounterWithdrawalParty.Fresh(Scope, 1, [Scope.WorldId]));
        Assert.Throws<ArgumentOutOfRangeException>(() => EncounterWithdrawalParty.Fresh(Scope, 0, [member.ActorId]));
        Assert.Throws<ArgumentException>(() => Fresh(member).RecordAccepted(0, other));
        Assert.Throws<ArgumentException>(() => new EncounterWithdrawalParty(Scope, 1, 0, [member.ActorId], [member]));
        Assert.Throws<ArgumentException>(() => new EncounterWithdrawalParty(Scope, 1, 1,
            [member.ActorId], [Member(1, walked: 1)]));
        Assert.Throws<ArgumentException>(() => new EncounterWithdrawalParty(new(Id(91), Id(92), Id(93)),
            1, 1, [member.ActorId], [member]));
        Assert.Throws<ArgumentException>(() => new EncounterWithdrawalParty(Scope, 1, 2,
            [member.ActorId, other.ActorId], [member, Member(2, command: 10_001)]));
        Assert.Throws<ArgumentException>(() => new EncounterWithdrawalParty(Scope, 1, 1,
            [member.ActorId, other.ActorId], [Member(1, command: 102)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Member(1, speed: 0));
        Assert.Throws<ArgumentException>(() => Member(1, walked: 4));
        Assert.Throws<ArgumentException>(() => new EncounterWithdrawalMember(member.Command, member.Origin.Offset(32, 0, 0), 100));
        Assert.Throws<OverflowException>(() => new EncounterWithdrawalParty(Scope, 1, ulong.MaxValue,
            [member.ActorId], []).RecordAccepted(ulong.MaxValue, member));
    }

    [Fact]
    public void Caller_arrays_cannot_rewrite_membership_or_retained_choices()
    {
        var member = Member(1);
        EntityId[] required = [member.ActorId];
        EncounterWithdrawalMember[] members = [member];
        var party = new EncounterWithdrawalParty(Scope, 1, 1, required, members);
        required[0] = Id(888);
        members[0] = Member(2);
        Assert.Equal(member.ActorId, Assert.Single(party.RequiredActors));
        Assert.Same(member, Assert.Single(party.Members));
    }

    [Fact]
    public void Canonical_round_trip_preserves_partial_arrival_and_independent_command_attribution()
    {
        var first = Member(1, 2, 70, 2);
        var second = Member(2, 3, 130, 1);
        var party = new EncounterWithdrawalParty(Scope, 1, 5, [second.ActorId, first.ActorId], [second, first]);
        byte[] bytes = EncounterWithdrawalCodecV1.Encode(party);
        var restored = EncounterWithdrawalCodecV1.Decode(bytes);
        Assert.Equal(bytes, EncounterWithdrawalCodecV1.Encode(restored));
        Assert.Equal(party.ContentHash, restored.ContentHash);
        Assert.True(restored.GetMember(first.ActorId).HasArrived);
        Assert.False(restored.AllArrived);
        Assert.Equal(second.ExpectedFeet, restored.GetMember(second.ActorId).ExpectedFeet);
        Assert.Equal(second.Command.PrincipalId, restored.GetMember(second.ActorId).Command.PrincipalId);
        Assert.Equal(second.Command.OriginSessionId, restored.GetMember(second.ActorId).Command.OriginSessionId);
        Assert.Equal(36UL, restored.GetMember(first.ActorId).WalkedReadiness);
    }

    [Fact]
    public void Restoring_after_every_sample_preserves_the_component_result_of_independent_interleavings()
    {
        var members = new[] { Member(1, 1), Member(2, 2), Member(3, 3), Member(4, 4) };
        var basis = Fresh(members);
        foreach (var member in members) basis = basis.RecordAccepted(basis.Revision, member);
        var serial = basis;
        foreach (var member in members)
            for (int i = 0; i < member.Intent.Route.Count; i++) serial = Step(serial, member.ActorId);
        var interleaved = basis;
        for (int round = 0; round < 4; round++)
            foreach (var member in members.Reverse().Where(member => round < member.Intent.Route.Count))
                interleaved = EncounterWithdrawalCodecV1.Decode(EncounterWithdrawalCodecV1.Encode(Step(interleaved, member.ActorId)));
        // This compares only independent commitment bookkeeping; world effects still retain their actual order.
        Assert.Equal(serial.ContentHash, interleaved.ContentHash);
        Assert.True(interleaved.AllArrived);
    }

    [Fact]
    public void Maximum_member_and_route_counts_fit_the_declared_component_bound()
    {
        var members = Enumerable.Range(1, EncounterWithdrawalParty.MaximumMembers)
            .Select(slot => Member((ulong)slot, 32, 1, 32)).ToArray();
        var party = new EncounterWithdrawalParty(Scope, 1, (ulong)members.Length * 33,
            members.Select(member => member.ActorId), members);
        byte[] bytes = EncounterWithdrawalCodecV1.Encode(party);
        output.WriteLine($"Maximum member/route fixture: {members.Length} members, 32 samples each, {bytes.Length} bytes, hash {party.ContentHash}.");
        Assert.True(bytes.Length <= EncounterWithdrawalCodecV1.MaximumEncodedByteLength);
        Assert.Equal(party.ContentHash, EncounterWithdrawalCodecV1.Decode(bytes).ContentHash);
        Assert.All(party.Members, member => Assert.Equal(40_000UL, member.WalkedReadiness));
        Assert.Throws<ArgumentException>(() => EncounterWithdrawalParty.Fresh(Scope, 1,
            Enumerable.Range(1, EncounterWithdrawalParty.MaximumMembers + 1).Select(slot => Id((ulong)slot + 100))));
    }

    [Fact]
    public void Delegated_attribution_survives_component_restore_without_becoming_authorization()
    {
        var original = Member(1, 32);
        var witness = new WorldControlWitness(original.ActorId, Id(102), Id(40_000), Id(40_001), 2,
            WorldControlCapabilities.Combat, ContentHash.Compute("withdrawal-attribution"u8), Id(40_002), 1, 5, Id(40_003), 2);
        var command = new WorldActionCommand(Id(40_004), Id(20_002), Id(30_002), original.ActorId,
            Scope.WorldId, Scope.BranchId, 4, original.Intent, witness);
        var member = new EncounterWithdrawalMember(command, original.Origin, 100);
        var party = Fresh(member).RecordAccepted(0, member);
        var restored = EncounterWithdrawalCodecV1.Decode(EncounterWithdrawalCodecV1.Encode(party));
        Assert.Equal(witness, restored.GetMember(member.ActorId).Command.ControlWitness);
        Assert.Equal(WorldActionCodec.Encode(command), WorldActionCodec.Encode(restored.GetMember(member.ActorId).Command));
        // No inventory, permission state or authority was supplied: this is retained attribution only.
        Assert.Equal(0, restored.GetMember(member.ActorId).SamplesWalked);
    }

    [Fact]
    public void Another_action_kind_cannot_be_restored_as_a_withdrawal_choice()
    {
        var basis = Member(1);
        var command = new WorldActionCommand(Id(50_000), basis.Command.PrincipalId, basis.Command.OriginSessionId,
            basis.ActorId, Scope.WorldId, Scope.BranchId, 4, new QueueEncounterWaitIntent(Scope.EncounterId, 7));
        Assert.Throws<ArgumentException>(() => new EncounterWithdrawalMember(command, basis.Origin, 100));
        var fields = Fields(EncounterWithdrawalCodecV1.Encode(Fresh(basis).RecordAccepted(0, basis)));
        var members = Fields(fields[9]); var member = Fields(members[1]);
        member[1] = WorldActionCodec.Encode(command); members[1] = Frame(member); fields[9] = Frame(members);
        Assert.Throws<FormatException>(() => EncounterWithdrawalCodecV1.Decode(Frame(fields)));
    }

    [Theory]
    [InlineData("rules")]
    [InlineData("side")]
    [InlineData("revision")]
    [InlineData("membership-order")]
    [InlineData("member-order")]
    [InlineData("zero-speed")]
    [InlineData("extra-step")]
    public void Mutated_claims_are_refused_even_when_other_canonical_fields_are_intact(string mutation)
    {
        var first = Member(1);
        var second = Member(2);
        byte[] bytes = EncounterWithdrawalCodecV1.Encode(Fresh(first, second).RecordAccepted(0, first).RecordAccepted(1, second));
        var fields = Fields(bytes);
        switch (mutation)
        {
            case "rules": fields[2][0] ^= 1; break;
            case "side": fields[6] = new byte[8]; break;
            case "revision": fields[7] = new byte[8]; break;
            case "membership-order": fields[8] = fields[8][16..].Concat(fields[8][..16]).ToArray(); break;
            case "member-order":
                var rows = Fields(fields[9]); (rows[1], rows[2]) = (rows[2], rows[1]); fields[9] = Frame(rows); break;
            default:
                var members = Fields(fields[9]); var member = Fields(members[1]);
                if (mutation == "zero-speed") member[3] = new byte[8];
                else BinaryPrimitives.WriteUInt64BigEndian(member[4], 4);
                members[1] = Frame(member); fields[9] = Frame(members); break;
        }
        Assert.Throws<FormatException>(() => EncounterWithdrawalCodecV1.Decode(Frame(fields)));
    }

    [Fact]
    public void Codec_refuses_truncation_trailing_bytes_excessive_size_and_cross_format_interpretation()
    {
        var member = Member(1);
        byte[] bytes = EncounterWithdrawalCodecV1.Encode(Fresh(member).RecordAccepted(0, member));
        foreach (int length in new[] { 0, 4, 11, 12, 32, bytes.Length / 2, bytes.Length - 1 })
            Assert.Throws<FormatException>(() => EncounterWithdrawalCodecV1.Decode(bytes.AsSpan(0, length)));
        Assert.Throws<FormatException>(() => EncounterWithdrawalCodecV1.Decode(bytes.Concat(new byte[] { 0 }).ToArray()));
        Assert.Throws<FormatException>(() => EncounterWithdrawalCodecV1.Decode(new byte[EncounterWithdrawalCodecV1.MaximumEncodedByteLength + 1]));
        Assert.Throws<FormatException>(() => EncounterRetreatCodecV1.Decode(bytes));
        Assert.Throws<FormatException>(() => EncounterWithdrawalCodecV1.Decode(EncounterRetreatCodecV1.Encode(EncounterRetreatState.Fresh())));
    }

    private static byte[][] Fields(byte[] bytes)
    {
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8)));
        var fields = new byte[count][];
        int offset = 12;
        for (int i = 0; i < count; i++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset))); offset += 4;
            fields[i] = bytes.AsSpan(offset, length).ToArray(); offset += length;
        }
        Assert.Equal(bytes.Length, offset);
        return fields;
    }

    private static byte[] Frame(byte[][] fields)
    {
        byte[] bytes = new byte[12 + fields.Sum(field => 4 + field.Length)];
        "OFC\0"u8.CopyTo(bytes); BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), (uint)fields.Length);
        int offset = 12;
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset), (uint)field.Length); offset += 4;
            field.CopyTo(bytes, offset); offset += field.Length;
        }
        return bytes;
    }
}
