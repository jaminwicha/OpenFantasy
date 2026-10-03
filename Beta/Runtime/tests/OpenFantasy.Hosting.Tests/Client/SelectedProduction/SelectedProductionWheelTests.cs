using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Tests.Client.SelectedProduction;

/// <summary>Adversarial preview refreshes start from real offers; mutated candidates are never world execution evidence.</summary>
public sealed class SelectedProductionWheelTests
{
    [Fact]
    public async Task Selected_gather_wheel_refuses_changed_actor_selection_node_and_tool_pins()
    {
        await using var fixture = await SelectedProductionClientFixture.Open(WorldControlCapabilities.Production);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        var observed = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var client = SelectedProductionBindingTests.Client(fixture, player);
        var original = client.CreateOffers(observed, SelectedProductionBindingTests.Berry(observed, fixture))[FarmSemanticAction.Gather];
        Assert.True(original.CanExecute, original.BlockingReason);
        var captured = Assert.IsType<GatherIntent>(original.Intent);
        var changed = new[]
        {
            original.WithScope(original.Scope with { ExpectedActorRevision = original.Scope.ExpectedActorRevision + 1 }),
            original.WithScope(original.Scope with { ActorId = fixture.Grantee }),
            original.WithScope(original.Scope with { Selection = original.Scope.Selection! with { Generation = original.Scope.Selection!.Generation + 2 } }),
            Clone(original, captured with { NodeId = new(0xda70726f, 2) }),
            Clone(original, captured with { ExpectedNodeRevision = captured.ExpectedNodeRevision + 1 }),
            Clone(original, captured with { ToolLotId = new(0xda70726f, 3) }),
            Clone(original, captured with { ExpectedToolLotRevision = captured.ExpectedToolLotRevision + 1 })
        };
        foreach (var candidate in changed)
        {
            var slot = new FarmWheelSlot(original);
            slot.Refresh(candidate);
            Assert.False(slot.Offer.CanExecute);
            Assert.Null(slot.Offer.Intent);
            Assert.Equal(original.TargetId, slot.Offer.TargetId);
            Assert.Equal(original.Scope, slot.Offer.Scope);
        }
        var equal = new FarmWheelSlot(original);
        equal.Refresh(Clone(original, captured with { }));
        Assert.True(equal.Offer.CanExecute);
        Assert.Equal(captured, equal.Offer.Intent);
        // The old unselected wheel deliberately keeps its live preview refresh behavior.
        var legacy = original.WithScope(original.Scope with { Selection = null });
        var legacySlot = new FarmWheelSlot(legacy);
        legacySlot.Refresh(Clone(legacy, captured with { ExpectedNodeRevision = captured.ExpectedNodeRevision + 1 }));
        Assert.True(legacySlot.Offer.CanExecute);
    }

    [Fact]
    public async Task Selected_craft_wheel_accepts_equal_rehydrated_requests_but_not_changed_recipe_storage_or_ingredients()
    {
        await using var fixture = await SelectedProductionClientFixture.Open(WorldControlCapabilities.Production);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var client = SelectedProductionBindingTests.Client(fixture, player);
        await SelectedProductionBindingTests.GatherTwice(fixture, player, client);
        var observed = player.ReadSelectedObservation();
        var original = client.CreateOffers(observed)[FarmSemanticAction.Craft];
        Assert.True(original.CanExecute, original.BlockingReason);
        var request = Assert.IsType<CraftIntent>(original.Intent).Request;
        CraftRequest Copy(EntityId? recipe = null, ContentHash? version = null, ulong? production = null,
            EntityId? custodian = null, ulong? storage = null, IEnumerable<IngredientRequest>? ingredients = null) =>
            new(recipe ?? request.RecipeId, version ?? request.RecipeVersion, production ?? request.ExpectedProductionRevision,
                custodian ?? request.CustodianId, storage ?? request.ExpectedStorageRevision,
                ingredients ?? request.Ingredients.Select(value => new IngredientRequest(value.Kind, value.LotId,
                    value.ExpectedRevision, value.Quantity)));
        var recreated = Copy();
        Assert.NotSame(request, recreated);
        Assert.NotSame(request.Ingredients[0], recreated.Ingredients[0]);
        var equal = new FarmWheelSlot(original);
        equal.Refresh(Clone(original, new CraftIntent(recreated)));
        Assert.True(equal.Offer.CanExecute);
        Assert.Same(recreated, Assert.IsType<CraftIntent>(equal.Offer.Intent).Request);
        var first = request.Ingredients[0];
        var changedInputs = new[]
        {
            new IngredientRequest(first.Kind, new(0xda70726f, 4), first.ExpectedRevision, first.Quantity),
            new IngredientRequest(first.Kind, first.LotId, first.ExpectedRevision + 1, first.Quantity),
            new IngredientRequest(first.Kind, first.LotId, first.ExpectedRevision, first.Quantity + 1),
            new IngredientRequest(first.Kind == IngredientLotKind.Source ? IngredientLotKind.Transformed : IngredientLotKind.Source,
                first.LotId, first.ExpectedRevision, first.Quantity)
        };
        var changes = new List<CraftRequest>
        {
            Copy(recipe: new(0xda70726f, 5)), Copy(version: ContentHash.Compute([77])),
            Copy(production: request.ExpectedProductionRevision + 1), Copy(custodian: new(0xda70726f, 6)),
            Copy(storage: request.ExpectedStorageRevision + 1)
        };
        changes.AddRange(changedInputs.Select(firstChanged => Copy(ingredients: new[] { firstChanged }.Concat(request.Ingredients.Skip(1)))));
        var before = fixture.State;
        foreach (var changed in changes)
        {
            var slot = new FarmWheelSlot(original);
            slot.Refresh(Clone(original, new CraftIntent(changed)));
            Assert.False(slot.Offer.CanExecute);
            Assert.Null(slot.Offer.Intent);
            Assert.Equal(original.Scope, slot.Offer.Scope);
        }
        Assert.Same(before, fixture.State);
    }

    private static FarmActionOffer Clone(FarmActionOffer original, WorldActionIntent intent) =>
        new(original.Action, original.Scope, original.Label, original.TargetName, original.TargetId,
            original.CostText, original.CompletionText, intent, null, original.DialogueId, original.DetailText);
}
