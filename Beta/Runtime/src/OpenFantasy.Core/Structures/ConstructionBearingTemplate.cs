using System.Collections.ObjectModel;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Structures;

public enum StructuralJointKind { VerticalBearing, BracedFace }

/// <summary>A capacity valid only inside its declared member span and required support count.</summary>
public sealed record StructuralMemberRating(uint MemberIndex, MassMilligrams Capacity,
    uint MaximumSpanMillimetres, ushort MinimumSupportCount = 1);

public sealed record StructuralMemberJoint(uint SupportedMemberIndex, uint SupportingMemberIndex,
    StructuralJointKind Kind);

/// <summary>Explicit structural interpretation of one exact fabricated definition; contact and hardness imply no rating.</summary>
public sealed class ConstructionBearingTemplate
{
    public ConstructionBearingTemplate(EntityId definitionId, ContentHash definitionVersion,
        IEnumerable<StructuralMemberRating> members, IEnumerable<StructuralMemberJoint> joints)
    {
        ArgumentNullException.ThrowIfNull(definitionVersion);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(joints);
        var ratings = members.Take(ConstructionDefinition.MaximumMembers + 1).OrderBy(value => value.MemberIndex).ToArray();
        var connections = joints.Take(ConstructionDefinition.MaximumMembers * ConstructionDefinition.MaximumMembers + 1)
            .OrderBy(value => value.SupportedMemberIndex).ThenBy(value => value.SupportingMemberIndex).ToArray();
        if (ratings.Length is 0 or > ConstructionDefinition.MaximumMembers
            || ratings.Where((value, index) => value.MemberIndex != index || value.Capacity.Value == 0
                || value.MaximumSpanMillimetres is 0 or > 8000 || value.MinimumSupportCount is 0 or > 32).Any()
            || connections.Length > ConstructionDefinition.MaximumMembers * ConstructionDefinition.MaximumMembers
            || connections.Any(value => value.SupportedMemberIndex >= ratings.Length || value.SupportingMemberIndex >= ratings.Length
                || value.SupportedMemberIndex == value.SupportingMemberIndex || !Enum.IsDefined(value.Kind))
            || connections.Select(value => (value.SupportedMemberIndex, value.SupportingMemberIndex)).Distinct().Count() != connections.Length)
        {
            throw new ArgumentException("Structural ratings require complete bounded member ordinals and unique declared joints.");
        }
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        Members = Array.AsReadOnly(ratings);
        Joints = Array.AsReadOnly(connections);
        Version = ContentHash.Compute(CanonicalEncodingV1.Frame("construction.bearing-template.v1",
            definitionId.ToByteArray(), definitionVersion.ToByteArray(),
            CanonicalEncodingV1.Frame("structure.member-ratings", ratings.Select(value =>
                (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("structure.member-rating",
                    CanonicalEncodingV1.Unsigned64(value.MemberIndex), CanonicalEncodingV1.Unsigned64(value.Capacity.Value),
                    CanonicalEncodingV1.Unsigned64(value.MaximumSpanMillimetres), CanonicalEncodingV1.Unsigned64(value.MinimumSupportCount))).ToArray()),
            CanonicalEncodingV1.Frame("structure.member-joints", connections.Select(value =>
                (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("structure.member-joint",
                    CanonicalEncodingV1.Unsigned64(value.SupportedMemberIndex), CanonicalEncodingV1.Unsigned64(value.SupportingMemberIndex),
                    CanonicalEncodingV1.Unsigned64((ulong)value.Kind))).ToArray())));
    }

    public EntityId DefinitionId { get; }
    public ContentHash DefinitionVersion { get; }
    public ReadOnlyCollection<StructuralMemberRating> Members { get; }
    public ReadOnlyCollection<StructuralMemberJoint> Joints { get; }
    public ContentHash Version { get; }

    internal void ValidateFor(ConstructionDefinition definition)
    {
        if (definition.DefinitionId != DefinitionId || definition.Version != DefinitionVersion
            || definition.Members.Count != Members.Count)
        {
            throw new ArgumentException("A structural template must match the exact complete fabricated definition.");
        }
        foreach (StructuralMemberRating rating in Members)
        {
            MillimetreBox member = definition.Members[(int)rating.MemberIndex];
            if (Math.Max(member.MaxX - member.MinX, member.MaxY - member.MinY) > rating.MaximumSpanMillimetres)
            {
                throw new ArgumentException("A member exceeds its explicitly rated horizontal span.");
            }
        }
        foreach (StructuralMemberJoint joint in Joints)
        {
            MillimetreBox upper = definition.Members[(int)joint.SupportedMemberIndex];
            MillimetreBox lower = definition.Members[(int)joint.SupportingMemberIndex];
            if (upper.MinZ == 0 || JointArea(upper, lower, joint.Kind) == 0)
            {
                throw new ArgumentException("Declared bearings require a positive physical face and cannot override a ground member.");
            }
        }
    }

    internal static ulong JointArea(MillimetreBox upper, MillimetreBox lower, StructuralJointKind kind)
    {
        long x = Math.Max(0, Math.Min(upper.MaxX, lower.MaxX) - Math.Max(upper.MinX, lower.MinX));
        long y = Math.Max(0, Math.Min(upper.MaxY, lower.MaxY) - Math.Max(upper.MinY, lower.MinY));
        long z = Math.Max(0, Math.Min(upper.MaxZ, lower.MaxZ) - Math.Max(upper.MinZ, lower.MinZ));
        if (kind == StructuralJointKind.VerticalBearing)
        {
            return upper.MinZ == lower.MaxZ ? checked((ulong)x * (ulong)y) : 0;
        }
        if (upper.MaxX == lower.MinX || upper.MinX == lower.MaxX) { return checked((ulong)y * (ulong)z); }
        if (upper.MaxY == lower.MinY || upper.MinY == lower.MaxY) { return checked((ulong)x * (ulong)z); }
        return 0;
    }
}
