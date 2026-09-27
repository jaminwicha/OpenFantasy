using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Progression.ResourceHistory;

/// <summary>A reference to admitted causal evidence and its rule pin; not proof of permission or payment.</summary>
public sealed record ResourceChangeCause
{
    public ResourceChangeCause(EntityId evidenceId, EntityId definitionId, ContentHash definitionVersion)
    {
        ArgumentNullException.ThrowIfNull(definitionVersion);
        if (evidenceId == definitionId)
            throw new ArgumentException("Causal evidence cannot alias its rule definition.");
        EvidenceId = evidenceId;
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
    }

    public EntityId EvidenceId { get; }
    public EntityId DefinitionId { get; }
    public ContentHash DefinitionVersion { get; }
}
