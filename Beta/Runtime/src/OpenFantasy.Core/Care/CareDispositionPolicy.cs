using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Care;

/// <summary>The fixed admitted interpretation of voluntary request closure; it grants no treatment permission.</summary>
public static class CareDispositionPolicy
{
    public static ContentHash RulesPin { get; } = ContentHash.Compute(CanonicalEncodingV1.Frame(
        "care.voluntary-closure.v1",
        "exact-request;patient-cancel;helper-decline;one-terminal;no-resource-effect;per-patient-closed-defeat-frontier"u8.ToArray()));
}
