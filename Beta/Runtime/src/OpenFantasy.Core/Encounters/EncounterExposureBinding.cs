using OpenFantasy.Core.Content;
using OpenFantasy.Core.Exposure;

namespace OpenFantasy.Core.Encounters;

/// <summary>Captured arithmetic binding, not evidence that its timing history was admitted by a world.</summary>
public sealed record EncounterExposureState(EncounterTimingScope Scope, ulong BoundAtTacticalUnits,
    LocalExposureDuration ExposureAtBinding, ulong ObservedRevision, ulong ObservedTacticalUnits,
    ContentHash ObservedTimingHash, ExposureProgressState Progress);

/// <summary>
/// Carries one subject's existing rate progress across accepted tactical intervals. The caller owns
/// authenticating clock ancestry, the original binding, subject membership and atomic effect publication.
/// A plausible snapshot or canonical archive is not an authorization to grant work or recovery.
/// </summary>
public sealed class EncounterExposureBinding
{
    private EncounterExposureBinding(EncounterExposureState state, ExposureProgress progress)
    {
        Scope = state.Scope;
        BoundAtTacticalUnits = state.BoundAtTacticalUnits;
        ExposureAtBinding = state.ExposureAtBinding;
        ObservedRevision = state.ObservedRevision;
        ObservedTacticalUnits = state.ObservedTacticalUnits;
        ObservedTimingHash = state.ObservedTimingHash;
        Progress = progress;
    }

    public EncounterTimingScope Scope { get; }
    public ulong BoundAtTacticalUnits { get; }
    public LocalExposureDuration ExposureAtBinding { get; }
    public ulong ObservedRevision { get; }
    public ulong ObservedTacticalUnits { get; }
    public ContentHash ObservedTimingHash { get; }
    public ExposureProgress Progress { get; }

    /// <summary>Starts at this clock boundary without retroactively granting earlier encounter exposure.</summary>
    public static EncounterExposureBinding Begin(EncounterTimingSnapshot clock, ExposureProgress progress)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(progress);
        return new EncounterExposureBinding(new EncounterExposureState(clock.Scope, clock.ElapsedTacticalUnits,
            progress.Elapsed, clock.Revision, clock.ElapsedTacticalUnits, clock.ContentHash, progress.Capture()), progress);
    }

    public EncounterExposureState Capture() => new(Scope, BoundAtTacticalUnits, ExposureAtBinding,
        ObservedRevision, ObservedTacticalUnits, ObservedTimingHash, Progress.Capture());

    public static EncounterExposureBinding Restore(EncounterExposureState state, EncounterTimingSnapshot expectedClock,
        EntityId expectedSubjectId, ExposureRate expectedRate)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(expectedClock);
        ArgumentNullException.ThrowIfNull(state.Progress);
        if (state.Scope != expectedClock.Scope || state.ObservedRevision != expectedClock.Revision ||
            state.ObservedTacticalUnits != expectedClock.ElapsedTacticalUnits ||
            state.ObservedTimingHash != expectedClock.ContentHash || state.Progress.SubjectId != expectedSubjectId)
        {
            throw new ArgumentException("Exposure restoration requires the exact expected subject and timing snapshot.", nameof(state));
        }
        if (state.BoundAtTacticalUnits > state.ObservedTacticalUnits ||
            state.BoundAtTacticalUnits % EncounterTimingSnapshot.UnitsPerTick != 0)
        {
            throw new ArgumentException("The exposure binding must begin at an earlier whole tactical tick.", nameof(state));
        }
        LocalExposureDuration experienced = ExposurePacingV1.FromTacticalUnits(
            state.ObservedTacticalUnits - state.BoundAtTacticalUnits);
        if (state.Progress.Elapsed != state.ExposureAtBinding.Add(experienced))
        {
            throw new ArgumentException("Exposure does not match the retained interval and initial progress.", nameof(state));
        }
        ExposureProgress progress = ExposureProgress.Restore(state.Progress, expectedRate);
        return new EncounterExposureBinding(state, progress);
    }

    /// <summary>
    /// Projects cumulative time from a clock whose intervening history the caller has already admitted.
    /// Monotonicity and hash checks reject stale/duplicate observations but do not authenticate ancestry.
    /// </summary>
    public EncounterExposureBinding ObserveAcceptedTiming(EncounterTimingSnapshot clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (clock.Scope != Scope || clock.Revision < ObservedRevision ||
            clock.ElapsedTacticalUnits < ObservedTacticalUnits)
        {
            throw new ArgumentException("Exposure cannot cross scopes or move its observed clock backwards.", nameof(clock));
        }
        ContentHash nextHash = clock.ContentHash;
        if (clock.Revision == ObservedRevision)
        {
            if (nextHash != ObservedTimingHash)
            {
                throw new ArgumentException("The same timing revision cannot name different state.", nameof(clock));
            }
            return this;
        }
        ulong units = clock.ElapsedTacticalUnits - ObservedTacticalUnits;
        if ((UInt128)units > (UInt128)(clock.Revision - ObservedRevision) * EncounterTimingSnapshot.UnitsPerTick)
        {
            throw new ArgumentException("Observed tactical time exceeds the possible accepted tick count.", nameof(clock));
        }
        ExposureProgress progress = Progress.Advance(ExposurePacingV1.FromTacticalUnits(units));
        return new EncounterExposureBinding(new EncounterExposureState(Scope, BoundAtTacticalUnits,
            ExposureAtBinding, clock.Revision, clock.ElapsedTacticalUnits, nextHash, progress.Capture()), progress);
    }
}
