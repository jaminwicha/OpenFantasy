using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Encounters.EncounterActionFixture;

namespace OpenFantasy.Core.Tests.Encounters;

public sealed class EncounterActionSnapshotTests
{
    [Fact]
    public void Queuing_and_recovery_retain_full_request_without_spending_rng_then_damage_consumes_exactly_its_draw()
    {
        var initial = Begin();
        byte[] original = EncounterActionCodecV1.Encode(initial);
        var queued = initial.WithPending([Command()]);
        Assert.Equal(initial.Random, queued.Random);
        Assert.Equal(WorldActionCodec.Encode(Command()), WorldActionCodec.Encode(Assert.Single(queued.Pending)));
        var resumed = EncounterActionCodecV1.Decode(EncounterActionCodecV1.Encode(queued), Scope, TestContext.Current.CancellationToken);
        var outcome = Damaged(queued, out var afterDraw);
        var complete = queued.WithResolution([], afterDraw, outcome);
        var resumedComplete = resumed.WithResolution([], afterDraw, outcome);
        Assert.Empty(complete.Pending);
        Assert.Equal(queued.Revision + 1, complete.Revision);
        Assert.Equal(EncounterActionCodecV1.Encode(complete), EncounterActionCodecV1.Encode(resumedComplete));
        Assert.Equal(afterDraw, complete.CreateRandom().CaptureState());
        Assert.Equal(original, EncounterActionCodecV1.Encode(initial));
        Assert.Equal(9U, complete.LastResolution!.Damage);
        Assert.Equal(11U, complete.LastResolution.TargetHpAfter);
        // HP remains descriptive outcome data here; there is no independently owned actor-health collection.
        Assert.Equal(3, complete.Combatants.Count);
    }

    [Fact]
    public void Wait_and_invalidated_choices_keep_the_exact_random_position_and_positive_delay()
    {
        var state = Begin().WithPending([Command()]);
        var invalidated = new EncounterActionResolution(2, 2, Id(10), Id(200), Id(20), Id(100),
            EncounterActionResolutionStatus.Invalidated, 0, 0, 0, 0, 67, 0, []);
        var next = state.WithResolution([], state.Random, invalidated);
        Assert.Equal(state.Random, next.Random);
        Assert.Equal(67UL, next.LastResolution!.Delay);
        var wait = new EncounterActionResolution(3, 3, Id(20), Id(201), null, null,
            EncounterActionResolutionStatus.Wait, 0, 0, 0, 0, 100, 0, []);
        next = next.WithResolution([], next.Random, wait);
        Assert.Equal(state.Random, next.Random);
        var random = state.CreateRandom();
        random.NextUInt64(201);
        Assert.Throws<ArgumentException>(() => state.WithResolution([], random.CaptureState(), invalidated));
    }

    [Fact]
    public void Wrong_draw_repeated_outcome_and_unrelated_pending_changes_are_rejected_atomically()
    {
        var state = Begin().WithPending([Command(), Command(actor: 20, target: 10, command: 201)]);
        byte[] before = EncounterActionCodecV1.Encode(state);
        var outcome = Damaged(state, out var next);
        Assert.Throws<ArgumentException>(() => state.WithResolution([Command(actor: 20, target: 10, command: 201)], state.Random, outcome));
        var extraDraw = state.CreateRandom();
        extraDraw.NextUInt64(201);
        extraDraw.NextUInt64(201);
        Assert.Throws<ArgumentException>(() => state.WithResolution([Command(actor: 20, target: 10, command: 201)], extraDraw.CaptureState(), outcome));
        Assert.Throws<ArgumentException>(() => state.WithResolution([], next, outcome));
        var complete = state.WithResolution([Command(actor: 20, target: 10, command: 201)], next, outcome);
        Assert.Throws<ArgumentException>(() => complete.WithResolution([], next, outcome));
        Assert.Equal(before, EncounterActionCodecV1.Encode(state));
        var cancelling = Damaged(state, out var afterCancellationDraw, cancelled: [Id(201)]);
        Assert.Empty(state.WithResolution([], afterCancellationDraw, cancelling).Pending);
    }

    [Fact]
    public void Bystanders_empty_grants_and_opposing_sides_are_exact_encounter_membership()
    {
        var state = Begin();
        Assert.Equal((ushort)0, state.GetCombatant(Id(30)).Side);
        Assert.Empty(state.GetCombatant(Id(30)).AbilityIds);
        Assert.Throws<ArgumentException>(() => new EncounterCombatant(Id(30), 0, [Id(100)]));
        Assert.Throws<ArgumentException>(() => state.WithPending([Command(target: 30)]));
        Assert.Throws<ArgumentException>(() => state.WithPending([Command(actor: 30)]));
        Assert.Throws<ArgumentException>(() => state.WithPending([Command(target: 10)]));
        Assert.Throws<ArgumentException>(() => state.WithPending([Command(ability: 101)]));
        Assert.Throws<ArgumentException>(() => state.WithPending([Command(target: 99)]));
        Assert.Throws<ArgumentException>(() => EncounterActionSnapshot.Begin(Scope, Seed, [Ability()], [new(Id(10), 1, [Id(101)])]));
        var oneSide = EncounterActionSnapshot.Begin(Scope, Seed, [Ability()], [new(Id(10), 1, [Id(100)]), new(Id(20), 1, [Id(100)])]);
        Assert.Throws<ArgumentException>(() => oneSide.WithPending([Command()]));
    }

    [Fact]
    public void Catalogs_pending_ids_and_stream_scope_are_bounded_unique_and_non_aliasing()
    {
        var state = Begin();
        Assert.Throws<ArgumentException>(() => state.WithPending([Command(), Command(command: 201)]));
        Assert.Throws<ArgumentException>(() => state.WithPending([Command(), Command(actor: 20, target: 10)]));
        Assert.Throws<ArgumentException>(() => state.WithPending([Command(command: 100)]));
        Assert.Throws<ArgumentException>(() => state.WithPending([Command(scope: new(Id(1), Id(2), Id(5)))]));
        Assert.Throws<ArgumentException>(() => EncounterActionSnapshot.Begin(Scope, Seed, [Ability(), Ability()], state.Combatants));
        Assert.Throws<ArgumentException>(() => EncounterActionSnapshot.Begin(Scope, Seed, [Ability(10)], state.Combatants));
        Assert.Throws<ArgumentException>(() => new EncounterActionSnapshot(Scope, Id(5), 0, state.Abilities, state.Combatants, [], state.Random));
        Assert.Throws<ArgumentException>(() => new EncounterActionSnapshot(Scope, Seed, 0, state.Abilities, state.Combatants, [], state.Random with { Exhausted = true }));
        int read = 0;
        IEnumerable<EncounterAbilityDefinition> TooMany()
        {
            while (true) { yield return Ability((ulong)(100 + read++)); }
        }
        Assert.Throws<ArgumentException>(() => EncounterActionSnapshot.Begin(Scope, Seed, TooMany(), state.Combatants));
        Assert.Equal(33, read);
    }

    [Fact]
    public void Exhausted_stream_cannot_redraw_and_maximum_revision_cannot_publish_a_successor()
    {
        var initial = Begin();
        var random = initial.CreateRandom();
        random.Restore(initial.Random with { NextCounter = ulong.MaxValue });
        random.NextUInt64();
        var exhausted = new EncounterActionSnapshot(Scope, Seed, 1, initial.Abilities, initial.Combatants, [Command()], random.CaptureState());
        Assert.True(exhausted.CreateRandom().CaptureState().Exhausted);
        Assert.Throws<OverflowException>(() => exhausted.CreateRandom().NextUInt64(201));
        var maximum = new EncounterActionSnapshot(Scope, Seed, ulong.MaxValue, initial.Abilities, initial.Combatants, [], initial.Random);
        Assert.Throws<OverflowException>(() => maximum.WithPending([Command()]));
        Assert.Equal(EncounterActionCodecV1.Encode(exhausted), EncounterActionCodecV1.Encode(
            EncounterActionCodecV1.Decode(EncounterActionCodecV1.Encode(exhausted), Scope, TestContext.Current.CancellationToken)));
    }
}
