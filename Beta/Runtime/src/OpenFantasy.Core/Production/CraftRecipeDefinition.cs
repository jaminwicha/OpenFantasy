using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Production;

/// <summary>One instantaneous ordinary-material operation with explicit item-to-physical bindings.</summary>
public sealed class CraftRecipeDefinition
{
    public CraftRecipeDefinition(EntityId definitionId, MaterialRecipeDefinition physicalRecipe,
        IEnumerable<ItemPhysicalBinding> bindings, IEnumerable<OutputMaterialAllocation> allocations)
    {
        ArgumentNullException.ThrowIfNull(physicalRecipe);
        if (definitionId == physicalRecipe.DefinitionId || physicalRecipe.RequiredWork.Value != 1)
        {
            throw new ArgumentException("Instant crafting requires a distinct definition and one abstract completed operation.");
        }
        Bindings = ProductionCollections.Copy(bindings, MaterialCatalog.MaximumDefinitionCount,
            binding => binding.Item.DefinitionId);
        var physicalIds = new HashSet<EntityId>();
        var allIds = new HashSet<EntityId> { definitionId, physicalRecipe.DefinitionId };
        foreach (ItemPhysicalBinding binding in Bindings)
        {
            if (!physicalIds.Add(binding.Physical.DefinitionId)
                || !allIds.Add(binding.Item.DefinitionId) || !allIds.Add(binding.Physical.DefinitionId)
                || !physicalRecipe.Materials.Any(material => material.DefinitionId == binding.Physical.DefinitionId
                    && material.Version == binding.Physical.Version))
            {
                throw new ArgumentException("Every item binding requires one distinct exact recipe material.");
            }
        }
        if (physicalIds.Count != physicalRecipe.Materials.Count)
        {
            throw new ArgumentException("Every physical recipe material requires an explicit counted item binding.");
        }
        DefinitionId = definitionId;
        PhysicalRecipe = physicalRecipe;
        Inputs = Array.AsReadOnly(physicalRecipe.Terms.Where(term => term.Role == RecipeMaterialRole.Input).ToArray());
        Outputs = Array.AsReadOnly(physicalRecipe.Terms.Where(term => term.Role != RecipeMaterialRole.Input).ToArray());
        Allocations = ValidateAllocations(allocations);
        Version = ContentHash.Compute(ProductionCodecV1.EncodeRecipe(this));
    }

    public EntityId DefinitionId { get; }
    public MaterialRecipeDefinition PhysicalRecipe { get; }
    public ReadOnlyCollection<ItemPhysicalBinding> Bindings { get; }
    public ReadOnlyCollection<RecipeTerm> Inputs { get; }
    public ReadOnlyCollection<RecipeTerm> Outputs { get; }
    public ContentHash Version { get; }
    public ReadOnlyCollection<OutputMaterialAllocation> Allocations { get; }
    public const int MaximumAllocations = 256;

    private ReadOnlyCollection<OutputMaterialAllocation> ValidateAllocations(IEnumerable<OutputMaterialAllocation> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var allocations = new Dictionary<(uint Output, EntityId Input), OutputMaterialAllocation>();
        var inputTotals = new Dictionary<CountedItemDefinition, MassMilligrams>();
        var outputTotals = new Dictionary<uint, MassMilligrams>();
        foreach (OutputMaterialAllocation allocation in values)
        {
            ArgumentNullException.ThrowIfNull(allocation);
            if (allocations.Count == MaximumAllocations || allocation.OutputIndex >= Outputs.Count
                || !Inputs.Any(input => ItemFor(input) == allocation.InputItem)
                || !allocations.TryAdd((allocation.OutputIndex, allocation.InputItem.DefinitionId), allocation))
            {
                throw new ArgumentException("Output material allocations must be unique, bounded and name admitted input/output terms.");
            }
            MassMilligrams totalMass = allocation.MassPerOutputUnit.Multiply(Outputs[(int)allocation.OutputIndex].Count);
            inputTotals[allocation.InputItem] = inputTotals.GetValueOrDefault(allocation.InputItem).Add(totalMass);
            outputTotals[allocation.OutputIndex] = outputTotals.GetValueOrDefault(allocation.OutputIndex).Add(allocation.MassPerOutputUnit);
        }
        foreach (RecipeTerm input in Inputs)
        {
            ItemPhysicalBinding binding = BindingFor(ItemFor(input));
            if (inputTotals.GetValueOrDefault(binding.Item) != binding.Physical.MassPerUnit.Multiply(input.Count))
            {
                throw new ArgumentException("Every input's mass must be allocated exactly once across the output units.");
            }
            _ = binding.Physical.VolumePerUnit.Multiply(input.Count);
        }
        for (int index = 0; index < Outputs.Count; index++)
        {
            ItemPhysicalBinding binding = BindingFor(ItemFor(Outputs[index]));
            if (outputTotals.GetValueOrDefault((uint)index) != binding.Physical.MassPerUnit)
            {
                throw new ArgumentException("Every output unit must contain exactly its declared physical mass.");
            }
            _ = binding.Physical.VolumePerUnit.Multiply(Outputs[index].Count);
        }
        return Array.AsReadOnly(allocations.Values.OrderBy(value => value.OutputIndex)
            .ThenBy(value => value.InputItem.DefinitionId).ToArray());
    }

    public CountedItemDefinition ItemFor(RecipeTerm term) => Bindings.Single(binding =>
        binding.Physical.DefinitionId == term.Material.DefinitionId && binding.Physical.Version == term.Material.Version).Item;

    public ItemPhysicalBinding BindingFor(CountedItemDefinition item) => Bindings.FirstOrDefault(binding => binding.Item == item)
        ?? throw new ArgumentException("The item has no exact physical binding in this recipe.", nameof(item));
}
