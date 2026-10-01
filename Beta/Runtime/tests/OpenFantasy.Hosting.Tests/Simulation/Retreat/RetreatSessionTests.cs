using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.Retreat;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.Retreat;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.Retreat;

/// <summary>
/// The committed retreat in the explicit retreat-capable cargo world, through ordinary session cutoffs: the earned and
/// bought setup, the real wolf, walked samples with their readiness, the shared NPC rule, refusals, durable recovery and
/// the preserved ordinary victory/defeat paths. Nothing is injected except the one marked counterfactual successor.
/// </summary>
public sealed class RetreatSessionTests
{
    private const string Seed = "retreat-session";

    private static (WorldSnapshot Genesis, string Path, string Directory) Fresh()
    {
        string directory = Path.Combine(Path.GetTempPath(), "openfantasy-retreat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return (CargoRetreatWorldAdmission.CreateEmbedded(Seed, "Traveler"), Path.Combine(directory, "world.sqlite"), directory);
    }

    [Fact]
    public async Task The_player_buys_retreats_on_walked_ground_with_real_costs_then_travels_trades_and_reopens_exactly()
    {
        var (genesis, save, directory) = Fresh();
        try
        {
            Assert.Equal(WorldEncounterRetreatAdmission.ProfileVersion, genesis.ProfileVersion);
            CargoCircuitSources sources = CargoCircuitSources.LoadEmbedded();
            EntityId player = genesis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
            CellCoordinate standing = genesis.Space.GetActor(player).Feet.Cell;
            CargoCorridor corridor = CargoCorridor.CreateCircuit(genesis.Space.Regions.Single(region => region.Bounds.Contains(standing)).Bounds, sources);
            CellCoordinate farmStart = new(standing.X, standing.Y, standing.Z - 1);
            CellCoordinate[] toMarla = [.. CargoCorridor.Lane([farmStart, new CellCoordinate(farmStart.X, corridor.OriginApproach.Y, farmStart.Z), corridor.OriginApproach])];
            CellCoordinate[] toFarm = [.. toMarla.Reverse()];
            RetreatJourneyWorld world = new(save, genesis, reopen: false);
            EntityId marla = world.Markets.Origin.MerchantActorId;
            EntityId wolf = world.FirstScenario.TargetActorId;
            var rations = world.Markets.Origin.Commodity;
            CountedSourceLot shelf = genesis.Inventory.Custody.Lots.Single(lot => lot.Definition == rations);

            // Earn the only money through Wren's ordinary quest, buy one actual lot at Marla's counter and carry it back.
            foreach (FarmSemanticAction action in new[] { FarmSemanticAction.Speak, FarmSemanticAction.Give, FarmSemanticAction.Give, FarmSemanticAction.CloseDialogue })
            {
                Assert.Equal(WorldActionRejection.None, (await world.SubmitOffer(action)).Rejection);
            }
            ulong reward = world.Balance(player);
            Assert.True(reward > 0);
            Assert.True(await world.Walk(toMarla, 8_000) > 0);
            var (firstBuy, firstTotal) = await world.Buy(world.Markets.Origin.MarketId, 1);
            Assert.Equal(WorldActionRejection.None, firstBuy.Rejection);
            await world.Walk(toFarm, 8_000);
            await world.ApproachFirstWolf();

            // Engage the real farm wolf, then retreat when the world offers the player a decision.
            Assert.Equal(WorldActionRejection.None, (await world.EnterFirstWolf()).Rejection);
            int wolfHp = world.State.Progression!.GetActor(wolf).CurrentHp;
            LocalRetreatOffer offer = await world.PlayerRetreatOffer();
            Assert.True(offer.CanQueue, $"The retreat must be offered: {offer.BlockedReason}.");
            Assert.InRange(offer.Samples, 1, EncounterRetreatRules.MaximumRouteSamples);
            Assert.Equal(EncounterRetreatRules.SampleDelay(world.State.Pacing!.Encounter!.GetActor(player).NormalizedSpeed), offer.SampleDelay);
            SubunitPosition origin = world.Feet(player);
            Assert.Equal(origin, offer.Origin);
            int playerHpBefore = world.State.Progression!.GetActor(player).CurrentHp;
            ulong ticksBefore = world.State.EncounterLifecycle!.TacticalTicks;
            WorldActionCommand retreat = world.RetreatCommand(offer, RetreatJourneyWorld.NewId());
            Assert.Equal(WorldActionRejection.None, (await world.Submit(retreat)).Rejection);
            List<SubunitPosition> walked = [world.Feet(player)];
            await world.TickUntil(() =>
            {
                SubunitPosition now = world.Feet(player);
                if (now != walked[^1]) walked.Add(now);
                return world.State.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Exploration;
            }, 2_000, "retreat");
            WorldSnapshot after = world.State;
            EncounterRetreatOutcome record = after.Retreat!.Last!;
            Assert.Equal(EncounterRetreatStatus.Disengaged, record.Status);
            Assert.Equal((player, retreat.CommandId, offer.Samples), (record.ActorId, record.CommandId, record.SamplesTaken));
            Assert.Equal(offer.Destination, after.Space.GetActor(player).Feet);
            // Every published position is one ordinary 32-subunit cardinal sample from the last: no teleport or skipped ground.
            Assert.All(walked.Zip(walked.Skip(1)), pair => Assert.Equal(32, Math.Abs(pair.Second.X - pair.First.X) + Math.Abs(pair.Second.Y - pair.First.Y)));
            Assert.Equal(offer.Samples, walked.Count - 1);
            Assert.Equal(offer.TotalDelay, record.ReadinessCharged);
            // Real costs: tactical time passed, the wolf kept its HP, the player's HP only fell (if bitten) and debt carries.
            Assert.True(after.EncounterLifecycle!.TacticalTicks > ticksBefore);
            Assert.Equal(wolfHp, after.Progression!.GetActor(wolf).CurrentHp);
            Assert.True(after.Progression.GetActor(player).CurrentHp <= playerHpBefore);
            Assert.Equal(offer.SampleDelay, after.EncounterLifecycle.GetDebt(player).RemainingDelay);
            Assert.False(WorldActorAvailability.CanAct(after, player));
            Assert.Null(after.Pacing!.Encounter);

            // Ordinary travel and trade continue after the retreat's readiness is paid by ordinary exploration.
            await world.TickUntil(() => WorldActorAvailability.CanAct(world.State, player), 20, "debt");
            await world.Walk(toMarla, 8_000);
            var (secondBuy, secondTotal) = await world.Buy(world.Markets.Origin.MarketId, 1);
            Assert.Equal(WorldActionRejection.None, secondBuy.Rejection);
            WorldSnapshot beforeClose = world.State;
            await world.StopAsync();

            await using var reopened = new RetreatJourneyWorld(save, genesis, reopen: true);
            WorldSnapshot recovered = reopened.State;
            Assert.Equal(beforeClose.ContentHash, recovered.ContentHash);
            Assert.Equal(reward - firstTotal - secondTotal, reopened.Balance(player));
            Assert.Equal(beforeClose.Progression!.GetActor(player).CurrentHp, recovered.Progression!.GetActor(player).CurrentHp);
            Assert.Equal(beforeClose.EncounterLifecycle!.GetDebt(player), recovered.EncounterLifecycle!.GetDebt(player));
            Assert.Equal(beforeClose.Space.GetActor(player).Feet, recovered.Space.GetActor(player).Feet);
            Assert.Equal(EncounterRetreatCodecV1.Encode(beforeClose.Retreat!), EncounterRetreatCodecV1.Encode(recovered.Retreat!));
            var carried = recovered.Inventory.Custody.Lots.Where(lot => lot.Definition == rations
                && lot.CustodianId == recovered.GetResident(player).InventoryCustodianId).ToArray();
            Assert.Equal(2UL, carried.Aggregate(0UL, (sum, lot) => sum + lot.Quantity));
            Assert.All(carried, lot => Assert.Equal(shelf.Source.SourceId, lot.Source.SourceId));
            Assert.Equal(1UL, recovered.Retreat!.AcceptedCount);
            // The exact retry of the committed request returns its original receipt without another effect.
            WorldActionReceipt retried = await reopened.Session.SubmitAction(retreat);
            Assert.Equal((retreat.CommandId, WorldActionRejection.None), (retried.CommandId, retried.Rejection));
            Assert.Equal(recovered.ContentHash, reopened.State.ContentHash);
            Assert.Equal(beforeClose.Progression.Currency.GetActorAccount(marla).Balance, reopened.Balance(marla));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task An_opted_in_wolf_retreats_through_the_same_intent_admission_body_and_costs()
    {
        var (genesis, save, directory) = Fresh();
        try
        {
            EntityId wolf = genesis.EncounterLifecycle!.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.FirstScenarioKey).TargetActorId;
            await using var world = new RetreatJourneyWorld(save, genesis, reopen: false,
                new LocalRetreatConfiguration([new LocalRetreatPreference(wolf, 1000)]));
            EntityId player = world.Player;
            SubunitPosition wolfOrigin = world.Feet(wolf);
            int playerHp = world.State.Progression!.GetActor(player).CurrentHp;
            Assert.Equal(WorldActionRejection.None, (await world.EnterFirstWolf()).Rejection);
            // The player waits; the wolf, under its own host control, commits its advised retreat when due.
            await world.TickUntil(() =>
            {
                var view = world.Session.ReadView();
                if (view.EncounterActions is { CanWait: true } actions && world.State.Retreat!.Active?.ActorId != player)
                {
                    _ = world.Session.SubmitAction(world.PlayerCommand(new QueueEncounterWaitIntent(actions.EncounterId, actions.TimingRevision),
                        RetreatJourneyWorld.NewId()));
                }
                return world.State.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Exploration;
            }, 3_000, "wolf retreat");
            WorldSnapshot after = world.State;
            EncounterRetreatOutcome record = after.Retreat!.Last!;
            Assert.Equal((wolf, EncounterRetreatStatus.Disengaged), (record.ActorId, record.Status));
            Assert.Equal(wolfOrigin, record.Origin);
            Assert.Equal(record.Final, after.Space.GetActor(wolf).Feet);
            Assert.NotEqual(wolfOrigin, record.Final);
            uint wolfSpeed = WorldEncounterLifecycleAdmission_NormalizedSpeed(after, wolf);
            Assert.Equal(EncounterRetreatRules.SampleDelay(wolfSpeed) * (ulong)record.SamplesTaken, record.ReadinessCharged);
            Assert.Equal(playerHp, after.Progression!.GetActor(player).CurrentHp);
            // The player's own side, identity and position are untouched; bystanders keep their sides and feet.
            Assert.Equal(EncounterLifecyclePhase.Exploration, after.EncounterLifecycle!.Phase);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static uint WorldEncounterLifecycleAdmission_NormalizedSpeed(WorldSnapshot world, EntityId actor) =>
        checked((uint)world.Progression!.GetActor(actor).Stats.Speed * 10);

    [Fact]
    public async Task Stale_wrong_controller_blocked_contact_and_unready_retreats_are_refused_without_effect()
    {
        var (genesis, save, directory) = Fresh();
        try
        {
            await using var world = new RetreatJourneyWorld(save, genesis, reopen: false);
            EntityId wolf = world.FirstScenario.TargetActorId;
            Assert.Equal(WorldActionRejection.None, (await world.EnterFirstWolf()).Rejection);
            LocalRetreatOffer offer = await world.PlayerRetreatOffer();
            Assert.True(offer.CanQueue);
            WorldSnapshot state = world.State;
            WorldActionRejection Preview(WorldActionCommand command) => world.Authority.Preview(new WorldStepBatch(
                RetreatJourneyWorld.NewId(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
                WorldBatchKind.EncounterDecision, [], [command], state.ProfileVersion)).Receipts.Single().Rejection;
            QueueEncounterRetreatIntent Intent(ulong timing, SubunitPosition destination, IEnumerable<EncounterRetreatDirection> route) =>
                new(offer.EncounterId, timing, destination, route);
            WorldActionCommand Player(WorldActionIntent intent, ulong? revision = null) => new(RetreatJourneyWorld.NewId(),
                world.Delivery.PrincipalId, world.Delivery.SessionId, world.Player, state.WorldId, state.BranchId,
                revision ?? offer.ActorRevision, intent);
            Assert.Equal(WorldActionRejection.None, Preview(Player(Intent(offer.TimingRevision, offer.Destination, offer.Route))));
            // Stale timing, stale actor revision and a destination that the route does not reach.
            Assert.Equal(WorldActionRejection.RevisionConflict, Preview(Player(Intent(offer.TimingRevision + 1, offer.Destination, offer.Route))));
            Assert.Equal(WorldActionRejection.RevisionConflict, Preview(Player(Intent(offer.TimingRevision, offer.Destination, offer.Route), offer.ActorRevision + 1)));
            Assert.Equal(WorldActionRejection.RevisionConflict, Preview(Player(Intent(offer.TimingRevision, offer.Origin, offer.Route))));
            // A route that ends still in contact, and a route through the wolf's own body.
            var oneStep = new[] { offer.Route[0] };
            Assert.Equal(WorldActionRejection.OutOfReach, Preview(Player(Intent(offer.TimingRevision,
                QueueEncounterRetreatIntent.Endpoint(offer.Origin, oneStep), oneStep))));
            SubunitPosition wolfFeet = world.Feet(wolf);
            EncounterRetreatDirection toward = wolfFeet.X > offer.Origin.X ? EncounterRetreatDirection.East
                : wolfFeet.X < offer.Origin.X ? EncounterRetreatDirection.West
                : wolfFeet.Y > offer.Origin.Y ? EncounterRetreatDirection.South : EncounterRetreatDirection.North;
            var through = Enumerable.Repeat(toward, 16).ToArray();
            Assert.Equal(WorldActionRejection.Unavailable, Preview(Player(Intent(offer.TimingRevision,
                QueueEncounterRetreatIntent.Endpoint(offer.Origin, through), through))));
            // Another actor's retreat under the player's delivery is refused by the authority's dispatch authentication.
            ActorControlBinding wolfControl = state.Inventory.Controls.Single(value => value.ActorId == wolf);
            Assert.NotEqual(wolfControl.PrincipalId, world.Delivery.PrincipalId);
            Assert.Throws<AuthorityDispatchException>(() => Preview(new WorldActionCommand(RetreatJourneyWorld.NewId(),
                world.Delivery.PrincipalId, world.Delivery.SessionId, wolf, state.WorldId, state.BranchId,
                state.Space.GetActor(wolf).Revision, Intent(offer.TimingRevision, world.Feet(wolf), [EncounterRetreatDirection.North]))));
            // The session itself refuses another actor's command.
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => world.Session.SubmitAction(new WorldActionCommand(RetreatJourneyWorld.NewId(),
                world.Delivery.PrincipalId, world.Delivery.SessionId, wolf, state.WorldId, state.BranchId, 0,
                Intent(offer.TimingRevision, world.Feet(wolf), [EncounterRetreatDirection.North]))));
            Assert.Equal(state.ContentHash, world.State.ContentHash);

            // After a strike the player is recovering: a retreat is refused until its own next boundary.
            EntityId strike = state.EncounterLifecycle!.Abilities.Single(value => value.Key == EncounterFarmAdmission.StrikeKey).DefinitionId;
            Assert.Equal(WorldActionRejection.None, (await world.Submit(world.PlayerCommand(new QueueEncounterAbilityIntent(offer.EncounterId,
                offer.TimingRevision, strike, wolf), RetreatJourneyWorld.NewId()))).Rejection);
            await world.TickUntil(() => !world.State.Pacing!.Encounter!.GetActor(world.Player).IsDue, 200, "strike");
            Assert.Equal(LocalRetreatBlockedReason.Recovering, world.Session.ReadView().Retreat!.Offer!.BlockedReason);
            WorldSnapshot recovering = world.State;
            state = recovering;
            Assert.Equal(WorldActionRejection.Unavailable, Preview(new WorldActionCommand(RetreatJourneyWorld.NewId(),
                world.Delivery.PrincipalId, world.Delivery.SessionId, world.Player, state.WorldId, state.BranchId,
                state.Space.GetActor(world.Player).Revision, new QueueEncounterRetreatIntent(offer.EncounterId,
                    state.Pacing!.Encounter!.Revision, offer.Destination, offer.Route))));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task A_committed_retreat_resumes_after_full_journal_recovery_without_skipping_samples_and_retries_exactly()
    {
        var (genesis, save, directory) = Fresh();
        try
        {
            WorldActionCommand retreat;
            LocalRetreatOffer offer;
            EncounterRetreatContinuation saved;
            ulong savedRevision;
            await using (var world = new RetreatJourneyWorld(save, genesis, reopen: false))
            {
                Assert.Equal(WorldActionRejection.None, (await world.EnterFirstWolf()).Rejection);
                offer = await world.PlayerRetreatOffer();
                Assert.True(offer.Samples >= 3, "The proving route needs several samples.");
                retreat = world.RetreatCommand(offer, RetreatJourneyWorld.NewId());
                Assert.Equal(WorldActionRejection.None, (await world.Submit(retreat)).Rejection);
                // Request committed: the retreat is queued and retained before its first sample.
                Assert.False(world.State.Retreat!.Active!.HasStarted);
                await world.TickUntil(() => world.State.Retreat!.Active is { SamplesTaken: >= 2 }, 400, "two samples");
                world.Session.SetReady(false);
                await world.StopAsync();
                using var closed = SqliteWorldAuthority.OpenExisting(save, WorldSnapshotCodec.Encode(genesis));
                saved = closed.ReadStateForHost().Retreat!.Active!;
                savedRevision = closed.ReadStateForHost().Revision;
            }
            await using var reopened = new RetreatJourneyWorld(save, genesis, reopen: true);
            WorldSnapshot recovered = reopened.State;
            Assert.Equal(savedRevision, recovered.Revision);
            Assert.Equal(saved.SamplesTaken, recovered.Retreat!.Active!.SamplesTaken);
            Assert.Equal(saved.ExpectedFeet, recovered.Space.GetActor(reopened.Player).Feet);
            Assert.Equal(EncounterLifecyclePhase.Active, recovered.EncounterLifecycle!.Phase);
            // The exact retry is the original receipt; a new command for the same actor is refused while it holds.
            WorldActionReceipt retried = await reopened.Session.SubmitAction(retreat);
            Assert.Equal((retreat.CommandId, WorldActionRejection.None), (retried.CommandId, retried.Rejection));
            Assert.Equal(1UL, reopened.State.Retreat!.AcceptedCount);
            List<SubunitPosition> walked = [reopened.Feet(reopened.Player)];
            await reopened.TickUntil(() =>
            {
                SubunitPosition now = reopened.Feet(reopened.Player);
                if (now != walked[^1]) walked.Add(now);
                return reopened.State.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Exploration;
            }, 2_000, "resumed retreat");
            Assert.Equal(offer.Samples - saved.SamplesTaken, walked.Count - 1);
            Assert.Equal(EncounterRetreatStatus.Disengaged, reopened.State.Retreat!.Last!.Status);
            Assert.Equal(offer.Destination, reopened.Feet(reopened.Player));
            Assert.Equal(offer.TotalDelay, reopened.State.Retreat.Last.ReadinessCharged);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task An_occupancy_change_after_selection_interrupts_without_movement_and_still_charges_the_sample()
    {
        var (genesis, save, directory) = Fresh();
        try
        {
            await using var world = new RetreatJourneyWorld(save, genesis, reopen: false);
            Assert.Equal(WorldActionRejection.None, (await world.EnterFirstWolf()).Rejection);
            LocalRetreatOffer offer = await world.PlayerRetreatOffer();
            Assert.Equal(WorldActionRejection.None, (await world.Submit(world.RetreatCommand(offer, RetreatJourneyWorld.NewId()))).Rejection);
            await world.TickUntil(() => world.State.Retreat!.Active is { HasStarted: true }, 400, "first sample");
            await world.StopAsync();
            using var authority = SqliteWorldAuthority.OpenExisting(save, WorldSnapshotCodec.Encode(genesis));
            WorldSnapshot state = authority.ReadStateForHost();
            EncounterRetreatContinuation active = state.Retreat!.Active!;
            // Counterfactual successor (not persisted): the other wolf, a bystander of this episode, now stands where
            // the committed next sample leads. Only the authority's processor is run against it.
            EntityId bystander = state.EncounterLifecycle!.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.SecondScenarioKey).TargetActorId;
            Assert.Equal(0, state.EncounterActions!.GetCombatant(bystander).Side);
            ActorPresence mover = state.Space.GetActor(active.ActorId);
            ActorPresence blocker = state.Space.GetActor(bystander);
            (long dx, long dy) = QueueEncounterRetreatIntent.Displacement(active.NextDirection);
            long reach = (dx != 0 ? mover.Condition.Definition.ClearanceDimensions.WidthSubunits + blocker.Condition.Definition.ClearanceDimensions.WidthSubunits
                : mover.Condition.Definition.ClearanceDimensions.DepthSubunits + blocker.Condition.Definition.ClearanceDimensions.DepthSubunits) / 2 + 16;
            var placed = new ActorPresence(blocker.Binding, blocker.Condition, blocker.Scope,
                mover.Feet.Offset(Math.Sign(dx) * reach, Math.Sign(dy) * reach, 0), blocker.Revision + 1, blocker.HomePlaceId);
            WorldSnapshot blocked;
            try
            {
                blocked = new WorldSnapshot(state.Inventory, state.Space.WithActors(state.Space.Actors.Select(value => value.ActorId == bystander ? placed : value)),
                    state.Residents, state.AdmissionVersion, state.Calendar, state.Revision, state.AdvancingSteps, state.Dialogues,
                    state.Gathering, state.Production, state.Progression, state.Quests, state.Construction, reservoirs: state.Reservoirs,
                    reservoirSeals: state.ReservoirSeals, liquidCells: state.LiquidCells, excavation: state.Excavation, pacing: state.Pacing,
                    encounterLifecycle: state.EncounterLifecycle, care: state.Care, resourceHistory: state.ResourceHistory,
                    coordination: state.Coordination, retreat: state.Retreat);
            }
            catch (ArgumentException error)
            {
                Assert.Fail("The counterfactual blocker must be an otherwise admitted world: " + error.Message);
                return;
            }
            // Advance the counterfactual to the mover's own boundary: ordinary ticks, and Wait for any automatic actor due first.
            var processor = new WorldStepProcessor();
            WorldSnapshot current = blocked;
            for (int boundaries = 0; ; boundaries++)
            {
                Assert.True(boundaries < 800);
                var due = WorldEncounterActions.DueActors(current);
                if (due.Count != 0 && due[0].ActorId == active.ActorId) break;
                var timing = current.Pacing!.Encounter!;
                var undecided = due.Where(value => value.Control == EncounterActorControl.Automatic
                    && timing.Decisions.All(decision => decision.ActorId != value.ActorId)).ToArray();
                List<WorldActionCommand> waits = [.. undecided.Select(value =>
                {
                    ActorControlBinding control = current.Inventory.Controls.Single(binding => binding.ActorId == value.ActorId);
                    return new WorldActionCommand(RetreatJourneyWorld.NewId(), control.PrincipalId, control.SessionId, value.ActorId,
                        current.WorldId, current.BranchId, current.Space.GetActor(value.ActorId).Revision,
                        new QueueEncounterWaitIntent(timing.EncounterId, timing.Revision));
                })];
                var kind = waits.Count != 0 ? WorldBatchKind.EncounterDecision
                    : due.Count == 0 ? WorldBatchKind.TacticalAdvance : WorldBatchKind.EncounterResolution;
                current = processor.Decide(current, new WorldStepBatch(RetreatJourneyWorld.NewId(), current.WorldId, current.BranchId,
                    current.Revision, current.AdvancingSteps, kind, [], waits, current.ProfileVersion), _ => false).Candidate;
                Assert.NotNull(current.Retreat!.Active);
            }
            WorldStepDecision decision = processor.Decide(current, new WorldStepBatch(RetreatJourneyWorld.NewId(), current.WorldId,
                current.BranchId, current.Revision, current.AdvancingSteps, WorldBatchKind.EncounterResolution, [], [],
                current.ProfileVersion), _ => false);
            WorldSnapshot interrupted = decision.Candidate;
            EncounterRetreatOutcome record = interrupted.Retreat!.Last!;
            Assert.Equal((EncounterRetreatStatus.Interrupted, EncounterRetreatReason.Blocked), (record.Status, record.Reason));
            Assert.Null(interrupted.Retreat.Active);
            Assert.Equal(current.Space.GetActor(active.ActorId).Feet, interrupted.Space.GetActor(active.ActorId).Feet);
            ulong delay = EncounterRetreatRules.SampleDelay(current.Pacing!.Encounter!.GetActor(active.ActorId).NormalizedSpeed);
            Assert.Equal(delay, interrupted.Pacing!.Encounter!.GetActor(active.ActorId).RemainingDelay);
            Assert.Equal(checked(active.ReadinessCharged + delay), record.ReadinessCharged);
            Assert.Equal(EncounterLifecyclePhase.Active, interrupted.EncounterLifecycle!.Phase);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task Ordinary_victory_and_defeat_still_end_retreat_capable_encounters_through_their_own_paths()
    {
        var (genesis, save, directory) = Fresh();
        try
        {
            await using var world = new RetreatJourneyWorld(save, genesis, reopen: false);
            EntityId wolf = world.FirstScenario.TargetActorId;
            EntityId strike = world.State.EncounterLifecycle!.Abilities.Single(value => value.Key == EncounterFarmAdmission.StrikeKey).DefinitionId;
            Assert.Equal(WorldActionRejection.None, (await world.EnterFirstWolf()).Rejection);
            await world.TickUntil(() =>
            {
                var actions = world.Session.ReadView().EncounterActions;
                if (actions?.AbilityOffers.FirstOrDefault(value => value.AbilityDefinitionId == strike && value.TargetActorId == wolf && value.CanQueue) is not null)
                {
                    _ = world.Session.SubmitAction(world.PlayerCommand(new QueueEncounterAbilityIntent(actions.EncounterId,
                        actions.TimingRevision, strike, wolf), RetreatJourneyWorld.NewId()));
                }
                return world.State.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Outcome;
            }, 4_000, "victory");
            Assert.Equal(0, world.State.Progression!.GetActor(wolf).CurrentHp);
            Assert.Null(world.State.Retreat!.Last);
            Assert.Equal(LocalRetreatBlockedReason.Outcome, world.Session.ReadView().Retreat!.Offer!.BlockedReason);
            var hash = world.Session.ReadView().EncounterLifecycle!.ReleaseActionHash!;
            Assert.Equal(WorldActionRejection.None, (await world.Submit(world.PlayerCommand(new ReleaseEncounterIntent(
                world.State.EncounterActions!.Scope.EncounterId, hash), RetreatJourneyWorld.NewId()))).Rejection);
            Assert.Equal(EncounterLifecyclePhase.Exploration, world.State.EncounterLifecycle!.Phase);

            // Defeat: the second wolf, answered only with Wait, incapacitates the player; no retreat is fabricated.
            await world.TickUntil(() => WorldActorAvailability.CanAct(world.State, world.Player), 400, "debt");
            var second = world.State.EncounterLifecycle!.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.SecondScenarioKey);
            var entry = world.Session.ReadView().EncounterLifecycle!.Entries.Single(value => value.ScenarioId == second.DefinitionId);
            Assert.True(entry.CanEnter);
            Assert.Equal(WorldActionRejection.None, (await world.Submit(world.PlayerCommand(new EnterEncounterIntent(second.DefinitionId,
                entry.EncounterId, world.State.EncounterLifecycle!.EncounterCount, entry.TargetRevision), RetreatJourneyWorld.NewId()))).Rejection);
            await world.TickUntil(() =>
            {
                if (world.Session.ReadView().EncounterActions is { CanWait: true } actions)
                {
                    _ = world.Session.SubmitAction(world.PlayerCommand(new QueueEncounterWaitIntent(actions.EncounterId, actions.TimingRevision),
                        RetreatJourneyWorld.NewId()));
                }
                return world.State.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Outcome;
            }, 6_000, "defeat");
            Assert.Equal(0, world.State.Progression!.GetActor(world.Player).CurrentHp);
            Assert.Equal(LocalRetreatBlockedReason.Outcome, world.Session.ReadView().Retreat!.Offer!.BlockedReason);
            Assert.Equal(0UL, world.State.Retreat!.AcceptedCount);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task The_accepted_cargo_world_keeps_its_profile_and_refuses_a_retreat_request()
    {
        string directory = Path.Combine(Path.GetTempPath(), "openfantasy-retreat-old-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            WorldSnapshot cargo = CargoWorldAdmission.CreateEmbedded(Seed, "Traveler");
            Assert.Equal(WorldMarketAdmission.ProfileVersion, cargo.ProfileVersion);
            Assert.Null(cargo.Retreat);
            Assert.Throws<ArgumentException>(() => WorldEncounterRetreatAdmission.CreateFresh(
                WorldEncounterRetreatAdmission.CreateFresh(cargo)));
            await using var world = new RetreatJourneyWorld(Path.Combine(directory, "world.sqlite"), cargo, reopen: false);
            Assert.Null(world.Session.ReadView().Retreat);
            Assert.Equal(WorldActionRejection.None, (await world.EnterFirstWolf()).Rejection);
            await world.TickUntil(() => world.Session.ReadView().EncounterActions?.CanWait == true, 400, "due");
            var actions = world.Session.ReadView().EncounterActions!;
            SubunitPosition feet = world.Feet(world.Player);
            EncounterRetreatDirection[] route = [EncounterRetreatDirection.North];
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => world.Session.SubmitAction(world.PlayerCommand(
                new QueueEncounterRetreatIntent(actions.EncounterId, actions.TimingRevision,
                    QueueEncounterRetreatIntent.Endpoint(feet, route), route), RetreatJourneyWorld.NewId())));
            Assert.Throws<ArgumentException>(() => new WorldStepBatch(RetreatJourneyWorld.NewId(), cargo.WorldId, cargo.BranchId, 1, 0,
                WorldBatchKind.EncounterDecision, [], [world.PlayerCommand(new QueueEncounterRetreatIntent(actions.EncounterId,
                    actions.TimingRevision, QueueEncounterRetreatIntent.Endpoint(feet, route), route), RetreatJourneyWorld.NewId())],
                cargo.ProfileVersion));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
