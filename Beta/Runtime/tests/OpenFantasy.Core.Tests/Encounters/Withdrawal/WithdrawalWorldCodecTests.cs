using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Encounters.Withdrawal;

public sealed class WithdrawalWorldCodecTests(ITestOutputHelper output)
{
    private static EntityId Id(ulong id) => new(0x5749544844524157, id);
    private static readonly EncounterTimingScope Scope = new(Id(1), Id(2), Id(3));
    private static EncounterWithdrawalMember Member(ulong slot, int walked = 0)
    {
        var origin = new SubunitPosition((long)slot * 256, 512, 256);
        var route = Enumerable.Repeat(EncounterRetreatDirection.North, 32).ToArray();
        var intent = new QueueEncounterRetreatIntent(Scope.EncounterId, 7,
            QueueEncounterRetreatIntent.Endpoint(origin, route), route);
        return new(new(Id(10_000 + slot), Id(20_000 + slot), Id(30_000 + slot), Id(100 + slot),
            Scope.WorldId, Scope.BranchId, 4, intent), origin, 100, walked);
    }
    private static EncounterWithdrawalParty Party(ushort side, params EncounterWithdrawalMember[] members) =>
        new(Scope, side, (ulong)members.Sum(member => member.SamplesWalked + 1), members.Select(member => member.ActorId), members);
    private static EncounterRetreatState Owner(params EncounterWithdrawalParty[] parties) =>
        new(EncounterRetreatRules.Pin, 0, null, null, new(5000, parties));

    [Fact]
    public void Two_sides_and_terminal_records_round_trip_with_all_actor_and_command_identities()
    {
        var first = Member(1, 12);
        var second = Member(2, 32);
        var ended = Member(3, 11);
        var last = new EncounterWithdrawalResolution(100, [new(ended, EncounterWithdrawalEndReason.Blocked, ended.SampleReadiness)]);
        var owner = new EncounterRetreatState(EncounterRetreatRules.Pin, 0, null, null,
            new(100, [Party(2, second), Party(1, first)], last));
        var bytes = EncounterRetreatCodecV2.Encode(owner);
        var restored = EncounterRetreatCodecV2.Decode(bytes);
        Assert.Equal(bytes, EncounterRetreatCodecV2.Encode(restored));
        Assert.Equal(owner.ContentHash, restored.ContentHash);
        Assert.True(restored.Holds(first.ActorId));
        Assert.False(restored.Holds(second.ActorId));
        Assert.Equal(ended.WalkedReadiness + ended.SampleReadiness,
            Assert.Single(restored.Withdrawal!.Last!.Ended).ReadinessCharged);
        Assert.Throws<ArgumentException>(() => EncounterRetreatCodecV1.Encode(owner));
    }

    [Fact]
    public void Shared_actor_limit_and_cross_side_identity_collisions_are_rejected()
    {
        var first = Member(1);
        Assert.Throws<ArgumentException>(() => Owner(Party(1, first), Party(2, first)));
        Assert.Throws<ArgumentException>(() => Owner(Party(1, first), Party(1, Member(2))));
        Assert.Throws<ArgumentException>(() => Owner(Party(1, first), Party(2, Member(2)), Party(3, Member(3))));
        Assert.Throws<ArgumentException>(() => Owner(Party(1, Enumerable.Range(1, 64).Select(i => Member((ulong)i)).ToArray()),
            Party(2, Enumerable.Range(65, 65).Select(i => Member((ulong)i)).ToArray())));
        Assert.Throws<ArgumentException>(() => new EncounterRetreatState(EncounterRetreatRules.Pin, 1,
            new EncounterRetreatContinuation(first.Command, first.Origin, 0, 0), null, EncounterWithdrawalState.Empty));
    }

    [Fact]
    public void Full_actor_route_owner_fits_its_bound_without_changing_complete_world_budget()
    {
        var owner = Owner(Party(1, Enumerable.Range(1, 64).Select(i => Member((ulong)i, 31)).ToArray()),
            Party(2, Enumerable.Range(65, 64).Select(i => Member((ulong)i, 32)).ToArray()));
        var bytes = EncounterRetreatCodecV2.Encode(owner);
        Assert.True(bytes.Length <= EncounterRetreatCodecV2.MaximumEncodedByteLength);
        Assert.Equal(16 * 1024 * 1024, WorldSnapshotCodec.MaximumStateByteLength);
        Assert.Equal(bytes, EncounterRetreatCodecV2.Encode(EncounterRetreatCodecV2.Decode(bytes)));
        output.WriteLine($"128 actors,32-sample routes: {bytes.Length} bytes; {owner.ContentHash}");
    }

    [Fact]
    public void Unknown_rules_truncation_trailing_and_legacy_bytes_refuse_new_decode()
    {
        byte[] bytes = EncounterRetreatCodecV2.Encode(Owner(Party(1, Member(1))));
        byte[] wrong = bytes.ToArray();
        wrong[wrong.AsSpan().IndexOf(EncounterWithdrawalState.RulesPin.ToByteArray())] ^= 1;
        Assert.Throws<FormatException>(() => EncounterRetreatCodecV2.Decode(wrong));
        Assert.Throws<FormatException>(() => EncounterRetreatCodecV2.Decode(bytes[..^1]));
        Assert.Throws<FormatException>(() => EncounterRetreatCodecV2.Decode([.. bytes, 0]));
        Assert.Throws<FormatException>(() => EncounterRetreatCodecV1.Decode(bytes));
        Assert.Throws<FormatException>(() => EncounterRetreatCodecV2.Decode(EncounterRetreatCodecV1.Encode(EncounterRetreatState.Fresh())));
    }

    [Theory]
    [InlineData(EncounterWithdrawalEndReason.Blocked)]
    [InlineData(EncounterWithdrawalEndReason.Unadmitted)]
    [InlineData(EncounterWithdrawalEndReason.ControlLost)]
    [InlineData(EncounterWithdrawalEndReason.NoGroundLocomotion)]
    public void Failed_attempt_requires_exact_one_sample_cost_and_an_unwalked_remainder(EncounterWithdrawalEndReason reason)
    {
        var walking = Member(1, 12);
        Assert.Throws<ArgumentException>(() => new EncounterWithdrawalEnd(walking, reason));
        Assert.Throws<ArgumentException>(() => new EncounterWithdrawalEnd(walking, reason, walking.SampleReadiness + 1));
        Assert.Equal(walking.WalkedReadiness + walking.SampleReadiness,
            new EncounterWithdrawalEnd(walking, reason, walking.SampleReadiness).ReadinessCharged);
        Assert.Throws<ArgumentException>(() => new EncounterWithdrawalEnd(Member(1, 32), reason, walking.SampleReadiness));
    }

    [Fact]
    public void Cancellation_does_not_charge_another_sample_or_forge_arrival()
    {
        var walking = Member(1, 12);
        Assert.Throws<ArgumentException>(() => new EncounterWithdrawalEnd(walking, EncounterWithdrawalEndReason.Disengaged));
        Assert.Throws<ArgumentException>(() => new EncounterWithdrawalEnd(walking, EncounterWithdrawalEndReason.ContactRemains));
        Assert.Throws<ArgumentException>(() => new EncounterWithdrawalEnd(walking, EncounterWithdrawalEndReason.ChoiceChanged, walking.SampleReadiness));
        Assert.Equal(walking.WalkedReadiness, new EncounterWithdrawalEnd(walking, EncounterWithdrawalEndReason.ChoiceChanged).ReadinessCharged);
    }

    [Fact]
    public void Cancellation_command_round_trips_and_retains_explicit_version()
    {
        var member = Member(1);
        var command = new WorldActionCommand(Id(40000), member.Command.PrincipalId, member.Command.OriginSessionId,
            member.ActorId, Scope.WorldId, Scope.BranchId, 4,
            new CancelEncounterWithdrawalIntent(Scope.EncounterId, 9, member.Command.CommandId));
        byte[] bytes = WorldActionCodec.Encode(command);
        Assert.Equal(bytes, WorldActionCodecV24.Encode(command));
        Assert.Equal(bytes, WorldActionCodec.Encode(WorldActionCodec.Decode(bytes)));
        Assert.Throws<FormatException>(() => WorldActionCodecV19.Decode(bytes));
        Assert.Throws<FormatException>(() => WorldActionCodecV24.Decode([.. bytes, 0]));
        Assert.Throws<FormatException>(() => WorldActionCodecV24.Decode(bytes[..^1]));
    }
}
