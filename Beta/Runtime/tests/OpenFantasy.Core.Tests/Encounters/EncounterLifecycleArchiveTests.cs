using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Encounters;
using static OpenFantasy.Core.Tests.Encounters.EncounterLifecycleFixture;

namespace OpenFantasy.Core.Tests.Encounters;

public sealed class EncounterLifecycleArchiveTests
{
    [Fact]
    public void Maximum_complete_catalogs_pending_commands_and_full_width_values_fit_the_reserved_archive()
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
            actors.Select(actor => new EncounterReadinessDebt(actor.ActorId, ulong.MaxValue)).Reverse(), Id(5_000), actions);
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
    }

    [Theory]
    [InlineData("version")]
    [InlineData("phase")]
    [InlineData("future-entry")]
    [InlineData("unearned-clock")]
    [InlineData("count-zero")]
    [InlineData("seed")]
    [InlineData("selected-scenario")]
    [InlineData("encounter-context")]
    [InlineData("action-world")]
    [InlineData("ability-pin")]
    [InlineData("scenario-side")]
    [InlineData("unknown-grant")]
    [InlineData("debt-actor")]
    [InlineData("released-pending")]
    public void Inconsistent_full_archive_fields_cannot_be_reinterpreted_as_a_valid_lifecycle(string corruption)
    {
        var fields = Fields(EncounterLifecycleCodecV1.Encode(Queued().Lifecycle));
        var abilities = Fields(fields[11]);
        var ability = Fields(abilities[1]);
        var scenarios = Fields(fields[12]);
        var scenario = Fields(scenarios[1]);
        var combatants = Fields(scenario[7]);
        var combatant = Fields(combatants[1]);
        var debts = Fields(fields[13]);
        var debt = Fields(debts[1]);
        var actions = Fields(fields[16]);
        switch (corruption)
        {
            case "version": Number(fields[1], 2); break;
            case "phase": Number(fields[10], 99); break;
            case "future-entry": Number(fields[8], 1); break;
            case "unearned-clock": Number(fields[6], ulong.MaxValue); break;
            case "count-zero": Number(fields[9], 0); break;
            case "seed": fields[4][0] ^= 1; break;
            case "selected-scenario": Id(501).WriteBytes(fields[14]); break;
            case "encounter-context": Id(1_001).WriteBytes(fields[15]); break;
            case "action-world": actions[3][0] ^= 1; break;
            case "ability-pin": Number(ability[5], 11); break;
            case "scenario-side": Number(combatant[2], 2); break;
            case "unknown-grant": Id(999).WriteBytes(combatant[3]); break;
            case "debt-actor": Id(99).WriteBytes(debt[1]); break;
            case "released-pending": Number(fields[10], (ulong)EncounterLifecyclePhase.Exploration); break;
            default: throw new InvalidOperationException();
        }
        abilities[1] = Frame(ability); fields[11] = Frame(abilities);
        combatants[1] = Frame(combatant); scenario[7] = Frame(combatants); scenarios[1] = Frame(scenario); fields[12] = Frame(scenarios);
        debts[1] = Frame(debt); fields[13] = Frame(debts); fields[16] = Frame(actions);
        Assert.Throws<FormatException>(() => EncounterLifecycleCodecV1.Decode(Frame(fields), World, Branch, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Missing_duplicate_reordered_rows_foreign_scope_truncation_trailing_and_cancellation_refuse()
    {
        byte[] original = EncounterLifecycleCodecV1.Encode(Outcome());
        foreach (int catalog in new[] { 11, 12, 13 })
        {
            var fields = Fields(original);
            var rows = Fields(fields[catalog]);
            (rows[1], rows[2]) = (rows[2], rows[1]);
            fields[catalog] = Frame(rows);
            Assert.Throws<FormatException>(() => EncounterLifecycleCodecV1.Decode(Frame(fields), World, Branch, TestContext.Current.CancellationToken));
            rows[2] = rows[1]; fields[catalog] = Frame(rows);
            Assert.Throws<FormatException>(() => EncounterLifecycleCodecV1.Decode(Frame(fields), World, Branch, TestContext.Current.CancellationToken));
        }
        var missing = Fields(original);
        var debts = Fields(missing[13]); debts.RemoveAt(1); missing[13] = Frame(debts);
        Assert.Throws<FormatException>(() => EncounterLifecycleCodecV1.Decode(Frame(missing), World, Branch, TestContext.Current.CancellationToken));
        var contextMissing = Fields(original); contextMissing[15] = [];
        Assert.Throws<FormatException>(() => EncounterLifecycleCodecV1.Decode(Frame(contextMissing), World, Branch, TestContext.Current.CancellationToken));
        Assert.Throws<FormatException>(() => EncounterLifecycleCodecV1.Decode(original, Id(99), Branch, TestContext.Current.CancellationToken));
        Assert.Throws<FormatException>(() => EncounterLifecycleCodecV1.Decode(original, World, Id(99), TestContext.Current.CancellationToken));
        foreach (int length in new[] { 0, 11, 12, original.Length / 2, original.Length - 1 })
            Assert.Throws<FormatException>(() => EncounterLifecycleCodecV1.Decode(original[..length], World, Branch, TestContext.Current.CancellationToken));
        Assert.Throws<FormatException>(() => EncounterLifecycleCodecV1.Decode(original.Concat(new byte[] { 0 }).ToArray(), World, Branch, TestContext.Current.CancellationToken));
        Assert.Throws<FormatException>(() => EncounterLifecycleCodecV1.Decode(new byte[EncounterLifecycleCodecV1.MaximumEncodedByteLength + 1], World, Branch, TestContext.Current.CancellationToken));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => EncounterLifecycleCodecV1.Decode(original, World, Branch, cancellation.Token));
    }
}
