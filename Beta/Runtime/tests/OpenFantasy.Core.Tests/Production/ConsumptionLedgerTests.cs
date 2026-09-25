using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using static OpenFantasy.Core.Tests.Production.ConsumptionFixture;
using static OpenFantasy.Core.Tests.Production.InstantCraftFixture;

namespace OpenFantasy.Core.Tests.Production;

public sealed class ConsumptionLedgerTests
{
    [Fact]
    public void Exact_raw_consumption_retains_source_rights_without_inventing_output_or_recipient_stock()
    {
        var state = ConsumedRaw();
        ConsumedItemClaim claim = Assert.Single(state.Production.ConsumedItems);
        Assert.True(state.Production.SupportsConsumption);
        Assert.Equal(1UL, claim.Ingredient.Quantity);
        Assert.Equal((Id(71), 0UL, 1UL), (claim.Ingredient.Source!.SourceId, claim.Ingredient.Source.Start, claim.Ingredient.Source.End));
        Assert.Equal(6UL, state.Inventory.Custody.GetLot(Id(70)).Quantity + claim.Ingredient.Quantity);
        Assert.Empty(state.Production.Batches);
        Assert.Empty(state.Production.Lots);
        Assert.Equal(0UL, state.Production.GetCombinedStoredQuantity(state.Inventory, Id(61)));
        Assert.Equal(9UL, state.Production.GetCombinedStoredQuantity(state.Inventory, Id(60)));
        Assert.True(state.Production.ContainsIdentity(claim.ConsumptionId));
        Assert.Equal(EntityId.DeriveSpawned(claim.CommandId, 0), claim.ConsumptionId);
        Assert.True(state.Production.ContainsIdentity(claim.CommandId));
        Assert.Equal(1UL, state.Inventory.Custody.GetLot(Id(70)).Revision);
        Assert.Equal(0UL, claim.Ingredient.Revision);
    }

    [Fact]
    public void Actual_crafted_output_can_be_consumed_once_and_all_other_outputs_remain_live()
    {
        var state = ConsumedProduct();
        var consumed = Assert.Single(state.Production.ConsumedItems).Ingredient;
        Assert.Equal(IngredientLotKind.Transformed, consumed.Kind);
        Assert.Equal(2UL, state.Production.Lots.Single(lot => lot.Definition == Rations).Quantity + consumed.Quantity);
        Assert.Equal(1UL, state.Production.Lots.Single(lot => lot.Definition == Offcuts).Quantity);
        Assert.Single(state.Production.Batches);
        // Removing the claim does not turn the consumed output interval into unowned material.
        Assert.Throws<ArgumentException>(() => WithClaims(state.Production, []));
        Assert.Throws<ArgumentException>(() => new ProductionSnapshot(state.Production.WorldId, state.Production.BranchId,
            state.Production.Revision, state.Production.Recipes, state.Production.Batches, state.Production.Lots));
        var claim = Assert.Single(state.Production.ConsumedItems);
        Assert.Throws<ArgumentException>(() => WithClaims(state.Production,
            [claim, Change(claim, command: Id(9003))]));
        TransformedLot surviving = state.Production.Lots.Single(lot => lot.Definition == Rations);
        var duplicated = new TransformedLot(Id(9999), surviving.CustodianId, surviving.Definition, consumed.Output!, 0);
        Assert.Throws<ArgumentException>(() => WithClaims(state.Production, [claim], [.. state.Production.Lots, duplicated]));
    }

    [Fact]
    public void Consumed_raw_claims_conflict_with_live_stock_and_real_recipe_retirements()
    {
        var raw = ConsumedRaw();
        Assert.Throws<ArgumentException>(() => raw.Production.ValidateForAuthority(Create().Inventory));
        var initial = Create();
        CraftPlan crafted = Craft(initial.Inventory, initial.Production);
        RetiredIngredient retired = crafted.CandidateProduction.Batches.Single().Ingredients.Single(value => value.Definition == Berries);
        var claim = new ConsumedItemClaim(EntityId.DeriveSpawned(Id(9001), 0), Id(9001), Id(10), Id(11), Id(60), retired);
        Assert.Throws<ArgumentException>(() => new ProductionSnapshot(Id(1), Id(2), 2,
            crafted.CandidateProduction.Recipes, crafted.CandidateProduction.Batches, crafted.CandidateProduction.Lots, [claim]));
    }

    [Fact]
    public void Recipe_input_and_consumption_cannot_retire_the_same_produced_interval()
    {
        var initial = Create(chained: true);
        CraftPlan first = Craft(initial.Inventory, initial.Production);
        TransformedLot ration = first.CandidateProduction.Lots.Single(lot => lot.Definition == Rations);
        CraftPlan second = Craft(first.CandidateInventory, first.CandidateProduction, 6001,
            Request(first.CandidateInventory, first.CandidateProduction,
                [new(IngredientLotKind.Transformed, ration.LotId, 0, 1)], Repack(Recipe())));
        var consumed = new ConsumedItemClaim(EntityId.DeriveSpawned(Id(9001), 0), Id(9001), Id(10), Id(11), Id(60),
            new RetiredIngredient(ration.LotId, 0, ration.Definition,
                new ProductionOutputInterval(ration.Output.BatchId, ration.Output.OutputIndex, 0, 1)));
        Assert.Throws<ArgumentException>(() => new ProductionSnapshot(Id(1), Id(2), 3,
            second.CandidateProduction.Recipes, second.CandidateProduction.Batches, second.CandidateProduction.Lots, [consumed]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Consumed_lot_history_rejects_a_remainder_with_unchanged_revision(bool produced)
    {
        var state = produced ? ConsumedProduct() : ConsumedRaw();
        if (produced)
        {
            TransformedLot original = state.Production.Lots.Single(lot => lot.Definition == Rations);
            var wrong = new TransformedLot(original.LotId, original.CustodianId, original.Definition, original.Output, 0);
            Assert.Throws<ArgumentException>(() => WithClaims(state.Production, state.Production.ConsumedItems,
                state.Production.Lots.Select(lot => lot.LotId == original.LotId ? wrong : lot)));
        }
        else
        {
            CountedSourceLot original = state.Inventory.Custody.GetLot(Id(70));
            var wrong = new CountedSourceLot(original.LotId, original.CustodianId, original.Definition, original.Source, 0);
            CountedAuthorityState inventory = WithRawLots(state.Inventory,
                state.Inventory.Custody.Lots.Select(lot => lot.LotId == original.LotId ? wrong : lot));
            Assert.Throws<ArgumentException>(() => state.Production.ValidateForAuthority(inventory));
        }
    }

    [Theory]
    [InlineData("source")]
    [InlineData("amount")]
    [InlineData("definition")]
    [InlineData("actor")]
    [InlineData("recipient")]
    [InlineData("custodian")]
    [InlineData("world")]
    [InlineData("branch")]
    public void Authority_join_rejects_foreign_or_missing_material_and_participants(string changed)
    {
        var state = ConsumedRaw();
        ConsumedItemClaim claim = Assert.Single(state.Production.ConsumedItems);
        RetiredIngredient ingredient = claim.Ingredient;
        if (changed is "source" or "amount" or "definition")
            ingredient = new RetiredIngredient(ingredient.LotId, ingredient.Revision,
                changed == "definition" ? new CountedItemDefinition(Id(9990), ContentHash.Compute("unadmitted"u8)) : ingredient.Definition,
                new SourceInterval(changed == "source" ? Id(9991) : Id(71), 0, changed == "amount" ? 7UL : 1UL));
        var altered = Change(claim, actor: changed == "actor" ? Id(9992) : null,
            recipient: changed == "recipient" ? Id(9993) : null, custodian: changed == "custodian" ? Id(9994) : null,
            ingredient: ingredient);
        var production = new ProductionSnapshot(changed == "world" ? Id(9995) : Id(1), changed == "branch" ? Id(9996) : Id(2),
            1, state.Production.Recipes, [], [], [altered]);
        Assert.Throws<ArgumentException>(() => production.ValidateForAuthority(state.Inventory));
    }

    [Fact]
    public void Consumption_ids_and_commands_cannot_alias_existing_roles_or_other_commits()
    {
        var state = ConsumedProduct();
        ConsumedItemClaim claim = Assert.Single(state.Production.ConsumedItems);
        CraftRecipeDefinition recipe = state.Production.Recipes[0];
        EntityId[] roles = [Id(1), Id(2), Id(10), Id(11), Id(60), claim.Ingredient.LotId,
            claim.Ingredient.Definition.DefinitionId, recipe.DefinitionId, recipe.PhysicalRecipe.DefinitionId,
            recipe.Bindings[0].Physical.DefinitionId, state.Production.Batches[0].BatchId,
            state.Production.Batches[0].CommandId];
        foreach (EntityId role in roles)
        {
            Assert.Throws<ArgumentException>(() => WithClaims(state.Production, [Change(claim, id: role)]));
            Assert.Throws<ArgumentException>(() => WithClaims(state.Production, [Change(claim, command: role)]));
        }
        Assert.Throws<ArgumentException>(() => WithClaims(state.Production, [Change(claim, id: claim.CommandId)]));
        Assert.Throws<ArgumentException>(() => WithClaims(state.Production,
            [claim, Change(claim, command: claim.ConsumptionId,
                ingredient: new RetiredIngredient(Id(9003), 0, Wood, new SourceInterval(Id(73), 3, 4)))]));
        Assert.Throws<ArgumentException>(() => WithClaims(state.Production,
            [claim, Change(claim, ingredient: new RetiredIngredient(Id(9003), 0, Wood, new SourceInterval(Id(73), 3, 4)))]));
        Assert.Throws<ArgumentException>(() => WithClaims(state.Production, [claim, claim]));

        var raw = ConsumedRaw();
        ConsumedItemClaim rawClaim = Assert.Single(raw.Production.ConsumedItems);
        foreach (EntityId authorityRole in new[] { Id(20), Id(30), Id(72), Id(73) })
        {
            ProductionSnapshot aliased = WithClaims(raw.Production, [Change(rawClaim, command: authorityRole)]);
            Assert.Throws<ArgumentException>(() => aliased.ValidateForAuthority(raw.Inventory));
        }
    }

    [Fact]
    public void Consumed_collections_are_copied_and_bounded_before_unbounded_input_enumeration()
    {
        int visited = 0;
        IEnumerable<ConsumedItemClaim> Claims()
        {
            for (ulong index = 0; ; index++)
            {
                visited++;
                yield return new(EntityId.DeriveSpawned(Id(20_000 + index), 0), Id(20_000 + index), Id(10), Id(11), Id(60),
                    new RetiredIngredient(Id(30_000 + index), 0, Berries, new SourceInterval(Id(40_000 + index), 0, 1)));
            }
        }
        Assert.Throws<ArgumentException>(() => new ProductionSnapshot(Id(1), Id(2), 256, [], [], [], Claims()));
        Assert.Equal(ProductionSnapshot.MaximumConsumedItems + 1, visited);
        ConsumedItemClaim[] claims = Claims().Take(ProductionSnapshot.MaximumConsumedItems).ToArray();
        EntityId[] expectedIdentities = claims.Select(claim => claim.ConsumptionId).Order().ToArray();
        var full = new ProductionSnapshot(Id(1), Id(2), 256, [], [], [], claims.Reverse());
        claims[0] = claims[1];
        Assert.Equal(expectedIdentities, full.ConsumedItems.Select(claim => claim.ConsumptionId).ToArray());
        Assert.Throws<NotSupportedException>(() => ((IList<ConsumedItemClaim>)full.ConsumedItems).Clear());
        Assert.Throws<ArgumentException>(() => new ProductionSnapshot(Id(1), Id(2), 0, [], [], [], full.ConsumedItems.Take(1)));
        Assert.Equal(256, ProductionCodecV2.Decode(ProductionCodecV2.Encode(full)).ConsumedItems.Count);
    }
}
