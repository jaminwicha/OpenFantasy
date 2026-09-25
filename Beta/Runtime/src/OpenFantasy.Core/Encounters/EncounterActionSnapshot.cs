using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Encounters;

/// <summary>
/// Retained physical-action definitions, explicit grants, requests and deterministic draw position.
/// World admission owns controller authentication, timing matches, range, capability and HP/custody effects.
/// </summary>
public sealed class EncounterActionSnapshot
{
    public const int MaximumAbilities = 32;
    public const int MaximumCombatants = 64;
    public const int MaximumPending = 64;
    public const int MaximumPendingCommandByteLength = 1_024;
    public const string RandomDomain = "encounter.physical-damage.v1";
    public static ContentHash RulesPin { get; } = ContentHash.Compute(CanonicalEncodingV1.Text(
        "encounter.physical-actions.v1;damage=max(1,floor(max(1,floor(attack*powerTenths/10)-floor(defense/2))*variation/1000));variation=900+uniform[0,201);delay=ceil(10000*numerator/(denominator*normalizedSpeed));invalidated=no-mana-no-draw;hp-owned-by-progression",
        nameof(RulesPin)));

    private readonly Dictionary<EntityId, EncounterAbilityDefinition> _abilities;
    private readonly Dictionary<EntityId, EncounterCombatant> _combatants;

    public EncounterActionSnapshot(EncounterTimingScope scope, EntityId seed, ulong revision,
        IEnumerable<EncounterAbilityDefinition> abilities, IEnumerable<EncounterCombatant> combatants,
        IEnumerable<WorldActionCommand> pending, RandomCounterState random,
        EncounterActionResolution? lastResolution = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        Abilities = Copy(abilities, ability => ability.DefinitionId, MaximumAbilities, nameof(abilities));
        Combatants = Copy(combatants, actor => actor.ActorId, MaximumCombatants, nameof(combatants));
        Pending = Copy(pending, command => command.ActorId, MaximumPending, nameof(pending));
        _abilities = Abilities.ToDictionary(ability => ability.DefinitionId);
        _combatants = Combatants.ToDictionary(actor => actor.ActorId);
        if (Combatants.Count == 0 || Abilities.Select(ability => ability.Key).Distinct(StringComparer.Ordinal).Count() != Abilities.Count
            || Abilities.Any(ability => scope.Contains(ability.DefinitionId))
            || Combatants.Any(actor => scope.Contains(actor.ActorId) || _abilities.ContainsKey(actor.ActorId)
                || actor.AbilityIds.Any(id => !_abilities.ContainsKey(id))))
        {
            throw new ArgumentException("Encounter catalogs require distinct scoped identities, unique keys and complete ability grants.");
        }
        HashSet<EntityId> commands = [];
        foreach (WorldActionCommand command in Pending)
        {
            if (command.WorldId != scope.WorldId || command.BranchId != scope.BranchId
                || !_combatants.TryGetValue(command.ActorId, out var actor)
                || command.Intent is not QueueEncounterAbilityIntent intent || intent.EncounterId != scope.EncounterId
                || !actor.AbilityIds.Contains(intent.AbilityDefinitionId)
                || !_combatants.TryGetValue(intent.TargetActorId, out var target) || actor.Side == 0 || target.Side == 0 || actor.Side == target.Side
                || scope.Contains(command.CommandId) || _abilities.ContainsKey(command.CommandId) || _combatants.ContainsKey(command.CommandId)
                || !commands.Add(command.CommandId) || WorldActionCodec.Encode(command).Length > MaximumPendingCommandByteLength)
            {
                throw new ArgumentException("Pending abilities require exact scope, unique commands, owned grants and admitted opposing targets.", nameof(pending));
            }
        }
        Scope = scope;
        Seed = seed;
        Revision = revision;
        Random = random;
        LastResolution = lastResolution;
        _ = CreateRandom();
        if (lastResolution is not null)
        {
            ValidateResolution(lastResolution, commands);
        }
    }

    public EncounterTimingScope Scope { get; }
    public EntityId Seed { get; }
    public ulong Revision { get; }
    public ReadOnlyCollection<EncounterAbilityDefinition> Abilities { get; }
    public ReadOnlyCollection<EncounterCombatant> Combatants { get; }
    public ReadOnlyCollection<WorldActionCommand> Pending { get; }
    public RandomCounterState Random { get; }
    public EncounterActionResolution? LastResolution { get; }
    public ContentHash RulesVersion => RulesPin;
    public ContentHash ContentHash => ContentHash.Compute(EncounterActionCodecV1.Encode(this));

    public static EncounterActionSnapshot Begin(EncounterTimingScope scope, EntityId seed,
        IEnumerable<EncounterAbilityDefinition> definitions, IEnumerable<EncounterCombatant> combatants)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var random = new CounterRandomV1(seed.ToByteArray(), RulesPin.Value, RandomDomain, scope.EncounterId);
        return new EncounterActionSnapshot(scope, seed, 0, definitions, combatants, [], random.CaptureState());
    }

    public EncounterAbilityDefinition GetAbility(EntityId definitionId) => _abilities.TryGetValue(definitionId, out var ability)
        ? ability : throw new ArgumentException("The ability is not admitted in this encounter.", nameof(definitionId));

    public EncounterCombatant GetCombatant(EntityId actorId) => _combatants.TryGetValue(actorId, out var actor)
        ? actor : throw new ArgumentException("The combatant is not admitted in this encounter.", nameof(actorId));

    /// <summary>Returns an independent mutable stream restored at this immutable owner's exact next draw.</summary>
    public CounterRandomV1 CreateRandom()
    {
        var random = new CounterRandomV1(Seed.ToByteArray(), RulesPin.Value, RandomDomain, Scope.EncounterId);
        random.Restore(Random);
        return random;
    }

    public EncounterActionSnapshot WithPending(IEnumerable<WorldActionCommand> pending) =>
        new(Scope, Seed, checked(Revision + 1), Abilities, Combatants, pending, Random, LastResolution);

    public EncounterActionSnapshot WithResolution(IEnumerable<WorldActionCommand> remaining, RandomCounterState random,
        EncounterActionResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        if (LastResolution is { } prior && (resolution.WorldSequence <= prior.WorldSequence || resolution.TimingRevision <= prior.TimingRevision))
        {
            throw new ArgumentException("A resolution must follow the retained action boundary.", nameof(resolution));
        }
        RandomCounterState expectedRandom = Random;
        if (resolution.Status == EncounterActionResolutionStatus.Damaged)
        {
            CounterRandomV1 draw = CreateRandom();
            uint expectedVariation = checked((uint)(900 + draw.NextUInt64(201)));
            if (resolution.VariationPermille != expectedVariation)
            {
                throw new ArgumentException("Damage variation does not match the next owned uniform draw.", nameof(resolution));
            }
            expectedRandom = draw.CaptureState();
        }
        if (random != expectedRandom)
        {
            throw new ArgumentException("The resolution must retain exactly its applicable draw and no others.", nameof(random));
        }
        if (resolution.Status != EncounterActionResolutionStatus.Wait)
        {
            WorldActionCommand? original = Pending.FirstOrDefault(command => command.CommandId == resolution.DecisionCommandId);
            if (original?.Intent is not QueueEncounterAbilityIntent intent || original.ActorId != resolution.ActorId
                || intent.TargetActorId != resolution.TargetActorId || intent.AbilityDefinitionId != resolution.AbilityDefinitionId)
            {
                throw new ArgumentException("The resolved ability must match one exact retained pending request.", nameof(resolution));
            }
        }
        else if (Pending.Any(command => command.CommandId == resolution.DecisionCommandId))
        {
            throw new ArgumentException("A retained ability request cannot be resolved as a Wait.", nameof(resolution));
        }
        var candidate = new EncounterActionSnapshot(Scope, Seed, checked(Revision + 1), Abilities, Combatants, remaining, random, resolution);
        var expected = Pending.Where(command => command.CommandId != resolution.DecisionCommandId
            && !resolution.CancelledDecisionIds.Contains(command.CommandId)).ToArray();
        if (candidate.Pending.Count != expected.Length || candidate.Pending.Where((command, index) =>
            !WorldActionCodec.Encode(command).AsSpan().SequenceEqual(WorldActionCodec.Encode(expected[index]))).Any())
        {
            throw new ArgumentException("Resolution must preserve every other exact pending request.", nameof(remaining));
        }
        return candidate;
    }

    private void ValidateResolution(EncounterActionResolution resolution, HashSet<EntityId> pendingIds)
    {
        bool IsReserved(EntityId identity) => Scope.Contains(identity) || _abilities.ContainsKey(identity) || _combatants.ContainsKey(identity);
        if (Revision == 0 || !_combatants.TryGetValue(resolution.ActorId, out var actor)
            || IsReserved(resolution.DecisionCommandId) || pendingIds.Contains(resolution.DecisionCommandId)
            || resolution.CancelledDecisionIds.Any(id => IsReserved(id) || pendingIds.Contains(id)))
        {
            throw new ArgumentException("The retained resolution has invalid actors or decision identities.", nameof(resolution));
        }
        if (resolution.TargetActorId is { } targetId)
        {
            if (!_combatants.TryGetValue(targetId, out var target) || actor.Side == 0 || target.Side == 0 || target.Side == actor.Side
                || resolution.AbilityDefinitionId is not { } abilityId || !actor.AbilityIds.Contains(abilityId)
                || resolution.Status == EncounterActionResolutionStatus.Damaged && resolution.ManaSpent != GetAbility(abilityId).ManaCost)
            {
                throw new ArgumentException("The retained resolution requires its exact granted ability and opposing target.", nameof(resolution));
            }
        }
    }

    private static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> source, Func<T, EntityId> identity,
        int maximum, string parameterName) where T : class
    {
        ArgumentNullException.ThrowIfNull(source, parameterName);
        SortedDictionary<EntityId, T> values = [];
        foreach (T item in source)
        {
            ArgumentNullException.ThrowIfNull(item, parameterName);
            if (values.Count == maximum || !values.TryAdd(identity(item), item))
            {
                throw new ArgumentException("Encounter collections must be bounded with unique identities.", parameterName);
            }
        }
        return Array.AsReadOnly(values.Values.ToArray());
    }
}
