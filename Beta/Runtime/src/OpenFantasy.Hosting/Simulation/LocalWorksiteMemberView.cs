using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>
/// One actual installed member at its committed pose. Geometry is exact local millimetres at Origin;
/// Bounds is its outward-rounded subunit collision envelope, not a material quantity.
/// </summary>
public sealed record LocalWorksiteMemberView(EntityId StructureId, uint MemberIndex, ulong Revision,
    uint ConditionParts, ConstructionMemberDisposition Disposition, SubunitPosition Origin,
    MillimetreBox Geometry, SubunitBox Bounds);
