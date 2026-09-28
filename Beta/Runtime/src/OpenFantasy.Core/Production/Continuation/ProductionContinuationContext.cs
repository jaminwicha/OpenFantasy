using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Production;

/// <summary>
/// Exact bounded closing context for a production segment. Created only from the actual supplied
/// owners; it retains neither a parent context nor recursive batch ancestry. Host authentication and
/// permanent historical identity lookup remain separate required authority responsibilities.
/// </summary>
public sealed class ProductionContinuationContext
{
    private ProductionContinuationContext(ProductionContinuationBasis basis, CountedAuthorityState inventory,
        ProductionSnapshot production, bool supportsMaterialOperations = false, GatheringSnapshot? gathering = null,
        ConstructionSnapshot? construction = null, bool supportsConstruction = false, TerrainRepresentationBoundary? terrainRepresentation = null,
        TransportMaterialBoundary? reservedTransport = null, ExcavationMaterialBoundary? excavation = null,
        bool completeWorld = false)
    {
        Basis = basis;
        Ordinal = checked((production.Continuation?.Ordinal ?? 0) + 1);
        BaseProductionRevision = production.Revision;
        BaseBatchCount = production.TotalBatchCount;
        BaseConsumedItemCount = production.TotalConsumedItemCount;
        SupportsMaterialOperations = supportsMaterialOperations;
        BaseMaterialOperationCount = production.TotalMaterialOperationCount;
        GatheringBaseline = gathering;
        SupportsConstruction = supportsConstruction;
        TerrainRepresentation = terrainRepresentation;
        ReservedTransport = reservedTransport;
        Excavation = excavation;
        if ((terrainRepresentation is not null ? 1 : 0) + (reservedTransport is not null ? 1 : 0) + (excavation is not null ? 1 : 0) > 1)
            throw new ArgumentException("Terrain and reserved transport require distinct explicitly qualified profiles.");
        SupportsCompleteWorld = completeWorld;
        if (completeWorld && (supportsMaterialOperations || supportsConstruction || terrainRepresentation is not null
                || reservedTransport is not null || excavation is not null || construction is null)
            || !completeWorld && production.Continuation?.SupportsCompleteWorld == true)
            throw new ArgumentException("The coordinated world keeps custody, work and construction with their actual owners.");
        // The coordinated world retains original installed construction batches without ordered operations.
        bool retainsConstruction = supportsConstruction || completeWorld;
        ConstructionRecipeIds = Array.AsReadOnly((construction?.Definitions.Select(value => value.MaterialTransform.DefinitionId)
            ?? production.Continuation?.ConstructionRecipeIds.AsEnumerable() ?? Array.Empty<EntityId>()).Order().ToArray());
        if (ConstructionRecipeIds.Count > ConstructionSnapshot.MaximumDefinitions
            || !retainsConstruction && ConstructionRecipeIds.Count != 0
            || supportsConstruction && ConstructionRecipeIds.Count == 0
            || production.Continuation is { } prior && (prior.SupportsConstruction || prior.SupportsCompleteWorld)
                && !ConstructionRecipeIds.SequenceEqual(prior.ConstructionRecipeIds))
            throw new ArgumentException("Construction continuation retains its exact bounded admitted recipe catalog.");
        ConstructionBatches = ConstructionBatchArchive.Capture(production, basis, construction, retainsConstruction, ConstructionRecipeIds);
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
            Number(completeWorld ? 7UL : excavation is not null ? 6UL : reservedTransport is not null ? 5UL : terrainRepresentation is not null ? 4UL : supportsConstruction ? 3UL : supportsMaterialOperations ? 2UL : 1UL), basis.ContentHash.ToByteArray(), Number(Ordinal), Number(BaseProductionRevision),
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
        if (supportsConstruction)
        {
            if (!supportsMaterialOperations) throw new ArgumentException("Construction continuation requires ordered material history.");
            fields.Add(CanonicalEncodingV1.Frame("continuation.construction-recipes.v1", ConstructionRecipeIds
                .Select(value => (ReadOnlyMemory<byte>)value.ToByteArray()).ToArray()));
            fields.Add(CanonicalEncodingV1.Frame("continuation.construction-batches.v1", ConstructionBatches
                .Select(value => (ReadOnlyMemory<byte>)ProductionCodecV1.EncodeBatch(value)).ToArray()));
        }
        if (terrainRepresentation is not null)
        {
            if (!supportsMaterialOperations) throw new ArgumentException("Terrain representation requires ordered material history.");
            fields.Add(Number(supportsConstruction ? 1UL : 0UL));
            fields.Add(terrainRepresentation.ContentHash.ToByteArray());
        }
        if (reservedTransport is not null)
        {
            if (!supportsMaterialOperations) throw new ArgumentException("Reserved transport requires ordered material history.");
            fields.Add(Number(supportsConstruction ? 1UL : 0UL));
            fields.Add(reservedTransport.ContentHash.ToByteArray());
        }
        if (excavation is not null)
        {
            if (!supportsMaterialOperations || gathering is null)
                throw new ArgumentException("Excavation requires ordered material history and its shared effort owner.");
            fields.Add(Number(supportsConstruction ? 1UL : 0UL));
            fields.Add(excavation.ContentHash.ToByteArray());
        }
        if (completeWorld)
        {
            fields.Add(CanonicalEncodingV1.Frame("continuation.construction-recipes.v1", ConstructionRecipeIds
                .Select(value => (ReadOnlyMemory<byte>)value.ToByteArray()).ToArray()));
            fields.Add(CanonicalEncodingV1.Frame("continuation.construction-batches.v1", ConstructionBatches
                .Select(value => (ReadOnlyMemory<byte>)ProductionCodecV1.EncodeBatch(value)).ToArray()));
        }
        ContentHash =ContentHash.Compute(CanonicalEncodingV1.Frame("production.continuation-context", fields.ToArray()));
    }

    public ProductionContinuationBasis Basis { get; }
    public ulong Ordinal { get; }
    public ulong BaseProductionRevision { get; }
    public ulong BaseBatchCount { get; }
    public ulong BaseConsumedItemCount { get; }
    public bool SupportsConsumption { get; }
    public bool SupportsMaterialOperations { get; }
    public bool SupportsConstruction { get; }
    /// <summary>
    /// Selected only by a coordinated complete-world cut. Crafting and consumption keep their revision
    /// meaning while raw custody, gathering and work stay with the world's actual owners.
    /// </summary>
    public bool SupportsCompleteWorld { get; }
    public bool SupportsExcavation => Excavation is not null;
    public ExcavationMaterialBoundary? Excavation { get; }
    public bool SupportsReservedTransport => ReservedTransport is not null;
    public TransportMaterialBoundary? ReservedTransport { get; }
    public bool SupportsTerrainRepresentation => TerrainRepresentation is not null;
    public TerrainRepresentationBoundary? TerrainRepresentation { get; }
    public ReadOnlyCollection<EntityId> ConstructionRecipeIds { get; }
    public ReadOnlyCollection<ProductionBatch> ConstructionBatches { get; }
    public ulong BaseMaterialOperationCount { get; }
    public GatheringSnapshot? GatheringBaseline { get; }
    public ReadOnlyCollection<CraftRecipeDefinition> Recipes { get; }
    public ReadOnlyCollection<CustodianStorage> Storages { get; }
    public ContentHash AuthorityAdmissionHash { get; }
    public ContentHash ContentHash { get; }

    internal static ProductionContinuationContext Capture(CountedAuthorityState inventory, ProductionSnapshot production) =>
        new(CaptureOrdinaryBasis(inventory, production), inventory, production,
            production.MaterialHistory is not null, production.MaterialHistory?.Gathering,
            supportsConstruction: production.Continuation?.SupportsConstruction == true,
            terrainRepresentation: production.MaterialHistory?.TerrainRepresentation);

    internal static ProductionContinuationContext CaptureMaterial(CountedAuthorityState inventory,
        ProductionSnapshot production, GatheringSnapshot? gathering) =>
        new(CaptureOrdinaryBasis(inventory, production), inventory, production, true, gathering,
            supportsConstruction: production.Continuation?.SupportsConstruction == true,
            terrainRepresentation: production.MaterialHistory?.TerrainRepresentation);

    internal static ProductionContinuationContext CaptureConstruction(CountedAuthorityState inventory,
        ProductionSnapshot production, ConstructionSnapshot construction, GatheringSnapshot? gathering) =>
        new(CaptureOrdinaryBasis(inventory, production), inventory, production, true, gathering, construction, true, production.MaterialHistory?.TerrainRepresentation);

    internal static ProductionContinuationContext CaptureTerrain(CountedAuthorityState inventory,
        ProductionSnapshot production, TerrainRepresentationBoundary terrain, ConstructionSnapshot? construction,
        GatheringSnapshot? gathering) =>
        new(ProductionContinuationBasis.Capture(inventory, production), inventory, production, true, gathering, construction,
            construction is not null || production.Continuation?.SupportsConstruction == true, terrain);

    internal static ProductionContinuationContext CaptureTransport(CountedAuthorityState inventory,
        ProductionSnapshot production, TransportMaterialBoundary transport, ConstructionSnapshot? construction,
        GatheringSnapshot? gathering) =>
        new(ProductionContinuationBasis.Capture(inventory, production), inventory, production, true, gathering, construction,
            construction is not null || production.Continuation?.SupportsConstruction == true, reservedTransport: transport);

    internal static ProductionContinuationContext CaptureExcavation(CountedAuthorityState inventory,
        ProductionSnapshot production, ExcavationMaterialBoundary excavation, ConstructionSnapshot construction,
        GatheringSnapshot gathering) =>
        new(ProductionContinuationBasis.Capture(inventory, production), inventory, production, true, gathering, construction,
            true, excavation: excavation);

    internal static ProductionContinuationContext CaptureWorld(CountedAuthorityState inventory,
        ProductionSnapshot production, ConstructionSnapshot construction) =>
        new(ProductionContinuationBasis.Capture(inventory, production), inventory, production,
            construction: construction, completeWorld: true);

    private static ProductionContinuationBasis CaptureOrdinaryBasis(CountedAuthorityState inventory, ProductionSnapshot production)
    {
        if (production.Continuation is { SupportsTerrainRepresentation: true } or { SupportsReservedTransport: true } or { SupportsExcavation: true })
            throw new ArgumentException("Geographic continuation must capture its actual complete owner.");
        return ProductionContinuationBasis.Capture(inventory, production);
    }

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
