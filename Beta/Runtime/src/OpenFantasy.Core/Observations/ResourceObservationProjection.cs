using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Observations;

/// <summary>
/// A complete replacement of this audience's resource-knowledge view. No private source
/// causes, authority head, suppression reasons or other actors' journal entries are retained.
/// </summary>
public sealed class ResourceObservationProjection
{
    internal ResourceObservationProjection(ObservationAudience audience, EntityId disclosureEpoch,
        ulong disclosureRevision, IEnumerable<ResourceObservationView> entries)
    {
        Audience = audience;
        DisclosureEpoch = disclosureEpoch;
        DisclosureRevision = disclosureRevision;
        Entries = Array.AsReadOnly(entries.ToArray());
    }

    public ObservationAudience Audience { get; }
    public EntityId DisclosureEpoch { get; }
    public ulong DisclosureRevision { get; }
    public IReadOnlyList<ResourceObservationView> Entries { get; }

    public ContentHash ComputeContentHash() => ContentHash.Compute(ObservationEncodingV1.Encode(this));
}
