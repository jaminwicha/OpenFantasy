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
