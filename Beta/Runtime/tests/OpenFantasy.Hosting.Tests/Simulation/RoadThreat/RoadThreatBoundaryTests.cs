using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.Retreat;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.Cargo;
using OpenFantasy.Hosting.Simulation.Retreat;
using OpenFantasy.Hosting.Simulation.RoadThreat;
using OpenFantasy.Hosting.Tests.Simulation.Retreat;

namespace OpenFantasy.Hosting.Tests.Simulation.RoadThreat;

/// <summary>
/// The hostile boundary against actual observation, input, readiness and recorded state. The warning row exists exactly
/// while the authority's own interaction query observes the chief (range, floor, sightline), with floor and wall
/// contrasts on a declared calibration of the spatial owner only. A waiting ordinary request cannot hold back an eligible
/// entry and is refused at that same cutoff, while outside reach it is answered and walking continues. Entry begins
/// exactly when the chief is eligible, including after a real disengagement measured through complete-journal reopens
/// mid-confrontation, mid-retreat and after it; staying to fight ends in a held Outcome that a reopen keeps and refuses.
/// The production selection creates, reopens and refuses other worlds' saves.
/// </summary>
public sealed class RoadThreatBoundaryTests
{
    [Fact]
    public async Task The_warning_row_follows_actual_observation_and_a_declared_calibration_hides_it_by_floor_or_wall()
    {
        WorldSnapshot genesis = RoadThreatJourney.Genesis("road-threat-observation-contrasts");
        RoadThreatBinding threat = RoadThreatJourney.Threat(genesis);
        using var files = new ScratchDirectory();
        var world = new RetreatJourneyWorld(files.Path("world.sqlite"), genesis, reopen: false);
        try
        {
            EntityId player = world.Player;
            EntityId chief = threat.ChiefActorId;
            // Real lived range contrasts: the farm and the lane approach publish nothing about the unobserved chief.
            Assert.Empty(world.Session.ReadView().RoadThreats!);
            CargoCorridor corridor = RoadThreatJourney.Corridor(genesis, player);
            CellCoordinate start = RoadThreatJourney.Standing(genesis, player);
            Dictionary<SpatialInteraction, int> seen = [];
            Assert.False(await WalkChecking(world, threat, [.. CargoCorridor.Lane([start, new CellCoordinate(start.X, corridor.OriginApproach.Y, start.Z),
                corridor.OriginApproach, corridor.Corner, new CellCoordinate(346, 313, start.Z)])], seen));
            Assert.True(seen.GetValueOrDefault(SpatialInteraction.OutOfRange) > 0 && seen.GetValueOrDefault(SpatialInteraction.Reachable) > 0,
                "The walk must cross from unobserved to observed ground.");

            // Observed beyond reach: the whole warning, from the same root, at the actual centre distance.
            WorldSnapshot watching = world.State;
            LocalRoadThreatView row = Assert.Single(world.Session.ReadView().RoadThreats!);
            Assert.Equal((chief, LocalRoadThreatState.Watching, 7, 800, 400L), (row.ActorId, row.State, row.Level, row.CurrentHp, row.ReachSubunits));
            Assert.Equal(RoadThreatJourney.CentreDistance(watching, player, chief), row.DistanceSubunits);
            Assert.InRange(row.DistanceSubunits, 401, LocalRoadThreatView.ObservationRangeSubunits);
            Assert.True(LocalRoadThreatView.Observes(watching.Space, player, chief));

            // Declared calibration of the spatial owner only (never saved or replayed): a high wall between the same two
            // bodies occludes the centre sightline, so the observation fails although range and floor are unchanged.
            SubunitPosition feet = watching.Space.GetActor(player).Feet;
            SubunitPosition chiefFeet = watching.Space.GetActor(chief).Feet;
            long wallY = (feet.Y + chiefFeet.Y) / 2;
            var wall = new SpatialStructure(RetreatJourneyWorld.NewId(), [new SubunitBox(new SubunitPosition(feet.X - 128, wallY - 8, feet.Z),
                new SubunitPosition(chiefFeet.X + 128, wallY + 8, feet.Z + 768))]);
            LocalSpatialSnapshot walled = watching.Space.WithStructures([.. watching.Space.Structures, wall]);
            Assert.Equal(SpatialInteraction.Occluded, walled.QueryInteraction(player, chief));
            Assert.False(LocalRoadThreatView.Observes(walled, player, chief));

            // Declared calibration of the spatial owner only: the farm's retained two-deep receiver basin. The chief in the
            // basin is within range of the player on its bank but on another floor; on the bank beside it, it is observed.
            SubunitPosition bank = BreachFarmLayout.ReceiverBank;
            LocalSpatialSnapshot? floors = null;
            foreach (long y in new long[] { 330, 329 })
            {
                foreach (long x in new long[] { 119, 118, 117 })
                {
                    floors ??= TryPlace(genesis.Space, (player, bank), (chief, new SubunitPosition(x * 256 + 128, y * 256 + 128, -256)));
                }
            }
            Assert.NotNull(floors);
            Assert.Equal(SpatialInteraction.DifferentFloor, floors!.QueryInteraction(player, chief));
            Assert.False(LocalRoadThreatView.Observes(floors, player, chief));
            LocalSpatialSnapshot? level = null;
            foreach ((long x, long y) in new (long, long)[] { (121, 329), (120, 328), (121, 328), (122, 329) })
            {
                level ??= TryPlace(genesis.Space, (player, bank), (chief, new SubunitPosition(x * 256 + 128, y * 256 + 128, bank.Z)));
            }
            Assert.NotNull(level);
            Assert.True(LocalRoadThreatView.Observes(level!, player, chief));
        }
        finally { await world.StopAsync(); }
    }

    [Fact]
    public async Task A_waiting_request_never_holds_back_entry_and_is_refused_at_that_cutoff_while_outside_reach_it_is_answered()
    {
        WorldSnapshot genesis = RoadThreatJourney.Genesis("road-threat-pending-contrast");
        RoadThreatBinding threat = RoadThreatJourney.Threat(genesis);
        using var files = new ScratchDirectory();
        var world = new RetreatJourneyWorld(files.Path("world.sqlite"), genesis, reopen: false);
        try
        {
            EntityId player = world.Player;
            CargoCorridor corridor = RoadThreatJourney.Corridor(genesis, player);
            CellCoordinate start = RoadThreatJourney.Standing(genesis, player);
            Assert.False(await RoadThreatJourney.WalkUntilEngaged(world, [.. CargoCorridor.Lane([start,
                new CellCoordinate(start.X, corridor.OriginApproach.Y, start.Z), corridor.OriginApproach, corridor.Corner,
                new CellCoordinate(346, 314, start.Z)])], 8_000));
            await world.TickUntil(() => WorldActorAvailability.CanAct(world.State, player), 400, "settle");
            int answered = 0;
            for (int sample = 0; ; sample++)
            {
                Assert.True(sample < 32, "The lane must reach the chief within a few cells of samples.");
                WorldSnapshot state = world.State;
                bool eligible = LocalRoadThreatController.CanInitiate(state, threat.Scenario);
                SubunitPosition before = state.Space.GetActor(player).Feet;
                Task<WorldActionReceipt> pending = world.Session.SubmitAction(UnreachableGather(world));
                world.Session.SetMovement(0, -WorldMovementInput.DisplacementPerStep);
                await world.Tick();
                if (!eligible)
                {
                    // Outside reach the waiting request receives its own durable refusal and the walk continues.
                    await SessionFixture.Until(() => pending.IsCompleted);
                    Assert.NotEqual(WorldActionRejection.None, (await pending).Rejection);
                    Assert.Equal((0UL, before.Offset(0, -WorldMovementInput.DisplacementPerStep, 0)),
                        (world.State.EncounterLifecycle!.EncounterCount, world.Feet(player)));
                    answered++;
                    continue;
                }
                // The first eligible cutoff is the chief's: the entry committed, the player did not move, and the waiting
                // request was refused at this boundary. Manual time does not advance here, so no later cutoff answers it.
                ulong entryRevision = world.State.Revision;
                await SessionFixture.Until(() => pending.IsCompleted);
                await Assert.ThrowsAsync<AuthorityDispatchException>(() => pending);
                Assert.Equal(entryRevision, world.State.Revision);
                Assert.Equal((1UL, threat.ScenarioId), (world.State.EncounterLifecycle!.EncounterCount, world.State.EncounterLifecycle.ScenarioId));
                Assert.Equal(before, world.Feet(player));
                break;
            }
            Assert.True(answered > 0);
            world.Session.SetMovement(0, 0);
            for (int index = 0; index < 4; index++) await world.Tick();
            // Nothing of any refused request executes later at the new boundary: the oak was never swung at.
            Assert.Equal(genesis.Gathering!.Nodes.Single(value => value.Definition.Kind == GatherNodeKind.Oak).RemainingSwings,
                world.State.Gathering!.Nodes.Single(value => value.Definition.Kind == GatherNodeKind.Oak).RemainingSwings);
            Assert.Equal(1UL, world.State.EncounterLifecycle!.EncounterCount);
        }
        finally { await world.StopAsync(); }
    }

    [Fact]
    public async Task Readiness_and_recorded_state_decide_entry_through_reopens_mid_encounter_mid_retreat_and_at_outcome()
    {
        WorldSnapshot genesis = RoadThreatJourney.Genesis("road-threat-readiness-reopen");
        RoadThreatBinding threat = RoadThreatJourney.Threat(genesis);
        using var files = new ScratchDirectory();
        string path = files.Path("world.sqlite");
        var world = new RetreatJourneyWorld(path, genesis, reopen: false);
        try
        {
            EntityId player = world.Player;
            EntityId chief = threat.ChiefActorId;
            CargoCorridor corridor = RoadThreatJourney.Corridor(genesis, player);
            CellCoordinate start = RoadThreatJourney.Standing(genesis, player);
            Assert.True(await WalkWithLaw(world, threat, [.. CargoCorridor.Lane([start, new CellCoordinate(start.X, corridor.OriginApproach.Y, start.Z),
                corridor.OriginApproach, corridor.Corner, new CellCoordinate(346, 310, start.Z)])], 8_000));

            // Reopen at the chief-initiated confrontation: the complete journal restores it and the encounter continues.
            WorldSnapshot entered = world.State;
            Assert.Equal((1UL, threat.ScenarioId), (entered.EncounterLifecycle!.EncounterCount, entered.EncounterLifecycle.ScenarioId));
            world = await Reopen(world, path, genesis, entered);
            await RoadThreatJourney.AwaitPlayerChoice(world);
            Assert.Equal(WorldActionRejection.None, (await world.Submit(RoadThreatJourney.Strike(world, chief))).Rejection);
            await RoadThreatJourney.AwaitPlayerChoice(world);
            Assert.True(world.State.Progression!.GetActor(player).CurrentHp < genesis.Progression!.GetActor(player).CurrentHp);

            // Reopen mid-retreat: the recorded route continues from its taken samples to disengagement.
            LocalRetreatOffer offer = await world.PlayerRetreatOffer();
            Assert.Equal(WorldActionRejection.None, (await world.Submit(world.RetreatCommand(offer, RetreatJourneyWorld.NewId()))).Rejection);
            await world.TickUntil(() => world.Session.ReadView().Retreat?.Progress is { SamplesTaken: >= 1 }, 400, "first retreat sample");
            world = await Reopen(world, path, genesis, world.State);
            await world.TickUntil(() => world.State.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Exploration, 2_000, "disengage");
            WorldSnapshot escaped = world.State;
            Assert.Equal((EncounterRetreatStatus.Disengaged, offer.Destination), (escaped.Retreat!.Last!.Status, escaped.Space.GetActor(player).Feet));

            // Both actors' readiness debts at the actual disengagement survive a complete-journal reopen exactly.
            var chiefDebt = escaped.EncounterLifecycle!.GetDebt(chief).RemainingDelay;
            var playerDebt = escaped.EncounterLifecycle.GetDebt(player).RemainingDelay;
            Assert.True(playerDebt > 0, "The retreat's exit recovery is retained.");
            world = await Reopen(world, path, genesis, escaped);
            Assert.Equal((chiefDebt, playerDebt), (world.State.EncounterLifecycle!.GetDebt(chief).RemainingDelay,
                world.State.EncounterLifecycle.GetDebt(player).RemainingDelay));

            // An immediate return: entry happens exactly at the first cutoff where the chief is eligible (in reach, no
            // debt), never earlier, however the two recoveries overlap; standing in reach while it recovers is lawful timing.
            CellCoordinate here = RoadThreatJourney.Standing(world.State, player);
            Assert.True(await WalkWithLaw(world, threat, [.. CargoCorridor.Lane([here, new CellCoordinate(346, here.Y, here.Z),
                new CellCoordinate(346, 312, here.Z)])], 4_000, holdInReach: 2_000));
            Assert.Equal(2UL, world.State.EncounterLifecycle!.EncounterCount);
            Assert.Equal(genesis.Space.GetActor(chief).Feet, world.State.Space.GetActor(chief).Feet);

            // Staying to fight: the chief's real hits incapacitate the Squire, and the Outcome holds.
            while (!WorldEncounterActions.IsOutcome(world.State))
            {
                await world.TickUntil(() => WorldEncounterActions.IsOutcome(world.State)
                    || world.State.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Active
                    && WorldEncounterActions.HasRequiredDecisions(world.State), 600, "next choice");
                if (WorldEncounterActions.IsOutcome(world.State)) break;
                Assert.Equal(WorldActionRejection.None, (await world.Submit(RoadThreatJourney.Strike(world, chief))).Rejection);
            }
            WorldSnapshot outcome = world.State;
            Assert.Equal(0, outcome.Progression!.GetActor(player).CurrentHp);
            Assert.True(WorldActorAvailability.CanCooperate(outcome, chief), "The living chief remains; no reward or relocation exists.");

            // Reopen at the Outcome: it is kept exactly, no further player action or movement is accepted, and no
            // automatic release, rescue or reward follows. Release authority stays the existing lifecycle law.
            world = await Reopen(world, path, genesis, outcome);
            Assert.True(WorldEncounterActions.IsOutcome(world.State));
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => world.Session.SubmitAction(UnreachableGather(world)));
            SubunitPosition held = world.Feet(player);
            for (int index = 0; index < 20; index++)
            {
                world.Session.SetMovement(0, WorldMovementInput.DisplacementPerStep);
                await world.Tick();
            }
            Assert.Equal((held, 2UL), (world.Feet(player), world.State.EncounterLifecycle!.EncounterCount));
            Assert.True(WorldEncounterActions.IsOutcome(world.State));
        }
        finally { await world.StopAsync(); }
    }

    [Fact]
    public async Task The_production_threat_selection_creates_reopens_and_refuses_other_worlds_saves()
    {
        using var files = new ScratchDirectory();
        string threatSave = files.Path("threat.sqlite");
        string retreatSave = files.Path("retreat.sqlite");
        WorldSnapshot expected = RoadThreatWorldAdmission.CreateEmbedded(RoadThreatWorldAdmission.SeedText, "Traveler");
        foreach (string pass in new[] { "create", "reopen" })
        {
            FarmClientSession session = await FarmClientSession.OpenCargoThreatAsync(threatSave, RoadThreatWorldAdmission.SeedText, "Traveler");
            try
            {
                LocalWorldView view = session.World.ReadView();
                Assert.Equal((expected.WorldId, expected.BranchId), (view.WorldId, view.BranchId));
                Assert.NotNull(view.Retreat);
                Assert.Empty(view.RoadThreats!);
                Assert.True(session.IsRegional, pass);
            }
            finally { await session.DisposeAsync(); }
        }
        // The retreat world's selection never reads the threatened save, and the threatened selection never reads it.
        await Assert.ThrowsAnyAsync<Exception>(() => FarmClientSession.OpenCargoRetreatAsync(threatSave, CargoRetreatWorldAdmission.SeedText, "Traveler"));
        FarmClientSession retreat = await FarmClientSession.OpenCargoRetreatAsync(retreatSave, CargoRetreatWorldAdmission.SeedText, "Traveler");
        try { Assert.Null(retreat.World.ReadView().RoadThreats); }
        finally { await retreat.DisposeAsync(); }
        await Assert.ThrowsAnyAsync<Exception>(() => FarmClientSession.OpenCargoThreatAsync(retreatSave, RoadThreatWorldAdmission.SeedText, "Traveler"));
        // The refusals left the threatened save its own world.
        FarmClientSession again = await FarmClientSession.OpenCargoThreatAsync(threatSave, RoadThreatWorldAdmission.SeedText, "Traveler");
        try { Assert.Equal(expected.WorldId, again.World.ReadView().WorldId); }
        finally { await again.DisposeAsync(); }
    }

    /// <summary>A well-formed gather of the farm's oak from the lane: the authority's ordinary refusal, never work.</summary>
    private static WorldActionCommand UnreachableGather(RetreatJourneyWorld world)
    {
        WorldSnapshot state = world.State;
        var node = state.Gathering!.Nodes.Single(value => value.Definition.Kind == GatherNodeKind.Oak);
        var tool = world.Session.ReadView().GatheringTools.Single(value => value.Kind == node.Definition.RequiredToolKind);
        return world.PlayerCommand(new GatherIntent(node.NodeId, node.Revision, tool.LotId, tool.Revision), RetreatJourneyWorld.NewId());
    }

    /// <summary>
    /// Walks with one ordinary sample per cutoff, checking at every cutoff that the published row exists exactly when the
    /// authority's interaction query observes the chief. Returns whether an encounter began.
    /// </summary>
    private static async Task<bool> WalkChecking(RetreatJourneyWorld world, RoadThreatBinding threat, IReadOnlyList<CellCoordinate> waypoints,
        Dictionary<SpatialInteraction, int> seen)
    {
        var coordinator = new CargoLegCoordinator(world.Player, waypoints.Select(CargoCorridor.FeetAt));
        for (int cutoffs = 0; ; cutoffs++)
        {
            WorldSnapshot state = world.State;
            SpatialInteraction observed = state.Space.QueryInteraction(world.Player, threat.ChiefActorId);
            seen[observed] = seen.GetValueOrDefault(observed) + 1;
            IReadOnlyList<LocalRoadThreatView> rows = world.Session.ReadView().RoadThreats!;
            Assert.Equal(observed == SpatialInteraction.Reachable ? 1 : 0, rows.Count);
            if (state.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Exploration)
            {
                world.Session.SetMovement(0, 0);
                return true;
            }
            CargoLegAdvice advice = coordinator.Advise(state);
            if (advice.State == CargoLegState.Arrived)
            {
                world.Session.SetMovement(0, 0);
                return false;
            }
            Assert.True(advice.State == CargoLegState.Moving && cutoffs < 8_000, $"The walk stopped with {advice.RouteStatus}.");
            world.Session.SetMovement(advice.DeltaX, advice.DeltaY);
            await world.Tick();
        }
    }

    /// <summary>
    /// Walks (then optionally stands) with one ordinary sample per cutoff, checking the hostile-entry law at every cutoff:
    /// the encounter count rises at a cutoff exactly when the pre-cutoff root made the chief eligible.
    /// </summary>
    private static async Task<bool> WalkWithLaw(RetreatJourneyWorld world, RoadThreatBinding threat, IReadOnlyList<CellCoordinate> waypoints,
        int maximumCutoffs, int holdInReach = 0)
    {
        var coordinator = new CargoLegCoordinator(world.Player, waypoints.Select(CargoCorridor.FeetAt));
        int standing = 0;
        for (int cutoffs = 0; cutoffs < maximumCutoffs; cutoffs++)
        {
            WorldSnapshot state = world.State;
            if (state.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Exploration)
            {
                world.Session.SetMovement(0, 0);
                return true;
            }
            bool eligible = LocalRoadThreatController.CanInitiate(state, threat.Scenario);
            CargoLegAdvice advice = coordinator.Advise(state);
            if (advice.State == CargoLegState.Arrived)
            {
                world.Session.SetMovement(0, 0);
                if (standing++ >= holdInReach) return false;
            }
            else
            {
                Assert.True(advice.State == CargoLegState.Moving, $"The walk stopped with {advice.RouteStatus}.");
                world.Session.SetMovement(advice.DeltaX, advice.DeltaY);
            }
            ulong count = state.EncounterLifecycle.EncounterCount;
            await world.Tick();
            Assert.Equal(eligible ? count + 1 : count, world.State.EncounterLifecycle!.EncounterCount);
        }
        world.Session.SetMovement(0, 0);
        return false;
    }

    private static async Task<RetreatJourneyWorld> Reopen(RetreatJourneyWorld world, string path, WorldSnapshot genesis, WorldSnapshot head)
    {
        await world.StopAsync();
        var reopened = new RetreatJourneyWorld(path, genesis, reopen: true);
        Assert.Equal(WorldSnapshotCodec.Encode(head), WorldSnapshotCodec.Encode(reopened.State));
        return reopened;
    }

    /// <summary>The calibrated spatial owner with two actors placed, or null when that placement is not admissible.</summary>
    private static LocalSpatialSnapshot? TryPlace(LocalSpatialSnapshot space, (EntityId Actor, SubunitPosition Feet) first,
        (EntityId Actor, SubunitPosition Feet) second)
    {
        try
        {
            return space.WithActors(space.Actors.Select(actor => actor.ActorId == first.Actor ? Placed(actor, first.Feet)
                : actor.ActorId == second.Actor ? Placed(actor, second.Feet) : actor));
        }
        catch (ArgumentException) { return null; }
    }

    private static ActorPresence Placed(ActorPresence actor, SubunitPosition feet) =>
        new(actor.Binding, actor.Condition, actor.Scope, feet, actor.Revision, actor.HomePlaceId);

    private sealed class ScratchDirectory : IDisposable
    {
        private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "openfantasy-road-boundary-" + Guid.NewGuid().ToString("N"));

        internal ScratchDirectory() => Directory.CreateDirectory(_root);

        internal string Path(string name) => System.IO.Path.Combine(_root, name);

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
