using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Explicit demonstration load and carried-mass calibrations, never inferred from hardness or contact.</summary>
internal static class WorksiteFarmPolicy
{
    private const ulong RatedCapacityMilligrams = 10_000_000_000;
    private const ulong HumanoidMassMilligrams = 70_000_000;
    private const ulong ToolMassMilligrams = 1_000_000;

    internal static ConstructionLoadPolicy Create(WorldSnapshot world)
    {
        ConstructionBearingTemplate[] templates = world.Construction!.Definitions.Select(CreateTemplate).ToArray();
        StableTerrainBearing[] foundations = world.Space.Regions.SelectMany(region => region.Cells)
            .Where(cell => cell.Stratum == TerrainStratum.Surface
                || cell.Coordinate == WorksiteFarmLayout.SupportCell.Offset(0, 0, -1))
            .Select(cell => new StableTerrainBearing(EntityId.DeriveGenerated(cell.SourceId!.Value.ToByteArray(),
                "worksite-farm.foundation.v1", "stable-substrate", "bearing", 0), cell.Coordinate,
                cell.SourceId.Value, cell.Material!, new MassMilligrams(RatedCapacityMilligrams))).ToArray();
        StructuralBodyMass[] bodies = world.Space.Actors.Select(actor => actor.Condition.Definition)
            .DistinctBy(body => body.DefinitionId)
            .Select(body => new StructuralBodyMass(body.DefinitionId, body.Version, new MassMilligrams(HumanoidMassMilligrams))).ToArray();
        HashSet<CountedItemDefinition> calibrated = world.Production!.Recipes
            .SelectMany(recipe => recipe.Bindings).Select(binding => binding.Item).ToHashSet();
        HashSet<CountedItemDefinition> tools = world.Gathering!.Tools.Select(tool => tool.Definition)
            .Concat(world.Construction.Definitions.Select(definition => definition.RequiredTool.Item)).ToHashSet();
        var supplemental = world.Inventory.Sources.Select(source => source.Definition)
            .Where(item => !calibrated.Contains(item)).Distinct().Select(item =>
            {
                MassMilligrams? substrate = world.Excavation!.Sites.FirstOrDefault(site => site.Material.Item == item)?.Material.Physical.MassPerUnit;
                if (substrate is { } mass) { return new StructuralItemMass(item, mass); }
                if (tools.Contains(item)) { return new StructuralItemMass(item, new MassMilligrams(ToolMassMilligrams)); }
                throw new InvalidDataException("Worksite content includes an item without an explicit carried-mass calibration.");
            });
        return new(world.Space.Scope, templates, foundations, new StructuralMassCatalog(bodies, supplemental));
    }

    private static ConstructionBearingTemplate CreateTemplate(ConstructionDefinition definition)
    {
        var ratings = definition.Members.Select((_, index) => new StructuralMemberRating((uint)index,
            new MassMilligrams(RatedCapacityMilligrams), 4000));
        var joints = definition.Members.SelectMany((upper, upperIndex) => definition.Members
            .Select((lower, lowerIndex) => (Member: lower, Index: lowerIndex))
            .Where(lower => upper.MinZ > 0 && upper.MinZ == lower.Member.MaxZ
                && Math.Min(upper.MaxX, lower.Member.MaxX) > Math.Max(upper.MinX, lower.Member.MinX)
                && Math.Min(upper.MaxY, lower.Member.MaxY) > Math.Max(upper.MinY, lower.Member.MinY))
            .Select(lower => new StructuralMemberJoint((uint)upperIndex, (uint)lower.Index, StructuralJointKind.VerticalBearing)));
        return new(definition.DefinitionId, definition.Version, ratings, joints);
    }

}
