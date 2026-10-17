using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.PartyWithdrawal;

public sealed class PartyWithdrawalWorldTests
{
    [Fact]
    public void Revoked_delegated_route_finishes_its_paid_obligation_and_owner_returns_at_arrival_before_ally()
    {
        var run = WithdrawalWorldFixture.Active();
        var order = WorldEncounterActions.DueActors(run.State).Select(actor => actor.ActorId).ToList();
        var owner = run.Humans.OrderBy(actor => WorldEncounterRetreatAdvisor.Advise(run.State, actor).Route.Count)
            .ThenBy(actor => order.IndexOf(actor)).First();
        var grantee = run.Humans.Single(actor => actor != owner);
        var offer = run.Command(owner, new OfferControlPermissionIntent(grantee, 0, WorldControlCapabilities.CombatAndSurvival));
        Assert.Equal(WorldActionRejection.None, Assert.Single(run.Apply(WorldBatchKind.ControlPermission, [offer]).Receipts).Rejection);
        var offered = run.State.ControlPermissions!.Find(owner, grantee)!;
        var accept = run.Command(grantee, new AcceptControlPermissionIntent(owner, offered.Revision, offer.CommandId, offered.TermsHash!));
        Assert.Equal(WorldActionRejection.None, Assert.Single(run.Apply(WorldBatchKind.ControlPermission, [accept]).Receipts).Rejection);
        run.ControllerEvent(owner, WorldControllerEventKind.Disconnected);
        // Core input to the explicit expiry boundary; real elapsed Host allowance is a separate integration proof.
        run.ControllerEvent(owner, WorldControllerEventKind.AllowanceExpired);
        var state = run.State;
        var grant = state.ControlPermissions!.Find(owner, grantee)!;
        var controller = state.Controllers!.Get(owner);
        var witness = new WorldControlWitness(owner, grantee, grant.GrantCommandId!.Value, grant.AcceptanceCommandId!.Value,
            grant.Revision, grant.Terms!.Capabilities, grant.TermsHash!, run.NextId(), 1, state.Revision + 1,
            controller.DisconnectId!.Value, controller.Revision);
        run.Apply(WorldBatchKind.ControlAssignment, assignmentEvent: new(owner, WorldControlAssignmentEventKind.Acquire, 0, witness));
        var delivery = run.Delivery(grantee);
        var choice = run.Retreat(owner);
        var delegated = new WorldActionCommand(choice.CommandId, delivery.PrincipalId, delivery.SessionId, owner,
            choice.WorldId, choice.BranchId, choice.ExpectedActorRevision, choice.Intent, witness);
        run.Choose(delegated, run.Retreat(grantee));
        var revoke = run.Command(owner, new RevokeControlPermissionIntent(grantee, grant.Revision));
        Assert.Equal(WorldActionRejection.None, Assert.Single(run.Apply(WorldBatchKind.ControlPermission, [revoke]).Receipts).Rejection);
        Assert.Null(WorldActorCommandAuthority.AuthorizeSelection(run.State, delegated));
        Assert.False(WorldControllerBoundaries.CanRestoreHuman(run.State, owner));
        Assert.Throws<InvalidOperationException>(() => run.ControllerEvent(owner, WorldControllerEventKind.Reconnected));
        run.Restore();
        for (int step = 0; !run.State.Retreat!.Withdrawal!.FindMember(owner)!.HasArrived; step++)
        {
            Assert.True(step < 1200);
            run.ChooseDue(attacks: false);
            run.ResolveOrTick();
        }
        Assert.Equal(EncounterLifecyclePhase.Active, run.State.EncounterLifecycle!.Phase);
        Assert.False(run.State.Retreat!.Withdrawal!.FindMember(grantee)!.HasArrived);
        Assert.True(WorldControllerBoundaries.CanRestoreHuman(run.State, owner));
        var arrival = run.State;
        run.ControllerEvent(owner, WorldControllerEventKind.Reconnected);
        Assert.Equal(WorldControllerMode.Human, run.State.Controllers!.Get(owner).Mode);
        Assert.Equal(arrival.Pacing!.Progress.Capture(), run.State.Pacing!.Progress.Capture());
        Assert.Equal(arrival.Pacing.Encounter!.GetActor(owner).RemainingDelay, run.State.Pacing.Encounter!.GetActor(owner).RemainingDelay);
        Assert.Equal(delegated.ContentHash, run.State.Retreat.Withdrawal.FindMember(owner)!.Command.ContentHash);
        Assert.Equal(delivery.PrincipalId, run.State.Retreat.Withdrawal.FindMember(owner)!.Command.PrincipalId);
        var cancel = run.Command(owner, new CancelEncounterWithdrawalIntent(run.State.Pacing.Encounter.EncounterId,
            run.State.Pacing.Encounter.Revision, delegated.CommandId));
        var forbidden = new WorldActionCommand(run.NextId(), delivery.PrincipalId, delivery.SessionId, owner,
            cancel.WorldId, cancel.BranchId, cancel.ExpectedActorRevision, cancel.Intent, witness);
        Assert.Equal(WorldActionRejection.NotAuthorized,
            Assert.Single(run.Apply(WorldBatchKind.EncounterDecision, [forbidden]).Receipts).Rejection);
        run.Choose(cancel);
        Assert.Null(run.State.Retreat.Withdrawal.FindMember(owner));
        Assert.Equal(delegated.ContentHash, Assert.Single(run.State.Retreat.Withdrawal.Last!.Ended).Member.Command.ContentHash);
        run.Restore();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    public void Complete_side_withdrawal_preserves_all_people_and_debt_for_supported_human_counts(int humanCount)
    {
        var run = WithdrawalWorldFixture.Active(humanCount);
        var original = run.State;
        int choices = 0;
        for (int step = 0; run.State.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Active; step++)
        {
            Assert.True(step < 1200, "The whole side must depart through bounded ordinary tactical play.");
            foreach (var actor in WorldEncounterActions.DueActors(run.State).Where(actor => run.Humans.Contains(actor.ActorId)).ToArray())
            {
                if (run.State.Retreat!.Withdrawal!.FindMember(actor.ActorId) is not null
                    || run.State.Pacing!.Encounter!.Decisions.Any(value => value.ActorId == actor.ActorId)) continue;
                Assert.True(++choices <= humanCount * 3, "Obstructed routes require bounded explicit new player choices.");
                run.Choose(run.Retreat(actor.ActorId));
                Assert.Equal(run.Humans.Order(), Assert.Single(run.State.Retreat.Withdrawal.Parties).RequiredActors);
            }
            run.ChooseDue(attacks: false);
            var before = run.State;
            var due = WorldEncounterActions.DueActors(before).FirstOrDefault();
            var member = due is null ? null : before.Retreat!.Withdrawal!.FindMember(due.ActorId);
            run.ResolveOrTick();
            if (run.State.EncounterLifecycle.Phase != EncounterLifecyclePhase.Exploration) continue;
            Assert.NotNull(member);
            Assert.Equal(before.Calendar, run.State.Calendar);
            Assert.Equal(before.Pacing!.Progress.Capture(), run.State.Pacing!.Progress.Capture());
            Assert.Equal(before.Progression!.ContentHash, run.State.Progression!.ContentHash);
            foreach (var timed in before.Pacing.Encounter!.Actors)
                Assert.Equal(timed.ActorId == member.ActorId ? member.SampleReadiness : timed.RemainingDelay,
                    run.State.EncounterLifecycle.GetDebt(timed.ActorId).RemainingDelay);
        }
        Assert.Equal(original.Space.Actors.Select(actor => actor.ActorId), run.State.Space.Actors.Select(actor => actor.ActorId));
        Assert.Equal(original.Inventory.ContentHash, run.State.Inventory.ContentHash);
        Assert.Equal(run.Humans.Order(), run.State.Retreat!.Withdrawal!.Last!.Ended.Select(end => end.Member.ActorId));
        Assert.All(run.State.Retreat.Withdrawal.Last.Ended, end => Assert.Equal(EncounterWithdrawalEndReason.Disengaged, end.Reason));
        Assert.Null(run.State.EncounterOutcome!.Last);
        run.Restore();
    }

    [Fact]
    public void Actual_incapacity_ends_the_choice_but_keeps_the_ally_required_and_the_encounter_active()
    {
        var run = WithdrawalWorldFixture.Active();
        var actor = run.Humans[0];
        // Reach a vulnerable state through actual opponent attacks, never by replacing the health owner.
        for (int step = 0; ; step++)
        {
            Assert.True(step < 1200);
            var world = run.State;
            var target = world.Progression!.GetActor(actor);
            Assert.True(target.CurrentHp > 0);
            uint minimumHit = WorldEncounterRetreats.LivingOpponents(world, actor)
                .SelectMany(opponent => opponent.AbilityIds.Select(id => (Opponent: opponent.ActorId, Ability: world.EncounterActions!.GetAbility(id))))
                .Where(value => WorldEncounterActions.EvaluateAbility(world, value.Opponent,
                    new QueueEncounterAbilityIntent(world.Pacing!.Encounter!.EncounterId, world.Pacing.Encounter.Revision,
                        value.Ability.DefinitionId, actor)) == WorldActionRejection.None)
                .Select(value => EncounterActionRules.ComputePhysicalDamage(world.Progression.GetActor(value.Opponent).Stats.Attack,
                    target.Stats.Defense, value.Ability.PowerTenths, 900)).DefaultIfEmpty(0U).Max();
            if (minimumHit > 0 && target.CurrentHp <= minimumHit * 2
                && WorldEncounterActions.DueActors(world).Any(value => value.ActorId == actor)
                && world.Pacing!.Encounter!.Decisions.All(value => value.ActorId != actor)) break;
            run.ChooseDue();
            run.ResolveOrTick();
        }
        var basis = run.State;
        var advice = WorldEncounterRetreatAdvisor.Advise(basis, actor);
        Assert.True(advice.IsFound);
        QueueEncounterRetreatIntent? route = null;
        foreach (var (outward, back) in new[]
        {
            (EncounterRetreatDirection.North, EncounterRetreatDirection.South),
            (EncounterRetreatDirection.East, EncounterRetreatDirection.West),
            (EncounterRetreatDirection.South, EncounterRetreatDirection.North),
            (EncounterRetreatDirection.West, EncounterRetreatDirection.East)
        })
        {
            int loops = (EncounterRetreatRules.MaximumRouteSamples - advice.Route.Count) / 2;
            var samples = Enumerable.Range(0, loops).SelectMany(_ => new[] { outward, back }).Concat(advice.Route);
            var candidate = new QueueEncounterRetreatIntent(basis.Pacing!.Encounter!.EncounterId,
                basis.Pacing.Encounter.Revision, advice.Destination, samples);
            if (WorldEncounterRetreats.EvaluateQueue(basis, actor, candidate) != WorldActionRejection.None) continue;
            route = candidate;
            break;
        }
        Assert.NotNull(route);
        var command = run.Command(actor, route);
        run.Choose(command);
        bool downed = false;
        for (int step = 0; step < 1200 && !downed; step++)
        {
            run.ChooseDue();
            var before = run.State;
            run.ResolveOrTick();
            if (run.State.Progression!.GetActor(actor).CurrentHp != 0) continue;
            var ended = Assert.Single(run.State.Retreat!.Withdrawal!.Last!.Ended);
            Assert.Equal(command.CommandId, ended.Member.Command.CommandId);
            Assert.Equal(EncounterWithdrawalEndReason.Incapacitated, ended.Reason);
            Assert.Equal(0UL, ended.FailedSampleCost);
            Assert.Equal(before.Space.GetActor(actor).Feet, run.State.Space.GetActor(actor).Feet);
            Assert.Equal(before.Pacing!.Encounter!.GetActor(actor).RemainingDelay,
                run.State.Pacing!.Encounter!.GetActor(actor).RemainingDelay);
            var party = Assert.Single(run.State.Retreat.Withdrawal.Parties);
            Assert.Equal(run.Humans.Order(), party.RequiredActors);
            Assert.Empty(party.Members);
            Assert.Equal(EncounterLifecyclePhase.Active, run.State.EncounterLifecycle!.Phase);
            run.Restore();
            run.ChooseDue(attacks: false);
            run.ResolveOrTick();
            Assert.Contains(actor, Assert.Single(run.State.Retreat.Withdrawal.Parties).RequiredActors);
            downed = true;
        }
        Assert.True(downed, "A real opponent must interrupt the retained route through actual damage.");
    }

    [Fact]
    public void Two_cancellations_in_one_boundary_retain_both_commands_and_the_complete_roster()
    {
        var run = WithdrawalWorldFixture.Active();
        var routes = run.Humans.Select(run.Retreat).ToArray();
        run.Choose(routes);
        var before = run.State;
        var cancels = routes.Select(route => run.Command(route.ActorId, new CancelEncounterWithdrawalIntent(
            before.Pacing!.Encounter!.EncounterId, before.Pacing.Encounter.Revision, route.CommandId))).ToArray();
        run.Choose(cancels);
        var book = Assert.Single(run.State.Retreat!.Withdrawal!.Parties);
        Assert.Empty(book.Members);
        Assert.Equal(run.Humans.Order(), book.RequiredActors);
        Assert.Equal(routes.Select(route => route.CommandId).Order(), run.State.Retreat.Withdrawal.Last!.Ended
            .Select(end => end.Member.Command.CommandId).Order());
        Assert.All(run.State.Retreat.Withdrawal.Last.Ended, end => Assert.Equal(0UL, end.ReadinessCharged));
        Assert.Equal(before.Pacing!.Encounter!.Actors, run.State.Pacing!.Encounter!.Actors);
        Assert.Equal(before.Calendar, run.State.Calendar);
        Assert.True(WorldEncounterActions.HasRequiredDecisions(run.State));
        Assert.All(routes, route => Assert.True(run.State.IdentityIndex.Contains(route.CommandId)));
        run.Restore();
        Assert.Equal(2, run.State.Retreat!.Withdrawal!.Last!.Ended.Count);
        run.ChooseDue(attacks: false);
        run.ResolveOrTick();
        Assert.Equal(EncounterActionResolutionStatus.Wait, run.State.EncounterActions!.LastResolution!.Status);
    }

    [Fact]
    public void A_newly_occupied_route_interrupts_only_its_actor_and_charges_the_blocked_attempt()
    {
        var run = WithdrawalWorldFixture.Active();
        QueueConvergingRoutes(run);
        bool blocked = false;
        for (int step = 0; step < 1200 && !blocked; step++)
        {
            Assert.Equal(EncounterLifecyclePhase.Active, run.State.EncounterLifecycle!.Phase);
            run.ChooseDue(attacks: false);
            var before = run.State;
            run.ResolveOrTick();
            if (run.State.Retreat!.Withdrawal!.Last is not { } last || last.WorldSequence != run.State.Revision) continue;
            var end = Assert.Single(last.Ended);
            Assert.Equal(EncounterWithdrawalEndReason.Blocked, end.Reason);
            Assert.Same(before.Space, run.State.Space);
            Assert.Equal(end.Member.SampleReadiness, end.FailedSampleCost);
            Assert.Equal(end.Member.SampleReadiness, run.State.Pacing!.Encounter!.GetActor(end.Member.ActorId).RemainingDelay);
            var party = Assert.Single(run.State.Retreat.Withdrawal.Parties);
            Assert.Equal(run.Humans.Order(), party.RequiredActors);
            Assert.True(Assert.Single(party.Members).HasArrived);
            Assert.Null(run.State.Retreat.Withdrawal.FindMember(end.Member.ActorId));
            Assert.False(party.AllArrived);
            Assert.Equal(before.Calendar, run.State.Calendar);
            run.Restore();
            blocked = true;
        }
        Assert.True(blocked, "The later actor must meet the earlier arrival's actual body, not swap or teleport through it.");
    }

    internal static void QueueConvergingRoutes(WithdrawalWorldFixture run)
    {
        var state = run.State;
        var destinations = (from actor in run.Humans
            let advised = WorldEncounterRetreatAdvisor.Advise(state, actor).Destination
            from dx in new[] { 0L, -32L, 32L, -64L, 64L }
            from dy in new[] { 0L, -32L, 32L, -64L, 64L }
            select advised.Offset(dx, dy, 0)).Distinct();
        QueueEncounterRetreatIntent[]? selected = null;
        foreach (var destination in destinations)
        {
            var choices = run.Humans.Select(actor =>
            {
                var feet = state.Space.GetActor(actor).Feet;
                // Each actor retains its actual movement lattice. Nearby destinations collide physically
                // without assuming that separately staged actors can name an identical lattice point.
                long Snap(long delta) => (delta >= 0 ? (delta + 16) / 32 : (delta - 16) / 32) * 32;
                var target = feet.Offset(Snap(destination.X - feet.X), Snap(destination.Y - feet.Y), 0);
                return GroundRoutes(state, actor, target).SelectMany(WithDetours).Select(route => new QueueEncounterRetreatIntent(
                    state.Pacing!.Encounter!.EncounterId, state.Pacing.Encounter.Revision, target, route)).ToArray();
            }).ToArray();
            selected = (from first in choices[0] from second in choices[1]
                where first.Route.Count != second.Route.Count
                let routes = new[] { first, second }
                where MeetsArrivedBody(state, run.Humans, routes)
                where WorldEncounterRetreats.EvaluateQueue(state, run.Humans[0], first) == WorldActionRejection.None
                    && WorldEncounterRetreats.EvaluateQueue(state, run.Humans[1], second) == WorldActionRejection.None
                select routes).FirstOrDefault();
            if (selected is not null)
            {
                break;
            }
        }
        Assert.NotNull(selected);
        run.Choose(run.Humans.Select((actor, index) => run.Command(actor, selected[index])).ToArray());
    }

    private static bool MeetsArrivedBody(WorldSnapshot world, EntityId[] actors, QueueEncounterRetreatIntent[] routes)
    {
        // A fixture-only geometric preflight selects the desired counterexample. Actual assertions
        // below still use paid world transactions and the timing owner, never these diagnostic poses.
        Assert.Single(actors.Select(actor => world.Pacing!.Encounter!.GetActor(actor).NormalizedSpeed).Distinct());
        var order = WorldEncounterActions.DueActors(world).Where(actor => actors.Contains(actor.ActorId))
            .Select(actor => Array.IndexOf(actors, actor.ActorId)).ToArray();
        var space = world.Space;
        int[] walked = [0, 0];
        for (int round = 0; round < 32; round++)
        {
            foreach (int index in order)
            {
                if (walked[index] == routes[index].Route.Count) continue;
                var next = WorldEncounterRetreats.WalkSample(space, actors[index], routes[index].Route[walked[index]], out _);
                if (next is null) return walked[1 - index] == routes[1 - index].Route.Count;
                space = next;
                walked[index]++;
            }
        }
        return false;
    }

    private static IEnumerable<EncounterRetreatDirection[]> CardinalRoutes(SubunitPosition from, SubunitPosition to)
    {
        long dx = to.X - from.X, dy = to.Y - from.Y;
        if (to.Z != from.Z || dx % 32 != 0 || dy % 32 != 0 || (Math.Abs(dx) + Math.Abs(dy)) / 32 is < 1 or > 32) yield break;
        var horizontal = Enumerable.Repeat(dx < 0 ? EncounterRetreatDirection.West : EncounterRetreatDirection.East,
            checked((int)Math.Abs(dx / 32))).ToArray();
        var vertical = Enumerable.Repeat(dy < 0 ? EncounterRetreatDirection.North : EncounterRetreatDirection.South,
            checked((int)Math.Abs(dy / 32))).ToArray();
        yield return [.. horizontal, .. vertical];
        yield return [.. vertical, .. horizontal];
    }

    private static IEnumerable<EncounterRetreatDirection[]> WithDetours(EncounterRetreatDirection[] route)
    {
        yield return route;
        // Distinct paid travel times let one actor arrive before the other meets its body.
        // Every candidate still passes ordinary queue admission and real timed execution.
        foreach (int distance in new[] { 2, 4, 8 })
        {
            if (route.Length + 2 * distance > EncounterRetreatRules.MaximumRouteSamples) continue;
            foreach (var (outward, back) in new[]
            {
                (EncounterRetreatDirection.North, EncounterRetreatDirection.South),
                (EncounterRetreatDirection.East, EncounterRetreatDirection.West),
                (EncounterRetreatDirection.South, EncounterRetreatDirection.North),
                (EncounterRetreatDirection.West, EncounterRetreatDirection.East)
            })
                yield return [.. Enumerable.Repeat(outward, distance), .. Enumerable.Repeat(back, distance), .. route];
        }
    }

    private static IEnumerable<EncounterRetreatDirection[]> GroundRoutes(WorldSnapshot world, EntityId actor, SubunitPosition target)
    {
        var origin = world.Space.GetActor(actor).Feet;
        foreach (var route in CardinalRoutes(origin, target)) yield return route;
        Queue<LocalSpatialSnapshot> open = new([world.Space]);
        Dictionary<SubunitPosition, (SubunitPosition Parent, EncounterRetreatDirection Direction, int Depth)> previous = [];
        HashSet<SubunitPosition> visited = [origin];
        while (open.Count != 0 && visited.Count <= 4096)
        {
            var space = open.Dequeue();
            var from = space.GetActor(actor).Feet;
            int depth = from == origin ? 0 : previous[from].Depth;
            if (depth == 32) continue;
            foreach (var direction in new[] { EncounterRetreatDirection.North, EncounterRetreatDirection.East,
                EncounterRetreatDirection.South, EncounterRetreatDirection.West })
            {
                var delta = QueueEncounterRetreatIntent.Displacement(direction);
                var to = from.Offset(delta.X, delta.Y, 0);
                if (visited.Contains(to)) continue;
                var moved = WorldEncounterRetreats.WalkSample(space, actor, direction, out _);
                if (moved is null) continue;
                visited.Add(to);
                previous.Add(to, (from, direction, depth + 1));
                if (to == target)
                {
                    List<EncounterRetreatDirection> route = [];
                    for (var cursor = to; cursor != origin; cursor = previous[cursor].Parent) route.Add(previous[cursor].Direction);
                    route.Reverse();
                    yield return route.ToArray();
                    yield break;
                }
                open.Enqueue(moved);
            }
        }
    }

    [Fact]
    public void Explicit_profile_keeps_legacy_bytes_and_refuses_lived_fresh_admission_and_foreign_codecs()
    {
        var old = WithdrawalWorldFixture.Legacy;
        byte[] before = WorldSnapshotCodecV40.Encode(old);
        var fresh = WorldPartyWithdrawalAdmission.CreateFresh(old);
        Assert.Equal(41, fresh.ProfileVersion);
        Assert.Equal(40, old.ProfileVersion);
        Assert.Equal(before, WorldSnapshotCodecV40.Encode(old));
        byte[] bytes = WorldSnapshotCodec.Encode(fresh);
        Assert.Equal(bytes, WorldSnapshotCodecV41.Encode(fresh));
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));
        Assert.Throws<FormatException>(() => WorldSnapshotCodecV40.Decode(bytes));
        Assert.Throws<FormatException>(() => WorldSnapshotCodecV41.Decode(before));
        Assert.Throws<ArgumentException>(() => EncounterRetreatCodecV1.Encode(fresh.Retreat!));
        Assert.Throws<FormatException>(() => EncounterRetreatCodecV2.Decode(EncounterRetreatCodecV1.Encode(old.Retreat!)));
        Assert.Throws<ArgumentException>(() => WorldPartyWithdrawalAdmission.CreateFresh(WithdrawalWorldFixture.Active().State));
    }

    [Fact]
    public void One_choice_cannot_move_an_undecided_ally_or_bypass_full_wait()
    {
        var run = WithdrawalWorldFixture.Active();
        var actor = run.Humans[0];
        var ally = run.Humans[1];
        var feet = run.State.Space.GetActor(ally).Feet;
        run.Choose(run.Retreat(actor));
        var book = Assert.Single(run.State.Retreat!.Withdrawal!.Parties);
        Assert.Equal(run.Humans.Order(), book.RequiredActors);
        Assert.Single(book.Members);
        Assert.True(WorldEncounterActions.HasRequiredDecisions(run.State));
        Assert.Throws<InvalidOperationException>(() => run.Apply(WorldBatchKind.EncounterResolution));
        Assert.Throws<InvalidOperationException>(() => run.Apply(WorldBatchKind.TacticalAdvance));
        Assert.Equal(feet, run.State.Space.GetActor(ally).Feet);
        run.Restore();
        Assert.True(WorldEncounterActions.HasRequiredDecisions(run.State));
    }

    [Fact]
    public void Independent_routes_restore_at_every_boundary_and_depart_only_with_the_entire_side()
    {
        var run = WithdrawalWorldFixture.Active();
        var before = run.State;
        var routes = run.Humans.Select(run.Retreat).ToArray();
        run.Choose(routes);
        Assert.Equal(2, Assert.Single(run.State.Retreat!.Withdrawal!.Parties).Members.Count);
        bool observedPartialArrival = false, observedAttack = false;
        int boundaries = 0;
        while (run.State.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Active)
        {
            Assert.True(boundaries++ < 1200, "The accepted physical routes must finish within a finite tactical journey.");
            var group = Assert.Single(run.State.Retreat!.Withdrawal!.Parties);
            if (group.Members.Any(member => member.HasArrived) && !group.AllArrived)
            {
                observedPartialArrival = true;
                Assert.All(group.Members.Where(member => member.HasArrived), member =>
                    Assert.False(WorldEncounterRetreats.HoldsActor(run.State, member.ActorId)));
            }
            run.ChooseDue();
            var spatial = run.State.Space;
            var timing = run.State.Pacing!.Encounter!;
            var first = WorldEncounterActions.DueActors(run.State).FirstOrDefault();
            var walker = first is null ? null : run.State.Retreat.Withdrawal.FindMember(first.ActorId);
            run.ResolveOrTick();
            if (walker is { HasArrived: false })
            {
                Assert.Equal(walker.NextFeet, run.State.Space.GetActor(walker.ActorId).Feet);
                Assert.All(spatial.Actors.Where(actor => actor.ActorId != walker.ActorId), actor =>
                    Assert.Equal(actor.Feet, run.State.Space.GetActor(actor.ActorId).Feet));
                if (run.State.Pacing.Encounter is { } paid)
                    Assert.Equal(walker.SampleReadiness, paid.GetActor(walker.ActorId).RemainingDelay);
            }
            if (run.State.EncounterActions?.LastResolution is { Status: EncounterActionResolutionStatus.Damaged }) observedAttack = true;
            run.Restore();
        }
        Assert.Equal(EncounterLifecyclePhase.Exploration, run.State.EncounterLifecycle!.Phase);
        Assert.True(observedPartialArrival, "An early arrival must remain an active participant while its ally walks.");
        Assert.True(observedAttack, "An actual opposing attack must resolve during withdrawal.");
        var terminal = run.State.Retreat!.Withdrawal!.Last!;
        Assert.Equal(run.Humans.Order(), terminal.Ended.Select(end => end.Member.ActorId));
        Assert.All(terminal.Ended, end =>
        {
            Assert.Equal(EncounterWithdrawalEndReason.Disengaged, end.Reason);
            Assert.True(end.Member.HasArrived);
            Assert.Equal(end.Member.ExpectedFeet, run.State.Space.GetActor(end.Member.ActorId).Feet);
            Assert.True(WorldEncounterRetreats.IsClearOfContact(before, run.State.Space, end.Member.ActorId));
        });
        Assert.Empty(run.State.Retreat.Withdrawal.Parties);
        Assert.Equal(before.Inventory.ContentHash, run.State.Inventory.ContentHash);
        Assert.Equal(before.Coordination!.Sources.ContentHash, run.State.Coordination!.Sources.ContentHash);
        Assert.Null(run.State.Pacing!.Encounter);
        Assert.Null(run.State.EncounterOutcome!.Last);
        // Replay exactly the same accepted batches, including the ordinary approach, from original genesis.
        var replay = run.Genesis;
        HashSet<EntityId> occupied = [];
        foreach (var batch in run.Batches)
        {
            var decision = new WorldStepProcessor().Decide(replay, batch, occupied.Contains);
            occupied.UnionWith(decision.Identities.Select(value => value.Identity));
            replay = decision.Candidate;
        }
        Assert.Equal(WorldSnapshotCodec.Encode(run.State), WorldSnapshotCodec.Encode(replay));
    }

    [Fact]
    public void Cancellation_between_due_boundaries_preserves_paid_debt_position_and_required_membership()
    {
        var run = WithdrawalWorldFixture.Active();
        var actor = run.Humans[0];
        run.Choose(run.Retreat(actor), run.Wait(run.Humans[1]));
        for (int step = 0; run.State.Retreat!.Withdrawal!.FindMember(actor)!.SamplesWalked == 0; step++)
        {
            Assert.True(step < 16);
            run.ChooseDue(attacks: false);
            run.ResolveOrTick();
        }
        var before = run.State;
        var member = before.Retreat!.Withdrawal!.FindMember(actor)!;
        Assert.False(member.HasArrived);
        Assert.False(before.Pacing!.Encounter!.GetActor(actor).IsDue);
        var cancellation = run.Command(actor, new CancelEncounterWithdrawalIntent(before.Pacing.Encounter.EncounterId,
            before.Pacing.Encounter.Revision, member.Command.CommandId));
        run.Choose(cancellation);
        Assert.Same(before.Space, run.State.Space);
        Assert.Equal(before.Calendar, run.State.Calendar);
        Assert.Equal(before.AdvancingSteps, run.State.AdvancingSteps);
        Assert.Equal(before.Pacing.Encounter.GetActor(actor), run.State.Pacing!.Encounter!.GetActor(actor));
        Assert.Null(run.State.Retreat!.Withdrawal!.FindMember(actor));
        Assert.Contains(actor, Assert.Single(run.State.Retreat.Withdrawal.Parties).RequiredActors);
        Assert.Equal(EncounterWithdrawalEndReason.ChoiceChanged, Assert.Single(run.State.Retreat.Withdrawal.Last!.Ended).Reason);
        Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(run.Apply(WorldBatchKind.EncounterDecision, [run.Wait(actor)]).Receipts).Rejection);
        run.Restore();
    }

    [Fact]
    public void Foreign_or_stale_choice_and_cancel_never_gain_control()
    {
        var run = WithdrawalWorldFixture.Active();
        var actor = run.Humans[0];
        var command = run.Retreat(actor);
        var foreign = run.Delivery(run.Humans[1]);
        var impersonated = new WorldActionCommand(run.NextId(), foreign.PrincipalId, foreign.SessionId, actor,
            command.WorldId, command.BranchId, command.ExpectedActorRevision, command.Intent);
        Assert.Equal(WorldActionRejection.NotAuthorized,
            Assert.Single(run.Apply(WorldBatchKind.EncounterDecision, [impersonated]).Receipts).Rejection);
        run.Choose(command);
        var stale = run.Command(actor, new CancelEncounterWithdrawalIntent(run.State.Pacing!.Encounter!.EncounterId,
            run.State.Pacing.Encounter.Revision - 1, command.CommandId));
        Assert.Equal(WorldActionRejection.RevisionConflict,
            Assert.Single(run.Apply(WorldBatchKind.EncounterDecision, [stale]).Receipts).Rejection);
        Assert.Equal(command.ContentHash, run.State.Retreat!.Withdrawal!.FindMember(actor)!.Command.ContentHash);
        Assert.Throws<ArgumentException>(() => new WorldStepBatch(run.NextId(), run.State.WorldId, run.State.BranchId,
            run.State.Revision, run.State.AdvancingSteps, WorldBatchKind.EncounterDecision, [], [stale], 40));
    }

    [Fact]
    public void Complete_world_rejects_omitted_ally_or_mismatched_speed_and_progress()
    {
        var run = WithdrawalWorldFixture.Active();
        run.Choose(run.Retreat(run.Humans[0]));
        var world = run.State;
        var state = world.Retreat!.Withdrawal!;
        var party = Assert.Single(state.Parties);
        var member = Assert.Single(party.Members);
        void Reject(EncounterWithdrawalParty replacement)
        {
            var retreat = new EncounterRetreatState(world.Retreat.RulesPin, world.Retreat.AcceptedCount, null,
                world.Retreat.Last, new EncounterWithdrawalState(state.Revision, [replacement]));
            Assert.Throws<ArgumentException>(() => new WorldSnapshot(world.Inventory, world.Space, world.Residents,
                world.AdmissionVersion, world.Calendar, world.Revision, world.AdvancingSteps, world.Dialogues,
                world.Gathering, world.Production, world.Progression, world.Quests, world.Construction,
                reservoirs: world.Reservoirs, reservoirSeals: world.ReservoirSeals, liquidCells: world.LiquidCells,
                excavation: world.Excavation, pacing: world.Pacing, encounterLifecycle: world.EncounterLifecycle,
                care: world.Care, resourceHistory: world.ResourceHistory, coordination: world.Coordination,
                retreat: retreat, encounterOutcome: world.EncounterOutcome, controllers: world.Controllers,
                recovery: world.Recovery, controlPermissions: world.ControlPermissions, effectiveControl: world.EffectiveControl));
        }
        Reject(new(party.Scope, party.Side, party.Revision, [member.ActorId], party.Members));
        Reject(new(party.Scope, party.Side, party.Revision, party.RequiredActors,
            [new(member.Command, member.Origin, member.NormalizedSpeed + 1, member.SamplesWalked)]));
        Reject(new(party.Scope, party.Side, party.Revision + 1, party.RequiredActors,
            [new(member.Command, member.Origin, member.NormalizedSpeed, 1)]));
    }
}
