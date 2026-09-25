using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Host;

/// <summary>Read-only description of admitted clock state, without selecting or resolving an interval.</summary>
internal sealed class WorldPacingDiagnosticReport
{
    public WorldPacingDiagnosticReport(WorldPacingState state)
    {
        Hash = state.ContentHash.Value;
        Calendar = state.Calendar;
        ProcessBoundary = state.ProcessBoundary;
        ProcessIntervals = state.ProcessIntervals;
        RemainderMilliseconds = state.Progress.Remainder;
        if (state.Encounter is { } timing)
        {
            Encounter = new
            {
                Id = timing.EncounterId.ToString(), timing.Revision, timing.ElapsedTacticalUnits,
                Hash = timing.ContentHash.Value,
                RequiredDecisions = timing.RequiredDecisions.Select(actor => actor.ActorId.ToString()).ToArray(),
                DueActors = timing.DueActors.Select(actor => actor.ActorId.ToString()).ToArray(),
                Actors = timing.Actors.Select(actor => new
                {
                    Id = actor.ActorId.ToString(), actor.NormalizedSpeed, actor.RemainingDelay, Control = actor.Control.ToString()
                }).ToArray(),
                Decisions = timing.Decisions.Select(decision => new
                {
                    ActorId = decision.ActorId.ToString(), CommandId = decision.DecisionId.ToString(),
                    Kind = decision.Kind.ToString(), CommandHash = decision.ActionPin.Value
                }).ToArray()
            };
        }
    }

    public string Hash { get; }
    public OpenFantasy.Core.WorldInstant Calendar { get; }
    public OpenFantasy.Core.WorldInstant ProcessBoundary { get; }
    public ulong ProcessIntervals { get; }
    public ulong RemainderMilliseconds { get; }
    public object? Encounter { get; }
}
