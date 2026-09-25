namespace OpenFantasy.Core.Production;

/// <summary>
/// One retained disposition of exact raw or produced item rights. This records the material and
/// participants; it grants no care permission, physiological effect or new item output.
/// </summary>
public sealed class ConsumedItemClaim
{
    public ConsumedItemClaim(EntityId consumptionId, EntityId commandId, EntityId actorId,
        EntityId recipientActorId, EntityId custodianId, RetiredIngredient ingredient)
    {
        ArgumentNullException.ThrowIfNull(ingredient);
        if (consumptionId != EntityId.DeriveSpawned(commandId, 0))
            throw new ArgumentException("A consumed item claim must use its command's deterministic consumption identity.", nameof(consumptionId));
        ConsumptionId = consumptionId;
        CommandId = commandId;
        ActorId = actorId;
        RecipientActorId = recipientActorId;
        CustodianId = custodianId;
        Ingredient = ingredient;
    }

    public EntityId ConsumptionId { get; }
    public EntityId CommandId { get; }
    public EntityId ActorId { get; }
    public EntityId RecipientActorId { get; }
    public EntityId CustodianId { get; }
    public RetiredIngredient Ingredient { get; }
}
