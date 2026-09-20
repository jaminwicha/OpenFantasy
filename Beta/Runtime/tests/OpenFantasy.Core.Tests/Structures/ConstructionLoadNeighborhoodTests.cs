using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Tests.Construction;
using OpenFantasy.Core.Tests.Gathering;
using OpenFantasy.Core.Tests.Worlds;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Structures;

public sealed class ConstructionLoadNeighborhoodTests
{
    [Fact]
    public void A_changed_foundation_reaches_its_dependent_actor_and_the_actors_alternate_support()
    {
        ConstructionBearingModel model = Model();
        EntityId actorId = AdmissionFixture.Id(10);
        EntityId actorElement = ConstructionBearingModel.ActorIdentity(actorId);
        ConstructionLoadNeighborhood fromFoundation = ConstructionLoadNeighborhood.Create(model, [Id(1)]);
        EntityId[] expected = new[] { Id(1), Id(2), actorElement }.Order().ToArray();
        Assert.Equal(expected, fromFoundation.ElementIds);
        Assert.Equal(new[] { actorId }, fromFoundation.ActorIds);
        Assert.Empty(fromFoundation.StructureIds);
        Assert.Equal(model.InputPin, fromFoundation.InputPin);
        Assert.Equal(new[] { Id(1) }, fromFoundation.ChangedElementIds);
        Assert.DoesNotContain(Id(3), fromFoundation.ElementIds);
        Assert.DoesNotContain(ConstructionBearingModel.ActorIdentity(AdmissionFixture.Id(11)), fromFoundation.ElementIds);
        Assert.Equal(expected, ConstructionLoadNeighborhood.Create(model, [actorElement]).ElementIds);
        Assert.Equal(expected, ConstructionLoadNeighborhood.Create(model, [Id(2)]).ElementIds);
    }

    [Fact]
    public void Multiple_seed_components_are_canonical_copied_and_read_only()
    {
        ConstructionBearingModel model = Model();
        List<EntityId> seeds = [Id(4), Id(1)];
        ConstructionLoadNeighborhood forward = ConstructionLoadNeighborhood.Create(model, seeds);
        ConstructionLoadNeighborhood reverse = ConstructionLoadNeighborhood.Create(model, seeds.AsEnumerable().Reverse());
        seeds.Clear();
        Assert.Equal(new[] { Id(1), Id(4) }, forward.ChangedElementIds);
        Assert.Equal(forward.ChangedElementIds, reverse.ChangedElementIds);
        Assert.Equal(forward.ElementIds, reverse.ElementIds);
        Assert.Equal(forward.ActorIds, reverse.ActorIds);
        Assert.Equal(model.Graph.Elements.Select(element => element.ElementId).Order(), forward.ElementIds);
        Assert.Equal(new[] { AdmissionFixture.Id(10), AdmissionFixture.Id(11) }, forward.ActorIds);
        Assert.Throws<NotSupportedException>(() => ((IList<EntityId>)forward.ChangedElementIds).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<EntityId>)forward.ElementIds).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<EntityId>)forward.StructureIds).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<EntityId>)forward.ActorIds).Clear());
        Assert.Equal(model.InputPin, reverse.InputPin);
    }

    [Fact]
    public void Empty_duplicate_unknown_and_excessive_seeds_are_rejected_before_closure()
    {
        ConstructionBearingModel model = Model();
        Assert.Throws<ArgumentNullException>(() => ConstructionLoadNeighborhood.Create(null!, [Id(1)]));
        Assert.Throws<ArgumentNullException>(() => ConstructionLoadNeighborhood.Create(model, null!));
        Assert.Throws<ArgumentException>(() => ConstructionLoadNeighborhood.Create(model, []));
        Assert.Throws<ArgumentException>(() => ConstructionLoadNeighborhood.Create(model, [Id(1), Id(1)]));
        Assert.Throws<ArgumentException>(() => ConstructionLoadNeighborhood.Create(model, [Id(9999)]));
        int enumerated = 0;
        IEnumerable<EntityId> Excessive()
        {
            while (true)
            {
                enumerated++;
                yield return Id((ulong)enumerated);
            }
        }
        Assert.Throws<ArgumentException>(() => ConstructionLoadNeighborhood.Create(model, Excessive()));
        Assert.Equal(StructuralLoadGraph.MaximumElements + 1, enumerated);
    }

    private static ConstructionBearingModel Model()
    {
        WorldSnapshot basis = ConstructionWorldFixture.Create();
        EntityId player = AdmissionFixture.Id(10);
        ActorPresence[] actors = basis.Space.Actors.Select(actor => new ActorPresence(actor.Binding, actor.Condition,
            actor.Scope, new(actor.ActorId == player ? 256 : 768, 128, 256), actor.Revision, actor.HomePlaceId)).ToArray();
        var space = new LocalSpatialSnapshot(basis.Space.Scope, basis.Space.Regions, actors);
        WorldSnapshot world = ConstructionWorldFixture.Copy(basis, space: space);
        TerrainRegionSnapshot terrain = world.Space.Regions.Single();
        StableTerrainBearing[] anchors = Enumerable.Range(0, 4).Select(x =>
        {
            TerrainCell cell = terrain.GetCell(new(x, 0, 0));
            return new StableTerrainBearing(Id((ulong)x + 1), cell.Coordinate, cell.SourceId!.Value, cell.Material!, new(1_000_000));
        }).ToArray();
        var masses = new StructuralMassCatalog(actors.Select(actor => new StructuralBodyMass(actor.Binding.DefinitionId,
            actor.Binding.DefinitionVersion, new(1000))).DistinctBy(body => body.DefinitionId),
            [new(GatheringFixture.Axe, new(50)), new(world.Construction!.Definitions.Single().RequiredTool.Item, new(75))]);
        return ConstructionBearingModel.Bind(world, [], anchors, masses);
    }

    private static EntityId Id(ulong value) => new(0x6e65696768626f72, value);
}
