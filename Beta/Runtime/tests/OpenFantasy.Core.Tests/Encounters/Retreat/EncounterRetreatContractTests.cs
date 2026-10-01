using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Encounters.EncounterLifecycleFixture;

namespace OpenFantasy.Core.Tests.Encounters.Retreat;

public sealed class EncounterRetreatContractTests
{
    private static readonly EncounterRetreatDirection[] Route =
        [EncounterRetreatDirection.West, EncounterRetreatDirection.West, EncounterRetreatDirection.North];

    internal static WorldActionCommand Command(ulong commandId = 900, IEnumerable<EncounterRetreatDirection>? route = null,
        SubunitPosition? origin = null, ulong timingRevision = 7)
    {
        EncounterRetreatDirection[] samples = (route ?? Route).ToArray();
        SubunitPosition from = origin ?? new SubunitPosition(1_000, 2_000, 0);
        return new WorldActionCommand(Id(commandId), Id(800), Id(801), Id(100), World, Branch, 3,
            new QueueEncounterRetreatIntent(Id(10_000), timingRevision, QueueEncounterRetreatIntent.Endpoint(from, samples), samples));
    }

    [Fact]
    public void The_pinned_law_charges_an_eighth_action_per_sample_rounded_up_and_keeps_a_full_cell_margin()
    {
        Assert.Equal(32, EncounterRetreatRules.SampleSubunits);
        // Exact charges: ceil(10000·100/(800·speed)); at speed 100 a cell (8 samples) charges 104, not 100.
        Assert.Equal(13UL, EncounterRetreatRules.SampleDelay(100));
        Assert.Equal(1_250UL, EncounterRetreatRules.SampleDelay(1));
        Assert.Equal(1UL, EncounterRetreatRules.SampleDelay(2_000));
        Assert.Equal(18UL, EncounterRetreatRules.SampleDelay(70));
        foreach (uint speed in new uint[] { 1, 70, 100, 130, 333, 2_000 })
        {
            ulong standard = (10_000UL + speed - 1) / speed;
            ulong cell = EncounterRetreatRules.SampleDelay(speed) * 8;
            // A cell never charges less than one standard action, and rounding adds less than one unit per sample.
            Assert.InRange(cell, standard, standard + 8);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => EncounterRetreatRules.SampleDelay(0));
        // The margin is a full cell even for the longest interaction range; there is no cap that eats it.
        Assert.Equal(608, EncounterRetreatRules.ContactRadius(352));
        Assert.Equal(LocalSpatialSnapshot.InteractionRangeSubunits + LocalSpatialSnapshot.MaximumMoveSubunits,
            EncounterRetreatRules.ContactRadius(LocalSpatialSnapshot.InteractionRangeSubunits));
        Assert.Throws<ArgumentOutOfRangeException>(() => EncounterRetreatRules.ContactRadius(0));
        Assert.StartsWith("encounter.retreat.v2;", EncounterRetreatRules.RulesText);
        Assert.Contains("sightline-floor-ignored", EncounterRetreatRules.RulesText);
        Assert.Contains("exactly-two-positive-sides", EncounterRetreatRules.RulesText);
        Assert.Equal(EncounterRetreatRules.Pin, EncounterRetreatState.Fresh().RulesPin);
    }

    [Fact]
    public void Only_a_lone_combatant_against_exactly_one_other_side_may_retreat()
    {
        EntityId[] none = [];
        EncounterCombatant Actor(ulong id, ushort side) => new(Id(id), side, none);
        // One against one, with any number of bystanders, and one against several opponents of a single side.
        Assert.True(EncounterRetreatRules.IsSupportedScope([Actor(1, 1), Actor(2, 2), Actor(3, 0)], Id(1)));
        Assert.True(EncounterRetreatRules.IsSupportedScope([Actor(1, 1), Actor(2, 2), Actor(3, 2)], Id(1)));
        Assert.False(EncounterRetreatRules.IsSupportedScope([Actor(1, 1), Actor(2, 2), Actor(3, 2)], Id(2)));
        // An ally (living or downed: the rule is about who would be left behind, not their health) refuses the retreat.
        Assert.False(EncounterRetreatRules.IsSupportedScope([Actor(1, 1), Actor(4, 1), Actor(2, 2)], Id(1)));
        // A third side would have its own fight ended by someone else's exit.
        Assert.False(EncounterRetreatRules.IsSupportedScope([Actor(1, 1), Actor(2, 2), Actor(3, 3)], Id(1)));
        // A bystander never retreats and an episode with no opposing side offers nothing to leave.
        Assert.False(EncounterRetreatRules.IsSupportedScope([Actor(1, 1), Actor(2, 2), Actor(3, 0)], Id(3)));
        Assert.False(EncounterRetreatRules.IsSupportedScope([Actor(1, 1), Actor(3, 0)], Id(1)));
    }

    [Fact]
    public void A_route_names_one_to_thirty_two_known_cardinal_samples_and_derives_its_flat_endpoint()
    {
        var origin = new SubunitPosition(0, 0, 512);
        Assert.Equal(new SubunitPosition(-64, -32, 512), QueueEncounterRetreatIntent.Endpoint(origin, Route));
        Assert.Throws<ArgumentException>(() => new QueueEncounterRetreatIntent(Id(1), 0, origin, []));
        Assert.Throws<ArgumentException>(() => new QueueEncounterRetreatIntent(Id(1), 0, origin,
            Enumerable.Repeat(EncounterRetreatDirection.East, EncounterRetreatRules.MaximumRouteSamples + 1)));
        Assert.Throws<ArgumentException>(() => new QueueEncounterRetreatIntent(Id(1), 0, origin, [(EncounterRetreatDirection)5]));
        var longest = new QueueEncounterRetreatIntent(Id(1), 0, origin,
            Enumerable.Repeat(EncounterRetreatDirection.South, EncounterRetreatRules.MaximumRouteSamples));
        Assert.Equal(EncounterRetreatRules.MaximumRouteSamples, longest.Route.Count);
    }

    [Fact]
    public void Retreat_requests_use_their_own_command_version_and_reject_malformed_routes()
    {
        WorldActionCommand command = Command();
        byte[] bytes = WorldActionCodec.Encode(command);
        Assert.Equal(19UL, BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(12 + 4 + "world.actor-action".Length + 4, 8)));
        WorldActionCommand restored = WorldActionCodec.Decode(bytes);
        var intent = Assert.IsType<QueueEncounterRetreatIntent>(restored.Intent);
        Assert.Equal(Route, intent.Route);
        Assert.Equal(command.ContentHash, restored.ContentHash);
        Assert.Equal(bytes, WorldActionCodecV19.Encode(restored));

        // Each route byte is one known direction; an unknown direction, trailing byte or truncation is refused.
        int routeAt = bytes.Length - Route.Length;
        foreach (byte invalid in new byte[] { 0, 5, 255 })
        {
            byte[] corrupt = bytes.ToArray();
            corrupt[routeAt] = invalid;
            Assert.Throws<FormatException>(() => WorldActionCodec.Decode(corrupt));
        }
        Assert.ThrowsAny<FormatException>(() => WorldActionCodec.Decode(bytes.Append((byte)0).ToArray()));
        Assert.ThrowsAny<FormatException>(() => WorldActionCodec.Decode(bytes[..^1]));
        // A Wait or ability codec never encodes a retreat; a retreat codec never encodes them.
        Assert.Throws<ArgumentException>(() => WorldActionCodecV10.Encode(command));
        Assert.Throws<ArgumentException>(() => WorldActionCodecV19.Encode(new WorldActionCommand(Id(901), Id(800), Id(801),
            Id(100), World, Branch, 3, new QueueEncounterWaitIntent(Id(10_000), 7))));
    }

    [Fact]
    public void The_retreat_owner_round_trips_canonically_with_its_continuation_and_terminal_record()
    {
        WorldActionCommand command = Command();
        var origin = new SubunitPosition(1_000, 2_000, 0);
        var continuation = new EncounterRetreatContinuation(command, origin, 2, 26);
        Assert.Equal(new SubunitPosition(936, 2_000, 0), continuation.ExpectedFeet);
        Assert.Equal(EncounterRetreatDirection.North, continuation.NextDirection);
        var last = new EncounterRetreatOutcome(Id(101), Id(950), Id(9_000), origin, new SubunitPosition(1_000, 2_384, 0),
            12, 12, 156, EncounterRetreatStatus.Disengaged, EncounterRetreatReason.None, 44, [Id(70_002), Id(70_001)]);
        var state = new EncounterRetreatState(EncounterRetreatRules.Pin, 2, continuation, last);
        byte[] bytes = EncounterRetreatCodecV1.Encode(state);
        EncounterRetreatState restored = EncounterRetreatCodecV1.Decode(bytes);
        Assert.Equal(bytes, EncounterRetreatCodecV1.Encode(restored));
        Assert.Equal(2, restored.Active!.SamplesTaken);
        Assert.Equal(command.ContentHash, restored.Active.Command.ContentHash);
        Assert.Equal(new[] { Id(70_001), Id(70_002) }, restored.Last!.CancelledDecisionIds);
        Assert.Equal(state.ContentHash, restored.ContentHash);
        Assert.Equal(EncounterRetreatCodecV1.Encode(EncounterRetreatState.Fresh()),
            EncounterRetreatCodecV1.Encode(EncounterRetreatCodecV1.Decode(EncounterRetreatCodecV1.Encode(EncounterRetreatState.Fresh()))));
        Assert.ThrowsAny<FormatException>(() => EncounterRetreatCodecV1.Decode(bytes.Append((byte)1).ToArray()));
        Assert.ThrowsAny<FormatException>(() => EncounterRetreatCodecV1.Decode(bytes[..^3]));
        byte[] wrongPin = bytes.ToArray();
        wrongPin[bytes.AsSpan().IndexOf(EncounterRetreatRules.Pin.ToByteArray())] ^= 1;
        Assert.ThrowsAny<FormatException>(() => EncounterRetreatCodecV1.Decode(wrongPin));
    }

    [Fact]
    public void Retreat_records_refuse_inconsistent_status_samples_origins_and_counts()
    {
        WorldActionCommand command = Command();
        var origin = new SubunitPosition(1_000, 2_000, 0);
        // The continuation's origin must reproduce the captured destination; a completed route is never a continuation.
        Assert.Throws<ArgumentException>(() => new EncounterRetreatContinuation(command, new SubunitPosition(0, 0, 0), 0, 0));
        Assert.Throws<ArgumentException>(() => new EncounterRetreatContinuation(command, origin, Route.Length, 39));
        Assert.Throws<ArgumentException>(() => new EncounterRetreatContinuation(command, origin, 1, 0));
        Assert.Throws<ArgumentException>(() => new EncounterRetreatOutcome(Id(101), Id(950), Id(9_000), origin, origin,
            2, 3, 26, EncounterRetreatStatus.Disengaged, EncounterRetreatReason.None, 1));
        Assert.Throws<ArgumentException>(() => new EncounterRetreatOutcome(Id(101), Id(950), Id(9_000), origin, origin,
            1, 3, 13, EncounterRetreatStatus.Interrupted, EncounterRetreatReason.None, 1));
        Assert.Throws<ArgumentException>(() => new EncounterRetreatOutcome(Id(101), Id(950), Id(9_000), origin, origin,
            1, 3, 13, EncounterRetreatStatus.Cancelled, EncounterRetreatReason.Blocked, 1));
        Assert.Throws<ArgumentException>(() => new EncounterRetreatOutcome(Id(101), Id(950), Id(9_000), origin, origin,
            1, 3, 13, EncounterRetreatStatus.Interrupted, EncounterRetreatReason.Blocked, 1, [Id(5)]));
        Assert.Throws<ArgumentException>(() => new EncounterRetreatState(EncounterRetreatRules.Pin, 0,
            new EncounterRetreatContinuation(command, origin, 0, 0), null));
        Assert.Throws<ArgumentException>(() => new EncounterRetreatState(ContentHash.Compute([1]), 0, null, null));
        var fresh = EncounterRetreatState.Fresh();
        Assert.Equal(0UL, fresh.AcceptedCount);
        Assert.Null(fresh.Active);
        Assert.Null(fresh.Last);
    }
}
