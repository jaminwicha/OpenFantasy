using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Exposure;

namespace OpenFantasy.Core.Worlds;

/// <summary>One already-selected exploration boundary; creation is not a clock-selection authority.</summary>
internal readonly struct WorldExplorationBoundary
{
    internal WorldExplorationBoundary(WorldInstant calendar, ulong advancingSteps, WorldPacingState? pacing,
        EncounterLifecycleSnapshot? lifecycle)
    {
        // Preserve the ordinary interpreter's operation and overflow order.
        Pacing = pacing?.AdvanceExploration();
        Lifecycle = lifecycle?.AdvanceExploration();
        Calendar = Pacing?.Calendar ?? calendar.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep);
        AdvancingSteps = checked(advancingSteps + 1);
    }

    internal WorldPacingState? Pacing { get; }
    internal EncounterLifecycleSnapshot? Lifecycle { get; }
    internal WorldInstant Calendar { get; }
    internal ulong AdvancingSteps { get; }
}
