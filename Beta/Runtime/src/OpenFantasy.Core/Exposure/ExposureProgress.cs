using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Exposure;

/// <summary>
/// Immutable rate-bound progress and fractional carry. Interval ownership, deduplication,
/// interrupts and effects remain the authority's responsibility.
/// </summary>
public sealed class ExposureProgress
{
    private ExposureProgress(EntityId subjectId, ExposureRate rate, LocalExposureDuration elapsed,
        ulong completedUnits, ulong remainder)
    {
        ArgumentNullException.ThrowIfNull(rate);
        if (remainder >= rate.Denominator || rate.Numerator == 0 && remainder != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(remainder), "Fractional carry must be normalized for this rate.");
        }
        SubjectId = subjectId;
        Rate = rate;
        Elapsed = elapsed;
        CompletedUnits = completedUnits;
        Remainder = remainder;
    }

    public EntityId SubjectId { get; }
    public ExposureRate Rate { get; }
    public LocalExposureDuration Elapsed { get; }
    public ulong CompletedUnits { get; }
    public ulong Remainder { get; }

    public static ExposureProgress Begin(EntityId subjectId, ExposureRate rate) =>
        new(subjectId, rate, LocalExposureDuration.Zero, 0, 0);

    /// <summary>Restores exact captured fields against the explicitly expected immutable rate.</summary>
    public static ExposureProgress Restore(ExposureProgressState state, ExposureRate expectedRate)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(expectedRate);
        if (state.Rate != expectedRate)
        {
            throw new ArgumentException("Restoration cannot reinterpret fractional carry under another rate.", nameof(expectedRate));
        }
        (ulong units, ulong remainder) = Evaluate(state.Elapsed, expectedRate, 0);
        if (state.CompletedUnits != units || state.Remainder != remainder)
        {
            throw new ArgumentException("Captured progress is inconsistent with its experienced time and pinned rate.", nameof(state));
        }
        return new ExposureProgress(state.SubjectId, state.Rate, state.Elapsed, state.CompletedUnits, state.Remainder);
    }

    public ExposureProgressState Capture() => new(SubjectId, Rate, Elapsed, CompletedUnits, Remainder);

    public ExposureProgress Advance(LocalExposureDuration experienced)
    {
        if (experienced == LocalExposureDuration.Zero)
        {
            return this;
        }
        (ulong gained, ulong remainder) = Evaluate(experienced, Rate, Remainder);
        ulong total = checked(CompletedUnits + gained);
        return new ExposureProgress(SubjectId, Rate, Elapsed.Add(experienced), total, remainder);
    }

    public ContentHash ComputeContentHash() => ContentHash.Compute(ExposureEncodingV1.Encode(Capture()));

    private static (ulong Units, ulong Remainder) Evaluate(LocalExposureDuration duration, ExposureRate rate, ulong carry)
    {
        // Divide first so eon-scale durations do not require an unnecessarily wide product.
        UInt128 milliseconds = duration.TotalMilliseconds;
        UInt128 whole = checked((milliseconds / rate.Denominator) * rate.Numerator);
        UInt128 fractional = checked((milliseconds % rate.Denominator) * rate.Numerator + carry);
        UInt128 units = checked(whole + fractional / rate.Denominator);
        return (checked((ulong)units), (ulong)(fractional % rate.Denominator));
    }
}
