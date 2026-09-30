using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Production;

internal sealed class ProductionIdentityRegistry
{
    private readonly Dictionary<EntityId, string> _roles = [];
    private readonly Dictionary<EntityId, ContentHash> _pins = [];
    private readonly Dictionary<EntityId, CountedItemDefinition> _calibrations = [];

    internal ProductionIdentityRegistry(ProductionSnapshot state)
    {
        Add(state.WorldId, "world");
        Add(state.BranchId, "branch");
        foreach (CraftRecipeDefinition recipe in state.Recipes)
        {
            AddPin(recipe.DefinitionId, "craft-recipe", recipe.Version);
            AddPin(recipe.PhysicalRecipe.DefinitionId, "physical-recipe", recipe.PhysicalRecipe.Version);
            foreach (ItemPhysicalBinding binding in recipe.Bindings)
            {
                AddPin(binding.Item.DefinitionId, "item", binding.Item.Version);
                AddPin(binding.Physical.DefinitionId, "physical-material", binding.Physical.Version);
                if (_calibrations.TryGetValue(binding.Item.DefinitionId, out CountedItemDefinition? prior)
                    && prior != binding.Physical.Pin())
                {
                    throw new ArgumentException("An admitted item cannot have conflicting physical calibrations.");
                }
                _calibrations[binding.Item.DefinitionId] = binding.Physical.Pin();
            }
        }
        if (state.Continuation is { } context)
        {
            AddContinuation(context.Basis);
            foreach (ProductionBatch batch in context.ConstructionBatches)
            {
                Add(EntityId.DeriveSpawned(batch.CommandId, (ulong)state.GetRecipe(batch.RecipeId).Outputs.Count + 1), "structure");
                foreach (RetiredIngredient ingredient in batch.Ingredients) Add(ingredient.LotId, "raw-lot");
            }
            if (context.GatheringBaseline is { } gathering)
            {
                foreach (var node in gathering.Nodes)
                {
                    Add(node.NodeId, "gather-node");
                    Add(node.SourceId, "source");
                    AddPin(node.Definition.DefinitionId, "gather-node-definition", node.Definition.Version);
                    AddPin(node.Definition.YieldDefinition.DefinitionId, "item", node.Definition.YieldDefinition.Version);
                }
                foreach (var tool in gathering.Tools) AddPin(tool.Definition.DefinitionId, "item", tool.Definition.Version);
                foreach (var actor in gathering.Actors) Add(actor.ActorId, "actor");
            }
        }
        if (state.MaterialHistory is { } material)
        {
            foreach (MaterialOperation operation in material.Operations)
            {
                Add(operation.CommandId, "command");
                if (operation is MaterialExcavationWorkOperation { Evidence.IssuedLot: { } issued })
                {
                    Add(issued.LotId, "raw-lot");
                    Add(issued.CustodianId, "custodian");
                    Add(issued.Source.SourceId, "source");
                    AddPin(issued.Definition.DefinitionId, "item", issued.Definition.Version);
                }
                if (operation is MaterialReservedTransportOperation transport)
                    foreach (EntityId id in transport.Evidence.CreatedIds) Add(id, "raw-lot");
                if (operation is MaterialTerrainRepresentationOperation representation)
                    foreach (EntityId id in representation.Evidence.CreatedIds) Add(id, "raw-lot");
                if (operation is MaterialConstructionOperation construction)
                    Add(construction.Evidence.StructureId, "structure");
                if (operation is MaterialConsentedTransferOperation consented)
                {
                    var approval = consented.Approval;
                    var terms = approval.Command.Terms;
                    string lotRole = terms.LotKind == OpenFantasy.Core.Authority.ConsentedMaterial.ConsentedMaterialLotKind.Produced
                        ? "derived-lot" : "raw-lot";
                    Add(consented.AcceptanceId, "acceptance");
                    Add(approval.SenderDelivery.PrincipalId, "principal");
                    Add(approval.SenderDelivery.SessionId, "session");
                    Add(approval.RecipientDelivery.PrincipalId, "principal");
                    Add(approval.RecipientDelivery.SessionId, "session");
                    Add(terms.SenderActorId, "actor");
                    Add(terms.RecipientActorId, "actor");
                    Add(terms.SourceCustodianId, "custodian");
                    Add(terms.DestinationCustodianId, "custodian");
                    Add(terms.LotId, lotRole);
                    Add(consented.MovedLotId, lotRole);
                    AddPin(terms.Definition.DefinitionId, "item", terms.Definition.Version);
                }
                if (operation is MaterialSpotExchangeOperation exchange)
                {
                    var evidence = exchange.Evidence;
                    var quote = evidence.Quote;
                    string role = quote.LotKind == OpenFantasy.Core.Markets.SpotLotKind.Produced ? "derived-lot" : "raw-lot";
                    Add(quote.QuoteId, "market-quote");
                    Add(quote.LotId, role);
                    Add(evidence.MovedLotId, role);
                    Add(quote.IssuerActorId, "actor");
                    Add(quote.CounterpartyActorId, "actor");
                    Add(quote.SellerCustodianId, "custodian");
                    Add(quote.BuyerCustodianId, "custodian");
                    AddPin(quote.Definition.DefinitionId, "item", quote.Definition.Version);
                }
                if (operation is MaterialGatherOperation gather)
                {
                    Add(gather.Evidence.IssuedLot.LotId, "raw-lot");
                    Add(gather.Evidence.IssuedLot.CustodianId, "custodian");
                    Add(gather.Evidence.IssuedLot.Source.SourceId, "source");
                    AddPin(gather.Evidence.IssuedLot.Definition.DefinitionId, "item", gather.Evidence.IssuedLot.Definition.Version);
                }
            }
        }
        foreach (ProductionBatch batch in state.Batches)
        {
            Add(batch.BatchId, "batch");
            Add(batch.CommandId, "command");
            Add(batch.ActorId, "actor");
            Add(batch.CustodianId, "custodian");
            AddPin(batch.RecipeId, "craft-recipe", batch.RecipeVersion);
            foreach (RetiredIngredient ingredient in batch.Ingredients)
            {
                Add(ingredient.LotId, ingredient.Kind == IngredientLotKind.Source ? "raw-lot" : "derived-lot");
                AddPin(ingredient.Definition.DefinitionId, "item", ingredient.Definition.Version);
                if (ingredient.Source is { } source)
                {
                    Add(source.SourceId, "source");
                }
                else
                {
                    Add(ingredient.Output!.BatchId, "batch");
                }
            }
        }
        foreach (TransformedLot lot in state.Lots)
        {
            Add(lot.LotId, "derived-lot");
            Add(lot.CustodianId, "custodian");
            AddPin(lot.Definition.DefinitionId, "item", lot.Definition.Version);
            Add(lot.Output.BatchId, "batch");
            if (lot.ReservedBy is { } reservation)
            {
                Add(reservation, "reservation");
            }
        }
        var commandIds = state.Batches.Select(batch => batch.CommandId).ToHashSet();
        foreach (ConsumedItemClaim claim in state.ConsumedItems)
        {
            if (!commandIds.Add(claim.CommandId))
                throw new ArgumentException("A consumed item command cannot be reused by another consumption or production batch.");
            Add(claim.ConsumptionId, "consumption");
            Add(claim.CommandId, "command");
            Add(claim.ActorId, "actor");
            Add(claim.RecipientActorId, "actor");
            Add(claim.CustodianId, "custodian");
            RetiredIngredient ingredient = claim.Ingredient;
            Add(ingredient.LotId, ingredient.Kind == IngredientLotKind.Source ? "raw-lot" : "derived-lot");
            AddPin(ingredient.Definition.DefinitionId, "item", ingredient.Definition.Version);
            if (ingredient.Source is { } source) Add(source.SourceId, "source");
            else Add(ingredient.Output!.BatchId, "batch");
        }
    }

    private ProductionIdentityRegistry(ProductionIdentityRegistry original)
    {
        _roles = new(original._roles);
        _pins = new(original._pins);
        _calibrations = new(original._calibrations);
    }

    private void AddContinuation(ProductionContinuationBasis basis)
    {
        foreach (ProductionBatchWitness witness in basis.BatchWitnesses)
        {
            Add(witness.BatchId, "batch");
            Add(witness.CommandId, "command");
            Add(witness.ActorId, "actor");
            Add(witness.CustodianId, "custodian");
            AddPin(witness.RecipeId, "craft-recipe", witness.RecipeVersion);
        }
        foreach (CountedSourceLot lot in basis.RawLots)
        {
            Add(lot.LotId, "raw-lot");
            Add(lot.CustodianId, "custodian");
            Add(lot.Source.SourceId, "source");
            AddPin(lot.Definition.DefinitionId, "item", lot.Definition.Version);
            if (lot.ReservedBy is { } reservation) Add(reservation, "reservation");
        }
        foreach (TransformedLot lot in basis.TransformedLots)
        {
            Add(lot.LotId, "derived-lot");
            Add(lot.CustodianId, "custodian");
            Add(lot.Output.BatchId, "batch");
            AddPin(lot.Definition.DefinitionId, "item", lot.Definition.Version);
            if (lot.ReservedBy is { } reservation) Add(reservation, "reservation");
        }
        foreach (DirectRawRetirementRange range in basis.RawRetirements)
        {
            Add(range.CustodianId, "custodian");
            Add(range.Source.SourceId, "source");
            AddPin(range.Definition.DefinitionId, "item", range.Definition.Version);
        }
    }

    internal bool Contains(EntityId identity) => _roles.ContainsKey(identity);

    internal bool ContainsMatching(Func<EntityId, bool> predicate) => _roles.Keys.Any(predicate);

    internal void ValidateMaterialBinding(ItemPhysicalBinding binding)
    {
        ValidateExistingPin(binding.Item.DefinitionId, "item", binding.Item.Version);
        ValidateExistingPin(binding.Physical.DefinitionId, "physical-material", binding.Physical.Version);
        if (_calibrations.TryGetValue(binding.Item.DefinitionId, out var existing) && existing != binding.Physical.Pin())
        {
            throw new ArgumentException("Terrain and production require the same physical calibration for each item.");
        }
    }

    private void ValidateExistingPin(EntityId identity, string role, ContentHash version)
    {
        if (_roles.TryGetValue(identity, out string? existingRole) && existingRole != role
            || _pins.TryGetValue(identity, out var existingVersion) && existingVersion != version)
        {
            throw new ArgumentException("Material bindings cannot alias production roles or change pinned versions.");
        }
    }

    internal void ValidateAuthority(CountedAuthorityState inventory)
    {
        var combined = new ProductionIdentityRegistry(this);
        combined.Add(inventory.WorldId, "world");
        combined.Add(inventory.BranchId, "branch");
        foreach (EntityId actor in inventory.Actors)
        {
            combined.Add(actor, "actor");
        }
        foreach (EntityId reservation in inventory.ReservationIds)
        {
            combined.Add(reservation, "reservation");
        }
        foreach (ActorControlBinding control in inventory.Controls)
        {
            combined.Add(control.PrincipalId, "principal");
            combined.Add(control.SessionId, "session");
        }
        foreach (CustodianStorage storage in inventory.Custody.Storages)
        {
            combined.Add(storage.CustodianId, "custodian");
        }
        foreach (AdmittedCountedSource source in inventory.Sources)
        {
            combined.Add(source.SourceId, "source");
            combined.AddPin(source.Definition.DefinitionId, "item", source.Definition.Version);
        }
        foreach (CountedSourceLot lot in inventory.Custody.Lots)
        {
            combined.Add(lot.LotId, "raw-lot");
        }
    }

    private void Add(EntityId identity, string role)
    {
        if (_roles.TryGetValue(identity, out string? existing) && existing != role)
        {
            throw new ArgumentException("Production identities cannot alias incompatible roles.");
        }
        _roles[identity] = role;
    }

    private void AddPin(EntityId identity, string role, ContentHash version)
    {
        Add(identity, role);
        if (_pins.TryGetValue(identity, out ContentHash? existing) && existing != version)
        {
            throw new ArgumentException("Shared production definitions require exactly one pinned version.");
        }
        _pins[identity] = version;
    }
}
