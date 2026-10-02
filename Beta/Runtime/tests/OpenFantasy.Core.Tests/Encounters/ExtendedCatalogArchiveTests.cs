using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Encounters;
using static OpenFantasy.Core.Tests.Encounters.EncounterLifecycleFixture;

namespace OpenFantasy.Core.Tests.Encounters;

public sealed class ExtendedCatalogArchiveTests
{
    [Fact]
    public void Full_width_catalog_actions_and_counters_fit_but_oversized_combination_is_refused_at_admission()
    {
        var abilities = Enumerable.Range(1_000, 32).Select(id => new EncounterAbilityDefinition(Id((ulong)id),
            "a" + new string('x', 59) + id, new string('n', 128), EncounterActionFixture.Source, 1_000, 768,
            1_000_000, 1_000_000, 1_000_000, 256, BodyFunction.NaturalWeapon)).ToArray();
        var actors = Enumerable.Range(100, 64).Select(id => new EncounterCombatant(Id((ulong)id), (ushort)(id % 2 + 1),
            abilities.Select(ability => ability.DefinitionId))).ToArray();
        var scenarios = Enumerable.Range(5_000, 8).Select(id => new EncounterScenarioDefinition(Id((ulong)id),
            "s" + new string('x', 59) + id, new string('n', 128), Id(100), Id(101), 768, actors.Reverse())).ToArray();
        var scope = new EncounterTimingScope(World, Branch, Id(10_000));
        var initial = EncounterActionSnapshot.Begin(scope, Seed, abilities, actors);
        var pending = actors.Select(actor => EncounterActionFixture.Command(actor.ActorId.Low,
            actor.ActorId.Low % 2 == 0 ? 101UL : 100UL, 20_000 + actor.ActorId.Low, ability: 1_000, scope: scope)).ToArray();
        var result = new EncounterActionResolution(ulong.MaxValue, ulong.MaxValue, Id(100), Id(99_999), Id(101), Id(1_000),
            EncounterActionResolutionStatus.Damaged, 1, uint.MaxValue, uint.MaxValue - 1, 1_000_000, ulong.MaxValue, 1_100,
            Enumerable.Range(50_000, 64).Select(id => Id((ulong)id)));
        var actions = new EncounterActionSnapshot(scope, Seed, ulong.MaxValue, abilities, actors, pending,
            initial.Random with { NextCounter = ulong.MaxValue, Exhausted = true }, result);
        var maximum = new EncounterLifecycleSnapshot(World, Branch, Seed, ulong.MaxValue, ulong.MaxValue - 1,
            0, 0, 1, EncounterLifecyclePhase.Active, abilities.Reverse(), scenarios.Reverse(),
            actors.Select(actor => new EncounterReadinessDebt(actor.ActorId, ulong.MaxValue)).Reverse(), Id(5_000), actions, catalogVersion: 2);
        byte[] encoded = EncounterLifecycleCodecV1.Encode(maximum);
        Assert.True(encoded.Length < EncounterLifecycleCodecV1.MaximumEncodedByteLength);
        var restored = Restore(maximum);
        Assert.Equal(encoded, EncounterLifecycleCodecV1.Encode(restored));
        Assert.Equal(8, restored.Scenarios.Count);
        Assert.Equal(32, restored.Abilities.Count);
        Assert.Equal(64, restored.Debts.Count);
        Assert.Equal(64, restored.Actions!.Pending.Count);
        Assert.Equal(64, restored.Actions.LastResolution!.CancelledDecisionIds.Count);
        Assert.All(restored.Debts, debt => Assert.Equal(ulong.MaxValue, debt.RemainingDelay));
        Assert.Equal(EncounterActionCodecV1.Encode(actions), EncounterActionCodecV1.Encode(restored.Actions));
        var tooMany = Enumerable.Range(5_000, 9).Select(id => new EncounterScenarioDefinition(Id((ulong)id),
            "s" + new string('x', 59) + id, new string('n', 128), Id(100), Id(101), 768, actors)).ToArray();
        Assert.Throws<ArgumentException>(() => EncounterLifecycleSnapshot.BeginFresh(World, Branch, Seed,
            abilities, tooMany, actors.Select(actor => actor.ActorId), catalogVersion: 2));
        var fields = Fields(encoded);
        var rows = Fields(fields[12]);
        // Add one individually lawful full-width scenario beyond the joint catalog budget.
        foreach (var scenario in tooMany.Skip(8))
        {
            var one = EncounterLifecycleSnapshot.BeginFresh(World, Branch, Seed, abilities, [scenario],
                actors.Select(actor => actor.ActorId), catalogVersion: 2);
            rows.Add(Fields(Fields(EncounterLifecycleCodecV1.Encode(one))[12])[1]);
        }
        fields[12] = Frame(rows);
        byte[] oversizedCatalog = Frame(fields);
        Assert.True(oversizedCatalog.Length < EncounterLifecycleCodecV1.MaximumEncodedByteLength);
        Assert.Throws<FormatException>(() => EncounterLifecycleCodecV1.Decode(oversizedCatalog, World, Branch,
            TestContext.Current.CancellationToken));
    }
}
