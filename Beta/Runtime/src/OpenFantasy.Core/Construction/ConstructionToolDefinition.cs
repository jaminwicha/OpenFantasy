using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Construction;

public enum ConstructionToolOperation { CutCrossSlots = 1 }
public enum ConstructionStockInterpretation { AuthoredJoineryBlankStock = 1 }
public enum ConstructionStockLocation { BuilderCarried = 1 }

/// <summary>An explicitly admitted tool operation, never inferred from an actor's sprite or class.</summary>
public sealed class ConstructionToolDefinition
{
    public ConstructionToolDefinition(EntityId definitionId, CountedItemDefinition item, ConstructionToolOperation operation)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (definitionId == item.DefinitionId || !Enum.IsDefined(operation))
        {
            throw new ArgumentException("The tool qualification requires a distinct definition and known operation.");
        }
        DefinitionId = definitionId;
        Item = item;
        Operation = operation;
        Version = ContentHash.Compute(ConstructionCodecV1.EncodeTool(this));
    }

    public EntityId DefinitionId { get; }
    public CountedItemDefinition Item { get; }
    public ConstructionToolOperation Operation { get; }
    public ContentHash Version { get; }
}
