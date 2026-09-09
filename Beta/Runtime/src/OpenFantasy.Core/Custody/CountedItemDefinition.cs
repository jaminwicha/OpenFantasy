using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Custody;

/// <summary>Pins the meaning of one counted source unit; payload validation belongs to content admission.</summary>
public sealed record CountedItemDefinition
{
    public CountedItemDefinition(EntityId definitionId, ContentHash version)
    {
        ArgumentNullException.ThrowIfNull(version);
        DefinitionId = definitionId;
        Version = version;
    }

    public EntityId DefinitionId { get; }
    public ContentHash Version { get; }
}
