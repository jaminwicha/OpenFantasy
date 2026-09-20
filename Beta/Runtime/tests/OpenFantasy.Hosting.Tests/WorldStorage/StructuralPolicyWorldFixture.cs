using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

/// <summary>Explicit authored ratings over the real funded construction fixture; not production balance.</summary>
internal sealed class StructuralPolicyWorldFixture
{
    internal StructuralPolicyWorldFixture(Func<int, ulong>? memberCapacity = null,
        ulong foundationCapacity = 1_000_000_000, ulong bodyMass = 70_000_000)
    {
        Stairs = new StairNavigationWorldFixture(physicalConstruction: true);
        Genesis = WorldStructuralPolicyAdmission.CreateGenesis(Stairs.Genesis,
            PolicyFor(Stairs.Genesis, memberCapacity, foundationCapacity, bodyMass));
    }

    internal StairNavigationWorldFixture Stairs { get; }
    internal WorldSnapshot Genesis { get; }

    internal WorldSnapshot FundAndBuild(string path) => Stairs.FundAndBuild(path, Genesis);

    internal static ConstructionLoadPolicy PolicyFor(WorldSnapshot world, Func<int, ulong>? memberCapacity = null,
        ulong foundationCapacity = 1_000_000_000, ulong bodyMass = 70_000_000)
    {
        ConstructionDefinition definition = world.Construction!.Projects.Single().Definition;
        var ratings = definition.Members.Select((_, index) => new StructuralMemberRating((uint)index,
            new(memberCapacity?.Invoke(index) ?? 100_000_000), 4000)).ToArray();
        uint deck = (uint)definition.Members.Select((member, index) => (member, index))
            .Single(value => value.member.MinZ == 2000).index;
        var joints = definition.Members.Select((member, index) => (member, index))
            .Where(value => value.member.MaxZ == 2000 && value.member.MinY == 3750)
            .Select(value => new StructuralMemberJoint(deck, (uint)value.index, StructuralJointKind.VerticalBearing));
        var template = new ConstructionBearingTemplate(definition.DefinitionId, definition.Version, ratings, joints);
        var anchors = world.Space.Regions.SelectMany(region => region.Cells)
            .Where(cell => cell.IsSolid && cell.Coordinate.Z == 0)
            .Select(cell => new StableTerrainBearing(EntityId.DeriveSpawned(cell.SourceId!.Value, 900000), cell.Coordinate,
                cell.SourceId.Value, cell.Material!, new(foundationCapacity)));
        var bodies = world.Space.Actors.Select(actor => actor.Condition.Definition).DistinctBy(value => value.DefinitionId)
            .Select(body => new StructuralBodyMass(body.DefinitionId, body.Version, new(bodyMass)));
        var known = world.Production!.Recipes.SelectMany(recipe => recipe.Bindings).Select(binding => binding.Item).ToHashSet();
        var tools = world.Inventory.Sources.Select(source => source.Definition).Where(item => !known.Contains(item)).Distinct()
            .Select(item => new StructuralItemMass(item, new(1_000_000)));
        return new(world.Space.Scope, [template], anchors, new(bodies, tools));
    }
}
