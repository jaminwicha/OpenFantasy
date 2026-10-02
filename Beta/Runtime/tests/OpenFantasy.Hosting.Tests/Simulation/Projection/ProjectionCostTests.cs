using System.Diagnostics;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;
using OpenFantasy.Hosting.Simulation.RoadThreat;

namespace OpenFantasy.Hosting.Tests.Simulation.Projection;

public sealed class ProjectionCostTests(ITestOutputHelper output)
{
    [Fact]
    public void Four_player_projection_preserves_exact_bytes_and_reports_component_cost()
    {
        WorldSnapshot world = CargoOutcomeWorldAdmission.CreateEmbedded(CargoOutcomeWorldAdmission.SeedText,
            new[] { "Amber", "Birch", "Cedar", "Dawn" });
        EntityId[] players = world.Residents.Where(actor => actor.ControllerKind == ResidentControllerKind.Player)
            .Select(actor => actor.ActorId).Order().ToArray();
        var observers = players.ToDictionary(actor => actor, actor => new LocalRegionObserver(actor));
        byte[][] expected = players.Select(actor => PlayerViewCodec.Encode(new LocalWorldView(world, actor, false, observers[actor]))).ToArray();
        Measure("regional", actor => observers[actor].Observe(world));
        Measure("worksite", actor => new LocalWorksiteView(world, actor));
        Measure("liquids", _ => new LocalLiquidView(world));
        Measure("restorative", actor => LocalRestorativeView.Project(world, actor));
        Measure("proposals", actor => LocalMaterialProposalView.Project(world, actor));
        Measure("markets", actor => LocalMarketView.Project(world, actor));
        Measure("road-threats", actor => LocalRoadThreatView.Project(world, actor));
        Measure("full", actor => new LocalWorldView(world, actor, false, observers[actor]));
        for (int index = 0; index < players.Length; index++)
            Assert.Equal(expected[index], PlayerViewCodec.Encode(new LocalWorldView(world, players[index], false, observers[players[index]])));

        void Measure(string label, Func<EntityId, object?> project)
        {
            foreach (EntityId actor in players) GC.KeepAlive(project(actor));
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            const int rounds = 8;
            for (int round = 0; round < rounds; round++)
                foreach (EntityId actor in players) GC.KeepAlive(project(actor));
            long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            output.WriteLine($"projection:{label}; calls:{rounds * players.Length}; milliseconds:{Stopwatch.GetElapsedTime(start).TotalMilliseconds:F3}; bytes:{bytes}");
        }
    }
}
