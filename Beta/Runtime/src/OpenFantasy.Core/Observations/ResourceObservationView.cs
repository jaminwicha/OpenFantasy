using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Observations;

public enum KnowledgeBasis : ulong
{
    DirectObservation = 1,
    Report = 2,
    Belief = 3,
    Inference = 4,
    DreamImpression = 5
}

/// <summary>
/// Disclosed claims, not authoritative inventory. Subject and material identities must already
/// be known to this observer; confidence is a declared assessment, not a probability of truth.
/// </summary>
public sealed class ResourceObservationView
{
    public const uint MaximumConfidence = 1000;

    public ResourceObservationView(EntityId disclosureId, ulong disclosureRevision,
        EntityId knownSubjectId, CountedItemDefinition knownMaterial, ulong quantity,
        KnowledgeBasis basis, uint confidence, WorldInstant acquiredAt, EntityId? knownReporterId = null)
    {
        ArgumentNullException.ThrowIfNull(knownMaterial);
        if (disclosureRevision == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(disclosureRevision));
        }

        if (!Enum.IsDefined(basis))
        {
            throw new ArgumentOutOfRangeException(nameof(basis));
        }

        if (confidence > MaximumConfidence)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence));
        }

        if (knownReporterId is not null && basis != KnowledgeBasis.Report)
        {
            throw new ArgumentException("Only a report can name a known reporter.", nameof(knownReporterId));
        }

        DisclosureId = disclosureId;
        DisclosureRevision = disclosureRevision;
        KnownSubjectId = knownSubjectId;
        KnownMaterial = knownMaterial;
        Quantity = quantity;
        Basis = basis;
        Confidence = confidence;
        AcquiredAt = acquiredAt;
        KnownReporterId = knownReporterId;
    }

    public EntityId DisclosureId { get; }
    public ulong DisclosureRevision { get; }
    public EntityId KnownSubjectId { get; }
    public CountedItemDefinition KnownMaterial { get; }
    public ulong Quantity { get; }
    public KnowledgeBasis Basis { get; }
    public uint Confidence { get; }
    public WorldInstant AcquiredAt { get; }
    public EntityId? KnownReporterId { get; }
}
