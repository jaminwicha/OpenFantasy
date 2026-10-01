using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;

namespace OpenFantasy.Hosting.Tests.Simulation.RoadThreat;

public sealed class RoadThreatObservationRegressionTests
{
    [Fact]
    public void The_distant_farm_does_not_disclose_the_camp_chiefs_condition()
    {
        WorldSnapshot world = RoadThreatJourney.Genesis("road-threat-observation");
        var player = world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        var chief = RoadThreatJourney.Threat(world).ChiefActorId;
        Assert.True(RoadThreatJourney.CentreDistance(world, player, chief)
            > LocalRegionObserver.ObservationRadiusCells * 256);
        Assert.Empty(new LocalWorldView(world, player, false, new LocalRegionObserver(player)).RoadThreats ?? []);
        Assert.Empty(new LocalWorldView(world, player).RoadThreats ?? []);
    }
}
