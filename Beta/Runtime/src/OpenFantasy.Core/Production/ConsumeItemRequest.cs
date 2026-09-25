using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Production;

/// <summary>An exact owned-material selection; medical permission and effects require their own owner.</summary>
public sealed class ConsumeItemRequest
{
    public ConsumeItemRequest(EntityId custodianId, ulong expectedStorageRevision, ulong expectedProductionRevision,
        EntityId recipientActorId, CountedItemDefinition expectedDefinition, IngredientRequest ingredient)
    {
        ArgumentNullException.ThrowIfNull(expectedDefinition);
        ArgumentNullException.ThrowIfNull(ingredient);
        CustodianId = custodianId;
        ExpectedStorageRevision = expectedStorageRevision;
        ExpectedProductionRevision = expectedProductionRevision;
        RecipientActorId = recipientActorId;
        ExpectedDefinition = expectedDefinition;
        Ingredient = ingredient;
    }

    public EntityId CustodianId { get; }
    public ulong ExpectedStorageRevision { get; }
    public ulong ExpectedProductionRevision { get; }
    public EntityId RecipientActorId { get; }
    public CountedItemDefinition ExpectedDefinition { get; }
    public IngredientRequest Ingredient { get; }
}
