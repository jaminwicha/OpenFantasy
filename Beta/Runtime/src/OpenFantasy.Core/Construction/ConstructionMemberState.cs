using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;

namespace OpenFantasy.Core.Construction;

public enum ConstructionMemberDisposition
{
    Standing = 1,
    Falling = 2,
    Resting = 3
}

/// <summary>Retained placement and condition of one original physical member; no material is created or retired.</summary>
public sealed class ConstructionMemberState
{
    public ConstructionMemberState(EntityId structureId, uint memberIndex, ulong revision, uint conditionParts,
        SubunitPosition displacement, ConstructionMemberDisposition disposition)
    {
        if (conditionParts > StructuralElement.MaximumConditionParts || !Enum.IsDefined(disposition)
            || disposition == ConstructionMemberDisposition.Standing && displacement != default
            || revision == 0 && (conditionParts != StructuralElement.MaximumConditionParts
                || displacement != default || disposition != ConstructionMemberDisposition.Standing))
        {
            throw new ArgumentException("Member condition and placement must retain a valid disposition and pristine initial state.");
        }
        StructureId = structureId;
        MemberIndex = memberIndex;
        Revision = revision;
        ConditionParts = conditionParts;
        Displacement = displacement;
        Disposition = disposition;
    }

    public EntityId StructureId { get; }
    public uint MemberIndex { get; }
    public EntityId ElementId => ConstructionBearingModel.MemberIdentity(StructureId, MemberIndex);
    public ulong Revision { get; }
    public uint ConditionParts { get; }
    public SubunitPosition Displacement { get; }
    public ConstructionMemberDisposition Disposition { get; }

    public ConstructionMemberState WithState(ulong expectedRevision, uint conditionParts,
        SubunitPosition displacement, ConstructionMemberDisposition disposition)
    {
        if (expectedRevision != Revision)
        {
            throw new ArgumentException("The member revision does not match the retained state.", nameof(expectedRevision));
        }
        if (conditionParts == ConditionParts && displacement == Displacement && disposition == Disposition)
        {
            throw new ArgumentException("A member transition must change its physical state.");
        }
        return new(StructureId, MemberIndex, checked(Revision + 1), conditionParts, displacement, disposition);
    }
}
