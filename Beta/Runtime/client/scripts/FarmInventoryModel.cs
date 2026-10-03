using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>A display grouping, never a merge of physical lots or delivery permissions.</summary>
internal readonly record struct FarmInventoryKey(EntityId DefinitionId, IngredientLotKind Provenance, bool Deliverable);

internal sealed record FarmInventoryEntry(FarmInventoryKey Key, string Name, string Description,
    ulong Quantity, int LotCount, uint? ToolPower)
{
    internal string Details => Description + $"\n\nOwned: {Quantity} · Stacks: {LotCount}"
        + (ToolPower is { } power ? $"\nGathering power: {power}" : "")
        + (Key.Provenance == IngredientLotKind.Transformed ? "\nMade through crafting." : "")
        + (!Key.Deliverable ? "\nProtected or reserved: cannot be delivered." : "");
}

/// <summary>Read-only inspection of the current actor's public pack; no command or authority access.</summary>
internal sealed class FarmInventoryModel(EntityId woodDefinitionId, EntityId? berryDefinitionId)
{
    private FarmInventoryScope? _scope;
    internal IReadOnlyList<FarmInventoryEntry> Entries { get; private set; } = Array.Empty<FarmInventoryEntry>();
    internal FarmInventoryKey? SelectedKey { get; private set; }
    internal FarmInventoryEntry? Selected => Entries.FirstOrDefault(entry => entry.Key == SelectedKey);

    internal void Refresh(LocalWorldView view) => Refresh(FarmInventoryPresentation.From(view));

    internal void Refresh(FarmInventoryPresentation view)
    {
        var scope = view.Scope;
        if (_scope != scope)
        {
            _scope = scope;
            SelectedKey = null;
        }
        Entries = Array.AsReadOnly(view.Inventory.Lots.Where(lot => lot.Quantity > 0)
            .GroupBy(lot => new FarmInventoryKey(lot.DefinitionId, lot.Provenance, lot.AvailableForDelivery))
            .Select(group => Describe(view, group.Key, group.ToArray()))
            .OrderBy(entry => entry.ToolPower is null ? 1 : 0)
            .ThenBy(entry => entry.Key.DefinitionId).ThenBy(entry => entry.Key.Provenance)
            .ThenBy(entry => entry.Key.Deliverable).ToArray());
        if (Selected is null)
        {
            SelectedKey = Entries.Count == 0 ? null : Entries[0].Key;
        }
    }

    internal bool Select(FarmInventoryKey key)
    {
        if (!Entries.Any(entry => entry.Key == key))
        {
            return false;
        }
        SelectedKey = key;
        return true;
    }

    private FarmInventoryEntry Describe(FarmInventoryPresentation view, FarmInventoryKey key, LocalItemLotView[] lots)
    {
        LocalGatherToolLotView? tool = view.GatheringTools.FirstOrDefault(candidate => lots.Any(lot => lot.LotId == candidate.LotId));
        (string name, string description) = tool?.Kind switch
        {
            GatherToolKind.Axe => ("Axe", "A gathering tool for chopping wood."),
            GatherToolKind.Sickle => ("Sickle", "A gathering tool for harvesting plants."),
            _ when key.DefinitionId == woodDefinitionId => ("Wood", "A useful stock of wood for craftwork."),
            _ when key.DefinitionId == berryDefinitionId => ("Berries", "Berries for packing rations."),
            _ => DescribeRecipeMaterial(view, key.DefinitionId)
        };
        if (view.MaterialDescriptions?.Find(key.DefinitionId) is { } material)
        {
            name = material.DisplayName;
            if (description == "An item in your pack. No further details are known.")
                description = "An identified material in this character's pack. Its uses depend on available skills and actions.";
        }
        return new FarmInventoryEntry(key, name, description,
            lots.Aggregate(0UL, (sum, lot) => checked(sum + lot.Quantity)), lots.Length, tool?.Power);
    }

    private static (string Name, string Description) DescribeRecipeMaterial(FarmInventoryPresentation view, EntityId definition)
    {
        var restorative = view.Restoratives.FirstOrDefault(value => value.ItemDefinitionId == definition);
        if (restorative is not null)
            return (restorative.ItemName, $"Restores up to {restorative.HealHitPoints} HP during exploration. Inspect the Care wheel page to use a dose.");
        LocalCraftTermView? term = view.RationsRecipe?.Terms.FirstOrDefault(value => value.DefinitionId == definition);
        return term?.Role switch
        {
            RecipeMaterialRole.Output => ("Rations", "Cold berry portions packed in wooden containers."),
            RecipeMaterialRole.Waste => ("Wood offcuts", "Small pieces left over from packing supplies."),
            _ => ("Unfamiliar item", "An item in your pack. No further details are known.")
        };
    }
}
