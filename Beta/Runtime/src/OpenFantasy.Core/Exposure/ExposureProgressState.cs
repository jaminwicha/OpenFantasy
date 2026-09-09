namespace OpenFantasy.Core.Exposure;

/// <summary>Immutable interchange fields, validated by Restore before authoritative arithmetic.</summary>
public sealed record ExposureProgressState(EntityId SubjectId, ExposureRate Rate,
    LocalExposureDuration Elapsed, ulong CompletedUnits, ulong Remainder);
