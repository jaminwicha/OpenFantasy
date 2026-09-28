using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Production;

/// <summary>
/// Exact bounded closing context for a production segment. Created only from the actual supplied
/// owners; it retains neither a parent context nor recursive batch ancestry. Host authentication and
/// permanent historical identity lookup remain separate required authority responsibilities.
/// </summary>
public sealed class ProductionContinuationContext
{
    private ProductionContinuationContext(ProductionContinuationBasis basis, CountedAuthorityState inventory,
        ProductionSnapshot production)
    {
        Basis = basis;
        Ordinal = checked((production.Continuation?.Ordinal ?? 0) + 1);
        BaseProductionRevision = production.Revision;
        BaseBatchCount = production.TotalBatchCount;
        BaseConsumedItemCount = production.TotalConsumedItemCount;
        if (checked(BaseBatchCount + BaseConsumedItemCount) > BaseProductionRevision)
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
        ContentHash = ContentHash.Compute(CanonicalEncodingV1.Frame("production.continuation-context",
            Number(1), basis.ContentHash.ToByteArray(), Number(Ordinal), Number(BaseProductionRevision),
            Number(BaseBatchCount), Number(BaseConsumedItemCount), Number(SupportsConsumption ? 1UL : 0UL), recipes,
            AuthorityAdmissionHash.ToByteArray(), CanonicalEncodingV1.Frame("continuation.storages.v1",
                Storages.Select(value => (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("continuation.storage.v1",
                    value.CustodianId.ToByteArray(), Number(value.UnitCapacity), Number(value.Revision))).ToArray())));
    }

    public ProductionContinuationBasis Basis { get; }
    public ulong Ordinal { get; }
    public ulong BaseProductionRevision { get; }
    public ulong BaseBatchCount { get; }
    public ulong BaseConsumedItemCount { get; }
    public bool SupportsConsumption { get; }
    public ReadOnlyCollection<CraftRecipeDefinition> Recipes { get; }
    public ReadOnlyCollection<CustodianStorage> Storages { get; }
    public ContentHash AuthorityAdmissionHash { get; }
    public ContentHash ContentHash { get; }

    internal static ProductionContinuationContext Capture(CountedAuthorityState inventory, ProductionSnapshot production) =>
        new(ProductionContinuationBasis.Capture(inventory, production), inventory, production);

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
