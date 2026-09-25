using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Encounters;

public enum EncounterLifecyclePhase { Exploration = 1, Confrontation = 2, Staging = 3, Active = 4, Outcome = 5 }

/// <summary>
/// Permanent encounter catalogs, phase, cumulative clocks and actor readiness debt. The world owns
/// entry legality, HP/outcome eligibility, timing ancestry and publication; this record grants no effects.
/// </summary>
public sealed class EncounterLifecycleSnapshot
{
    public const int MaximumAbilities = 32;
    public const int MaximumScenarios = 8;
    public const int MaximumActors = 64;
    public const ulong ExplorationRecoveryUnits = 180;
    private readonly Dictionary<EntityId, EncounterScenarioDefinition> _scenarios;
    private readonly Dictionary<EntityId, EncounterReadinessDebt> _debts;

    public EncounterLifecycleSnapshot(EntityId worldId, EntityId branchId, EntityId seed, ulong revision,
        ulong explorationSteps, ulong tacticalTicks, ulong entryTacticalTicks, ulong encounterCount,
        EncounterLifecyclePhase phase, IEnumerable<EncounterAbilityDefinition> abilities,
        IEnumerable<EncounterScenarioDefinition> scenarios, IEnumerable<EncounterReadinessDebt> debts,
        EntityId? scenarioId = null, EncounterActionSnapshot? actions = null)
    {
        if (worldId == branchId || !Enum.IsDefined(phase) || entryTacticalTicks > tacticalTicks
            || (UInt128)explorationSteps + tacticalTicks + encounterCount > revision
            || (UInt128)(tacticalTicks - entryTacticalTicks) * EncounterTimingSnapshot.UnitsPerTick > ulong.MaxValue)
        {
            throw new ArgumentException("Lifecycle scope, phase and cumulative clocks must be internally reachable.");
        }
        Abilities = Copy(abilities, ability => ability.DefinitionId, MaximumAbilities, nameof(abilities));
        Scenarios = Copy(scenarios, scenario => scenario.DefinitionId, MaximumScenarios, nameof(scenarios));
        Debts = Copy(debts, debt => debt.ActorId, MaximumActors, nameof(debts));
        if (Scenarios.Count == 0 || Debts.Count == 0
            || Abilities.Select(ability => ability.Key).Distinct(StringComparer.Ordinal).Count() != Abilities.Count
            || Scenarios.Select(scenario => scenario.Key).Distinct(StringComparer.Ordinal).Count() != Scenarios.Count)
        {
            throw new ArgumentException("Lifecycle catalogs require complete nonempty scenarios and actors with unique keys.");
        }
        HashSet<EntityId> roles = [worldId, branchId];
        foreach (EntityId identity in Abilities.Select(ability => ability.DefinitionId)
            .Concat(Scenarios.Select(scenario => scenario.DefinitionId)).Concat(Debts.Select(debt => debt.ActorId)))
        {
            if (!roles.Add(identity)) throw new ArgumentException("Lifecycle scope, definitions and actor roles must not alias.");
        }
        var abilityIds = Abilities.Select(ability => ability.DefinitionId).ToHashSet();
        foreach (var scenario in Scenarios)
        {
            if (!scenario.Combatants.Select(actor => actor.ActorId).SequenceEqual(Debts.Select(debt => debt.ActorId))
                || scenario.Combatants.Any(actor => actor.AbilityIds.Any(id => !abilityIds.Contains(id))))
            {
                throw new ArgumentException("Every scenario must retain the exact full debt actor set and admitted ability grants.");
            }
        }
        _scenarios = Scenarios.ToDictionary(scenario => scenario.DefinitionId);
        _debts = Debts.ToDictionary(debt => debt.ActorId);
        if (encounterCount == 0)
        {
            if (phase != EncounterLifecyclePhase.Exploration || actions is not null || scenarioId is not null
                || tacticalTicks != 0 || entryTacticalTicks != 0)
            {
                throw new ArgumentException("A lifecycle without an entered encounter cannot retain an episode or tactical time.");
            }
        }
        else
        {
            if (scenarioId is not { } selected || actions is null || !_scenarios.TryGetValue(selected, out var scenario)
                || actions.Scope.WorldId != worldId || actions.Scope.BranchId != branchId || actions.Seed != seed
                || roles.Contains(actions.Scope.EncounterId) || actions.Revision > revision
                || !SameAbilities(Abilities, actions.Abilities) || !SameCombatants(scenario.Combatants, actions.Combatants)
                || actions.LastResolution is { } result && result.WorldSequence > revision)
            {
                throw new ArgumentException("The selected action archive must retain this lifecycle's exact scope, seed, definitions and scenario.");
            }
            if (phase is EncounterLifecyclePhase.Exploration or EncounterLifecyclePhase.Outcome && actions.Pending.Count != 0)
            {
                throw new ArgumentException("An outcome or released episode cannot retain executable pending actions.");
            }
            if (phase is EncounterLifecyclePhase.Confrontation or EncounterLifecyclePhase.Staging
                && (tacticalTicks != entryTacticalTicks || actions.Revision != 0 || actions.Pending.Count != 0
                    || actions.LastResolution is not null || actions.Random.NextCounter != 0 || actions.Random.Exhausted))
            {
                throw new ArgumentException("Confrontation and staging retain a fresh unexecuted episode.");
            }
        }
        WorldId = worldId;
        BranchId = branchId;
        Seed = seed;
        Revision = revision;
        ExplorationSteps = explorationSteps;
        TacticalTicks = tacticalTicks;
        EntryTacticalTicks = entryTacticalTicks;
        EncounterCount = encounterCount;
        Phase = phase;
        ScenarioId = scenarioId;
        Actions = actions;
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public EntityId Seed { get; }
    public ulong Revision { get; }
    public ulong ExplorationSteps { get; }
    public ulong TacticalTicks { get; }
    public ulong EntryTacticalTicks { get; }
    public ulong EncounterCount { get; }
    public EncounterLifecyclePhase Phase { get; }
    public ReadOnlyCollection<EncounterAbilityDefinition> Abilities { get; }
    public ReadOnlyCollection<EncounterScenarioDefinition> Scenarios { get; }
    public ReadOnlyCollection<EncounterReadinessDebt> Debts { get; }
    public EntityId? ScenarioId { get; }
    public EncounterActionSnapshot? Actions { get; }
    public bool HasEncounter => Phase != EncounterLifecyclePhase.Exploration;
    public ContentHash ContentHash => ContentHash.Compute(EncounterLifecycleCodecV1.Encode(this));

    public static EncounterLifecycleSnapshot BeginFresh(EntityId worldId, EntityId branchId, EntityId seed,
        IEnumerable<EncounterAbilityDefinition> abilities, IEnumerable<EncounterScenarioDefinition> scenarios,
        IEnumerable<EntityId> actors)
    {
        ArgumentNullException.ThrowIfNull(actors);
        return new(worldId, branchId, seed, 0, 0, 0, 0, 0, EncounterLifecyclePhase.Exploration,
            abilities, scenarios, actors.Select(actor => new EncounterReadinessDebt(actor, 0)));
    }

    public EncounterScenarioDefinition GetScenario(EntityId scenarioId) => _scenarios.TryGetValue(scenarioId, out var scenario)
        ? scenario : throw new ArgumentException("The scenario is not admitted in this lifecycle.", nameof(scenarioId));

    public EncounterReadinessDebt GetDebt(EntityId actorId) => _debts.TryGetValue(actorId, out var debt)
        ? debt : throw new ArgumentException("The actor is not admitted in this readiness ledger.", nameof(actorId));

    public EncounterLifecycleSnapshot Begin(EntityId encounterId, EntityId scenarioId)
    {
        RequirePhase(EncounterLifecyclePhase.Exploration);
        if (Actions?.Scope.EncounterId == encounterId)
            throw new ArgumentException("Reentry requires a new encounter identity.", nameof(encounterId));
        var scenario = GetScenario(scenarioId);
        var actions = EncounterActionSnapshot.Begin(new(WorldId, BranchId, encounterId), Seed, Abilities, scenario.Combatants);
        return Next(ExplorationSteps, TacticalTicks, TacticalTicks, checked(EncounterCount + 1),
            EncounterLifecyclePhase.Confrontation, Debts, scenarioId, actions);
    }

    public EncounterLifecycleSnapshot AdvanceStaging()
    {
        var phase = Phase switch
        {
            EncounterLifecyclePhase.Confrontation => EncounterLifecyclePhase.Staging,
            EncounterLifecyclePhase.Staging => EncounterLifecyclePhase.Active,
            _ => throw new InvalidOperationException("Only confrontation and staging admit an in-place staging boundary.")
        };
        return Next(ExplorationSteps, TacticalTicks, EntryTacticalTicks, EncounterCount, phase, Debts, ScenarioId, Actions);
    }

    public EncounterLifecycleSnapshot ObserveActive(EncounterTimingSnapshot timing, EncounterActionSnapshot actions,
        bool tacticalTick, bool isOutcome)
    {
        RequirePhase(EncounterLifecyclePhase.Active);
        ArgumentNullException.ThrowIfNull(timing);
        ArgumentNullException.ThrowIfNull(actions);
        ulong ticks = tacticalTick ? checked(TacticalTicks + 1) : TacticalTicks;
        ulong units = checked((ulong)((UInt128)(ticks - EntryTacticalTicks) * EncounterTimingSnapshot.UnitsPerTick));
        if (timing.Scope != Actions!.Scope || actions.Scope != timing.Scope || timing.ElapsedTacticalUnits != units
            || !timing.Actors.Select(actor => actor.ActorId).SequenceEqual(Debts.Select(debt => debt.ActorId)))
        {
            throw new ArgumentException("The observed timing must retain exact episode scope, complete actors and accepted tactical units.");
        }
        return Next(ExplorationSteps, ticks, EntryTacticalTicks, EncounterCount,
            isOutcome ? EncounterLifecyclePhase.Outcome : EncounterLifecyclePhase.Active,
            timing.Actors.Select(actor => new EncounterReadinessDebt(actor.ActorId, actor.RemainingDelay)), ScenarioId, actions);
    }

    public EncounterLifecycleSnapshot Release()
    {
        RequirePhase(EncounterLifecyclePhase.Outcome);
        return Next(ExplorationSteps, TacticalTicks, EntryTacticalTicks, EncounterCount,
            EncounterLifecyclePhase.Exploration, Debts, ScenarioId, Actions);
    }

    public EncounterLifecycleSnapshot AdvanceExploration()
    {
        RequirePhase(EncounterLifecyclePhase.Exploration);
        return Next(checked(ExplorationSteps + 1), TacticalTicks, EntryTacticalTicks, EncounterCount, Phase,
            Debts.Select(debt => new EncounterReadinessDebt(debt.ActorId,
                debt.RemainingDelay > ExplorationRecoveryUnits ? debt.RemainingDelay - ExplorationRecoveryUnits : 0)), ScenarioId, Actions);
    }

    public EncounterLifecycleSnapshot RetainBoundary() =>
        Next(ExplorationSteps, TacticalTicks, EntryTacticalTicks, EncounterCount, Phase, Debts, ScenarioId, Actions);

    private EncounterLifecycleSnapshot Next(ulong explorationSteps, ulong tacticalTicks, ulong entryTacticalTicks,
        ulong encounterCount, EncounterLifecyclePhase phase, IEnumerable<EncounterReadinessDebt> debts,
        EntityId? scenarioId, EncounterActionSnapshot? actions) => new(WorldId, BranchId, Seed, checked(Revision + 1),
            explorationSteps, tacticalTicks, entryTacticalTicks, encounterCount, phase, Abilities, Scenarios, debts, scenarioId, actions);

    private void RequirePhase(EncounterLifecyclePhase expected)
    {
        if (Phase != expected) throw new InvalidOperationException("The requested lifecycle boundary does not match its retained phase.");
    }

    private static bool SameAbilities(IReadOnlyList<EncounterAbilityDefinition> left, IReadOnlyList<EncounterAbilityDefinition> right) =>
        left.Count == right.Count && left.Select(ability => (ability.DefinitionId, ability.Version))
            .SequenceEqual(right.Select(ability => (ability.DefinitionId, ability.Version)));

    private static bool SameCombatants(IReadOnlyList<EncounterCombatant> left, IReadOnlyList<EncounterCombatant> right) =>
        left.Count == right.Count && !left.Where((actor, index) => actor.ActorId != right[index].ActorId
            || actor.Side != right[index].Side || !actor.AbilityIds.SequenceEqual(right[index].AbilityIds)).Any();

    private static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> source, Func<T, EntityId> identity, int maximum,
        string parameterName) where T : class
    {
        ArgumentNullException.ThrowIfNull(source, parameterName);
        SortedDictionary<EntityId, T> result = [];
        foreach (T item in source)
        {
            ArgumentNullException.ThrowIfNull(item, parameterName);
            if (result.Count == maximum || !result.TryAdd(identity(item), item))
                throw new ArgumentException("Lifecycle catalogs must have bounded unique identities.", parameterName);
        }
        return Array.AsReadOnly(result.Values.ToArray());
    }
}
