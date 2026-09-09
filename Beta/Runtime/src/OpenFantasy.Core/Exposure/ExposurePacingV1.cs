namespace OpenFantasy.Core.Exposure;

/// <summary>
/// Converts accepted progress under the initial pacing profile. Callers supply only advancing
/// steps/units; held, offline or unaccepted work contributes zero and is never queued here.
/// </summary>
public static class ExposurePacingV1
{
    public const uint ExplorationMillisecondsPerStep = 1_800;
    public const uint TacticalMillisecondsPerUnit = 10;

    public static LocalExposureDuration FromExplorationSteps(ulong advancingSteps) =>
        LocalExposureDuration.FromTotalMilliseconds((UInt128)advancingSteps * ExplorationMillisecondsPerStep);

    public static LocalExposureDuration FromTacticalUnits(ulong advancingUnits) =>
        LocalExposureDuration.FromTotalMilliseconds((UInt128)advancingUnits * TacticalMillisecondsPerUnit);
}
