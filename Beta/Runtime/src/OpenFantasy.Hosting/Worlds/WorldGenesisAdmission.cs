using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Fresh database admission is stricter than restoration of an otherwise valid complete root.</summary>
internal static class WorldGenesisAdmission
{
    internal static void Validate(WorldSnapshot genesis)
    {
        if (genesis.Revision != 0 || genesis.AdvancingSteps != 0 || genesis.HasReadingHold || genesis.PendingExcavation is not null)
        {
            throw new ArgumentException("A new local world requires unheld commit zero and step zero.", nameof(genesis));
        }
        if (genesis.EncounterActions is { } actions && (actions.Revision != 0 || actions.Pending.Count != 0
            || actions.LastResolution is not null || actions.Random.NextCounter != 0 || actions.Random.Exhausted
            || WorldEncounterActions.IsOutcome(genesis)))
        {
            throw new ArgumentException("A fresh encounter cannot import decisions, damage outcomes or consumed rolls.", nameof(genesis));
        }
        if (genesis.Pacing is not { } pacing)
        {
            return;
        }
        if (pacing.OriginCalendar != genesis.Calendar || pacing.Progress.Elapsed != LocalExposureDuration.Zero
            || pacing.Progress.CompletedUnits != 0 || pacing.Progress.Remainder != 0
            || pacing.Encounter is { } timing && (timing.Revision != 0 || timing.ElapsedTacticalUnits != 0 || timing.Decisions.Count != 0))
        {
            throw new ArgumentException("A fresh pacing world cannot import elapsed exposure or accepted encounter choices.", nameof(genesis));
        }
    }
}
