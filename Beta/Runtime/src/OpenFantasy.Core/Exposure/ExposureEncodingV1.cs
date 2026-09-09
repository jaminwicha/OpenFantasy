namespace OpenFantasy.Core.Exposure;

/// <summary>Canonical rate-bound progress hashing; storage decoding/migration is a separate contract.</summary>
public static class ExposureEncodingV1
{
    public static byte[] Encode(ExposureProgressState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        // Shape validation is mandatory even when callers directly construct interchange fields.
        ExposureProgress.Restore(state, state.Rate);
        return CanonicalEncodingV1.Frame("exposure.progress.v1", state.SubjectId.ToByteArray(),
            state.Rate.OutputUnitId.ToByteArray(), state.Rate.RulesVersion.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(state.Rate.Numerator),
            CanonicalEncodingV1.Unsigned64(state.Rate.Denominator),
            CanonicalEncodingV1.Unsigned64(state.Elapsed.Days),
            CanonicalEncodingV1.Unsigned64(state.Elapsed.MillisecondsWithinDay),
            CanonicalEncodingV1.Unsigned64(state.CompletedUnits), CanonicalEncodingV1.Unsigned64(state.Remainder));
    }
}
