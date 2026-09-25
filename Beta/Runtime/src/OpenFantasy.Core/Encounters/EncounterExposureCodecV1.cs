using OpenFantasy.Core.Content;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Encounters;

/// <summary>Bounded exact exposure bytes; authenticated timing history and effect receipts are separate.</summary>
public static class EncounterExposureCodecV1
{
    public const int MaximumEncodedByteLength = 1024;

    public static byte[] Encode(EncounterExposureBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return CanonicalEncodingV1.Frame("encounter.exposure.v1", CanonicalEncodingV1.Unsigned64(1),
            binding.Scope.WorldId.ToByteArray(), binding.Scope.BranchId.ToByteArray(), binding.Scope.EncounterId.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(binding.BoundAtTacticalUnits),
            CanonicalEncodingV1.Unsigned64(binding.ExposureAtBinding.Days),
            CanonicalEncodingV1.Unsigned64(binding.ExposureAtBinding.MillisecondsWithinDay),
            CanonicalEncodingV1.Unsigned64(binding.ObservedRevision),
            CanonicalEncodingV1.Unsigned64(binding.ObservedTacticalUnits), binding.ObservedTimingHash.ToByteArray(),
            ExposureEncodingV1.Encode(binding.Progress.Capture()));
    }

    public static EncounterExposureBinding Decode(ReadOnlySpan<byte> bytes, EncounterTimingSnapshot expectedClock,
        EntityId expectedSubjectId, ExposureRate expectedRate)
    {
        ArgumentNullException.ThrowIfNull(expectedClock);
        ArgumentNullException.ThrowIfNull(expectedRate);
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 12);
            reader.Tag("encounter.exposure.v1"u8);
            reader.Version();
            var scope = new EncounterTimingScope(reader.Identity(), reader.Identity(), reader.Identity());
            ulong boundAt = reader.Number();
            var initial = new LocalExposureDuration(reader.Number(), checked((uint)reader.Number()));
            ulong revision = reader.Number();
            ulong units = reader.Number();
            var hash = new ContentHash(Convert.ToHexString(reader.Exact(32)).ToLowerInvariant());
            ExposureProgressState progress = DecodeProgress(reader.Field(MaximumEncodedByteLength));
            reader.Complete();
            var state = new EncounterExposureState(scope, boundAt, initial, revision, units, hash, progress);
            EncounterExposureBinding binding = EncounterExposureBinding.Restore(state, expectedClock, expectedSubjectId, expectedRate);
            if (!Encode(binding).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The encounter exposure archive is not canonical.");
            }
            return binding;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            throw new FormatException("Invalid retained encounter exposure.", error);
        }
    }

    private static ExposureProgressState DecodeProgress(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 10);
        reader.Tag("exposure.progress.v1"u8);
        EntityId subject = reader.Identity();
        EntityId output = reader.Identity();
        var rules = new ContentHash(Convert.ToHexString(reader.Exact(32)).ToLowerInvariant());
        var rate = new ExposureRate(output, rules, reader.Number(), reader.Number());
        var elapsed = new LocalExposureDuration(reader.Number(), checked((uint)reader.Number()));
        var progress = new ExposureProgressState(subject, rate, elapsed, reader.Number(), reader.Number());
        reader.Complete();
        return progress;
    }
}
