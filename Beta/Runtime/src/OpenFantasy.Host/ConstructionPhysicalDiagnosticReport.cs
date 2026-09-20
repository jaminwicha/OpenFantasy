using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Host;

/// <summary>Read-only retained member state and original material backing; it does not certify structural safety.</summary>
internal sealed class ConstructionPhysicalDiagnosticReport
{
    internal ConstructionPhysicalDiagnosticReport(ConstructionPhysicalSnapshot physical)
    {
        Revision = physical.Revision;
        ContentHash = physical.ContentHash.Value;
        ConstructionHash = physical.ConstructionHash.Value;
        Members = physical.Members.Select(member =>
        {
            var source = physical.GetMemberReference(member.StructureId, member.MemberIndex);
            return new ConstructionPhysicalMemberReport(member.ElementId.ToString(), member.StructureId.ToString(),
                member.MemberIndex, member.Revision, member.ConditionParts, member.Disposition.ToString(),
                member.Displacement, source.InstalledLotId.ToString(), source.Mass.Value, source.Volume.Value);
        }).ToArray();
    }

    public ulong Revision { get; }
    public string ContentHash { get; }
    public string ConstructionHash { get; }
    public IReadOnlyList<ConstructionPhysicalMemberReport> Members { get; }
}

internal sealed record ConstructionPhysicalMemberReport(string ElementId, string StructureId, uint MemberIndex,
    ulong Revision, uint ConditionParts, string Disposition, SubunitPosition Displacement,
    string InstalledLotId, ulong MassMilligrams, ulong VolumeMicrolitres);
