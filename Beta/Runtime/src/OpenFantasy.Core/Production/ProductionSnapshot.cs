using OpenFantasy.Core.Terrain;
using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Geography;

namespace OpenFantasy.Core.Production;

/// <summary>Bounded production history and live derived contents, optionally tied to an exact closing context.</summary>
public sealed class ProductionSnapshot
{
    public const int MaximumRecipes = 32;
    public const int MaximumBatches = 256;
    public const int MaximumLots = CustodySnapshot.MaximumLots;
    public const int MaximumConsumedItems = 256;
    private readonly ProductionIdentityRegistry _identities;

    public ProductionSnapshot(EntityId worldId, EntityId branchId, ulong revision,
        IEnumerable<CraftRecipeDefinition> recipes, IEnumerable<ProductionBatch> batches, IEnumerable<TransformedLot> lots,
        IEnumerable<ConsumedItemClaim>? consumedItems = null)
        : this(worldId, branchId, revision, recipes, batches, lots, consumedItems, null)
    {
    }

    internal ProductionSnapshot(EntityId worldId, EntityId branchId, ulong revision,
        IEnumerable<CraftRecipeDefinition> recipes, IEnumerable<ProductionBatch> batches, IEnumerable<TransformedLot> lots,
        IEnumerable<ConsumedItemClaim>? consumedItems, ProductionContinuationContext? continuation,
        MaterialOperationHistory? materialHistory = null)
    {
        WorldId = worldId;
        BranchId = branchId;
        Revision = revision;
        Continuation = continuation;
        MaterialHistory = materialHistory;
        if ((materialHistory is not null) != (continuation?.SupportsMaterialOperations == true))
            throw new ArgumentException("Material history and its continued interpretation must be selected together.");
        Recipes = ProductionCollections.Copy(recipes, MaximumRecipes, value => value.DefinitionId);
        Batches = ProductionCollections.Copy(batches, MaximumBatches, value => value.BatchId);
        Lots = ProductionCollections.Copy(lots, MaximumLots, value => value.LotId);
        SupportsConsumption = consumedItems is not null;
        ConsumedItems = ProductionCollections.Copy(consumedItems ?? [], MaximumConsumedItems, value => value.ConsumptionId);
        if ((ulong)ConsumedItems.Count > revision)
            throw new ArgumentException("Consumed item history requires a retained production revision for each operation.", nameof(revision));
        _identities = new ProductionIdentityRegistry(this);
        ProductionClaimValidation.Validate(this);
        if (IsContinued) ProductionContinuationValidation.Validate(this);
    }

    public EntityId WorldId { get; }
    public EntityId BranchId { get; }
    public ulong Revision { get; }
    public ReadOnlyCollection<CraftRecipeDefinition> Recipes { get; }
    public ReadOnlyCollection<ProductionBatch> Batches { get; }
    public ReadOnlyCollection<TransformedLot> Lots { get; }
    public bool SupportsConsumption { get; }
    public ReadOnlyCollection<ConsumedItemClaim> ConsumedItems { get; }
    public ProductionContinuationContext? Continuation { get; }
    public bool IsContinued => Continuation is not null;
    public MaterialOperationHistory? MaterialHistory { get; }
    public ulong TotalMaterialOperationCount => MaterialHistory is { } history
        ? checked(Continuation!.BaseMaterialOperationCount + (ulong)history.Operations.Count)
        : checked(TotalBatchCount + TotalConsumedItemCount);
    public ulong TotalBatchCount => checked((Continuation?.BaseBatchCount ?? 0) + (ulong)Batches.Count);
    public ulong TotalConsumedItemCount => checked((Continuation?.BaseConsumedItemCount ?? 0) + (ulong)ConsumedItems.Count);
    public ContentHash ContentHash => ContentHash.Compute(Continuation?.SupportsSpotExchange == true ? ProductionCodecV12.Encode(this)
        : Continuation?.SupportsConsentedTransfer == true ? ProductionCodecV10.Encode(this)
        : Continuation?.SupportsConsentedWorld == true ? ProductionCodecV11.Encode(this)
        : Continuation?.SupportsCompleteWorld == true ? ProductionCodecV9.Encode(this)
        : Continuation?.SupportsExcavation == true ? ProductionCodecV8.Encode(this)
        : Continuation?.SupportsReservedTransport == true ? ProductionCodecV7.Encode(this)
        : Continuation?.SupportsTerrainRepresentation == true ? ProductionCodecV6.Encode(this)
        : Continuation?.SupportsConstruction == true ? ProductionCodecV5.Encode(this)
        : MaterialHistory is not null ? ProductionCodecV4.Encode(this)
        : IsContinued ? ProductionCodecV3.Encode(this) : SupportsConsumption
        ? ProductionCodecV2.Encode(this) : ProductionCodecV1.Encode(this));
    public bool ContainsIdentity(EntityId identity) => _identities.Contains(identity);

    /// <summary>
    /// Captures the actual closing owners and starts an empty local segment without changing live rights.
    /// This structural operation does not publish an archive, authenticate issuance or admit a world.
    /// The caller retains the complete closing bytes and permanent identity registry separately.
    /// </summary>
    public ProductionSnapshot Continue(CountedAuthorityState inventory)
    {
        RequireOrdinaryCut();
        ProductionContinuationContext context = ProductionContinuationContext.Capture(inventory, this);
        var next = new ProductionSnapshot(WorldId, BranchId, Revision, Recipes, [], Lots,
            SupportsConsumption ? [] : null, context,
            context.SupportsMaterialOperations ? new MaterialOperationHistory(context, []) : null);
        next.ValidateForAuthority(inventory);
        _ = next.ContentHash;
        return next;
    }

    /// <summary>Selects ordered custody/source interpretation explicitly; it does not migrate a world or authenticate source history.</summary>
    public ProductionSnapshot ContinueMaterial(CountedAuthorityState inventory, GatheringSnapshot? gathering = null)
    {
        RequireOrdinaryCut();
        if (MaterialHistory is { } retained)
        {
            if (gathering is not null && (retained.Gathering is null
                || gathering.ContentHash != retained.Gathering.AdvanceTo(gathering.AdvancingStep).ContentHash))
                throw new ArgumentException("A material cut cannot replace its retained source owner.", nameof(gathering));
            if (gathering is null) return Continue(inventory);
        }
        var context = ProductionContinuationContext.CaptureMaterial(inventory, this, gathering);
        var result = new ProductionSnapshot(WorldId, BranchId, Revision, Recipes, [], Lots,
            SupportsConsumption ? [] : null, context, new MaterialOperationHistory(context, []));
        result.ValidateForAuthority(inventory);
        _ = result.ContentHash;
        return result;
    }

    /// <summary>
    /// Explicitly selects ordered history that may retain bilateral approved transfers of raw and
    /// produced stock. It migrates no world, authenticates no participant and admits nothing durable;
    /// construction, geographic and coordinated complete-world owners cannot select it.
    /// </summary>
    public ProductionSnapshot ContinueConsentedMaterial(CountedAuthorityState inventory, GatheringSnapshot? gathering = null)
    {
        RequireOrdinaryCut();
        if (MaterialHistory is { } retained)
        {
            if (gathering is not null && (retained.Gathering is null
                || gathering.ContentHash != retained.Gathering.AdvanceTo(gathering.AdvancingStep).ContentHash))
                throw new ArgumentException("A consented material cut cannot replace its retained source owner.", nameof(gathering));
            gathering ??= retained.Gathering;
        }
        var context = ProductionContinuationContext.CaptureConsentedMaterial(inventory, this, gathering);
        var result = new ProductionSnapshot(WorldId, BranchId, Revision, Recipes, [], Lots,
            SupportsConsumption ? [] : null, context, new MaterialOperationHistory(context, []));
        result.ValidateForAuthority(inventory);
        _ = result.ContentHash;
        return result;
    }

    /// <summary>
    /// Selects standalone ordered paid material history while retaining any existing gift capability.
    /// Captures actual goods, not currency or market state. This is not a durable world migration.
    /// </summary>
    public ProductionSnapshot ContinueSpotExchange(CountedAuthorityState inventory, GatheringSnapshot? gathering = null)
    {
        RequireOrdinaryCut();
        if (MaterialHistory is { } retained)
        {
            if (gathering is not null && (retained.Gathering is null
                || gathering.ContentHash != retained.Gathering.AdvanceTo(gathering.AdvancingStep).ContentHash))
            {
                throw new ArgumentException("A paid material cut cannot replace its source owner.", nameof(gathering));
            }
            gathering ??= retained.Gathering;
        }
        var context = ProductionContinuationContext.CaptureSpotExchange(inventory, this, gathering);
        var result = new ProductionSnapshot(WorldId, BranchId, Revision, Recipes, [], Lots,
            SupportsConsumption ? [] : null, context, new MaterialOperationHistory(context, []));
        result.ValidateForAuthority(inventory);
        _ = result.ContentHash;
        return result;
    }

    /// <summary>Explicitly captures complete installed construction witnesses for standalone V5 composition.</summary>
    public ProductionSnapshot ContinueConstruction(CountedAuthorityState inventory, MaterialConstructionState state,
        GatheringSnapshot? gathering = null)
    {
        RequireOrdinaryCut();
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(state);
        state.ValidateMaterialBoundary(inventory, this);
        ConstructionSnapshot construction = state.Construction;
        if (construction.WorldId != WorldId || construction.BranchId != BranchId)
            throw new ArgumentException("Construction continuation requires the same exact scope.", nameof(construction));
        if (MaterialHistory is { } retained)
        {
            if (gathering is not null && (retained.Gathering is null
                || gathering.ContentHash != retained.Gathering.AdvanceTo(gathering.AdvancingStep).ContentHash))
                throw new ArgumentException("A construction cut cannot replace its retained source owner.", nameof(gathering));
            gathering ??= retained.Gathering;
        }
        var context = ProductionContinuationContext.CaptureConstruction(inventory, this, construction, gathering);
        var result = new ProductionSnapshot(WorldId, BranchId, Revision, Recipes, [], Lots,
            SupportsConsumption ? [] : null, context, new MaterialOperationHistory(context, []));
        result.ValidateForAuthority(inventory);
        _ = result.ContentHash;
        return result;
    }

    private void RequireOrdinaryCut()
    {
        if (Continuation is { SupportsTerrainRepresentation: true } or { SupportsReservedTransport: true } or { SupportsExcavation: true })
            throw new ArgumentException("Geographic material requires a cut through its actual complete admitted owner.");
    }

    internal ProductionSnapshot ContinueTerrain(CountedAuthorityState inventory, TerrainRepresentationBoundary terrain,
        MaterialConstructionState? construction, GatheringSnapshot? gathering)
    {
        if (Continuation is { SupportsReservedTransport: true } or { SupportsExcavation: true })
            throw new ArgumentException("Reserved transport cannot become terrain representation through a lower owner.");
        construction?.ValidateMaterialBoundary(inventory, this);
        if (MaterialHistory is { } retained)
        {
            if (gathering is not null && (retained.Gathering is null
                || gathering.ContentHash != retained.Gathering.AdvanceTo(gathering.AdvancingStep).ContentHash))
                throw new ArgumentException("A terrain cut cannot replace its retained source owner.", nameof(gathering));
            gathering ??= retained.Gathering;
        }
        var context = ProductionContinuationContext.CaptureTerrain(inventory, this, terrain, construction?.Construction, gathering);
        var result = new ProductionSnapshot(WorldId, BranchId, Revision, Recipes, [], Lots,
            SupportsConsumption ? [] : null, context, new MaterialOperationHistory(context, []));
        result.ValidateForAuthority(inventory);
        _ = result.ContentHash;
        return result;
    }

    internal ProductionSnapshot ContinueTransport(CountedAuthorityState inventory, TransportMaterialBoundary transport,
        MaterialConstructionState? construction, GatheringSnapshot? gathering)
    {
        if (Continuation is { SupportsTerrainRepresentation: true } or { SupportsExcavation: true })
            throw new ArgumentException("Terrain representation cannot become reserved transport through a lower owner.");
        construction?.ValidateMaterialBoundary(inventory, this);
        if (MaterialHistory is { } retained)
        {
            if (gathering is not null && (retained.Gathering is null
                || gathering.ContentHash != retained.Gathering.AdvanceTo(gathering.AdvancingStep).ContentHash))
                throw new ArgumentException("A transport cut cannot replace its retained source owner.", nameof(gathering));
            gathering ??= retained.Gathering;
        }
        var context = ProductionContinuationContext.CaptureTransport(inventory, this, transport, construction?.Construction, gathering);
        var result = new ProductionSnapshot(WorldId, BranchId, Revision, Recipes, [], Lots,
            SupportsConsumption ? [] : null, context, new MaterialOperationHistory(context, []));
        result.ValidateForAuthority(inventory);
        _ = result.ContentHash;
        return result;
    }

    internal ProductionSnapshot ContinueExcavation(CountedAuthorityState inventory, ExcavationMaterialBoundary excavation,
        ConstructionSnapshot construction, GatheringSnapshot gathering)
    {
        if (Continuation is not null && !Continuation.SupportsExcavation)
            throw new ArgumentException("Excavation requires its actual admitted complete source owner.");
        if (MaterialHistory is { } history && (history.Excavation?.ContentHash != excavation.ContentHash
            || history.Gathering?.ContentHash != gathering.ContentHash))
            throw new ArgumentException("An excavation cut cannot replace current terrain, work or shared effort.");
        var context = ProductionContinuationContext.CaptureExcavation(inventory, this, excavation, construction, gathering);
        var result = new ProductionSnapshot(WorldId, BranchId, Revision, Recipes, [], Lots,
            SupportsConsumption ? [] : null, context, new MaterialOperationHistory(context, []));
        result.ValidateForAuthority(inventory);
        _ = result.ContentHash;
        return result;
    }

    /// <summary>
    /// Coordinated complete-world cut. Crafting and consumption keep their revision meaning; raw custody,
    /// gathering and work remain with the world's actual owners, so no material operation history exists.
    /// </summary>
    internal ProductionSnapshot ContinueWorld(CountedAuthorityState inventory, ConstructionSnapshot construction,
        bool consentedWorld = false)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(construction);
        if (Continuation is { SupportsCompleteWorld: false })
            throw new ArgumentException("A standalone continued owner cannot join the coordinated complete world.");
        var context = ProductionContinuationContext.CaptureWorld(inventory, this, construction, consentedWorld);
        var result = new ProductionSnapshot(WorldId, BranchId, Revision, Recipes, [], Lots,
            SupportsConsumption ? [] : null, context);
        result.ValidateForAuthority(inventory);
        _ = result.ContentHash;
        return result;
    }

    /// <summary>
    /// The one cut of an explicit proposal transition. The segment that follows validates produced custody
    /// with the complete world's consent evidence; lots, batches, claims and revisions stay exact.
    /// </summary>
    internal ProductionSnapshot AdoptConsentedWorld(CountedAuthorityState inventory, ConstructionSnapshot construction)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(construction);
        if (Continuation is { SupportsCompleteWorld: false } or { SupportsConsentedWorld: true })
            throw new ArgumentException("Only a coordinated owner without consent evidence adopts it at a transition.");
        var context = ProductionContinuationContext.CaptureWorldAdoptingConsent(inventory, this, construction);
        var result = new ProductionSnapshot(WorldId, BranchId, Revision, Recipes, [], Lots,
            SupportsConsumption ? [] : null, context);
        result.ValidateForAuthority(inventory);
        _ = result.ContentHash;
        return result;
    }

    internal bool ContainsIdentityMatching(Func<EntityId, bool> predicate) => _identities.ContainsMatching(predicate);
    internal void ValidateMaterialBinding(ItemPhysicalBinding binding) => _identities.ValidateMaterialBinding(binding);

    public CraftRecipeDefinition GetRecipe(EntityId recipeId) => Recipes.FirstOrDefault(value => value.DefinitionId == recipeId)
        ?? throw new ArgumentException("The crafting recipe is outside this admitted production profile.", nameof(recipeId));

    public ulong GetStoredQuantity(EntityId custodianId)
    {
        ulong quantity = 0;
        foreach (TransformedLot lot in Lots.Where(value => value.CustodianId == custodianId))
        {
            quantity = checked(quantity + lot.Quantity);
        }
        return quantity;
    }

    public ulong GetCombinedStoredQuantity(CountedAuthorityState inventory, EntityId custodianId) =>
        checked(inventory.Custody.GetStoredQuantity(custodianId) + GetStoredQuantity(custodianId));

    /// <summary>
    /// Complete-world custody validation. Replays every produced lot through its retirements and
    /// the supplied ordered custody movements and requires the live lots to equal that partition.
    /// The movements are the projection of evidence owned by the world; production neither stores
    /// nor hashes them. Ordered material history validates custody through its own operation replay.
    /// </summary>
    public void ValidateCustodyEvidence(IEnumerable<ProducedCustodyMovement> movements) =>
        ProducedCustodyPartition.Validate(this, movements);

    public void ValidateForAuthority(CountedAuthorityState inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        if (inventory.WorldId != WorldId || inventory.BranchId != BranchId
            || inventory.Custody.Lots.Count + Lots.Count > MaximumLots)
        {
            throw new ArgumentException("Production must share custody scope and the combined live-lot admission bound.");
        }
        _identities.ValidateAuthority(inventory);
        ProductionClaimValidation.ValidateSourceClaims(this, inventory);
        if (IsContinued) ProductionContinuationValidation.ValidateInventory(this, inventory);
        foreach (ProductionBatch batch in Batches)
        {
            if (!inventory.Actors.Contains(batch.ActorId)
                || !inventory.Policies.Any(policy => policy.CustodianId == batch.CustodianId))
            {
                throw new ArgumentException("Production history requires retained actor and custodian participants.");
            }
        }
        foreach (ConsumedItemClaim claim in ConsumedItems)
        {
            if (!inventory.Actors.Contains(claim.ActorId) || !inventory.Actors.Contains(claim.RecipientActorId)
                || !inventory.Policies.Any(policy => policy.CustodianId == claim.CustodianId))
            {
                throw new ArgumentException("Consumed items require retained acting and recipient actors and an admitted custodian.");
            }
        }
        foreach (TransformedLot lot in Lots)
        {
            if (!inventory.Policies.Any(policy => policy.CustodianId == lot.CustodianId)
                || lot.ReservedBy is { } reservation && !inventory.ReservationIds.Contains(reservation))
            {
                throw new ArgumentException("Derived contents require an admitted custodian and explicit reservations.");
            }
        }
        foreach (CustodianStorage storage in inventory.Custody.Storages)
        {
            if ((UInt128)inventory.Custody.GetStoredQuantity(storage.CustodianId) + GetStoredQuantity(storage.CustodianId)
                > storage.UnitCapacity)
            {
                throw new ArgumentException("Combined raw and transformed contents exceed counted storage capacity.");
            }
        }
    }
}
