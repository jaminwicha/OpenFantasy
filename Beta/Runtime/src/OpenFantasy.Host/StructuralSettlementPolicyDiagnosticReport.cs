using OpenFantasy.Core.Structures;

namespace OpenFantasy.Host;

internal sealed class StructuralSettlementPolicyDiagnosticReport
{
    internal StructuralSettlementPolicyDiagnosticReport(StructuralSettlementPolicy policy)
    {
        ContentHash = policy.ContentHash.Value;
        MaximumDropSubunits = policy.MaximumDropSubunits;
        SafeActorDropSubunits = policy.SafeActorDropSubunits;
        SafeMemberDropSubunits = policy.SafeMemberDropSubunits;
        GravityMillimetresPerSecondSquared = policy.GravityMillimetresPerSecondSquared;
    }

    public string ContentHash { get; }
    public long MaximumDropSubunits { get; }
    public long SafeActorDropSubunits { get; }
    public long SafeMemberDropSubunits { get; }
    public ulong GravityMillimetresPerSecondSquared { get; }
}
