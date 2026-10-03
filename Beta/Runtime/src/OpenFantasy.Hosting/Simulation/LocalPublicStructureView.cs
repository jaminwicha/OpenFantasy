using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Actual visible member geometry, without condition, work, stock or construction-history claims.</summary>
public sealed record LocalPublicStructureMemberView
{
    public LocalPublicStructureMemberView(uint memberIndex, SubunitPosition origin, MillimetreBox geometry, SubunitBox collision)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(collision);
        if (memberIndex >= ConstructionDefinition.MaximumMembers || geometry.At(origin) != collision)
            throw new ArgumentException("A structure member requires its bounded exact geometry and collision envelope.");
        MemberIndex = memberIndex; Origin = origin; Geometry = geometry; Collision = collision;
    }
    public uint MemberIndex { get; }
    public SubunitPosition Origin { get; }
    public MillimetreBox Geometry { get; }
    public SubunitBox Collision { get; }
}

/// <summary>One installed structure in the sight-complete locality, independent of production permission.</summary>
public sealed class LocalPublicStructureView
{
    public LocalPublicStructureView(EntityId structureId, SubunitPosition origin, IEnumerable<LocalPublicStructureMemberView> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        var captured = members.Take(ConstructionDefinition.MaximumMembers + 1).ToArray();
        if (structureId == default || captured.Length is 0 or > ConstructionDefinition.MaximumMembers
            || captured.Any(member => member is null) || captured.Select(member => member.MemberIndex).Distinct().Count() != captured.Length)
            throw new ArgumentException("Public structures require bounded unique installed members.");
        StructureId = structureId; Origin = origin;
        Members = Array.AsReadOnly(captured.OrderBy(member => member.MemberIndex).ToArray());
    }
    public EntityId StructureId { get; }
    public SubunitPosition Origin { get; }
    public ReadOnlyCollection<LocalPublicStructureMemberView> Members { get; }

    internal static ReadOnlyCollection<LocalPublicStructureView> Project(WorldSnapshot state)
    {
        var structures = state.Construction?.Projects.Where(project => project.Installed is not null)
            .OrderBy(project => project.Installed!.StructureId).Select(project =>
            {
                EntityId id = project.Installed!.StructureId;
                var actual = state.ConstructionPhysical?.Members.Where(member => member.StructureId == id)
                    .ToDictionary(member => member.MemberIndex);
                var live = state.Space.Structures.Single(structure => structure.StructureId == id);
                var members = project.Definition.Members.Select((geometry, index) =>
                {
                    var offset = actual is null ? default : actual[checked((uint)index)].Displacement;
                    var origin = project.SiteFeet.Offset(offset.X, offset.Y, offset.Z);
                    return new LocalPublicStructureMemberView(checked((uint)index), origin, geometry, geometry.At(origin));
                }).Where(member => live.Boxes.Contains(member.Collision)).ToArray();
                if (members.Length != live.Boxes.Count)
                    throw new InvalidOperationException("Installed geometry disagrees with its retained live collision owner.");
                return new LocalPublicStructureView(id, project.SiteFeet, members);
            }).ToArray() ?? [];
        return Array.AsReadOnly(structures);
    }
}
