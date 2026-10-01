using OpenFantasy.Core;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Simulation.Cargo;

namespace OpenFantasy.Hosting.Tests.Content.Cargo;

/// <summary>
/// Local geometry qualification of the authored corridor: the actual farm region and actors, plus the corridor
/// terrain and both merchants at their retained tiles. The player's real body walks with ordinary 32-subunit
/// samples checked by <see cref="LocalSpatialSnapshot.PrepareMove"/>. This is not an admitted world or a session
/// journey; it qualifies the geometry, leg bounds and itinerary advice that such a journey would use.
/// </summary>
public sealed class CargoCorridorRouteTests
{
    private static readonly Lazy<WorldSnapshot> Farm = new(() => NativeTradingWorld.CreateEmbedded("cargo-corridor", "Traveler"));

    [Fact]
    public void The_corridor_is_fourteen_bounded_regions_covering_both_retained_tiles()
    {
        Circuit circuit = Circuit.Compose();
        CargoCorridor corridor = circuit.Corridor;
        Assert.Equal(14, corridor.AllRegions.Count());
        // The farm keeps both retained regions (surface and breach receiver): fifteen of the fixed sixteen.
        Assert.Equal(2, circuit.World.Space.Regions.Count);
        Assert.Equal(15, circuit.Space.Regions.Count);
        Assert.True(circuit.Space.Regions.Count <= LocalSpatialSnapshot.MaximumRegions);
        Assert.Equal(Circuit.FarmRegion(circuit.World).Bounds, corridor.Basis);
        Assert.All(circuit.World.Space.Regions, retained => Assert.Contains(circuit.Space.Regions, region => ReferenceEquals(region, retained)));
        Assert.Equal(0, corridor.RegionIndexOf(new CellCoordinate(96, 350, 0)));
        Assert.Equal(13, corridor.RegionIndexOf(new CellCoordinate(347, 140, 0)));
        Assert.Equal(-1, corridor.RegionIndexOf(new CellCoordinate(200, 200, 0)));
        CellCoordinate previous = corridor.OriginApproach;
        foreach (CellCoordinate waypoint in corridor.Outbound)
        {
            Assert.InRange(Math.Abs(waypoint.X - previous.X) + Math.Abs(waypoint.Y - previous.Y), 1, CargoCorridor.MaximumLegCells);
            previous = waypoint;
        }
        Assert.Equal(corridor.DestinationApproach, previous);
        // Corridor terrain shares the retained seed and rules pin.
        TerrainRegionSnapshot farm = Circuit.FarmRegion(circuit.World);
        Assert.All(circuit.Space.Regions, region => Assert.Equal(farm.Seed, region.Seed));
        Assert.All(circuit.Space.Regions, region => Assert.Equal(farm.Rules.Version, region.Rules.Version));
    }

    [Fact]
    public void The_players_real_body_walks_the_whole_route_and_back_in_bounded_ordinary_legs()
    {
        Circuit circuit = Circuit.Compose();
        CargoCorridor corridor = circuit.Corridor;
        Assert.NotEqual(SpatialInteraction.Reachable, circuit.Space.QueryInteraction(circuit.Player, circuit.Destination.ActorId));
        CellCoordinate start = circuit.Space.GetActor(circuit.Player).Feet.Cell.Offset(0, 0, -1);
        CellCoordinate[] approach = [.. CargoCorridor.Lane([start, new CellCoordinate(start.X, corridor.OriginApproach.Y, start.Z),
            corridor.OriginApproach])];

        Walk toMarla = Walk.Along(circuit.Space, corridor, circuit.Player, approach);
        Assert.Equal(SpatialInteraction.Reachable, toMarla.Space.QueryInteraction(circuit.Player, circuit.Origin.ActorId));

        Walk outbound = Walk.Along(toMarla.Space, corridor, circuit.Player, corridor.Outbound);
        Assert.Equal(SpatialInteraction.Reachable, outbound.Space.QueryInteraction(circuit.Player, circuit.Destination.ActorId));
        Assert.NotEqual(SpatialInteraction.Reachable, outbound.Space.QueryInteraction(circuit.Player, circuit.Origin.ActorId));
        long cells = Math.Abs(corridor.DestinationApproach.X - corridor.OriginApproach.X)
            + Math.Abs(corridor.DestinationApproach.Y - corridor.OriginApproach.Y);
        Assert.Equal(457, cells);
        Assert.True(outbound.Samples >= cells * (SubunitPosition.SubunitsPerCell / WorldMovementInput.DisplacementPerStep));
        Assert.Equal(13, outbound.RegionTransitions);

        CellCoordinate[] homeward = [.. corridor.Outbound.Reverse().Skip(1), corridor.OriginApproach];
        Walk returned = Walk.Along(outbound.Space, corridor, circuit.Player, homeward);
        Assert.Equal(SpatialInteraction.Reachable, returned.Space.QueryInteraction(circuit.Player, circuit.Origin.ActorId));
        Assert.Equal(13, returned.RegionTransitions);
        Assert.True(returned.Samples >= outbound.Samples - 8);
        // Every presence revision counts one applied sample: the body was never relocated.
        Assert.Equal((ulong)(toMarla.Samples + outbound.Samples + returned.Samples),
            returned.Space.GetActor(circuit.Player).Revision - circuit.Space.GetActor(circuit.Player).Revision);
        // The merchants never moved.
        Assert.Equal(circuit.Origin.Presence.Feet, returned.Space.GetActor(circuit.Origin.ActorId).Feet);
        Assert.Equal(circuit.Destination.Presence.Feet, returned.Space.GetActor(circuit.Destination.ActorId).Feet);
    }

    [Fact]
    public void An_occupied_uncovered_or_overlong_leg_stops_without_movement_and_reports_its_status()
    {
        Circuit circuit = Circuit.Compose();
        LocalSpatialSnapshot space = circuit.Space;
        CargoLegAdvice occupied = new CargoLegCoordinator(circuit.Player, [circuit.Origin.Presence.Feet]).Advise(space);
        Assert.Equal((CargoLegState.Blocked, (GroundRouteStatus?)GroundRouteStatus.Blocked, 0L, 0L),
            (occupied.State, occupied.RouteStatus, occupied.DeltaX, occupied.DeltaY));
        // Missing coverage is reported as unavailable cells, not as proof that no route exists.
        CargoLegAdvice uncovered = new CargoLegCoordinator(circuit.Player,
            [CargoCorridor.FeetAt(new CellCoordinate(200, 200, 0))]).Advise(space);
        Assert.Equal((CargoLegState.Blocked, (GroundRouteStatus?)GroundRouteStatus.Unavailable), (uncovered.State, uncovered.RouteStatus));
        // A covered destination beyond the fixed search budget stops; the budget is never enlarged.
        CargoLegAdvice overlong = new CargoLegCoordinator(circuit.Player,
            [CargoCorridor.FeetAt(circuit.Corridor.Corner)]).Advise(space);
        Assert.Equal((CargoLegState.Blocked, (GroundRouteStatus?)GroundRouteStatus.ExpansionLimit), (overlong.State, overlong.RouteStatus));
    }

    /// <summary>
    /// Latch semantics on geometry alone (an unapplied sample is simulated by not applying it; the admitted session
    /// refusal is exercised in the refinement session tests): the stop holds on an unchanged situation, even for a
    /// fresh equal snapshot object, and a relevant change near the leg revalidates from actual state.
    /// </summary>
    [Fact]
    public void A_refused_sample_is_replanned_once_then_the_stop_is_latched_until_a_relevant_change()
    {
        Circuit circuit = Circuit.Compose();
        LocalSpatialSnapshot space = circuit.Space;
        CellCoordinate start = space.GetActor(circuit.Player).Feet.Cell.Offset(0, 0, -1);
        var coordinator = new CargoLegCoordinator(circuit.Player, [CargoCorridor.FeetAt(start.Offset(0, 4, 0))]);
        CargoLegAdvice first = coordinator.Advise(space);
        Assert.Equal(CargoLegState.Moving, first.State);
        // The sample was not applied: the leg is replanned once from actual state.
        CargoLegAdvice second = coordinator.Advise(space);
        Assert.Equal((CargoLegState.Moving, 1), (second.State, coordinator.Replans));
        // Refused again at the same feet: stop instead of repeating a refused sample.
        CargoLegAdvice third = coordinator.Advise(space);
        Assert.Equal((CargoLegState.Blocked, 0L, 0L), (third.State, third.DeltaX, third.DeltaY));
        int queries = coordinator.RouteQueries;
        // Repeated calls on the unchanged situation, including a fresh but equal snapshot, stay stopped and query nothing.
        LocalSpatialSnapshot equal = space.WithActors(space.Actors);
        foreach (LocalSpatialSnapshot unchanged in new[] { space, equal, space })
        {
            Assert.Equal(third, coordinator.Advise(unchanged));
        }
        Assert.Equal((queries, 1, true), (coordinator.RouteQueries, coordinator.Replans, coordinator.IsLatched));
        // A body far beyond the leg's neighbourhood moving is not relevant: the stop holds.
        ActorPresence far = circuit.Destination.Presence;
        SpatialMoveResult distant = space.PrepareMove(far.ActorId, far.Revision, 0, WorldMovementInput.DisplacementPerStep);
        Assert.Equal(SpatialMoveStatus.Prepared, distant.Status);
        LocalSpatialSnapshot unrelated = space.WithActors(space.Actors.Select(value => value.ActorId == far.ActorId ? distant.Candidate! : value));
        Assert.Equal(third, coordinator.Advise(unrelated));
        Assert.Equal(queries, coordinator.RouteQueries);
        // The walker's own presence changing is relevant: the leg is revalidated and a fresh legal sample advised.
        ActorPresence walker = unrelated.GetActor(circuit.Player);
        SpatialMoveResult moved = unrelated.PrepareMove(circuit.Player, walker.Revision, 0, first.DeltaY);
        Assert.Equal(SpatialMoveStatus.Prepared, moved.Status);
        LocalSpatialSnapshot changed = unrelated.WithActors(unrelated.Actors.Select(value => value.ActorId == circuit.Player ? moved.Candidate! : value));
        CargoLegAdvice resumed = coordinator.Advise(changed);
        Assert.Equal((CargoLegState.Moving, false), (resumed.State, coordinator.IsLatched));
        Assert.Equal(queries + 1, coordinator.RouteQueries);
        Assert.Equal(SpatialMoveStatus.Prepared, changed.PrepareMove(circuit.Player, changed.GetActor(circuit.Player).Revision,
            resumed.DeltaX, resumed.DeltaY).Status);
    }

    [Fact]
    public void A_changed_collider_with_the_same_structure_identity_releases_a_blocked_leg()
    {
        Circuit circuit = Circuit.Compose();
        SubunitPosition start = circuit.Space.GetActor(circuit.Player).Feet;
        SubunitPosition target = start.Offset(0, 4 * SubunitPosition.SubunitsPerCell, 0);
        EntityId structureId = new(0x636172676f, 900);
        SpatialStructure Obstacle(SubunitPosition at) => new(structureId,
            [new SubunitBox(at.Offset(-64, -64, 0), at.Offset(64, 64, 256))]);
        LocalSpatialSnapshot blocked = circuit.Space.WithStructures([Obstacle(target)]);
        var coordinator = new CargoLegCoordinator(circuit.Player, [target]);
        Assert.Equal(CargoLegState.Blocked, coordinator.Advise(blocked).State);
        int queries = coordinator.RouteQueries;
        Assert.Equal(CargoLegState.Blocked, coordinator.Advise(blocked.WithStructures([Obstacle(target)])).State);
        Assert.Equal(queries, coordinator.RouteQueries);
        // Geometry is a value contract: an owner can rebuild a collider without changing its identity.
        LocalSpatialSnapshot clear = blocked.WithStructures([Obstacle(target.Offset(8 * SubunitPosition.SubunitsPerCell, 0, 0))]);
        Assert.Equal(GroundRouteStatus.Reachable, clear.QueryGroundRoute(circuit.Player, target).Status);
        Assert.Equal(CargoLegState.Moving, coordinator.Advise(clear).State);
        Assert.Equal(queries + 1, coordinator.RouteQueries);
    }

    private sealed record Circuit(WorldSnapshot World, CargoCorridor Corridor, LocalSpatialSnapshot Space, EntityId Player,
        CargoMerchantPresence Origin, CargoMerchantPresence Destination)
    {
        internal static Circuit Compose()
        {
            WorldSnapshot world = Farm.Value;
            CargoCircuitSources sources = CargoCircuitSources.LoadEmbedded();
            EntityId player = world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
            TerrainRegionSnapshot farmRegion = FarmRegion(world);
            CargoCorridor corridor = CargoCorridor.CreateCircuit(farmRegion.Bounds, sources);
            EntityId wren = world.Quests!.Definitions.Select(value => value.GiverActorId).Distinct().Single();
            long feetZ = world.Space.GetActor(player).Feet.Z;
            CargoMerchantPresence origin = CargoMerchantPresence.AtSourceTile(world, sources.Origin, wren, feetZ);
            CargoMerchantPresence destination = CargoMerchantPresence.AtSourceTile(world, sources.Destination, wren, feetZ);
            LocalSpatialSnapshot farm = world.Space;
            var space = new LocalSpatialSnapshot(farm.Scope, corridor.GenerateTerrain(farm.Regions),
                farm.Actors.Append(origin.Presence).Append(destination.Presence), farm.Structures, farm.GroundSupport,
                farm.StructureSupport);
            return new Circuit(world, corridor, space, player, origin, destination);
        }

        /// <summary>The retained full-size farm region where the player stands; the breach receiver below it is kept too.</summary>
        internal static TerrainRegionSnapshot FarmRegion(WorldSnapshot world)
        {
            EntityId player = world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
            CellCoordinate standing = world.Space.GetActor(player).Feet.Cell;
            return world.Space.Regions.Single(region => region.Bounds.Contains(standing));
        }
    }

    private sealed record Walk(LocalSpatialSnapshot Space, int Samples, int RegionTransitions, int Replans)
    {
        private const int MaximumCutoffs = 20_000;

        internal static Walk Along(LocalSpatialSnapshot space, CargoCorridor corridor, EntityId actor,
            IEnumerable<CellCoordinate> waypoints)
        {
            var coordinator = new CargoLegCoordinator(actor, waypoints.Select(CargoCorridor.FeetAt));
            int samples = 0;
            int transitions = 0;
            int region = corridor.RegionIndexOf(space.GetActor(actor).Feet.Cell);
            for (int cutoff = 0; cutoff < MaximumCutoffs; cutoff++)
            {
                CargoLegAdvice advice = coordinator.Advise(space);
                if (advice.State == CargoLegState.Arrived)
                {
                    return new Walk(space, samples, transitions, coordinator.Replans);
                }
                Assert.True(advice.State == CargoLegState.Moving, $"Leg {advice.LegIndex} stopped with {advice.RouteStatus}.");
                Assert.Equal(WorldMovementInput.DisplacementPerStep, Math.Abs(advice.DeltaX) + Math.Abs(advice.DeltaY));
                ActorPresence presence = space.GetActor(actor);
                SpatialMoveResult move = space.PrepareMove(actor, presence.Revision, advice.DeltaX, advice.DeltaY);
                if (move.Status != SpatialMoveStatus.Prepared)
                {
                    continue;
                }
                space = space.WithActors(space.Actors.Select(value => value.ActorId == actor ? move.Candidate! : value));
                samples++;
                int next = corridor.RegionIndexOf(space.GetActor(actor).Feet.Cell);
                Assert.True(next >= 0, "The walker left the admitted corridor.");
                if (next != region)
                {
                    transitions++;
                    region = next;
                }
            }
            throw new Xunit.Sdk.XunitException("The itinerary exceeded its bounded cutoffs.");
        }
    }
}
