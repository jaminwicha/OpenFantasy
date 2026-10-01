using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.Retreat;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.Retreat;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.Retreat;

/// <summary>
/// Contrasts of the retreat law at real boundaries reached by ordinary play. Transitions current content cannot reach
/// (a hobbled body, an ally or third side, a changed controller, an opponent standing at the destination, another
/// actor's choice still queued at disengagement) are installed as MARKED COUNTERFACTUAL roots: otherwise admitted
/// worlds with one replaced owner, run only through the authority's own processor and never persisted or played.
/// </summary>
public sealed class RetreatContrastTests
{
    private const string Seed = "retreat-contrasts";

    private sealed record Boundary(WorldSnapshot State, EntityId Player, EntityId Wolf, EntityId Bystander, LocalRetreatOffer Offer,
        TrustedCommandDelivery Delivery);

    /// <summary>A world seed whose clock orders the player before the first wolf at a shared instant.</summary>
    private static string PlayerFirstSeed()
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            string seed = Seed + "-" + attempt;
            WorldSnapshot genesis = CargoRetreatWorldAdmission.CreateEmbedded(seed, "Traveler");
            EntityId player = genesis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
            EntityId wolf = genesis.EncounterLifecycle!.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.FirstScenarioKey).TargetActorId;
            if (player.CompareTo(wolf) < 0) return seed;
        }
        throw new InvalidOperationException("No examined seed orders the player first.");
    }

    /// <summary>The player's first ready boundary against the real wolf, through ordinary session cutoffs.</summary>
    private static async Task<Boundary> FirstBoundary(string directory, string seed = Seed)
    {
        WorldSnapshot genesis = CargoRetreatWorldAdmission.CreateEmbedded(seed, "Traveler");
        string save = Path.Combine(directory, "world.sqlite");
        await using var world = new RetreatJourneyWorld(save, genesis, reopen: false);
        Assert.Equal(WorldActionRejection.None, (await world.EnterFirstWolf()).Rejection);
        LocalRetreatOffer offer = await world.PlayerRetreatOffer();
        Assert.True(offer.CanQueue, $"The retreat must be offered: {offer.BlockedReason}.");
        EntityId bystander = world.State.EncounterLifecycle!.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.SecondScenarioKey).TargetActorId;
        var boundary = new Boundary(world.State, world.Player, world.FirstScenario.TargetActorId, bystander, offer, world.Delivery);
        world.Session.SetReady(false);
        await world.StopAsync();
        return boundary;
    }

    /// <summary>MARKED COUNTERFACTUAL: the same root with replaced owners; full world admission still applies.</summary>
    private static WorldSnapshot Counterfactual(WorldSnapshot state, LocalSpatialSnapshot? space = null,
        EncounterLifecycleSnapshot? lifecycle = null, WorldPacingState? pacing = null, EncounterRetreatState? retreat = null) =>
        new(state.Inventory, space ?? state.Space, state.Residents, state.AdmissionVersion, state.Calendar, state.Revision,
            state.AdvancingSteps, state.Dialogues, state.Gathering, state.Production, state.Progression, state.Quests,
            state.Construction, reservoirs: state.Reservoirs, reservoirSeals: state.ReservoirSeals, liquidCells: state.LiquidCells,
            excavation: state.Excavation, pacing: pacing ?? state.Pacing, encounterLifecycle: lifecycle ?? state.EncounterLifecycle,
            care: state.Care, resourceHistory: state.ResourceHistory, coordination: state.Coordination, retreat: retreat ?? state.Retreat);

    /// <summary>MARKED COUNTERFACTUAL scenario: the selected episode's definition and actions give one actor another side.</summary>
    private static WorldSnapshot WithSide(WorldSnapshot state, EntityId actor, ushort side)
    {
        EncounterLifecycleSnapshot life = state.EncounterLifecycle!;
        EncounterActionSnapshot actions = life.Actions!;
        IEnumerable<EncounterCombatant> Swap(IEnumerable<EncounterCombatant> combatants) =>
            combatants.Select(value => value.ActorId == actor ? new EncounterCombatant(actor, side, value.AbilityIds) : value);
        var changed = new EncounterActionSnapshot(actions.Scope, actions.Seed, actions.Revision, actions.Abilities,
            Swap(actions.Combatants), actions.Pending, actions.Random, actions.LastResolution);
        var scenarios = life.Scenarios.Select(value => value.DefinitionId != life.ScenarioId ? value
            : new EncounterScenarioDefinition(value.DefinitionId, value.Key, value.DisplayName, value.InitiatingActorId,
                value.TargetActorId, value.EntryRangeSubunits, Swap(value.Combatants)));
        return Counterfactual(state, lifecycle: new EncounterLifecycleSnapshot(life.WorldId, life.BranchId, life.Seed, life.Revision,
            life.ExplorationSteps, life.TacticalTicks, life.EntryTacticalTicks, life.EncounterCount, life.Phase, life.Abilities,
            scenarios, life.Debts, life.ScenarioId, changed));
    }

    private static WorldSnapshot Hobbled(WorldSnapshot state, EntityId actor)
    {
        ActorPresence presence = state.Space.GetActor(actor);
        BodyConditionSnapshot condition = presence.Condition;
        LocomotionRequirement ground = condition.Definition.Locomotion.Single(value => value.Mode == LocomotionMode.Ground);
        var injuries = condition.Injuries.Concat(condition.Definition.Parts.Where(part => part.Functions.Contains(ground.SupportingFunction))
            .Select(part => new BodyInjury(RetreatJourneyWorld.NewId(), RetreatJourneyWorld.NewId(), part.Slot, BodyInjuryKind.FunctionLoss,
                ground.SupportingFunction)));
        var hobbled = new ActorPresence(presence.Binding, new BodyConditionSnapshot(condition.FormInstanceId, condition.Definition, injuries),
            presence.Scope, presence.Feet, presence.Revision, presence.HomePlaceId);
        Assert.False(hobbled.Condition.CanLocomote(LocomotionMode.Ground));
        return Counterfactual(state, space: state.Space.WithActors(state.Space.Actors.Select(value => value.ActorId == actor ? hobbled : value)));
    }

    /// <summary>
    /// MARKED COUNTERFACTUAL controller change: control bindings are pinned with custody and reservoir capacity, so the
    /// committed route is instead re-attributed to a session that no longer controls the actor.
    /// </summary>
    private static WorldSnapshot Uncontrolled(WorldSnapshot state)
    {
        EncounterRetreatState retreat = state.Retreat!;
        EncounterRetreatContinuation active = retreat.Active!;
        WorldActionCommand command = active.Command;
        var stale = new WorldActionCommand(command.CommandId, command.PrincipalId, RetreatJourneyWorld.NewId(), command.ActorId,
            command.WorldId, command.BranchId, command.ExpectedActorRevision, command.Intent);
        return Counterfactual(state, retreat: new EncounterRetreatState(retreat.RulesPin, retreat.AcceptedCount,
            new EncounterRetreatContinuation(stale, active.Origin, active.SamplesTaken, active.ReadinessCharged), retreat.Last));
    }

    private static WorldStepBatch Batch(WorldSnapshot world, WorldBatchKind kind, params WorldActionCommand[] actions) =>
        new(RetreatJourneyWorld.NewId(), world.WorldId, world.BranchId, world.Revision, world.AdvancingSteps, kind, [], actions, world.ProfileVersion);

    private static WorldActionCommand Command(WorldSnapshot world, EntityId actor, WorldActionIntent intent)
    {
        ActorControlBinding control = world.Inventory.Controls.Single(value => value.ActorId == actor);
        return new WorldActionCommand(RetreatJourneyWorld.NewId(), control.PrincipalId, control.SessionId, actor, world.WorldId,
            world.BranchId, world.Space.GetActor(actor).Revision, intent);
    }

    private static WorldActionCommand RetreatOf(Boundary boundary) => Command(boundary.State, boundary.Player,
        new QueueEncounterRetreatIntent(boundary.Offer.EncounterId, boundary.Offer.TimingRevision, boundary.Offer.Destination, boundary.Offer.Route));

    private static WorldActionRejection Queue(WorldSnapshot world, WorldActionCommand command) =>
        new WorldStepProcessor().Decide(world, Batch(world, WorldBatchKind.EncounterDecision, command), _ => false).Receipts.Single().Rejection;

    private static LocalRetreatBlockedReason Offered(WorldSnapshot world, EntityId player) =>
        new LocalWorldView(world, player).Retreat!.Offer!.BlockedReason;

    /// <summary>Ordinary boundaries (automatic due actors Wait, ticks otherwise) until the actor is the first due actor.</summary>
    private static WorldSnapshot UntilDue(WorldSnapshot current, EntityId actor, Func<WorldSnapshot, bool>? stop = null)
    {
        var processor = new WorldStepProcessor();
        for (int boundaries = 0; ; boundaries++)
        {
            Assert.True(boundaries < 1_000, "The actor's boundary was never reached.");
            var due = WorldEncounterActions.DueActors(current);
            if (due.Count != 0 && due[0].ActorId == actor && (stop?.Invoke(current) ?? true)) return current;
            var timing = current.Pacing!.Encounter!;
            WorldActionCommand[] waits = due.Where(value => value.Control == EncounterActorControl.Automatic
                    && timing.Decisions.All(decision => decision.ActorId != value.ActorId) && !WorldEncounterRetreats.HoldsActor(current, value.ActorId))
                .Select(value => Command(current, value.ActorId, new QueueEncounterWaitIntent(timing.EncounterId, timing.Revision))).ToArray();
            var kind = waits.Length != 0 ? WorldBatchKind.EncounterDecision : due.Count == 0 ? WorldBatchKind.TacticalAdvance : WorldBatchKind.EncounterResolution;
            current = processor.Decide(current, Batch(current, kind, waits), _ => false).Candidate;
        }
    }

    private static WorldSnapshot Resolve(WorldSnapshot world) =>
        new WorldStepProcessor().Decide(world, Batch(world, WorldBatchKind.EncounterResolution), _ => false).Candidate;

    private static string Fresh()
    {
        string directory = Path.Combine(Path.GetTempPath(), "openfantasy-retreat-contrast-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    [Fact]
    public async Task Queue_admission_refuses_unsupported_scope_lost_locomotion_and_respects_centre_contact()
    {
        string directory = Fresh();
        try
        {
            Boundary boundary = await FirstBoundary(directory);
            WorldSnapshot state = boundary.State;
            WorldActionCommand retreat = RetreatOf(boundary);
            Assert.Equal(WorldActionRejection.None, Queue(state, retreat));
            // Every prefix of the offered route passes complete root admission, as the advisor verified.
            LocalSpatialSnapshot space = state.Space;
            foreach (EncounterRetreatDirection direction in boundary.Offer.Route)
            {
                space = WorldEncounterRetreats.WalkSample(space, boundary.Player, direction, out _)!;
                Assert.True(WorldEncounterRetreats.AdmitsPlacement(state, space));
            }
            Assert.True(WorldEncounterRetreats.IsClearOfContact(state, space, boundary.Player));

            // The real episode is one lone player against one opposing side, with the second wolf a bystander.
            Assert.True(WorldEncounterRetreats.IsSupportedSide(state, boundary.Player));
            Assert.Equal(0, state.EncounterActions!.GetCombatant(boundary.Bystander).Side);
            // MARKED COUNTERFACTUAL scopes: an ally or a third side cannot even be installed in an admitted root, because an
            // episode's combatants are its authority-derived scenario's; the pure scope rule is pinned by the Core tests.
            ushort own = state.EncounterActions.GetCombatant(boundary.Player).Side;
            Assert.ThrowsAny<ArgumentException>(() => WithSide(state, boundary.Bystander, own));
            Assert.ThrowsAny<ArgumentException>(() => WithSide(state, boundary.Bystander, 3));

            // MARKED COUNTERFACTUAL body: without ground locomotion nothing is offered or admitted.
            WorldSnapshot hobbled = Hobbled(state, boundary.Player);
            Assert.Equal(WorldActionRejection.Unavailable, Queue(hobbled, retreat));
            Assert.Equal(LocalRetreatBlockedReason.NoGroundLocomotion, Offered(hobbled, boundary.Player));

            // Contact is the horizontal distance between actual body centres: exactly at the radius is still contact,
            // and a different floor never shortens it into an escape.
            ActorPresence wolf = state.Space.GetActor(boundary.Wolf);
            ActorPresence player = state.Space.GetActor(boundary.Player);
            long radius = WorldEncounterRetreats.ContactRadius(state, boundary.Wolf);
            Int128 Center(ActorPresence actor, SubunitPosition feet, bool x) => x
                ? 2 * (Int128)feet.X + actor.Condition.Definition.ClearanceDimensions.WidthSubunits % 2
                : 2 * (Int128)feet.Y + actor.Condition.Definition.ClearanceDimensions.DepthSubunits % 2;
            SubunitPosition at = new(wolf.Feet.X + radius, wolf.Feet.Y, wolf.Feet.Z);
            Int128 dx = Center(wolf, wolf.Feet, true) - Center(player, at, true);
            Int128 dy = Center(wolf, wolf.Feet, false) - Center(player, at, false);
            Assert.Equal(dx * dx + dy * dy - 4 * (Int128)radius * radius, WorldEncounterRetreats.ContactExcess(state, state.Space, boundary.Player, at));
            SubunitPosition inside = new(wolf.Feet.X + radius - 64, wolf.Feet.Y, wolf.Feet.Z);
            Assert.True(WorldEncounterRetreats.ContactExcess(state, state.Space, boundary.Player, inside) <= 0);
            Assert.True(WorldEncounterRetreats.ContactExcess(state, state.Space, boundary.Player, inside with { Z = inside.Z + 256 }) <= 0);
            Assert.True(WorldEncounterRetreats.ContactExcess(state, state.Space, boundary.Player,
                new SubunitPosition(wolf.Feet.X + radius + 64, wolf.Feet.Y, wolf.Feet.Z)) > 0);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task Changed_conditions_mid_route_interrupt_without_movement_and_a_held_actor_gets_no_other_choice()
    {
        string directory = Fresh();
        try
        {
            Boundary boundary = await FirstBoundary(directory);
            WorldActionCommand retreat = RetreatOf(boundary);
            WorldStepDecision queued = new WorldStepProcessor().Decide(boundary.State, Batch(boundary.State, WorldBatchKind.EncounterDecision, retreat), _ => false);
            // The player's next own boundary after its first walked sample: a standing, already started retreat.
            WorldSnapshot walking = UntilDue(queued.Candidate, boundary.Player, value => value.Retreat!.Active is { HasStarted: true });
            EncounterRetreatContinuation active = walking.Retreat!.Active!;
            ulong delay = EncounterRetreatRules.SampleDelay(walking.Pacing!.Encounter!.GetActor(boundary.Player).NormalizedSpeed);

            // The held player is offered no Wait or Strike and its world view never reads as an awaited choice.
            var view = new LocalWorldView(walking, boundary.Player);
            Assert.Equal(LocalEncounterActionBlockedReason.Retreating, view.EncounterActions!.WaitBlockedReason);
            Assert.All(view.EncounterActions.AbilityOffers, offer => Assert.False(offer.CanQueue));
            Assert.True(view.Encounter!.PlayerDecisionQueued);
            Assert.Equal(LocalRetreatBlockedReason.Retreating, view.Retreat!.Offer!.BlockedReason);
            Assert.Equal((active.SamplesTaken, active.Intent.Route.Count), (view.Retreat.Progress!.SamplesTaken, view.Retreat.Progress.RouteSamples));
            Assert.False(WorldEncounterActions.HasRequiredDecisions(walking));
            // A second command while the retreat holds is refused without effect: Wait, Strike or another retreat.
            var timing = walking.Pacing.Encounter;
            EntityId strike = walking.EncounterLifecycle!.Abilities.Single(value => value.Key == EncounterFarmAdmission.StrikeKey).DefinitionId;
            foreach (WorldActionIntent intent in new WorldActionIntent[] { new QueueEncounterWaitIntent(timing.EncounterId, timing.Revision),
                new QueueEncounterAbilityIntent(timing.EncounterId, timing.Revision, strike, boundary.Wolf),
                new QueueEncounterRetreatIntent(timing.EncounterId, timing.Revision,
                    QueueEncounterRetreatIntent.Endpoint(walking.Space.GetActor(boundary.Player).Feet, [active.NextDirection]), [active.NextDirection]) })
            {
                WorldStepDecision refused = new WorldStepProcessor().Decide(walking, Batch(walking, WorldBatchKind.EncounterDecision,
                    Command(walking, boundary.Player, intent)), _ => false);
                Assert.Equal(WorldActionRejection.Unavailable, refused.Receipts.Single().Rejection);
                Assert.Equal(EncounterRetreatCodecV1.Encode(walking.Retreat), EncounterRetreatCodecV1.Encode(refused.Candidate.Retreat!));
            }

            void Interrupted(WorldSnapshot changed, EncounterRetreatReason reason, LocalRetreatBlockedReason then)
            {
                WorldSnapshot after = Resolve(changed);
                EncounterRetreatOutcome record = after.Retreat!.Last!;
                Assert.Equal((EncounterRetreatStatus.Interrupted, reason), (record.Status, record.Reason));
                Assert.Equal((active.SamplesTaken, active.Command.CommandId), (record.SamplesTaken, record.CommandId));
                Assert.Null(after.Retreat.Active);
                Assert.Equal(changed.Space.GetActor(boundary.Player).Feet, after.Space.GetActor(boundary.Player).Feet);
                Assert.Equal(delay, after.Pacing!.Encounter!.GetActor(boundary.Player).RemainingDelay);
                Assert.Equal(active.ReadinessCharged + delay, record.ReadinessCharged);
                Assert.Equal(EncounterLifecyclePhase.Active, after.EncounterLifecycle!.Phase);
                // Combat continues; the player is no longer held and must decide again once the charge is paid.
                var seen = new LocalWorldView(after, boundary.Player);
                Assert.Equal(then, seen.Retreat!.Offer!.BlockedReason);
                Assert.Null(seen.Retreat.Progress);
                Assert.Equal((reason, record.CommandId), (seen.Retreat.Last!.Reason, seen.Retreat.Last.CommandId));
                Assert.NotEqual(LocalEncounterActionBlockedReason.Retreating, seen.EncounterActions!.WaitBlockedReason);
            }
            // MARKED COUNTERFACTUAL transitions after commitment, each against the same walked boundary.
            Interrupted(Hobbled(walking, boundary.Player), EncounterRetreatReason.NoGroundLocomotion, LocalRetreatBlockedReason.NoGroundLocomotion);
            // An ally cannot appear mid-route: admission refuses any episode whose sides differ from its scenario.
            Assert.ThrowsAny<ArgumentException>(() => WithSide(walking, boundary.Bystander, walking.EncounterActions!.GetCombatant(boundary.Player).Side));
            Interrupted(Uncontrolled(walking), EncounterRetreatReason.ControlLost, LocalRetreatBlockedReason.Recovering);
            // The unchanged boundary walks its next sample.
            WorldSnapshot stepped = Resolve(walking);
            Assert.Equal(active.SamplesTaken + 1, stepped.Retreat!.Active!.SamplesTaken);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task The_final_sample_keeps_contact_or_disengages_and_retains_cancelled_choice_identities()
    {
        string directory = Fresh();
        try
        {
            Boundary boundary = await FirstBoundary(directory, PlayerFirstSeed());
            WorldActionCommand retreat = RetreatOf(boundary);
            WorldSnapshot queued = new WorldStepProcessor().Decide(boundary.State, Batch(boundary.State, WorldBatchKind.EncounterDecision, retreat), _ => false).Candidate;
            WorldSnapshot last = UntilDue(queued, boundary.Player, value => value.Retreat!.Active is { } active && active.RemainingSamples == 1);
            EncounterRetreatContinuation active = last.Retreat!.Active!;
            SubunitPosition wolfFeet = last.Space.GetActor(boundary.Wolf).Feet;
            SubunitPosition bystanderFeet = last.Space.GetActor(boundary.Bystander).Feet;

            // MARKED COUNTERFACTUAL: the wolf now stands at the retreat's origin, within reach of the destination.
            ActorPresence wolf = last.Space.GetActor(boundary.Wolf);
            var moved = new ActorPresence(wolf.Binding, wolf.Condition, wolf.Scope, active.Origin, wolf.Revision + 1, wolf.HomePlaceId);
            WorldSnapshot pursued = Counterfactual(last, space: last.Space.WithActors(last.Space.Actors.Select(value => value.ActorId == boundary.Wolf ? moved : value)));
            WorldSnapshot contact = Resolve(pursued);
            EncounterRetreatOutcome kept = contact.Retreat!.Last!;
            Assert.Equal((EncounterRetreatStatus.Interrupted, EncounterRetreatReason.ContactRemains), (kept.Status, kept.Reason));
            // The final sample is walked and charged, yet the encounter continues with the player to decide again.
            Assert.Equal(active.Intent.Destination, contact.Space.GetActor(boundary.Player).Feet);
            Assert.Equal(active.Intent.Route.Count, kept.SamplesTaken);
            Assert.Equal(EncounterLifecyclePhase.Active, contact.EncounterLifecycle!.Phase);

            // MARKED COUNTERFACTUAL: the wolf is due at the same instant, after the player in the clock's identity order,
            // and its own Wait is still queued when the player's last sample breaks contact.
            Assert.True(boundary.Player.CompareTo(boundary.Wolf) < 0, "This seed must order the player before the wolf at a shared instant.");
            WorldActionCommand wolfWait = Command(last, boundary.Wolf, new QueueEncounterWaitIntent(last.Pacing!.Encounter!.EncounterId, last.Pacing.Encounter.Revision));
            var basis = last.Pacing.Encounter;
            var waiting = new EncounterTimingSnapshot(basis.Scope, basis.Revision + 1, basis.ElapsedTacticalUnits,
                basis.Actors.Select(value => value.ActorId == boundary.Wolf ? new EncounterActorTiming(value.ActorId, value.NormalizedSpeed, 0, value.Control) : value),
                basis.Decisions.Append(new EncounterDecision(boundary.Wolf, wolfWait.CommandId, EncounterDecisionKind.Wait, wolfWait.ContentHash)));
            // Moving that due instant also changes the permanent debt owner. The counterfactual must satisfy the
            // same clock/debt agreement as a real root, independently of the wolf's debt at the sampled boundary.
            var life = last.EncounterLifecycle!;
            var waitingLife = new EncounterLifecycleSnapshot(life.WorldId, life.BranchId, life.Seed, life.Revision,
                life.ExplorationSteps, life.TacticalTicks, life.EntryTacticalTicks, life.EncounterCount, life.Phase,
                life.Abilities, life.Scenarios,
                waiting.Actors.Select(actor => new EncounterReadinessDebt(actor.ActorId, actor.RemainingDelay)),
                life.ScenarioId, life.Actions);
            WorldSnapshot pending = Counterfactual(last, pacing: last.Pacing.ObserveEncounter(waiting), lifecycle: waitingLife);
            WorldSnapshot disengaged = Resolve(pending);
            EncounterRetreatOutcome record = disengaged.Retreat!.Last!;
            Assert.Equal(EncounterRetreatStatus.Disengaged, record.Status);
            Assert.Equal(new[] { wolfWait.CommandId }, record.CancelledDecisionIds);
            Assert.Null(disengaged.Pacing!.Encounter);
            Assert.Equal(EncounterLifecyclePhase.Exploration, disengaged.EncounterLifecycle!.Phase);

            // The ordinary disengagement (nothing installed) keeps every participant's identity, side, place and health.
            WorldSnapshot ordinary = Resolve(last);
            Assert.Equal(EncounterRetreatStatus.Disengaged, ordinary.Retreat!.Last!.Status);
            Assert.Empty(ordinary.Retreat.Last.CancelledDecisionIds);
            Assert.Equal(boundary.State.Residents.Select(value => value.ActorId), ordinary.Residents.Select(value => value.ActorId));
            Assert.Equal(wolfFeet, ordinary.Space.GetActor(boundary.Wolf).Feet);
            Assert.Equal(bystanderFeet, ordinary.Space.GetActor(boundary.Bystander).Feet);
            Assert.Equal(last.Progression!.GetActor(boundary.Wolf).CurrentHp, ordinary.Progression!.GetActor(boundary.Wolf).CurrentHp);
            Assert.Equal(last.Progression.GetActor(boundary.Bystander).CurrentHp, ordinary.Progression.GetActor(boundary.Bystander).CurrentHp);
            EncounterActionSnapshot retained = ordinary.EncounterLifecycle!.Actions!;
            Assert.Equal(last.EncounterActions!.Combatants.Select(value => (value.ActorId, value.Side)),
                retained.Combatants.Select(value => (value.ActorId, value.Side)));
            Assert.Equal(EncounterRetreatRules.SampleDelay(last.Pacing.Encounter.GetActor(boundary.Player).NormalizedSpeed),
                ordinary.EncounterLifecycle.GetDebt(boundary.Player).RemainingDelay);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task An_undecided_player_holds_the_world_while_an_opponent_walks_its_committed_retreat()
    {
        string directory = Fresh();
        try
        {
            WorldSnapshot genesis = CargoRetreatWorldAdmission.CreateEmbedded(Seed, "Traveler");
            EntityId wolf = genesis.EncounterLifecycle!.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.FirstScenarioKey).TargetActorId;
            await using var world = new RetreatJourneyWorld(Path.Combine(directory, "world.sqlite"), genesis, reopen: false,
                new LocalRetreatConfiguration([new LocalRetreatPreference(wolf, 1000)]));
            Assert.Equal(WorldActionRejection.None, (await world.EnterFirstWolf()).Rejection);
            // Real NPC retreat: the player answers only until the wolf commits, then stays undecided at its own boundary.
            await world.TickUntil(() =>
            {
                if (world.State.Retreat!.Active is { } active && active.ActorId == wolf) return true;
                if (world.Session.ReadView().EncounterActions is { CanWait: true } actions)
                {
                    _ = world.Session.SubmitAction(world.PlayerCommand(new QueueEncounterWaitIntent(actions.EncounterId, actions.TimingRevision),
                        RetreatJourneyWorld.NewId()));
                }
                return false;
            }, 600, "wolf commits");
            await world.TickUntil(() => world.Session.ReadView().EncounterActions?.CanWait == true
                || world.State.Retreat!.Active is null, 600, "player due while the wolf withdraws");
            Assert.NotNull(world.State.Retreat!.Active);
            // The opponent's withdrawal is visible only anonymously: no route, destination or command identity.
            LocalRetreatView seen = world.Session.ReadView().Retreat!;
            Assert.True(seen.OtherWithdrawing);
            Assert.Null(seen.Progress);
            Assert.Null(seen.Last);
            Assert.Equal(LocalRetreatBlockedReason.Committed, seen.Offer!.BlockedReason);
            WorldSnapshot held = world.State;
            Assert.True(WorldEncounterActions.HasRequiredDecisions(held));
            for (int cutoff = 0; cutoff < 12; cutoff++) await world.Tick();
            // Full Wait: no tick, sample or other effect while the required human has not chosen.
            Assert.Equal(held.ContentHash, world.State.ContentHash);
            var choice = world.Session.ReadView().EncounterActions!;
            Assert.Equal(WorldActionRejection.None, (await world.Submit(world.PlayerCommand(
                new QueueEncounterWaitIntent(choice.EncounterId, choice.TimingRevision), RetreatJourneyWorld.NewId()))).Rejection);
            int samples = held.Retreat!.Active!.SamplesTaken;
            await world.TickUntil(() => world.State.Retreat!.Active is not { } active || active.SamplesTaken > samples
                || world.Session.ReadView().EncounterActions?.CanWait == true, 200, "wolf walks on");
            Assert.True(world.State.Retreat!.Active is null || world.State.Retreat.Active.SamplesTaken > samples
                || world.State.Revision > held.Revision);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task A_conflicting_reuse_of_a_committed_retreat_identity_is_refused_and_the_exact_retry_is_not()
    {
        string directory = Fresh();
        try
        {
            WorldSnapshot genesis = CargoRetreatWorldAdmission.CreateEmbedded(Seed, "Traveler");
            await using var world = new RetreatJourneyWorld(Path.Combine(directory, "world.sqlite"), genesis, reopen: false);
            Assert.Equal(WorldActionRejection.None, (await world.EnterFirstWolf()).Rejection);
            LocalRetreatOffer offer = await world.PlayerRetreatOffer();
            WorldActionCommand retreat = world.RetreatCommand(offer, RetreatJourneyWorld.NewId());
            Assert.Equal(WorldActionRejection.None, (await world.Submit(retreat)).Rejection);
            WorldSnapshot committed = world.State;
            // Same identity, different captured route: never treated as a retry and never applied.
            EncounterRetreatDirection[] other = [.. offer.Route.Take(offer.Route.Count - 1)];
            var conflicting = new WorldActionCommand(retreat.CommandId, retreat.PrincipalId, retreat.OriginSessionId, retreat.ActorId,
                retreat.WorldId, retreat.BranchId, retreat.ExpectedActorRevision, new QueueEncounterRetreatIntent(offer.EncounterId,
                    offer.TimingRevision, QueueEncounterRetreatIntent.Endpoint(offer.Origin, other), other));
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => world.Session.SubmitAction(conflicting));
            Assert.Equal(committed.ContentHash, world.State.ContentHash);
            WorldActionReceipt again = await world.Session.SubmitAction(retreat);
            Assert.Equal((retreat.CommandId, WorldActionRejection.None), (again.CommandId, again.Rejection));
            Assert.Equal(committed.ContentHash, world.State.ContentHash);
            Assert.Equal(1UL, world.State.Retreat!.AcceptedCount);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
