using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Production;

/// <summary>
/// Nonrecursive identity and recipe pins of an actual live output's producer. The batch digest
/// identifies its complete canonical record; it is not an independently authenticated provenance proof.
/// </summary>
public sealed class ProductionBatchWitness
{
    internal ProductionBatchWitness(ProductionBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        BatchId = batch.BatchId;
        CommandId = batch.CommandId;
        ActorId = batch.ActorId;
        CustodianId = batch.CustodianId;
        RecipeId = batch.RecipeId;
        RecipeVersion = batch.RecipeVersion;
        CanonicalBatchHash = ContentHash.Compute(ProductionCodecV1.EncodeBatch(batch));
    }

    public EntityId BatchId { get; }
    public EntityId CommandId { get; }
    public EntityId ActorId { get; }
    public EntityId CustodianId { get; }
    public EntityId RecipeId { get; }
    public ContentHash RecipeVersion { get; }
    public ContentHash CanonicalBatchHash { get; }
}
