using System.Buffers.Binary;
using System.Text;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Worlds;

public sealed class WorldPacingTests
{
    private static EntityId Id(ulong value) => new(0, value);
    private static readonly EncounterTimingScope Scope = new(Id(1), Id(2), Id(3));
    private static readonly ContentHash ActionPin = ContentHash.Compute("world pacing work request"u8);
    private static EncounterTimingSnapshot Clock(ulong delay = 1000, ulong revision = 0, ulong units = 0) =>
        new(Scope, revision, units, [new(Id(10), 100, delay, EncounterActorControl.RequiredHuman)], []);
    private static WorldPacingState Begin(WorldInstant origin = default) => WorldPacingState.Begin(Id(1), Id(2), origin);
    private static WorldPacingState Reopen(WorldPacingState state) => WorldPacingCodecV1.Decode(
        WorldPacingCodecV1.Encode(state), Id(1), Id(2), TestContext.Current.CancellationToken);

    [Fact]
    public void Exploration_earns_one_interval_and_preserves_exact_negative_calendar_origin()
    {
        var origin = new WorldInstant(-4, WorldInstant.MillisecondsPerDay - 25);
        var initial = Begin(origin);
        var advanced = initial.AdvanceExploration();
        Assert.Equal(origin, initial.Calendar);
        Assert.Equal(origin, advanced.OriginCalendar);
        Assert.Equal(new WorldInstant(-3, 1775), advanced.Calendar);
        Assert.Equal(advanced.Calendar, advanced.ProcessBoundary);
        Assert.Equal(1UL, advanced.ProcessIntervals);
        Assert.Equal(0UL, advanced.Progress.Remainder);
        Assert.Equal(LocalExposureDuration.FromMilliseconds(1800), advanced.Progress.Elapsed);
        Assert.Equal(Id(1), advanced.Progress.SubjectId);
        Assert.Equal(WorldPacingState.ProcessRate, advanced.Progress.Rate);
        Assert.Null(advanced.Encounter);
        Assert.Null(advanced.EncounterOriginExposure);
        Assert.Equal(WorldPacingCodecV1.Encode(advanced), WorldPacingCodecV1.Encode(Reopen(advanced)));
    }

    [Fact]
    public void Thirty_five_ticks_hold_choices_and_restart_retain_carry_until_the_next_actual_tick()
    {
        var origin = new WorldInstant(7, 123);
        var initial = Begin(origin).AdvanceExploration();
        var pacing = initial.StartEncounter(Clock(175));
        for (int tick = 0; tick < 35; tick++)
        {
            var transition = pacing.Encounter!.TryAdvance(pacing.Encounter.Revision);
            Assert.Equal(EncounterTimingStatus.Advanced, transition.Status);
            pacing = Reopen(pacing.ObserveEncounter(transition.State));
        }
        Assert.Equal(1UL, pacing.ProcessIntervals);
        Assert.Equal(1750UL, pacing.Progress.Remainder);
        Assert.Equal(origin.AddMilliseconds(3550), pacing.Calendar);
        Assert.Equal(origin.AddMilliseconds(1800), pacing.ProcessBoundary);
        byte[] beforeHold = WorldPacingCodecV1.Encode(pacing);
        for (int choiceDelay = 0; choiceDelay < 40; choiceDelay++)
        {
            var held = pacing.Encounter!.TryAdvance(pacing.Encounter.Revision);
            Assert.Equal(EncounterTimingStatus.RequiredDecisionMissing, held.Status);
            Assert.Same(pacing, pacing.ObserveEncounter(held.State));
        }
        var queued = pacing.Encounter!.QueueDecision(pacing.Encounter.Revision,
            new(Id(10), Id(30), EncounterDecisionKind.Wait, ActionPin));
        var selected = Reopen(pacing.ObserveEncounter(queued));
        Assert.Equal(pacing.Progress.Capture(), selected.Progress.Capture());
        var confirmation = selected.Encounter!.TryConfirmNext(selected.Encounter.Revision);
        Assert.Equal(EncounterTimingStatus.Confirmed, confirmation.Status);
        selected = Reopen(selected.ObserveEncounter(confirmation.State));
        Assert.Equal(pacing.Progress.Capture(), selected.Progress.Capture());
        var next = selected.ObserveEncounter(selected.Encounter!.TryAdvance(selected.Encounter.Revision).State);
        Assert.Equal(2UL, next.ProcessIntervals);
        Assert.Equal(0UL, next.Progress.Remainder);
        Assert.Equal(origin.AddMilliseconds(3600), next.Calendar);
        Assert.Equal(next.Calendar, next.ProcessBoundary);
        Assert.Equal(initial.Progress.Elapsed, next.EncounterOriginExposure);
        Assert.Equal(beforeHold, WorldPacingCodecV1.Encode(pacing));
        Assert.Equal(initial.AdvanceExploration().Progress.Capture(), next.Progress.Capture());
    }

    [Fact]
    public void Every_observation_and_one_accepted_descendant_produce_the_same_pacing_bytes()
    {
        var clock = Clock();
        var initial = Begin().StartEncounter(clock);
        var each = initial;
        for (int tick = 0; tick < 39; tick++)
        {
            clock = clock.TryAdvance(clock.Revision).State;
            each = Reopen(each.ObserveEncounter(clock));
        }
        var together = initial.ObserveEncounter(clock);
        Assert.Equal(WorldPacingCodecV1.Encode(each), WorldPacingCodecV1.Encode(together));
        Assert.Equal(150UL, each.Progress.Remainder);
        Assert.Equal(1UL, each.ProcessIntervals);
        Assert.Equal(new WorldInstant(0, 1950), each.Calendar);
        Assert.Equal(new WorldInstant(0, 1800), each.ProcessBoundary);
        Assert.Same(each, each.ObserveEncounter(clock));
        Assert.Throws<InvalidOperationException>(() => each.AdvanceExploration());
        Assert.Throws<ArgumentException>(() => each.StartEncounter(Clock()));
    }

    [Fact]
    public void Entry_and_observation_reject_foreign_stale_substituted_or_excessive_timing()
    {
        var beginning = Begin();
        Assert.Throws<InvalidOperationException>(() => beginning.ObserveEncounter(Clock()));
        Assert.Throws<ArgumentException>(() => beginning.StartEncounter(Clock(revision: 1)));
        Assert.Throws<ArgumentException>(() => beginning.StartEncounter(Clock(revision: 1, units: 5)));
        foreach (var foreignScope in new[] { new EncounterTimingScope(Id(4), Id(2), Id(3)),
            new EncounterTimingScope(Id(1), Id(4), Id(3)) })
        {
            var foreign = new EncounterTimingSnapshot(foreignScope, 0, 0, Clock().Actors, []);
            Assert.Throws<ArgumentException>(() => beginning.StartEncounter(foreign));
        }
        var pacing = beginning.StartEncounter(Clock()).ObserveEncounter(Clock(825, 40, 175));
        byte[] original = WorldPacingCodecV1.Encode(pacing);
        Assert.Throws<ArgumentException>(() => pacing.ObserveEncounter(Clock(824, 40, 175)));
        Assert.Throws<ArgumentException>(() => pacing.ObserveEncounter(Clock(830, 39, 170)));
        Assert.Throws<ArgumentException>(() => pacing.ObserveEncounter(Clock(830, 41, 170)));
        Assert.Throws<ArgumentException>(() => pacing.ObserveEncounter(Clock(815, 41, 185)));
        var otherEncounter = new EncounterTimingSnapshot(new(Id(1), Id(2), Id(4)), 41, 180, Clock().Actors, []);
        Assert.Throws<ArgumentException>(() => pacing.ObserveEncounter(otherEncounter));
        Assert.Equal(original, WorldPacingCodecV1.Encode(pacing));
    }

    [Fact]
    public void Restore_checks_fixed_subject_rate_scope_pairing_exposure_and_carry()
    {
        var valid = Begin().AdvanceExploration().StartEncounter(Clock()).ObserveEncounter(Clock(995, 1, 5));
        ExposureProgressState progress = valid.Progress.Capture();
        WorldPacingState Restore(ExposureProgressState value, EncounterTimingSnapshot? clock,
            LocalExposureDuration? origin) => WorldPacingState.Restore(Id(1), Id(2), default, value, clock, origin);
        Assert.Equal(WorldPacingCodecV1.Encode(valid), WorldPacingCodecV1.Encode(
            Restore(progress, valid.Encounter, valid.EncounterOriginExposure)));
        Assert.Throws<ArgumentException>(() => Restore(progress with { SubjectId = Id(9) }, valid.Encounter, valid.EncounterOriginExposure));
        Assert.Throws<ArgumentException>(() => Restore(progress with { Rate = new(Id(9), ActionPin, 1, 1800) },
            valid.Encounter, valid.EncounterOriginExposure));
        Assert.Throws<ArgumentException>(() => Restore(progress with { Remainder = 0 }, valid.Encounter, valid.EncounterOriginExposure));
        Assert.Throws<ArgumentException>(() => Restore(progress with { CompletedUnits = 2 }, valid.Encounter, valid.EncounterOriginExposure));
        Assert.Throws<ArgumentException>(() => Restore(progress, valid.Encounter, null));
        Assert.Throws<ArgumentException>(() => Restore(progress, null, valid.EncounterOriginExposure));
        Assert.Throws<ArgumentException>(() => Restore(progress, valid.Encounter, LocalExposureDuration.Zero));
        Assert.Throws<ArgumentException>(() => Restore(progress,
            new EncounterTimingSnapshot(new(Id(1), Id(4), Id(3)), 1, 5, Clock().Actors, []), valid.EncounterOriginExposure));
        Assert.Throws<ArgumentException>(() => WorldPacingState.Begin(Id(1), Id(1), default));
    }

    [Fact]
    public void Restored_fractional_progress_is_not_reset_on_encounter_entry()
    {
        // Supplied consistent component state is a restoration test, not proof of admitted world history.
        ExposureProgress prior = ExposureProgress.Begin(Id(1), WorldPacingState.ProcessRate)
            .Advance(LocalExposureDuration.FromMilliseconds(1799));
        var pacing = WorldPacingState.Restore(Id(1), Id(2), default, prior.Capture()).StartEncounter(Clock());
        var next = Reopen(pacing.ObserveEncounter(pacing.Encounter!.TryAdvance(0).State));
        Assert.Equal(1UL, next.ProcessIntervals);
        Assert.Equal(49UL, next.Progress.Remainder);
        Assert.Equal(LocalExposureDuration.FromMilliseconds(1799), next.EncounterOriginExposure);
        Assert.Equal(new WorldInstant(0, 1849), next.Calendar);
    }

    [Fact]
    public void Wide_exposure_and_maximum_timing_sets_remain_exact_and_fit_the_codec()
    {
        var actors = Enumerable.Range(100, EncounterTimingSnapshot.MaximumActors)
            .Select(value => new EncounterActorTiming(Id((ulong)value), 100, 0, EncounterActorControl.RequiredHuman)).ToArray();
        var decisions = actors.Select(actor => new EncounterDecision(actor.ActorId, Id(actor.ActorId.Low + 1000),
            EncounterDecisionKind.Action, ActionPin)).ToArray();
        var clock = new EncounterTimingSnapshot(Scope, ulong.MaxValue, ulong.MaxValue, actors, decisions);
        LocalExposureDuration duration = ExposurePacingV1.FromTacticalUnits(ulong.MaxValue);
        Assert.True(duration.TotalMilliseconds > (UInt128)long.MaxValue);
        var progress = ExposureProgress.Begin(Id(1), WorldPacingState.ProcessRate).Advance(duration);
        var origin = new WorldInstant(-123, 456);
        var pacing = WorldPacingState.Restore(Id(1), Id(2), origin, progress.Capture(), clock, LocalExposureDuration.Zero);
        UInt128 elapsed = (UInt128)ulong.MaxValue * 10;
        Assert.Equal((ulong)(elapsed / 1800), pacing.ProcessIntervals);
        Assert.Equal((ulong)(elapsed % 1800), pacing.Progress.Remainder);
        Assert.Equal((Int128)elapsed, origin.MillisecondsUntil(pacing.Calendar));
        Assert.Equal((Int128)(elapsed / 1800 * 1800), origin.MillisecondsUntil(pacing.ProcessBoundary));
        byte[] bytes = WorldPacingCodecV1.Encode(pacing);
        Assert.True(bytes.Length < WorldPacingCodecV1.MaximumEncodedByteLength);
        Assert.Equal(bytes, WorldPacingCodecV1.Encode(Reopen(pacing)));
    }

    [Fact]
    public void Counter_and_calendar_overflow_leave_prior_states_unchanged()
    {
        UInt128 total = (UInt128)ulong.MaxValue * 1800;
        var duration = new LocalExposureDuration((ulong)(total / LocalExposureDuration.MillisecondsPerDay),
            (uint)(total % LocalExposureDuration.MillisecondsPerDay));
        var progress = ExposureProgress.Begin(Id(1), WorldPacingState.ProcessRate).Advance(duration);
        var full = WorldPacingState.Restore(Id(1), Id(2), default, progress.Capture());
        byte[] counterBefore = WorldPacingCodecV1.Encode(full);
        Assert.Throws<OverflowException>(() => full.AdvanceExploration());
        Assert.Equal(counterBefore, WorldPacingCodecV1.Encode(full));

        var end = Begin(new WorldInstant(long.MaxValue, WorldInstant.MillisecondsPerDay - 25)).StartEncounter(Clock());
        byte[] calendarBefore = WorldPacingCodecV1.Encode(end);
        Assert.Throws<OverflowException>(() => end.ObserveEncounter(end.Encounter!.TryAdvance(0).State));
        Assert.Equal(calendarBefore, WorldPacingCodecV1.Encode(end));
        Assert.Throws<OverflowException>(() => WorldPacingState.Restore(Id(1), Id(2),
            new WorldInstant(long.MaxValue, 0), progress.Capture()));
    }

    [Theory]
    [InlineData("subject")]
    [InlineData("rate")]
    [InlineData("carry")]
    [InlineData("completed")]
    [InlineData("origin-ms")]
    [InlineData("encounter-origin")]
    [InlineData("unpaired-timing")]
    [InlineData("unpaired-exposure")]
    [InlineData("foreign-timing")]
    [InlineData("partial-exposure")]
    public void Canonical_archives_reject_forged_or_unpaired_fields(string corruption)
    {
        var pacing = Begin().AdvanceExploration().StartEncounter(Clock()).ObserveEncounter(Clock(995, 1, 5));
        var fields = Fields(WorldPacingCodecV1.Encode(pacing));
        var progress = Fields(fields[4]);
        switch (corruption)
        {
            case "subject": fields[1] = Id(9).ToByteArray(); break;
            case "rate": BinaryPrimitives.WriteUInt64BigEndian(progress[5], 1801); break;
            case "carry": BinaryPrimitives.WriteUInt64BigEndian(progress[9], 51); break;
            case "completed": BinaryPrimitives.WriteUInt64BigEndian(progress[8], 2); break;
            case "origin-ms": BinaryPrimitives.WriteInt32BigEndian(fields[3].AsSpan(8), WorldInstant.MillisecondsPerDay); break;
            case "encounter-origin": BinaryPrimitives.WriteUInt64BigEndian(fields[6].AsSpan(8), 1801); break;
            case "unpaired-timing": fields[5] = []; break;
            case "unpaired-exposure": fields[6] = []; break;
            case "foreign-timing":
                var timing = Fields(fields[5]);
                timing[3] = Id(9).ToByteArray();
                fields[5] = Frame(timing);
                break;
            case "partial-exposure": fields[6] = fields[6][..^1]; break;
            default: throw new InvalidOperationException();
        }
        fields[4] = Frame(progress);
        byte[] malformed = Frame(fields);
        Assert.Throws<FormatException>(() => WorldPacingCodecV1.Decode(malformed, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Archive_truncation_trailing_bytes_bounds_expected_scope_and_cancellation_fail_closed()
    {
        var pacing = Begin().StartEncounter(Clock());
        byte[] bytes = WorldPacingCodecV1.Encode(pacing);
        for (int length = 0; length < bytes.Length; length++)
        {
            byte[] truncated = bytes[..length];
            Assert.Throws<FormatException>(() => WorldPacingCodecV1.Decode(truncated, TestContext.Current.CancellationToken));
        }
        Assert.Throws<FormatException>(() => WorldPacingCodecV1.Decode([.. bytes, 0], TestContext.Current.CancellationToken));
        Assert.Throws<FormatException>(() => WorldPacingCodecV1.Decode(new byte[WorldPacingCodecV1.MaximumEncodedByteLength + 1],
            TestContext.Current.CancellationToken));
        Assert.Throws<FormatException>(() => WorldPacingCodecV1.Decode(bytes, Id(9), Id(2), TestContext.Current.CancellationToken));
        Assert.Throws<FormatException>(() => WorldPacingCodecV1.Decode(bytes, Id(1), Id(9), TestContext.Current.CancellationToken));
        byte[] changedVersion = (byte[])bytes.Clone();
        changedVersion[7] = 2;
        Assert.Throws<FormatException>(() => WorldPacingCodecV1.Decode(changedVersion, TestContext.Current.CancellationToken));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => WorldPacingCodecV1.Decode(bytes, cancellation.Token));
    }

    private static List<byte[]> Fields(byte[] bytes)
    {
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8)));
        List<byte[]> fields = [];
        int offset = 12;
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
            offset += 4;
            fields.Add(bytes.AsSpan(offset, length).ToArray());
            offset += length;
        }
        return fields;
    }

    private static byte[] Frame(IReadOnlyList<byte[]> fields)
    {
        using var stream = new MemoryStream();
        stream.Write(Encoding.ASCII.GetBytes("OFC\0"));
        Write32(stream, 1);
        Write32(stream, checked((uint)fields.Count));
        foreach (byte[] field in fields)
        {
            Write32(stream, checked((uint)field.Length));
            stream.Write(field);
        }
        return stream.ToArray();
    }

    private static void Write32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        stream.Write(bytes);
    }
}
