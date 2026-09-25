using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Exposure;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Retains accepted world exposure and its complete exploration-sized process intervals.
/// The world authority authenticates timing ancestry and publishes process effects atomically;
/// restoring internally consistent fields does not establish that their time was earned.
/// </summary>
public sealed class WorldPacingState
{
    public static ExposureRate ProcessRate { get; } = new(
        new EntityId(0x776f726c642e7061, 0x63696e672e763100),
        ContentHash.Compute("world.pacing.v1:process-interval=1800ms;exploration=1800ms;tactical-unit=10ms;held=0ms"u8),
        1, ExposurePacingV1.ExplorationMillisecondsPerStep);

    private readonly EncounterExposureBinding? _encounterExposure;

    public WorldPacingState(EntityId worldId, EntityId branchId, WorldInstant originCalendar,
        ExposureProgressState progress, EncounterTimingSnapshot? encounter = null,
        LocalExposureDuration? encounterOriginExposure = null)
    {
        ArgumentNullException.ThrowIfNull(progress);
        if (worldId == branchId || progress.SubjectId != worldId
            || (encounter is null) != (encounterOriginExposure is null))
        {
            throw new ArgumentException("Pacing requires distinct scope, its world subject and paired encounter fields.");
        }
        Progress = ExposureProgress.Restore(progress, ProcessRate);
        if (encounter is not null)
        {
            if (encounter.WorldId != worldId || encounter.BranchId != branchId)
            {
                throw new ArgumentException("The encounter must belong to the pacing world's exact branch.", nameof(encounter));
            }
            _encounterExposure = EncounterExposureBinding.Restore(new EncounterExposureState(encounter.Scope,
                0, encounterOriginExposure!.Value, encounter.Revision, encounter.ElapsedTacticalUnits,
                encounter.ContentHash, progress), encounter, worldId, ProcessRate);
        }
        WorldId = worldId;
        BranchId = branchId;
        OriginCalendar = originCalendar;
        Encounter = encounter;
        EncounterOriginExposure = encounterOriginExposure;
        // Validate both derived instants at admission, before any caller can publish this state.
        Calendar = AddExposure(originCalendar, Progress.Elapsed);
        ProcessBoundary = AddExposure(originCalendar, LocalExposureDuration.FromTotalMilliseconds(
            (UInt128)ProcessIntervals * ExposurePacingV1.ExplorationMillisecondsPerStep));
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public WorldInstant OriginCalendar { get; }
    public ExposureProgress Progress { get; }
    public EncounterTimingSnapshot? Encounter { get; }
    public LocalExposureDuration? EncounterOriginExposure { get; }
    public WorldInstant Calendar { get; }
    public ulong ProcessIntervals => Progress.CompletedUnits;
    public WorldInstant ProcessBoundary { get; }
    public ContentHash ContentHash => ContentHash.Compute(WorldPacingCodecV1.Encode(this));

    public static WorldPacingState Begin(EntityId worldId, EntityId branchId, WorldInstant originCalendar) =>
        new(worldId, branchId, originCalendar, ExposureProgress.Begin(worldId, ProcessRate).Capture());

    public static WorldPacingState Restore(EntityId worldId, EntityId branchId, WorldInstant originCalendar,
        ExposureProgressState progress, EncounterTimingSnapshot? encounter = null,
        LocalExposureDuration? encounterOriginExposure = null) =>
        new(worldId, branchId, originCalendar, progress, encounter, encounterOriginExposure);

    public WorldPacingState AdvanceExploration()
    {
        if (Encounter is not null)
        {
            throw new InvalidOperationException("An active encounter cannot also earn exploration time.");
        }
        ExposureProgress next = Progress.Advance(ExposurePacingV1.FromExplorationSteps(1));
        return new(WorldId, BranchId, OriginCalendar, next.Capture());
    }

    public WorldPacingState StartEncounter(EncounterTimingSnapshot timing)
    {
        ArgumentNullException.ThrowIfNull(timing);
        if (Encounter is not null || timing.Revision != 0 || timing.ElapsedTacticalUnits != 0
            || timing.WorldId != WorldId || timing.BranchId != BranchId)
        {
            throw new ArgumentException("Encounter entry requires a fresh timing owner in this scope and no active encounter.", nameof(timing));
        }
        EncounterExposureBinding binding = EncounterExposureBinding.Begin(timing, Progress);
        return new(WorldId, BranchId, OriginCalendar, binding.Progress.Capture(), timing, binding.ExposureAtBinding);
    }

    /// <summary>
    /// Observes an authority-accepted timing descendant. Choices retain exposure and carry;
    /// only additional accepted tactical units earn time. The outer world validates ancestry.
    /// </summary>
    public WorldPacingState ObserveEncounter(EncounterTimingSnapshot timing)
    {
        ArgumentNullException.ThrowIfNull(timing);
        if (_encounterExposure is null)
        {
            throw new InvalidOperationException("No encounter is bound to this pacing owner.");
        }
        EncounterExposureBinding next = _encounterExposure.ObserveAcceptedTiming(timing);
        return ReferenceEquals(next, _encounterExposure) ? this
            : new(WorldId, BranchId, OriginCalendar, next.Progress.Capture(), timing, EncounterOriginExposure);
    }

    /// <summary>Retains all earned exposure and carry; complete lifecycle authority admits release.</summary>
    public WorldPacingState EndEncounter()
    {
        if (Encounter is null)
        {
            throw new InvalidOperationException("No active encounter can be released.");
        }
        return new(WorldId, BranchId, OriginCalendar, Progress.Capture());
    }

    private static WorldInstant AddExposure(WorldInstant origin, LocalExposureDuration elapsed)
    {
        // Add normalized days separately: valid exposure can exceed signed64 milliseconds.
        ulong milliseconds = (ulong)origin.MillisecondsWithinDay + elapsed.MillisecondsWithinDay;
        Int128 days = (Int128)origin.Days + elapsed.Days + milliseconds / WorldInstant.MillisecondsPerDay;
        return new WorldInstant(checked((long)days), (int)(milliseconds % WorldInstant.MillisecondsPerDay));
    }
}
