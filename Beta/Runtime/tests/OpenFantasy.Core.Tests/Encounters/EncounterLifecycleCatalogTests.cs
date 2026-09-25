using OpenFantasy.Core.Encounters;
using static OpenFantasy.Core.Tests.Encounters.EncounterLifecycleFixture;

namespace OpenFantasy.Core.Tests.Encounters;

public sealed class EncounterLifecycleCatalogTests
{
    [Fact]
    public void Authored_scenarios_are_complete_canonical_immutable_and_pinned_by_every_role_and_reach()
    {
        var abilities = Abilities;
        var scenarios = new[] { Scenario(), Scenario(501, 30) };
        var actors = EncounterLifecycleFixture.Actors;
        var state = EncounterLifecycleSnapshot.BeginFresh(World, Branch, Seed, abilities, scenarios, actors);
        byte[] canonical = EncounterLifecycleCodecV1.Encode(state);
        var reordered = EncounterLifecycleSnapshot.BeginFresh(World, Branch, Seed, abilities.Reverse(), scenarios.Reverse(), actors.Reverse());
        Assert.Equal(canonical, EncounterLifecycleCodecV1.Encode(reordered));
        var original = state.GetScenario(Id(500));
        var combatants = original.Combatants.ToArray();
        var alternate = new EncounterScenarioDefinition(original.DefinitionId, original.Key, original.DisplayName,
            original.InitiatingActorId, original.TargetActorId, original.EntryRangeSubunits, combatants.Reverse());
        Assert.Equal(original.Version, alternate.Version);
        combatants[0] = new(Id(10), 1, []);
        abilities[0] = EncounterActionFixture.Ability(102);
        scenarios[0] = Scenario(502);
        actors[0] = Id(99);
        Assert.Equal(canonical, EncounterLifecycleCodecV1.Encode(state));
        Assert.Equal(original.Version, alternate.Version);
        var shorterReach = new EncounterScenarioDefinition(original.DefinitionId, original.Key, original.DisplayName,
            original.InitiatingActorId, original.TargetActorId, 767, original.Combatants);
        Assert.NotEqual(original.Version, shorterReach.Version);
        var swappedRoles = new EncounterScenarioDefinition(original.DefinitionId, original.Key, original.DisplayName,
            original.TargetActorId, original.InitiatingActorId, original.EntryRangeSubunits, original.Combatants);
        Assert.NotEqual(original.Version, swappedRoles.Version);
        Assert.NotEqual(state.ContentHash, Copy(state, scenarios: [shorterReach, state.GetScenario(Id(501))]).ContentHash);
        Assert.Throws<ArgumentException>(() => state.GetScenario(Id(99)));
        Assert.Throws<ArgumentException>(() => state.GetDebt(Id(99)));
    }

    [Fact]
    public void Missing_extra_duplicate_unknown_and_alias_roles_reject_before_any_phase_change()
    {
        var state = Fresh();
        Assert.Throws<ArgumentException>(() => Copy(state, scenarios: []));
        Assert.Throws<ArgumentException>(() => Copy(state, debts: state.Debts.Skip(1)));
        Assert.Throws<ArgumentException>(() => Copy(state, debts: state.Debts.Append(new(Id(99), 0))));
        Assert.Throws<ArgumentException>(() => Copy(state, debts: state.Debts.Append(state.Debts[0])));
        Assert.Throws<ArgumentException>(() => Copy(state, abilities: state.Abilities.Skip(1)));
        Assert.Throws<ArgumentException>(() => Copy(state, abilities: state.Abilities.Append(state.Abilities[0])));
        Assert.Throws<ArgumentException>(() => Copy(state, scenarios: state.Scenarios.Append(state.Scenarios[0])));
        var alias = new EncounterScenarioDefinition(Id(100), "alias", "Invalid definition role", Id(10), Id(20), 100,
            [new(Id(10), 1, []), new(Id(20), 2, []), new(Id(30), 0, []), new(Id(40), 0, [])]);
        Assert.Throws<ArgumentException>(() => Copy(state, scenarios: [alias]));
        var unknownGrant = new EncounterScenarioDefinition(Id(500), "unknown", "Unknown grant", Id(10), Id(20), 100,
            [new(Id(10), 1, [Id(999)]), new(Id(20), 2, []), new(Id(30), 0, []), new(Id(40), 0, [])]);
        Assert.Throws<ArgumentException>(() => Copy(state, scenarios: [unknownGrant]));
        var repeatedKey = new EncounterScenarioDefinition(Id(502), state.Scenarios[0].Key, "Duplicate key", Id(10), Id(20), 100,
            state.Scenarios[0].Combatants);
        Assert.Throws<ArgumentException>(() => Copy(state, scenarios: state.Scenarios.Append(repeatedKey)));
        foreach (EntityId aliasId in new[] { World, Branch, Id(10), Id(100), Id(500) })
            Assert.Throws<ArgumentException>(() => state.Begin(aliasId, Id(500)));
        Assert.Throws<ArgumentException>(() => state.Begin(Id(1_000), Id(999)));
        Assert.Equal(EncounterLifecycleCodecV1.Encode(state), EncounterLifecycleCodecV1.Encode(Restore(state)));
    }

    [Theory]
    [InlineData("zero-range")]
    [InlineData("large-range")]
    [InlineData("same-actor")]
    [InlineData("same-side")]
    [InlineData("bystander")]
    [InlineData("missing-target")]
    [InlineData("definition-actor-alias")]
    [InlineData("invalid-key")]
    public void Scenario_entry_requires_real_opposing_roles_and_bounded_metadata(string invalid)
    {
        var actors = Scenario().Combatants;
        EntityId target = invalid == "same-actor" ? Id(10) : invalid == "missing-target" ? Id(99) : Id(20);
        if (invalid == "same-side") actors = Array.AsReadOnly(actors.Select(actor => actor.ActorId == Id(20)
            ? new EncounterCombatant(actor.ActorId, 1, actor.AbilityIds) : actor).ToArray());
        if (invalid == "bystander") actors = Array.AsReadOnly(actors.Select(actor => actor.ActorId == Id(20)
            ? new EncounterCombatant(actor.ActorId, 0, []) : actor).ToArray());
        long range = invalid == "zero-range" ? 0 : invalid == "large-range" ? 769 : 100;
        Assert.Throws<ArgumentException>(() => new EncounterScenarioDefinition(invalid == "definition-actor-alias" ? Id(10) : Id(500),
            invalid == "invalid-key" ? "Invalid Key" : "valid", "Scenario", Id(10), target, range, actors));
    }

    [Fact]
    public void Every_input_enumeration_has_a_finite_count_bound()
    {
        int seen = 0;
        IEnumerable<EncounterAbilityDefinition> TooManyAbilities()
        {
            for (ulong index = 1_000; ; index++) { seen++; yield return EncounterActionFixture.Ability(index); }
        }
        Assert.Throws<ArgumentException>(() => Copy(Fresh(), abilities: TooManyAbilities()));
        Assert.Equal(33, seen);
        seen = 0;
        IEnumerable<EncounterScenarioDefinition> TooManyScenarios()
        {
            for (ulong index = 5_000; ; index++) { seen++; yield return Scenario(index); }
        }
        Assert.Throws<ArgumentException>(() => Copy(Fresh(), scenarios: TooManyScenarios()));
        Assert.Equal(9, seen);
        seen = 0;
        IEnumerable<EncounterReadinessDebt> TooManyActors()
        {
            for (ulong index = 10; ; index++) { seen++; yield return new(Id(index), 0); }
        }
        Assert.Throws<ArgumentException>(() => Copy(Fresh(), debts: TooManyActors()));
        Assert.Equal(65, seen);
        Assert.Throws<ArgumentNullException>(() => Copy(Fresh(), debts: [null!]));
        Assert.Throws<ArgumentNullException>(() => new EncounterScenarioDefinition(Id(500), "valid", "Scenario", Id(10), Id(20), 100, [null!]));
    }
}
