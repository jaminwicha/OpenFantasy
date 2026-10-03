using OpenFantasy.Core;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>One admitted recipe and its currently disclosed costs; availability is advice, not authority.</summary>
internal sealed record FarmRecipeEntry(EntityId RecipeId, string Key, string Label,
    string InputsText, string OutputsText, FarmActionOffer Offer);

/// <summary>
/// Browsing is read-only. Preparing captures one exact batch; changed terms invalidate that capture,
/// and neither refreshing nor renewed stock silently prepares it again.
/// </summary>
internal sealed class FarmRecipeLibraryModel
{
    private FarmActionScope? _scope;
    internal IReadOnlyList<FarmRecipeEntry> Entries { get; private set; } = Array.Empty<FarmRecipeEntry>();
    internal EntityId? SelectedId { get; private set; }
    internal FarmRecipeEntry? Selected => Entries.FirstOrDefault(entry => entry.RecipeId == SelectedId);
    internal FarmActionOffer? PreparedOffer { get; private set; }
    internal string? Notice { get; private set; }

    internal void Refresh(FarmProductionPresentation production, FarmActionScope scope)
    {
        if (_scope is not null && !SameLifetime(_scope, scope)) Clear();
        _scope = scope;
        Entries = Array.AsReadOnly(production.Recipes.OrderBy(recipe => recipe.RecipeId)
            .Select(recipe => Describe(recipe, production, scope)).ToArray());
        if (Selected is null)
        {
            DiscardReview();
            SelectedId = Entries.Count == 0 ? null : Entries[0].RecipeId;
        }
        if (PreparedOffer is { } reviewed && (Selected is not { } selected
            || !SameCapture(reviewed, selected.Offer)))
            DiscardReview("This prepared batch changed. Review the ingredients and prepare it again.");
    }

    internal bool Select(EntityId recipeId)
    {
        if (!Entries.Any(entry => entry.RecipeId == recipeId)) return false;
        if (SelectedId != recipeId)
        {
            SelectedId = recipeId;
            DiscardReview();
        }
        return true;
    }

    internal bool Prepare()
    {
        var offer = Selected?.Offer;
        if (offer is null || !offer.CanExecute)
        {
            DiscardReview(offer?.BlockingReason ?? "Choose an available recipe.");
            return false;
        }
        PreparedOffer = offer;
        Notice = "Batch prepared. Confirm to spend these ingredients; nothing is reserved yet.";
        return true;
    }

    internal void DiscardReview(string? reason = null)
    {
        PreparedOffer = null;
        Notice = reason;
    }

    internal void Clear()
    {
        _scope = null;
        SelectedId = null;
        Entries = Array.Empty<FarmRecipeEntry>();
        DiscardReview();
    }

    private static FarmRecipeEntry Describe(LocalCraftRecipeView recipe,
        FarmProductionPresentation production, FarmActionScope scope)
    {
        string label = recipe.Key switch
        {
            "craft_rations" => "Pack rations",
            "craft_potion" => "Brew potion",
            _ => recipe.Key.Replace('_', ' ')
        };
        string inputs = DescribeTerms(recipe, production.MaterialDescriptions, outputs: false);
        string outputs = DescribeTerms(recipe, production.MaterialDescriptions, outputs: true);
        var selection = production.PrepareRecipe(recipe.RecipeId);
        string? blocker = production.LibraryContextBlocker ?? selection.BlockingReason;
        var offer = new FarmActionOffer(FarmSemanticAction.Craft, scope, label, production.PackName,
            null, inputs, "Crafted: " + outputs, selection.Request is null ? null : new CraftIntent(selection.Request),
            blocker, detailText: "Produces: " + outputs);
        return new(recipe.RecipeId, recipe.Key, label, inputs, outputs, offer);
    }

    private static string DescribeTerms(LocalCraftRecipeView recipe, LocalMaterialDescriptions? descriptions, bool outputs)
    {
        var terms = recipe.Terms.Where(term => (term.Role != RecipeMaterialRole.Input) == outputs)
            .Select(term => $"{term.Quantity} {descriptions?.Find(term.DefinitionId)?.DisplayName ?? "unidentified material"}"
                + (term.Role == RecipeMaterialRole.Waste ? " (by-product)" : "")).ToArray();
        return terms.Length == 0 ? "None" : string.Join(", ", terms);
    }

    private static bool SameLifetime(FarmActionScope left, FarmActionScope right) =>
        left.PrincipalId == right.PrincipalId && left.SessionId == right.SessionId && left.ActorId == right.ActorId
        && left.WorldId == right.WorldId && left.BranchId == right.BranchId && left.Selection == right.Selection;

    private static bool SameCapture(FarmActionOffer left, FarmActionOffer right)
    {
        if (!right.CanExecute || left.Scope != right.Scope || left.Label != right.Label
            || left.CostText != right.CostText || left.DetailText != right.DetailText
            || left.Intent is not CraftIntent before || right.Intent is not CraftIntent after) return false;
        var original = before.Request;
        var current = after.Request;
        return original.RecipeId == current.RecipeId && original.RecipeVersion == current.RecipeVersion
            && original.ExpectedProductionRevision == current.ExpectedProductionRevision
            && original.CustodianId == current.CustodianId && original.ExpectedStorageRevision == current.ExpectedStorageRevision
            && original.Ingredients.SequenceEqual(current.Ingredients);
    }
}
