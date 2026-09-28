using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;

namespace OpenFantasy.Core.Production;

/// <summary>
/// Exact bounded closing context for a production segment. Created only from the actual supplied
/// owners; it retains neither a parent context nor recursive batch ancestry. Host authentication and
/// permanent historical identity lookup remain separate required authority responsibilities.
/// </summary>
public sealed class ProductionContinuationContext
{
    private ProductionContinuationContext(ProductionContinuationBasis basis, CountedAuthorityState inventory,
        ProductionSnapshot production, bool supportsMaterialOperations = false, GatheringSnapshot? gathering = null)
    {
        Basis = basis;
        Ordinal = checked((production.Continuation?.Ordinal ?? 0) + 1);
        BaseProductionRevision = production.Revision;
        BaseBatchCount = production.TotalBatchCount;
        BaseConsumedItemCount = production.TotalConsumedItemCount;
        SupportsMaterialOperations = supportsMaterialOperations;
        BaseMaterialOperationCount = production.TotalMaterialOperationCount;
        GatheringBaseline = gathering;
        if (checked(BaseBatchCount + BaseConsumedItemCount) > BaseProductionRevision
            || BaseMaterialOperationCount > BaseProductionRevision)
            throw new ArgumentException("A continuation boundary requires a cumulative revision for every retained operation.");
        SupportsConsumption = production.SupportsConsumption;
        Recipes = Array.AsReadOnly(production.Recipes.ToArray());
        Storages = Array.AsReadOnly(inventory.Custody.Storages.Select(value =>
            new CustodianStorage(value.CustodianId, value.UnitCapacity, value.Revision)).ToArray());
        AuthorityAdmissionHash = AdmissionHash(inventory);
        byte[] recipes = CanonicalEncodingV1.Frame("production.recipes",
            Recipes.Select(value => (ReadOnlyMemory<byte>)ProductionCodecV1.EncodeRecipe(value)).ToArray());
        if (recipes.Length > 5 * 1024 * 1024)
            throw new ArgumentException("The continuation recipe admission exceeds its bounded frame.");
        List<ReadOnlyMemory<byte>> fields = [
            Number(supportsMaterialOperations ? 2UL : 1UL), basis.ContentHash.ToByteArray(), Number(Ordinal), Number(BaseProductionRevision),
            Number(BaseBatchCount), Number(BaseConsumedItemCount), Number(SupportsConsumption ? 1UL : 0UL), recipes,
            AuthorityAdmissionHash.ToByteArray(), CanonicalEncodingV1.Frame("continuation.storages.v1",
                Storages.Select(value => (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("continuation.storage.v1",
                    value.CustodianId.ToByteArray(), Number(value.UnitCapacity), Number(value.Revision))).ToArray())];
        if (supportsMaterialOperations)
        {
            ValidateGathering(inventory, basis, gathering);
            fields.Add(Number(BaseMaterialOperationCount));
            fields.Add(gathering is null ? Array.Empty<byte>() : GatheringCodecV1.Encode(gathering));
        }
        ContentHash = ContentHash.Compute(CanonicalEncodingV1.Frame("production.continuation-context", fields.ToArray()));
    }

    public ProductionContinuationBasis Basis { get; }
    public ulong Ordinal { get; }
    public ulong BaseProductionRevision { get; }
    public ulong BaseBatchCount { get; }
    public ulong BaseConsumedItemCount { get; }
    public bool SupportsConsumption { get; }
    public bool SupportsMaterialOperations { get; }
    public ulong BaseMaterialOperationCount { get; }
    public GatheringSnapshot? GatheringBaseline { get; }
    public ReadOnlyCollection<CraftRecipeDefinition> Recipes { get; }
    public ReadOnlyCollection<CustodianStorage> Storages { get; }
    public ContentHash AuthorityAdmissionHash { get; }
    public ContentHash ContentHash { get; }

    internal static ProductionContinuationContext Capture(CountedAuthorityState inventory, ProductionSnapshot production) =>
        new(ProductionContinuationBasis.Capture(inventory, production), inventory, production,
            production.MaterialHistory is not null, production.MaterialHistory?.Gathering);

    internal static ProductionContinuationContext CaptureMaterial(CountedAuthorityState inventory,
        ProductionSnapshot production, GatheringSnapshot? gathering) =>
        new(ProductionContinuationBasis.Capture(inventory, production), inventory, production, true, gathering);

    private static void ValidateGathering(CountedAuthorityState inventory, ProductionContinuationBasis basis, GatheringSnapshot? gathering)
    {
        if (gathering is null) return;
        if (gathering.WorldId != inventory.WorldId || gathering.BranchId != inventory.BranchId
            || !inventory.Actors.SequenceEqual(gathering.Actors.Select(value => value.ActorId)))
            throw new ArgumentException("Material source admission requires exact scope and admitted actor work owners.");
        foreach (GatherNodeState node in gathering.Nodes)
        {
            var source = inventory.Sources.FirstOrDefault(value => value.SourceId == node.SourceId);
            if (source is null || source.Definition != node.Definition.YieldDefinition || source.UnitCapacity != node.UnitCapacity
                || basis.RawLots.Any(value => value.Source.SourceId == node.SourceId && value.Source.End > node.NextUnissuedUnit)
                || basis.RawRetirements.Any(value => value.Source.SourceId == node.SourceId && value.Source.End > node.NextUnissuedUnit))
                throw new ArgumentException("Material rights must fit the actual admitted gathering source cursor, not just its capacity.");
        }
        if (gathering.Tools.Any(tool => !inventory.Sources.Any(source => source.Definition == tool.Definition)))
            throw new ArgumentException("Gathering tools require their exact admitted item pins.");
    }

    // This digest freezes admission independently of material contents and mutable storage clocks.
    // It is an observation, not an alternate active inventory or a grant of source issuance rights.
    internal static ContentHash AdmissionHash(CountedAuthorityState inventory)
    {
        var normalizedCustody = new CustodySnapshot(inventory.WorldId, inventory.BranchId, 0,
            inventory.Custody.Storages.Select(value => value with { Revision = 0 }), []);
        return inventory.WithCustody(normalizedCustody).ContentHash;
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
}
