using System.Globalization;
using System.Text.Json;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Exposure;

namespace OpenFantasy.Core.Tests.Encounters;

public sealed class EncounterExposureTests
{
    private static EntityId Id(ulong number) => new(0, number);
    private static readonly EncounterTimingScope Scope = new(Id(1), Id(2), Id(3));
    private static readonly ContentHash Rules = ContentHash.Compute("exposure-encounter-test-v1"u8);
    private static ExposureRate Rate(ulong numerator = 1, ulong denominator = 1800) => new(Id(20), Rules, numerator, denominator);
    private static EncounterTimingSnapshot Clock(ulong delay = 1000, ulong revision = 0, ulong units = 0) =>
        new(Scope, revision, units, [new EncounterActorTiming(Id(10), 100, delay, EncounterActorControl.RequiredHuman)], []);

    [Fact]
    public void IndependentIntegerAndByteVectorsMatchIncludingWideTacticalDuration()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "encounter-exposure-v1.json")));
        foreach (JsonElement sample in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            ulong Read(string name) => ulong.Parse(sample.GetProperty(name).GetString()!, CultureInfo.InvariantCulture);
            EncounterTimingSnapshot expectedClock = EncounterTimingCodecV1.Decode(
                Convert.FromHexString(sample.GetProperty("clockHex").GetString()!), Scope, TestContext.Current.CancellationToken);
            ExposureRate rate = Rate(Read("numerator"), Read("denominator"));
            ExposureProgress prior = ExposureProgress.Begin(Id(10), rate)
                .Advance(LocalExposureDuration.FromMilliseconds(Read("initialMs")));
            var initial = new EncounterTimingSnapshot(Scope, Read("boundAt") / 5, Read("boundAt"), expectedClock.Actors, []);
            EncounterExposureBinding binding = EncounterExposureBinding.Begin(initial, prior).ObserveAcceptedTiming(expectedClock);
            byte[] encoded = EncounterExposureCodecV1.Encode(binding);
            Assert.Equal(sample.GetProperty("hex").GetString(), Convert.ToHexString(encoded).ToLowerInvariant());
            Assert.Equal(sample.GetProperty("hash").GetString(), ContentHash.Compute(encoded).Value);
            Assert.Equal(Read("completed"), binding.Progress.CompletedUnits);
            Assert.Equal(Read("remainder"), binding.Progress.Remainder);
            Assert.Equal(sample.GetProperty("elapsedMs").GetString(), binding.Progress.Elapsed.TotalMilliseconds.ToString(CultureInfo.InvariantCulture));
            Assert.Equal(binding.Capture(), EncounterExposureCodecV1.Decode(encoded, expectedClock, Id(10), rate).Capture());
        }
    }

    [Fact]
    public void ThirtySixActualTicksEarnOneExplorationQuantumAndHeldTimeEarnsNothing()
    {
        EncounterTimingSnapshot clock = Clock(0);
        EncounterExposureBinding binding = EncounterExposureBinding.Begin(clock, ExposureProgress.Begin(Id(10), Rate()));
        for (int held = 0; held < 100; held++)
        {
            EncounterTimingTransition refused = clock.TryAdvance(clock.Revision);
            Assert.Equal(EncounterTimingStatus.RequiredDecisionMissing, refused.Status);
            Assert.Same(binding, binding.ObserveAcceptedTiming(refused.State));
        }
        for (ulong step = 0; step < 36; step++)
        {
            if (clock.DueActors.Count != 0)
            {
                clock = clock.QueueDecision(clock.Revision, new EncounterDecision(Id(10), Id(100 + step), EncounterDecisionKind.Wait, Rules));
                binding = binding.ObserveAcceptedTiming(clock);
                ExposureProgress before = binding.Progress;
                clock = clock.TryConfirmNext(clock.Revision).State;
                binding = binding.ObserveAcceptedTiming(clock);
                Assert.Same(before, binding.Progress);
            }
            EncounterTimingTransition accepted = clock.TryAdvance(clock.Revision);
            Assert.Equal(EncounterTimingStatus.Advanced, accepted.Status);
            clock = accepted.State;
            binding = binding.ObserveAcceptedTiming(clock);
        }
        Assert.Equal(180UL, clock.ElapsedTacticalUnits);
        Assert.Equal(LocalExposureDuration.FromMilliseconds(1800), binding.Progress.Elapsed);
        Assert.Equal(1UL, binding.Progress.CompletedUnits);
        Assert.Equal(0UL, binding.Progress.Remainder);
    }

    [Fact]
    public void ExistingCarrySurvivesDecisionHoldArchiveAndLaterTick()
    {
        EncounterTimingSnapshot clock = Clock(0);
        ExposureProgress prior = ExposureProgress.Begin(Id(10), Rate()).Advance(LocalExposureDuration.FromMilliseconds(1799));
        EncounterExposureBinding binding = EncounterExposureBinding.Begin(clock, prior);
        clock = clock.QueueDecision(0, new EncounterDecision(Id(10), Id(30), EncounterDecisionKind.Action, Rules));
        binding = binding.ObserveAcceptedTiming(clock);
        byte[] archive = EncounterExposureCodecV1.Encode(binding);
        EncounterTimingSnapshot reopenedClock = EncounterTimingCodecV1.Decode(EncounterTimingCodecV1.Encode(clock), Scope,
            TestContext.Current.CancellationToken);
        binding = EncounterExposureCodecV1.Decode(archive, reopenedClock, Id(10), Rate());
        Assert.Equal(archive, EncounterExposureCodecV1.Encode(binding));
        Assert.Equal(1799UL, binding.Progress.Remainder);
        clock = reopenedClock.TryConfirmNext(reopenedClock.Revision).State;
        binding = binding.ObserveAcceptedTiming(clock);
        Assert.Equal(prior.Capture(), binding.Progress.Capture());
        clock = clock.TryAdvance(clock.Revision).State;
        binding = binding.ObserveAcceptedTiming(clock);
        Assert.Equal(1UL, binding.Progress.CompletedUnits);
        Assert.Equal(49UL, binding.Progress.Remainder);
        Assert.Same(binding, binding.ObserveAcceptedTiming(clock));
        Assert.Equal(prior.ComputeContentHash(), ExposureProgress.Restore(prior.Capture(), Rate()).ComputeContentHash());
    }

    [Fact]
    public void BindingAtLaterBoundaryDoesNotGrantEarlierExposureAndBatchedObservationMatchesEachTick()
    {
        EncounterTimingSnapshot clock = Clock(1000, 20, 100);
        ExposureProgress prior = ExposureProgress.Begin(Id(99), Rate(7, 13)).Advance(LocalExposureDuration.FromMilliseconds(19));
        EncounterExposureBinding start = EncounterExposureBinding.Begin(clock, prior);
        EncounterExposureBinding each = start;
        for (int tick = 0; tick < 17; tick++)
        {
            clock = clock.TryAdvance(clock.Revision).State;
            each = each.ObserveAcceptedTiming(clock);
        }
        EncounterExposureBinding aggregate = start.ObserveAcceptedTiming(clock);
        Assert.Equal(each.Capture(), aggregate.Capture());
        Assert.Equal(869U, aggregate.Progress.Elapsed.MillisecondsWithinDay);
        Assert.Equal(467UL, aggregate.Progress.CompletedUnits);
        Assert.Equal(12UL, aggregate.Progress.Remainder);
        Assert.Equal(100UL, aggregate.BoundAtTacticalUnits);
        Assert.Equal(Id(99), aggregate.Progress.SubjectId);
    }

    [Fact]
    public void SameRevisionSubstitutionRollbackForeignScopeAndExcessiveTimeRejectAtomically()
    {
        EncounterTimingSnapshot clock = Clock(100, 20, 50);
        EncounterExposureBinding binding = EncounterExposureBinding.Begin(clock, ExposureProgress.Begin(Id(10), Rate()));
        byte[] original = EncounterExposureCodecV1.Encode(binding);
        Assert.Throws<ArgumentException>(() => binding.ObserveAcceptedTiming(Clock(99, 20, 50)));
        Assert.Throws<ArgumentException>(() => binding.ObserveAcceptedTiming(Clock(100, 19, 50)));
        Assert.Throws<ArgumentException>(() => binding.ObserveAcceptedTiming(Clock(100, 21, 45)));
        Assert.Throws<ArgumentException>(() => binding.ObserveAcceptedTiming(Clock(100, 21, 60)));
        foreach (EncounterTimingScope other in new[] { new EncounterTimingScope(Id(4), Id(2), Id(3)),
            new EncounterTimingScope(Id(1), Id(4), Id(3)), new EncounterTimingScope(Id(1), Id(2), Id(4)) })
        {
            var foreign = new EncounterTimingSnapshot(other, 21, 55, clock.Actors, []);
            Assert.Throws<ArgumentException>(() => binding.ObserveAcceptedTiming(foreign));
        }
        Assert.Equal(original, EncounterExposureCodecV1.Encode(binding));
    }

    [Fact]
    public void RestoreRequiresExpectedSubjectRateExactClockAndConsistentOriginAndCarry()
    {
        EncounterTimingSnapshot clock = Clock(1000, 20, 100);
        EncounterExposureBinding start = EncounterExposureBinding.Begin(clock,
            ExposureProgress.Begin(Id(10), Rate()).Advance(LocalExposureDuration.FromMilliseconds(17)));
        clock = clock.TryAdvance(clock.Revision).State;
        EncounterExposureState state = start.ObserveAcceptedTiming(clock).Capture();
        EncounterTimingSnapshot expected = clock;
        void Reject(EncounterExposureState invalid) => Assert.Throws<ArgumentException>(() =>
            EncounterExposureBinding.Restore(invalid, expected, Id(10), Rate()));
        Reject(state with { BoundAtTacticalUnits = 110 });
        Reject(state with { BoundAtTacticalUnits = 101 });
        Reject(state with { ExposureAtBinding = LocalExposureDuration.Zero });
        Reject(state with { ObservedRevision = 20 });
        Reject(state with { ObservedTacticalUnits = 100 });
        Reject(state with { ObservedTimingHash = Rules });
        Reject(state with { Progress = state.Progress with { Remainder = 68 } });
        Reject(state with { Progress = state.Progress with { CompletedUnits = 1 } });
        Assert.Throws<ArgumentException>(() => EncounterExposureBinding.Restore(state, expected, Id(11), Rate()));
        Assert.Throws<ArgumentException>(() => EncounterExposureBinding.Restore(state, expected, Id(10), Rate(1, 1700)));
        Assert.Equal(state, EncounterExposureBinding.Restore(state, expected, Id(10), Rate()).Capture());
    }

    [Fact]
    public void CanonicalArchiveRejectsTruncationTrailingUnknownVersionAndCorruption()
    {
        EncounterTimingSnapshot clock = Clock();
        EncounterExposureBinding binding = EncounterExposureBinding.Begin(clock, ExposureProgress.Begin(Id(10), Rate()));
        byte[] archive = EncounterExposureCodecV1.Encode(binding);
        void Reject(byte[] bytes) => Assert.Throws<FormatException>(() => EncounterExposureCodecV1.Decode(bytes, clock, Id(10), Rate()));
        for (int length = 0; length < archive.Length; length++)
        {
            Reject(archive[..length]);
        }
        Reject([.. archive, 0]);
        Reject(new byte[EncounterExposureCodecV1.MaximumEncodedByteLength + 1]);
        byte[] changed = (byte[])archive.Clone();
        changed[7] = 2;
        Reject(changed);
        changed = (byte[])archive.Clone();
        changed[^1] = 1;
        Reject(changed);
        Assert.Throws<FormatException>(() => EncounterExposureCodecV1.Decode(archive, clock, Id(11), Rate()));
        Assert.Throws<FormatException>(() => EncounterExposureCodecV1.Decode(archive, clock, Id(10), Rate(1, 1700)));
    }

    [Fact]
    public void OverflowLeavesPriorBindingAndClockUnchanged()
    {
        EncounterTimingSnapshot clock = Clock();
        ExposureProgress full = ExposureProgress.Begin(Id(10), Rate(ulong.MaxValue, 1))
            .Advance(LocalExposureDuration.FromMilliseconds(1));
        EncounterExposureBinding binding = EncounterExposureBinding.Begin(clock, full);
        byte[] prior = EncounterExposureCodecV1.Encode(binding);
        Assert.Throws<OverflowException>(() => binding.ObserveAcceptedTiming(clock.TryAdvance(0).State));
        Assert.Equal(prior, EncounterExposureCodecV1.Encode(binding));
        Assert.Equal(0UL, clock.Revision);
    }

    [Fact]
    public void ArithmeticProjectionCannotAuthenticatePlausibleCounterfeitHistory()
    {
        EncounterTimingSnapshot held = Clock(0);
        EncounterExposureBinding binding = EncounterExposureBinding.Begin(held, ExposureProgress.Begin(Id(10), Rate()));
        Assert.Equal(EncounterTimingStatus.RequiredDecisionMissing, held.TryAdvance(0).Status);
        // Shape-valid later state is deliberately constructible for archives and diagnostics. A live
        // authority must reject this invented ancestry before calling ObserveAcceptedTiming.
        EncounterTimingSnapshot counterfeit = Clock(100, 1, 5);
        EncounterExposureBinding arithmeticOnly = binding.ObserveAcceptedTiming(counterfeit);
        Assert.Equal(50U, arithmeticOnly.Progress.Elapsed.MillisecondsWithinDay);
        Assert.Equal(LocalExposureDuration.Zero, binding.Progress.Elapsed);
    }
}
