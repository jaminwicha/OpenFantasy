namespace OpenFantasy.Core.Production;

/// <summary>Nonnegative material mass; arithmetic rejects overflow and has no implicit unit conversions.</summary>
public readonly record struct MassMilligrams(ulong Value)
{
    public MassMilligrams Add(MassMilligrams other) => new(checked(Value + other.Value));
    public MassMilligrams Multiply(ulong count) => new(checked(Value * count));
}

/// <summary>Nonnegative occupied volume. A material transform need not preserve occupied volume.</summary>
public readonly record struct VolumeMicrolitres(ulong Value)
{
    public VolumeMicrolitres Add(VolumeMicrolitres other) => new(checked(Value + other.Value));
    public VolumeMicrolitres Multiply(ulong count) => new(checked(Value * count));
}

/// <summary>An ordinal material-resistance rating in the pinned rule definition, not a physical Mohs value.</summary>
public readonly record struct HardnessRating(uint Value);

/// <summary>Deterministic abstract work units, never wall milliseconds or player-input counts.</summary>
public readonly record struct WorkUnits(ulong Value);
