using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.PartyWithdrawal;

public sealed class PartyWithdrawalOutcomeTests
{
    [Fact]
    public void A_blocked_sample_reopens_with_paid_debt_and_the_arrived_ally_still_required()
    {
        using var journal = new PartyWithdrawalRecoveryTests.WithdrawalJournal();
        var run = journal.Run;
        PartyWithdrawalWorldTests.QueueConvergingRoutes(run);
        journal.Flush();
        bool blocked = false;
        for (int step = 0; !blocked; step++)
        {
            Assert.True(step < 1200);
            run.ChooseDue(attacks: false);
            var before = run.State;
            run.ResolveOrTick();
            journal.Flush();
            if (run.State.Retreat!.Withdrawal!.Last is not { } last || last.WorldSequence != run.State.Revision) continue;
            var ended = Assert.Single(last.Ended);
            Assert.Equal(EncounterWithdrawalEndReason.Blocked, ended.Reason);
            Assert.Same(before.Space, run.State.Space);
            Assert.Equal(before.Calendar, run.State.Calendar);
            Assert.Equal(ended.Member.SampleReadiness, ended.FailedSampleCost);
            Assert.Equal(ended.Member.SampleReadiness, run.State.Pacing!.Encounter!.GetActor(ended.Member.ActorId).RemainingDelay);
            var party = Assert.Single(run.State.Retreat.Withdrawal.Parties);
            Assert.Equal(run.Humans.Order(), party.RequiredActors);
            Assert.True(Assert.Single(party.Members).HasArrived);
            Assert.False(party.AllArrived);
            journal.Reopen();
            blocked = true;
        }
        run.ChooseDue(attacks: false);
        run.ResolveOrTick();
        journal.Flush();
        Assert.Equal(EncounterLifecyclePhase.Active, run.State.EncounterLifecycle!.Phase);
        journal.Reopen();
    }

    [Fact]
    public void Genuine_defeat_interrupts_withdrawal_preserves_its_command_and_completes_as_the_opponents_outcome()
    {
        using var journal = new PartyWithdrawalRecoveryTests.WithdrawalJournal(humans: 1);
        var run = journal.Run;
        EntityId actor = Assert.Single(run.Humans);
        for (int step = 0; ; step++)
        {
            Assert.True(step < 1200);
            var world = run.State;
            var patient = world.Progression!.GetActor(actor);
            Assert.True(patient.CurrentHp > 0);
            uint hit = WorldEncounterRetreats.LivingOpponents(world, actor)
                .SelectMany(opponent => opponent.AbilityIds.Select(id =>
                    (Opponent: opponent.ActorId, Ability: world.EncounterActions!.GetAbility(id))))
                .Where(value => WorldEncounterActions.EvaluateAbility(world, value.Opponent,
                    new QueueEncounterAbilityIntent(world.Pacing!.Encounter!.EncounterId, world.Pacing.Encounter.Revision,
                        value.Ability.DefinitionId, actor)) == WorldActionRejection.None)
                .Select(value => EncounterActionRules.ComputePhysicalDamage(world.Progression.GetActor(value.Opponent).Stats.Attack,
                    patient.Stats.Defense, value.Ability.PowerTenths, 900)).DefaultIfEmpty(0U).Max();
            if (hit > 0 && patient.CurrentHp <= hit * 2
                && WorldEncounterActions.DueActors(world).Any(value => value.ActorId == actor)
                && world.Pacing!.Encounter!.Decisions.All(value => value.ActorId != actor)) break;
            run.ChooseDue();
            run.ResolveOrTick();
            journal.Flush();
        }
        var command = run.Command(actor, ContactPreservingRoute(run, actor));
        run.Choose(command);
        journal.Flush();
        journal.Reopen();
        for (int step = 0; run.State.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Active; step++)
        {
            Assert.True(step < 1200);
            run.ChooseDue();
            run.ResolveOrTick();
            journal.Flush();
        }
        var defeated = run.State;
        Assert.Equal(EncounterLifecyclePhase.Outcome, defeated.EncounterLifecycle!.Phase);
        Assert.Equal(0, defeated.Progression!.GetActor(actor).CurrentHp);
        Assert.Empty(defeated.Retreat!.Withdrawal!.Parties);
        var ended = Assert.Single(defeated.Retreat.Withdrawal.Last!.Ended);
        Assert.Equal(command.ContentHash, ended.Member.Command.ContentHash);
        Assert.Equal(EncounterWithdrawalEndReason.Incapacitated, ended.Reason);
        Assert.Equal(0UL, ended.FailedSampleCost);
        Assert.Null(defeated.EncounterOutcome!.Last);
        var outcome = WorldEncounterOutcomes.Determine(defeated)!;
        Assert.NotEqual(defeated.EncounterActions!.GetCombatant(actor).Side, outcome.StandingSide);
        Assert.Equal(new[] { actor }, outcome.RequiredHumans);
        journal.Reopen();
        run.Apply(WorldBatchKind.EncounterLifecycle, [run.Command(actor,
            new AcknowledgeEncounterOutcomeIntent(outcome.EncounterId, outcome.ActionHash))]);
        run.Apply(WorldBatchKind.EncounterLifecycle);
        journal.Flush();
        Assert.Equal(EncounterLifecyclePhase.Exploration, run.State.EncounterLifecycle!.Phase);
        Assert.Equal(outcome.StandingSide, run.State.EncounterOutcome!.Last!.StandingSide);
        Assert.Equal(defeated.Calendar, run.State.Calendar);
        Assert.Equal(defeated.Progression.ContentHash, run.State.Progression!.ContentHash);
        Assert.Equal(command.ContentHash, Assert.Single(run.State.Retreat!.Withdrawal!.Last!.Ended).Member.Command.ContentHash);
        journal.Reopen();
    }

    [Fact]
    public void One_side_departing_explicitly_ends_the_other_sides_route_without_a_victory_or_lost_debt()
    {
        using var journal = new PartyWithdrawalRecoveryTests.WithdrawalJournal(humans: 1);
        var run = journal.Run;
        var commands = run.State.EncounterActions!.Combatants.Where(actor => actor.Side > 0)
            .Select(actor => run.Retreat(actor.ActorId)).ToArray();
        Assert.Equal(2, commands.Length);
        run.Choose(commands);
        Assert.Equal(2, run.State.Retreat!.Withdrawal!.Parties.Count);
        journal.Flush();
        journal.Reopen();
        for (int step = 0; run.State.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Active; step++)
        {
            Assert.True(step < 1200);
            run.ChooseDue(attacks: false);
            run.ResolveOrTick();
            journal.Flush();
        }
        Assert.Equal(EncounterLifecyclePhase.Exploration, run.State.EncounterLifecycle!.Phase);
        Assert.Null(run.State.EncounterOutcome!.Last);
        Assert.Empty(run.State.Retreat!.Withdrawal!.Parties);
        var ended = run.State.Retreat.Withdrawal.Last!.Ended;
        Assert.Equal(commands.Select(value => value.CommandId).Order(), ended.Select(value => value.Member.Command.CommandId).Order());
        Assert.Single(ended, value => value.Reason == EncounterWithdrawalEndReason.Disengaged);
        Assert.Single(ended, value => value.Reason == EncounterWithdrawalEndReason.EncounterEnded);
        journal.Reopen();
    }

    private static QueueEncounterRetreatIntent ContactPreservingRoute(WithdrawalWorldFixture run, EntityId actor)
    {
        var world = run.State;
        var advice = WorldEncounterRetreatAdvisor.Advise(world, actor);
        Assert.True(advice.IsFound);
        foreach (var (outward, back) in new[]
        {
            (EncounterRetreatDirection.North, EncounterRetreatDirection.South),
            (EncounterRetreatDirection.East, EncounterRetreatDirection.West),
            (EncounterRetreatDirection.South, EncounterRetreatDirection.North),
            (EncounterRetreatDirection.West, EncounterRetreatDirection.East)
        })
        {
            int loops = (EncounterRetreatRules.MaximumRouteSamples - advice.Route.Count) / 2;
            var route = Enumerable.Range(0, loops).SelectMany(_ => new[] { outward, back }).Concat(advice.Route);
            var candidate = new QueueEncounterRetreatIntent(world.Pacing!.Encounter!.EncounterId,
                world.Pacing.Encounter.Revision, advice.Destination, route);
            if (WorldEncounterRetreats.EvaluateQueue(world, actor, candidate) == WorldActionRejection.None) return candidate;
        }
        throw new InvalidOperationException("A lawful long route must keep the actual wounded actor exposed to opposing attacks.");
    }
}
