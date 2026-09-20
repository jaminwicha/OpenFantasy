using OpenFantasy.Core.Structures;

namespace OpenFantasy.Host;

/// <summary>Retained policy identity and finite catalog sizes; reporting performs no load binding or resolution.</summary>
internal sealed class ConstructionLoadPolicyDiagnosticReport
{
    internal ConstructionLoadPolicyDiagnosticReport(ConstructionLoadPolicy policy)
    {
        ContentHash = policy.ContentHash.Value;
        TemplateCount = policy.Templates.Count;
        FoundationCount = policy.Foundations.Count;
        BodyDefinitionCount = policy.Masses.Bodies.Count;
        SupplementalItemCount = policy.Masses.SupplementalItems.Count;
    }

    public string ContentHash { get; }
    public int TemplateCount { get; }
    public int FoundationCount { get; }
    public int BodyDefinitionCount { get; }
    public int SupplementalItemCount { get; }
}
