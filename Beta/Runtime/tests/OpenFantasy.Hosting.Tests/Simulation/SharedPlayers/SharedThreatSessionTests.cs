using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.Cargo;
using OpenFantasy.Hosting.Worlds;
using OpenFantasy.Hosting.Tests.Simulation.RoadThreat;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;
using static OpenFantasy.Hosting.Tests.Simulation.EncounterOutcome.TwoHumanOutcomeWorld;

namespace OpenFantasy.Hosting.Tests.Worlds.SharedPlayers;

public sealed class SharedThreatSessionTests
{
    [Fact]
    public async Task Closing_the_target_port_does_not_remove_a_lawful_hostile_opportunity()
    {
        using var files = new Simulation.SessionFiles();
        WorldSnapshot genesis = CargoOutcomeWorldAdmission.CreateTwoHumanWolves("shared-resident-threat", "Traveler", 1);
        var scenario = genesis.EncounterLifecycle!.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.FirstScenarioKey);
        EntityId player = scenario.InitiatingActorId;
        EntityId other = scenario.TargetActorId;
        var threat = CargoOutcomeWorldAdmission.Find(genesis, CargoCircuitSources.LoadEmbedded(), RoadThreatSources.LoadEmbedded())!;
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var time = new Simulation.ManualSessionTimeProvider();
        await using var host = new LocalWorldSession(authority, player, DeliveryOf(genesis, player), time);
        await using LocalPlayerConnection peer = await host.ConnectPlayerAsync(other, DeliveryOf(genesis, other));
        host.SetReady(true);
        var corridor = RoadThreatJourney.Corridor(genesis, player);
        var start = RoadThreatJourney.Standing(genesis, player);
        var lane = CargoCorridor.Lane([start, new CellCoordinate(start.X, corridor.OriginApproach.Y, start.Z),
            corridor.OriginApproach, corridor.Corner, new CellCoordinate(346, 314, start.Z)]);
        var journey = new CargoLegCoordinator(player, lane.Select(CargoCorridor.FeetAt));
        for (int count = 0; ; count++)
        {
            var advice = journey.Advise(authority.ReadStateForHost());
            if (advice.State == CargoLegState.Arrived) break;
            Assert.Equal(CargoLegState.Moving, advice.State);
            Assert.True(count < 8_000);
            host.SetMovement(advice.DeltaX, advice.DeltaY);
            await Tick(time);
        }
        host.SetMovement(0, 0);
        for (int count = 0; ; count++)
        {
            WorldSnapshot state = authority.ReadStateForHost();
            if (state.Space.QueryInteraction(threat.ChiefActorId, player, threat.Scenario.EntryRangeSubunits) == SpatialInteraction.Reachable) break;
            Assert.True(count < 64);
            host.SetMovement(0, -WorldMovementInput.DisplacementPerStep);
            await Tick(time);
            host.SetMovement(0, 0);
        }
        WorldSnapshot reached = authority.ReadStateForHost();
        Assert.Equal(EncounterLifecyclePhase.Exploration, reached.EncounterLifecycle!.Phase);
        await host.Player.DisposeAsync();
        Assert.Equal(LocalWorldRunState.Unavailable, host.ReadRunState());
        peer.SetReady(true);
        await Tick(time);
        WorldSnapshot entered = authority.ReadStateForHost();
        Assert.Equal(1UL, entered.EncounterLifecycle!.EncounterCount);
        Assert.Equal(threat.ScenarioId, entered.EncounterLifecycle.ScenarioId);
        Assert.Equal(reached.Calendar, entered.Calendar);
        Assert.Equal(reached.Space.GetActor(player).Feet, entered.Space.GetActor(player).Feet);
        Assert.False(host.Completion.IsCompleted);
        Assert.True((await host.VerifyRecordedHistoryAsync()).ReproducesHead);
    }
}
