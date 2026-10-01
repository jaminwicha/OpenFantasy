using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Tests.Simulation.Retreat;

namespace OpenFantasy.Hosting.Tests.Simulation.RoadThreat;

public sealed class RoadThreatSchedulingRegressionTests
{
    [Fact]
    public async Task Repeated_unreachable_gather_requests_cannot_suppress_hostile_entry_while_walking()
    {
        WorldSnapshot genesis = RoadThreatJourney.Genesis("road-threat-input-starvation");
        var threat = RoadThreatJourney.Threat(genesis);
        string directory = Path.Combine(Path.GetTempPath(), "openfantasy-road-scheduling-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await using var world = new RetreatJourneyWorld(Path.Combine(directory, "world.sqlite"), genesis, reopen: false);
            var corridor = RoadThreatJourney.Corridor(genesis, world.Player);
            var start = RoadThreatJourney.Standing(genesis, world.Player);
            await world.Walk([.. CargoCorridor.Lane([start,
                new CellCoordinate(start.X, corridor.OriginApproach.Y, start.Z), corridor.OriginApproach,
                corridor.Corner, new CellCoordinate(346, 314, start.Z)])], 8_000);

            int reachableCutoffs = 0;
            for (int sample = 0; sample < 48 && world.State.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Exploration; sample++)
            {
                WorldSnapshot state = world.State;
                if (state.Space.QueryInteraction(threat.ChiefActorId, world.Player, threat.Scenario.EntryRangeSubunits)
                    == SpatialInteraction.Reachable) reachableCutoffs++;
                var node = state.Gathering!.Nodes.Single(value => value.Definition.Kind == GatherNodeKind.Oak);
                var tool = world.Session.ReadView().GatheringTools.Single(value =>
                    value.Kind == node.Definition.RequiredToolKind);
                world.Session.SetMovement(0, -WorldMovementInput.DisplacementPerStep);
                var command = world.PlayerCommand(new GatherIntent(node.NodeId, node.Revision, tool.LotId, tool.Revision),
                    RetreatJourneyWorld.NewId());
                Task<WorldActionReceipt> pending = world.Session.SubmitAction(command);
                await world.Tick();
                // A hostile entry owns this cutoff; its queued ordinary request can be refused at the next boundary.
                if (world.State.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Exploration)
                {
                    await world.Tick();
                    await Assert.ThrowsAnyAsync<Exception>(async () => await pending);
                    break;
                }
                var receipt = await pending;
                Assert.NotEqual(WorldActionRejection.None, receipt.Rejection);
            }
            world.Session.SetMovement(0, 0);
            Assert.True(reachableCutoffs > 0, "The actual path must cross the chief's entry reach.");
            Assert.Equal(1UL, world.State.EncounterLifecycle!.EncounterCount);
            Assert.Equal(threat.ScenarioId, world.State.EncounterLifecycle.ScenarioId);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
