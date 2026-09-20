using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class CoupledFoundationAdmissionTests
{
    [Fact]
    public void Unowned_rated_foundation_removal_is_counterfactual_only_and_cannot_enter_a_coupled_world()
    {
        var fixture = new CoupledSettlementWorldFixture();
        WorldSnapshot basis = fixture.Genesis;
        Assert.Equal((ushort)17, basis.ProfileVersion);
        Assert.Empty(basis.Space.Structures);
        Assert.Empty(basis.ConstructionPhysical!.Members);

        CellCoordinate coordinate = CoupledSettlementWorldFixture.LandingCell;
        StableTerrainBearing anchor = Assert.Single(basis.StructuralPolicy!.Foundations,
            value => value.Cell == coordinate);
        TerrainRegionSnapshot originalRegion = Assert.Single(basis.Space.Regions,
            region => region.Bounds.Contains(coordinate));
        TerrainCell original = originalRegion.GetCell(coordinate);
        Assert.True(original.IsSolid);
        Assert.False(original.IsExcavated);
        Assert.Equal(anchor.SourceId, original.SourceId);
        Assert.Equal(anchor.Material, original.Material);
        Assert.DoesNotContain(basis.Excavation!.Sites,
            site => site.Cell == coordinate || site.TerrainSourceId == anchor.SourceId);

        TerrainRegionSnapshot excavated = originalRegion.Excavate(originalRegion.Revision, [coordinate]);
        TerrainCell proposed = excavated.GetCell(coordinate);
        Assert.True(proposed.IsExcavated);
        Assert.Equal(original.SourceId, proposed.SourceId);
        Assert.Equal(original.FormationId, proposed.FormationId);
        Assert.Equal(original.Material, proposed.Material);
        TerrainRegionSnapshot[] regions = basis.Space.Regions.Select(region =>
            ReferenceEquals(region, originalRegion) ? excavated : region).ToArray();

        // Geometry alone is clear: the unoccupied, unused rated cell supplies no current body or member.
        var space = new LocalSpatialSnapshot(basis.Space.Scope, regions, basis.Space.Actors,
            basis.Space.Structures, basis.Space.GroundSupport, basis.Space.StructureSupport);
        Assert.All(space.Actors, actor => Assert.Equal(SpatialOccupancy.Clear,
            space.QueryOccupancy(actor.ActorId, actor.Feet)));

        // Explicit diagnostic proposals may inspect lost support without claiming paid excavation.
        var scene = new ConstructionContactScene(basis, regions, basis.ConstructionPhysical!, basis.Space.Actors);
        ConstructionBearingModel diagnostic = basis.StructuralPolicy!.AssessContacts(scene);
        Assert.DoesNotContain(diagnostic.Graph.Elements, element => element.ElementId == anchor.ElementId);
        Assert.Equal(scene.InputPin, diagnostic.CandidateScenePin);
        Assert.Equal(basis.StructuralPolicy!.ContentHash, diagnostic.PolicyPin);

        Assert.Throws<ArgumentException>(() => new WorldSnapshot(basis.Inventory, space, basis.Residents,
            basis.AdmissionVersion, basis.Calendar, basis.Revision, basis.AdvancingSteps, basis.Dialogues,
            basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            basis.Watershed, basis.WatershedNetwork, basis.Reservoirs, basis.Transport, basis.Excavation,
            basis.ConstructionPhysical, basis.StructuralPolicy, basis.StructuralSettlementPolicy,
            basis.ConsequencePolicy, basis.PendingExcavation, basis.CoupledSettlementPolicy));
        Assert.False(originalRegion.GetCell(coordinate).IsExcavated);
    }
}
