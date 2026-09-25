using OpenFantasy.Core.Encounters;
using static OpenFantasy.Core.Tests.Encounters.EncounterLifecycleFixture;

namespace OpenFantasy.Core.Tests.Encounters;

public sealed class EncounterLifecycleTests
{
    [Fact]
    public void Queue_resume_outcome_release_and_new_scenario_preserve_debt_without_an_instant_extra_action()
    {
        var fresh = Fresh();
        Assert.False(fresh.HasEncounter);
        Assert.All(fresh.Debts, debt => Assert.Equal(0UL, debt.RemainingDelay));
        var confronted = Restore(fresh.Begin(Id(1_000), Id(500)));
        Assert.Equal(EncounterLifecyclePhase.Confrontation, confronted.Phase);
        Assert.Equal(1UL, confronted.EncounterCount);
        var staging = Restore(confronted.AdvanceStaging());
        Assert.Equal(EncounterLifecyclePhase.Staging, staging.Phase);
        var active = Restore(staging.AdvanceStaging());
        Assert.Equal(EncounterLifecyclePhase.Active, active.Phase);
        Assert.Equal(0UL, active.ExplorationSteps);
        Assert.Equal(0UL, active.TacticalTicks);

        var (queued, timing) = Queued();
        byte[] pendingBytes = EncounterActionCodecV1.Encode(queued.Actions!);
        var restored = Restore(queued);
        Assert.Equal(pendingBytes, EncounterActionCodecV1.Encode(restored.Actions!));
        Assert.Equal(EncounterLifecycleCodecV1.Encode(Complete(queued, timing)),
            EncounterLifecycleCodecV1.Encode(Complete(restored, timing)));
        var outcome = Restore(Complete(restored, timing));
        Assert.Equal(EncounterLifecyclePhase.Outcome, outcome.Phase);
        Assert.Equal(100UL, outcome.GetDebt(Id(10)).RemainingDelay);
        byte[] lastActions = EncounterActionCodecV1.Encode(outcome.Actions!);
        var released = Restore(outcome.Release());
        Assert.False(released.HasEncounter);
        Assert.Equal(EncounterLifecyclePhase.Exploration, released.Phase);
        Assert.Equal(outcome.Debts, released.Debts);
        Assert.Equal(lastActions, EncounterActionCodecV1.Encode(released.Actions!));
        Assert.Equal(outcome.ScenarioId, released.ScenarioId);
        Assert.Throws<ArgumentException>(() => released.Begin(Id(1_000), Id(501)));

        var second = Restore(released.Begin(Id(1_001), Id(501)).AdvanceStaging().AdvanceStaging());
        Assert.Equal(2UL, second.EncounterCount);
        Assert.Equal(Id(501), second.ScenarioId);
        Assert.Equal(outcome.Debts, second.Debts);
        Assert.Equal(released.TacticalTicks, second.EntryTacticalTicks);
        Assert.Equal(0UL, second.Actions!.Random.NextCounter);
        Assert.Null(second.Actions.LastResolution);
        Assert.Empty(second.Actions.Pending);
        Assert.Equal(0, second.Actions.GetCombatant(Id(20)).Side);
        Assert.Equal(2, second.Actions.GetCombatant(Id(30)).Side);
        Assert.False(Timing(second).GetActor(Id(10)).IsDue);
        Assert.Throws<ArgumentException>(() => Timing(second).QueueDecision(0,
            new(Id(10), Id(900), EncounterDecisionKind.Wait, second.ContentHash)));

        var held = Restore(released.RetainBoundary());
        Assert.Equal(released.Debts, held.Debts);
        Assert.Equal(released.ExplorationSteps, held.ExplorationSteps);
        Assert.Equal(released.TacticalTicks, held.TacticalTicks);
        var recovered = Restore(held.AdvanceExploration());
        Assert.Equal(1UL, recovered.ExplorationSteps);
        Assert.Equal(0UL, recovered.GetDebt(Id(10)).RemainingDelay);
        Assert.Equal(lastActions, EncounterActionCodecV1.Encode(recovered.Actions!));
        Assert.Equal(recovered.Debts, recovered.Begin(Id(1_002), Id(501)).Debts);
    }

    [Fact]
    public void Cumulative_ticks_preserve_fractional_exposure_and_do_not_reset_at_release_or_reentry()
    {
        // A retained component state, not a fabricated accepted world history. The world independently
        // authenticates these totals against pacing and its journal; this test checks exact composition.
        var outcome = Copy(Outcome(), revision: 100, exploration: 2, tactical: 35);
        var released = Restore(outcome.Release());
        UInt128 exposure = (UInt128)released.ExplorationSteps * 1_800 + (UInt128)released.TacticalTicks * 50;
        Assert.Equal((UInt128)5_350, exposure);
        Assert.Equal((UInt128)1_750, exposure % 1_800);
        var reentered = Restore(released.Begin(Id(1_001), Id(501)).AdvanceStaging().AdvanceStaging());
        Assert.Equal(35UL, reentered.EntryTacticalTicks);
        Assert.Equal(35UL, reentered.TacticalTicks);
        Assert.Equal(0UL, Timing(reentered).ElapsedTacticalUnits);
        var timing = new EncounterTimingSnapshot(reentered.Actions!.Scope, 1, 5,
            reentered.Debts.Select(debt => new EncounterActorTiming(debt.ActorId, 100,
                debt.RemainingDelay > 5 ? debt.RemainingDelay - 5 : 0,
                debt.ActorId == Id(10) ? EncounterActorControl.RequiredHuman : EncounterActorControl.Automatic)), []);
        var ticked = Restore(reentered.ObserveActive(timing, reentered.Actions!, tacticalTick: true, isOutcome: false));
        Assert.Equal(36UL, ticked.TacticalTicks);
        Assert.Equal(95UL, ticked.GetDebt(Id(10)).RemainingDelay);
        UInt128 nextExposure = (UInt128)ticked.ExplorationSteps * 1_800 + (UInt128)ticked.TacticalTicks * 50;
        Assert.Equal(exposure + 50, nextExposure);
        Assert.Equal((UInt128)0, nextExposure % 1_800);
        Assert.Equal((UInt128)3, nextExposure / 1_800);
        Assert.Throws<ArgumentException>(() => reentered.ObserveActive(timing, reentered.Actions!, tacticalTick: false, isOutcome: false));
        Assert.Throws<ArgumentException>(() => ticked.ObserveActive(timing, ticked.Actions!, tacticalTick: true, isOutcome: false));
    }

    [Fact]
    public void Only_exploration_pays_bounded_debt_and_counter_exhaustion_never_wraps()
    {
        var restored = Copy(Fresh(), debts: [new(Id(10), ulong.MaxValue), new(Id(20), 181), new(Id(30), 180), new(Id(40), 1)]);
        var held = restored.RetainBoundary();
        Assert.Equal(restored.Debts, held.Debts);
        var next = Restore(held.AdvanceExploration());
        Assert.Equal(ulong.MaxValue - 180, next.GetDebt(Id(10)).RemainingDelay);
        Assert.Equal(1UL, next.GetDebt(Id(20)).RemainingDelay);
        Assert.Equal(0UL, next.GetDebt(Id(30)).RemainingDelay);
        Assert.Equal(0UL, next.GetDebt(Id(40)).RemainingDelay);
        var confrontation = next.Begin(Id(1_000), Id(500));
        Assert.Equal(next.Debts, confrontation.AdvanceStaging().AdvanceStaging().Debts);
        Assert.Equal(confrontation.Debts, confrontation.RetainBoundary().Debts);
        Assert.Throws<InvalidOperationException>(() => confrontation.AdvanceExploration());
        Assert.Throws<InvalidOperationException>(() => confrontation.Release());
        Assert.Throws<InvalidOperationException>(() => next.AdvanceStaging());
        Assert.Throws<InvalidOperationException>(() => Active().Begin(Id(1_001), Id(501)));
        Assert.Throws<InvalidOperationException>(() => Outcome().ObserveActive(Timing(Outcome()), Outcome().Actions!, false, true));
        var exhausted = Copy(Fresh(), revision: ulong.MaxValue, exploration: ulong.MaxValue);
        Assert.Equal(EncounterLifecycleCodecV1.Encode(exhausted), EncounterLifecycleCodecV1.Encode(Restore(exhausted)));
        Assert.Throws<OverflowException>(() => exhausted.RetainBoundary());
        Assert.Throws<OverflowException>(() => exhausted.AdvanceExploration());
        Assert.Throws<OverflowException>(() => exhausted.Begin(Id(1_000), Id(500)));
        Assert.Throws<ArgumentException>(() => Copy(Fresh(), revision: 0, exploration: 1));
        Assert.Throws<ArgumentException>(() => Copy(Active(), revision: ulong.MaxValue, tactical: ulong.MaxValue));
        Assert.Throws<ArgumentException>(() => Copy(Active(), tactical: 1, entry: 2));
        Assert.Throws<ArgumentException>(() => Copy(Fresh(), revision: 1, tactical: 1));
    }

    [Fact]
    public void Observation_requires_complete_exact_scope_catalogs_and_actor_ledger()
    {
        var state = Active();
        var timing = Timing(state);
        var foreign = new EncounterTimingSnapshot(new(World, Branch, Id(1_001)), 0, 0, timing.Actors, []);
        Assert.Throws<ArgumentException>(() => state.ObserveActive(foreign, state.Actions!, false, false));
        var missing = new EncounterTimingSnapshot(timing.Scope, 0, 0, timing.Actors.Skip(1), []);
        Assert.Throws<ArgumentException>(() => state.ObserveActive(missing, state.Actions!, false, false));
        var changedAbility = EncounterActionFixture.Ability(power: 11);
        var swapped = EncounterActionSnapshot.Begin(timing.Scope, Seed, [changedAbility, Abilities[1]], state.Actions!.Combatants);
        Assert.Throws<ArgumentException>(() => state.ObserveActive(timing, swapped, false, false));
        var wrongScenario = EncounterActionSnapshot.Begin(timing.Scope, Seed, Abilities, Scenario(501, 30).Combatants);
        Assert.Throws<ArgumentException>(() => state.ObserveActive(timing, wrongScenario, false, false));
        var wrongSeed = EncounterActionSnapshot.Begin(timing.Scope, Id(99), Abilities, state.Actions.Combatants);
        Assert.Throws<ArgumentException>(() => state.ObserveActive(timing, wrongSeed, false, false));
        var (queued, queuedTiming) = Queued();
        Assert.Throws<ArgumentException>(() => queued.ObserveActive(queuedTiming, queued.Actions!, false, true));
        Assert.Equal(EncounterLifecycleCodecV1.Encode(state), EncounterLifecycleCodecV1.Encode(Restore(state)));
    }
}
