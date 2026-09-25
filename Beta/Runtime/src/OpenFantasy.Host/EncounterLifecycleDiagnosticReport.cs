using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Host;

/// <summary>Privileged lifecycle evidence, independent of player-facing information policy.</summary>
internal sealed class EncounterLifecycleDiagnosticReport
{
    internal EncounterLifecycleDiagnosticReport(WorldSnapshot world)
    {
        var lifecycle = world.EncounterLifecycle ?? throw new ArgumentException("Lifecycle ownership is required.");
        Hash = lifecycle.ContentHash.Value;
        Phase = lifecycle.Phase.ToString();
        Revision = lifecycle.Revision;
        ExplorationSteps = lifecycle.ExplorationSteps;
        TacticalTicks = lifecycle.TacticalTicks;
        EntryTacticalTicks = lifecycle.EntryTacticalTicks;
        EncounterCount = lifecycle.EncounterCount;
        ScenarioId = lifecycle.ScenarioId?.ToString();
        EncounterId = lifecycle.Actions?.Scope.EncounterId.ToString();
        RetainedActionHash = lifecycle.Actions?.ContentHash.Value;
        Debts = lifecycle.Debts.Select(debt => new { ActorId = debt.ActorId.ToString(), debt.RemainingDelay }).ToArray();
        Scenarios = lifecycle.Scenarios.Select(scenario => new
        {
            Id = scenario.DefinitionId.ToString(), scenario.Key, scenario.DisplayName,
            Initiator = scenario.InitiatingActorId.ToString(), Target = scenario.TargetActorId.ToString(),
            scenario.EntryRangeSubunits, Hash = scenario.Version.Value
        }).ToArray();
    }

    public string Hash { get; }
    public string Phase { get; }
    public ulong Revision { get; }
    public ulong ExplorationSteps { get; }
    public ulong TacticalTicks { get; }
    public ulong EntryTacticalTicks { get; }
    public ulong EncounterCount { get; }
    public string? ScenarioId { get; }
    public string? EncounterId { get; }
    public string? RetainedActionHash { get; }
    public object Debts { get; }
    public object Scenarios { get; }
}
