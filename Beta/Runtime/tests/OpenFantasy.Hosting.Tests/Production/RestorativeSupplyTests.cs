using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Hosting.Tests.Production.RestorativeSupplyFixture;

namespace OpenFantasy.Hosting.Tests.Production;

public sealed class RestorativeSupplyTests
{
    [Fact]
    public void Retained_Potion_and_three_berry_recipe_pins_are_separate_from_explicit_mass_calibration()
    {
        var fixture = new RestorativeSupplyFixture();
        Assert.Equal("3b7a5fdd562795e58d68a11007763b88cd25d40841c739c4fef9495161c097aa", fixture.Potion.Version.Value);
        Assert.Equal("a7b53e8648a12bb156269fe955ea868aeab9881ba75fb4b79bba057358a93214", fixture.Berries.Version.Value);
        Assert.Equal("dd98e32ef5f0442f9d1c37d10d431aab74226a77f73bc6858a5075e4dad41dc2", fixture.RecipePin.Value);
        Assert.Equal("Consumable", fixture.PotionPayload.GetProperty("kind").GetString());
        Assert.Equal(40, fixture.PotionPayload.GetProperty("healHp").GetInt32());
        Assert.Equal(0, fixture.PotionPayload.GetProperty("healMp").GetInt32());
        Assert.False(fixture.PotionPayload.TryGetProperty("mass", out _));
        Assert.Equal(fixture.RecipePin, fixture.Recipe.PhysicalRecipe.SourceRecipePayloadHash);
        Assert.Equal(3UL, Assert.Single(fixture.Recipe.Inputs).Count);
        Assert.Equal(1UL, Assert.Single(fixture.Recipe.Outputs).Count);
        Assert.Equal(new MassMilligrams(60_000), fixture.Recipe.PhysicalRecipe.InputMass);
        Assert.Equal(new MassMilligrams(60_000), fixture.Recipe.PhysicalRecipe.OutputMass);
        Assert.Equal(new MassMilligrams(0), fixture.Recipe.PhysicalRecipe.WasteMass);
        Assert.Equal(new MassMilligrams(60_000), Assert.Single(fixture.Recipe.Allocations).MassPerOutputUnit);
        Assert.All(fixture.Recipe.Bindings, binding =>
        {
            Assert.NotEqual(binding.Item.DefinitionId, binding.Physical.DefinitionId);
            Assert.NotEqual(binding.Item.Version, binding.Physical.Version);
        });
        Assert.Equal((UInt128)240_000, fixture.AccountedMass(fixture.Inventory, fixture.Production));
    }

    [Fact]
    public void Raw_and_actually_crafted_Potions_retire_exact_rights_and_another_craft_preserves_both_claims_after_archive_restore()
    {
        var fixture = new RestorativeSupplyFixture();
        byte[] originalInventory = AuthorityEncodingV1.Encode(fixture.Inventory);
        byte[] originalProduction = ProductionCodecV2.Encode(fixture.Production);
        var raw = Consume(fixture.Inventory, fixture.Production, Id(200), fixture.Request(fixture.Inventory, fixture.Production));
        AssertApplied(raw);
        ConsumedItemClaim rawClaim = Assert.IsType<ConsumedItemClaim>(raw.Claim);
        Assert.Equal(new SourceInterval(PotionSource, 0, 1), rawClaim.Ingredient.Source);
        Assert.Equal(IngredientLotKind.Source, rawClaim.Ingredient.Kind);
        CountedSourceLot remainder = raw.CandidateInventory.Custody.GetLot(RawPotionLot);
        Assert.Equal(new SourceInterval(PotionSource, 1, 2), remainder.Source);
        Assert.Equal(1UL, remainder.Revision);
        Assert.Equal(1UL, raw.CandidateInventory.Custody.GetStorage(Storage).Revision);
        Assert.Equal(0UL, raw.CandidateInventory.Custody.GetStorage(RecipientStorage).Revision);
        Assert.Equal((UInt128)240_000, fixture.AccountedMass(raw.CandidateInventory, raw.CandidateProduction));

        CraftPlan crafted = fixture.Craft(raw.CandidateInventory, raw.CandidateProduction, Id(201));
        Assert.Equal(WorldActionRejection.None, crafted.Rejection);
        Assert.True(crafted.CandidateProduction.SupportsConsumption);
        Assert.Equal(ClaimFields(rawClaim), ClaimFields(Assert.Single(crafted.CandidateProduction.ConsumedItems)));
        ProductionBatch batch = Assert.Single(crafted.CandidateProduction.Batches);
        Assert.Equal(new SourceInterval(BerrySource, 0, 3), Assert.Single(batch.Ingredients).Source);
        Assert.Equal(1UL, crafted.CandidateInventory.Custody.GetLot(BerryLot).Revision);
        TransformedLot output = Assert.Single(crafted.CandidateProduction.Lots);
        Assert.Equal(fixture.Potion, output.Definition);
        Assert.Equal(new ProductionOutputInterval(batch.BatchId, 0, 0, 1), output.Output);
        Assert.Equal((UInt128)240_000, fixture.AccountedMass(crafted.CandidateInventory, crafted.CandidateProduction));

        var used = Consume(crafted.CandidateInventory, crafted.CandidateProduction, Id(202),
            fixture.Request(crafted.CandidateInventory, crafted.CandidateProduction,
                new(IngredientLotKind.Transformed, output.LotId, output.Revision, 1)));
        AssertApplied(used);
        ConsumedItemClaim outputClaim = Assert.IsType<ConsumedItemClaim>(used.Claim);
        Assert.Equal(IngredientLotKind.Transformed, outputClaim.Ingredient.Kind);
        Assert.Equal(output.Output, outputClaim.Ingredient.Output);
        Assert.Empty(used.CandidateProduction.Lots);
        Assert.Equal(2, used.CandidateProduction.ConsumedItems.Count);
        Assert.Single(used.CandidateProduction.Batches);
        Assert.Equal((UInt128)240_000, fixture.AccountedMass(used.CandidateInventory, used.CandidateProduction));

        byte[] inventoryBytes = AuthorityEncodingV1.Encode(used.CandidateInventory);
        byte[] productionBytes = ProductionCodecV2.Encode(used.CandidateProduction);
        var restoredInventory = AuthorityEncodingV1.Decode(inventoryBytes);
        var restoredProduction = ProductionCodecV2.Decode(productionBytes);
        restoredProduction.ValidateForAuthority(restoredInventory);
        Assert.Equal(inventoryBytes, AuthorityEncodingV1.Encode(restoredInventory));
        Assert.Equal(productionBytes, ProductionCodecV2.Encode(restoredProduction));
        Assert.Equal(used.CandidateProduction.ContentHash, restoredProduction.ContentHash);
        CraftPlan next = fixture.Craft(restoredInventory, restoredProduction, Id(203));
        Assert.Equal(WorldActionRejection.None, next.Rejection);
        Assert.True(next.CandidateProduction.SupportsConsumption);
        Assert.Equal(restoredProduction.ConsumedItems.Select(ClaimFields), next.CandidateProduction.ConsumedItems.Select(ClaimFields));
        Assert.Equal(restoredProduction.Revision + 1, next.CandidateProduction.Revision);
        Assert.Equal(2, next.CandidateProduction.Batches.Count);
        Assert.Equal(new SourceInterval(BerrySource, 3, 6),
            Assert.Single(next.CandidateProduction.Batches, value => value.CommandId == Id(203)).Ingredients.Single().Source);
        Assert.DoesNotContain(next.CandidateInventory.Custody.Lots, lot => lot.LotId == BerryLot);
        Assert.Equal(1UL, Assert.Single(next.CandidateProduction.Lots).Quantity);
        Assert.Equal(fixture.Inventory.Sources, next.CandidateInventory.Sources);
        Assert.Equal((UInt128)240_000, fixture.AccountedMass(next.CandidateInventory, next.CandidateProduction));
        next.CandidateProduction.ValidateForAuthority(next.CandidateInventory);
        Assert.Equal(originalInventory, AuthorityEncodingV1.Encode(fixture.Inventory));
        Assert.Equal(originalProduction, ProductionCodecV2.Encode(fixture.Production));
    }

    [Fact]
    public void Consumed_source_and_produced_rights_cannot_be_reintroduced_or_spent_again()
    {
        var fixture = new RestorativeSupplyFixture();
        var originalRequest = fixture.Request(fixture.Inventory, fixture.Production);
        var raw = Consume(fixture.Inventory, fixture.Production, Id(210), originalRequest);
        AssertApplied(raw);
        AssertRejected(raw.CandidateInventory, raw.CandidateProduction,
            Consume(raw.CandidateInventory, raw.CandidateProduction, Id(211), originalRequest), WorldActionRejection.RevisionConflict);
        var oldLotSelection = fixture.Request(raw.CandidateInventory, raw.CandidateProduction,
            new(IngredientLotKind.Source, RawPotionLot, 0, 1));
        AssertRejected(raw.CandidateInventory, raw.CandidateProduction,
            Consume(raw.CandidateInventory, raw.CandidateProduction, Id(212), oldLotSelection), WorldActionRejection.Unavailable);
        var reusedCommand = Consume(raw.CandidateInventory, raw.CandidateProduction, Id(210),
            fixture.Request(raw.CandidateInventory, raw.CandidateProduction));
        AssertRejected(raw.CandidateInventory, raw.CandidateProduction, reusedCommand, WorldActionRejection.Unavailable);
        var forgedRaw = new CountedSourceLot(Id(220), Storage, fixture.Potion, new SourceInterval(PotionSource, 0, 1), 0);
        var reintroduced = WithLots(raw.CandidateInventory, raw.CandidateInventory.Custody.Lots.Append(forgedRaw));
        Assert.Throws<ArgumentException>(() => raw.CandidateProduction.ValidateForAuthority(reintroduced));

        var crafted = fixture.Craft(raw.CandidateInventory, raw.CandidateProduction, Id(213));
        Assert.Equal(WorldActionRejection.None, crafted.Rejection);
        TransformedLot output = Assert.Single(crafted.CandidateProduction.Lots);
        var selected = new IngredientRequest(IngredientLotKind.Transformed, output.LotId, output.Revision, 1);
        var used = Consume(crafted.CandidateInventory, crafted.CandidateProduction, Id(214),
            fixture.Request(crafted.CandidateInventory, crafted.CandidateProduction, selected));
        AssertApplied(used);
        AssertRejected(used.CandidateInventory, used.CandidateProduction,
            Consume(used.CandidateInventory, used.CandidateProduction, Id(215),
                fixture.Request(used.CandidateInventory, used.CandidateProduction, selected)), WorldActionRejection.Unavailable);
        Assert.Throws<ArgumentException>(() => new ProductionSnapshot(used.CandidateProduction.WorldId,
            used.CandidateProduction.BranchId, used.CandidateProduction.Revision, used.CandidateProduction.Recipes,
            used.CandidateProduction.Batches, [output], used.CandidateProduction.ConsumedItems));
        Assert.Throws<ArgumentException>(() => new ProductionSnapshot(used.CandidateProduction.WorldId,
            used.CandidateProduction.BranchId, used.CandidateProduction.Revision, used.CandidateProduction.Recipes,
            used.CandidateProduction.Batches, [], [raw.Claim!])); // Derived output cannot silently disappear.
    }

    [Fact]
    public void Wrong_owner_pin_quantity_or_revision_refuses_without_material_or_identity_candidates()
    {
        var fixture = new RestorativeSupplyFixture();
        var inventory = fixture.Inventory;
        var production = fixture.Production;
        var request = fixture.Request(inventory, production);
        AssertRejected(inventory, production, new ItemConsumptionPlanner().Plan(inventory, production,
            new TrustedCommandDelivery(Id(22), Id(32)), Actor, Id(230), request), WorldActionRejection.NotAuthorized);
        foreach (var bad in new[]
        {
            fixture.Request(inventory, production, definition: fixture.Berries),
            fixture.Request(inventory, production, definition: new CountedItemDefinition(fixture.Potion.DefinitionId, ContentHash.Compute("changed Potion"u8))),
            fixture.Request(inventory, production, new(IngredientLotKind.Source, RawPotionLot, 0, 3)),
            new ConsumeItemRequest(Storage, 0, 0, Id(999), fixture.Potion, request.Ingredient)
        })
            AssertRejected(inventory, production, Consume(inventory, production, Id(231), bad), WorldActionRejection.Unavailable);
        foreach (var stale in new[]
        {
            new ConsumeItemRequest(Storage, 1, 0, Recipient, fixture.Potion, request.Ingredient),
            new ConsumeItemRequest(Storage, 0, 1, Recipient, fixture.Potion, request.Ingredient)
        })
            AssertRejected(inventory, production, Consume(inventory, production, Id(232), stale), WorldActionRejection.RevisionConflict);
        var legacy = new ProductionSnapshot(production.WorldId, production.BranchId, 0, production.Recipes, [], []);
        byte[] oldBytes = ProductionCodecV1.Encode(legacy);
        AssertRejected(inventory, legacy, Consume(inventory, legacy, Id(233), fixture.Request(inventory, legacy)), WorldActionRejection.Unavailable);
        Assert.Equal(oldBytes, ProductionCodecV1.Encode(legacy));
    }

    private static ItemConsumptionPlan Consume(CountedAuthorityState inventory, ProductionSnapshot production,
        EntityId commandId, ConsumeItemRequest request) =>
        new ItemConsumptionPlanner().Plan(inventory, production, Delivery, Actor, commandId, request);

    private static void AssertApplied(ItemConsumptionPlan plan)
    {
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        ConsumedItemClaim claim = Assert.IsType<ConsumedItemClaim>(plan.Claim);
        Assert.Equal(Actor, claim.ActorId);
        Assert.Equal(Recipient, claim.RecipientActorId);
        Assert.Equal(Storage, claim.CustodianId);
        Assert.Equal(EntityId.DeriveSpawned(claim.CommandId, 0), Assert.Single(plan.CreatedIds));
        Assert.Equal(ClaimFields(claim), ClaimFields(Assert.Single(plan.CandidateProduction.ConsumedItems,
            value => value.ConsumptionId == claim.ConsumptionId)));
        plan.CandidateProduction.ValidateForAuthority(plan.CandidateInventory);
    }

    private static void AssertRejected(CountedAuthorityState inventory, ProductionSnapshot production,
        ItemConsumptionPlan plan, WorldActionRejection rejection)
    {
        Assert.Equal(rejection, plan.Rejection);
        Assert.Same(inventory, plan.CandidateInventory);
        Assert.Same(production, plan.CandidateProduction);
        Assert.Null(plan.Claim);
        Assert.Empty(plan.CreatedIds);
    }

    private static object ClaimFields(ConsumedItemClaim claim) => new
    {
        claim.ConsumptionId, claim.CommandId, claim.ActorId, claim.RecipientActorId, claim.CustodianId,
        claim.Ingredient.LotId, claim.Ingredient.Revision, claim.Ingredient.Definition,
        claim.Ingredient.Kind, claim.Ingredient.Source, claim.Ingredient.Output
    };
}
