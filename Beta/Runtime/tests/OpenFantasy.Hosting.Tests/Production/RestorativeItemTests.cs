using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Hosting.Tests.Production.RestorativeSupplyFixture;

namespace OpenFantasy.Hosting.Tests.Production;

/// <summary>Source-backed component composition; complete-world permission, timing and persistence are separate gates.</summary>
public sealed class RestorativeItemTests
{
    [Theory]
    [InlineData(false, false, 7, 47)]
    [InlineData(false, true, 7, 47)]
    [InlineData(true, false, 7, 47)]
    [InlineData(true, true, 55, 60)]
    [InlineData(false, false, 0, 40)]
    public void Actual_potion_composes_exact_material_and_capped_health_for_self_or_another_actor(
        bool crafted, bool self, int hp, int expected)
    {
        var supply = new RestorativeSupplyFixture();
        var inventory = supply.Inventory;
        var production = supply.Production;
        IngredientRequest ingredient;
        if (crafted)
        {
            var craft = supply.Craft(inventory, production, Id(8100));
            Assert.Equal(WorldActionRejection.None, craft.Rejection);
            inventory = craft.CandidateInventory;
            production = craft.CandidateProduction;
            var lot = Assert.Single(production.Lots);
            ingredient = new(IngredientLotKind.Transformed, lot.LotId, lot.Revision, 1);
        }
        else ingredient = new(IngredientLotKind.Source, RawPotionLot, 0, 1);
        EntityId patient = self ? Actor : Recipient;
        var progression = Progression(inventory, patient, hp);
        var before = progression.GetActor(patient);
        var request = new ConsumeItemRequest(Storage, inventory.Custody.GetStorage(Storage).Revision,
            production.Revision, patient, supply.Potion, ingredient);
        uint amount = supply.PotionPayload.GetProperty("healHp").GetUInt32();
        Assert.Equal(40U, amount);

        var plan = new RestorativeItemPlanner().Plan(inventory, production, progression, Delivery, Actor,
            Id(8200), request, before.Revision, amount);

        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Assert.Equal(expected, plan.CandidateProgression.GetActor(patient).CurrentHp);
        Assert.Equal(3, plan.CandidateProgression.GetActor(patient).CurrentMp);
        Assert.Same(before, plan.ActorTransition!.Before);
        Assert.Equal(expected - hp, plan.ActorTransition.Health.Delta);
        Assert.Same(plan.ActorTransition.Candidate, plan.CandidateProgression.GetActor(patient));
        Assert.Equal(1UL, plan.CandidateProgression.Revision);
        Assert.Equal(1UL, plan.ActorTransition.Candidate.Revision);
        var claim = Assert.Single(plan.CandidateProduction.ConsumedItems);
        Assert.Same(claim, plan.Claim);
        Assert.Equal(patient, claim.RecipientActorId);
        Assert.Equal(Actor, claim.ActorId);
        Assert.Equal(1UL, claim.Ingredient.Quantity);
        Assert.Equal(supply.AccountedMass(inventory, production),
            supply.AccountedMass(plan.CandidateInventory, plan.CandidateProduction));
        Assert.Same(progression.Currency, plan.CandidateProgression.Currency);
        Assert.Equal(hp, before.CurrentHp);
        Assert.Empty(production.ConsumedItems);

        var restoredProgression = ProgressionCodecV1.Decode(ProgressionCodecV1.Encode(plan.CandidateProgression));
        var restoredInventory = AuthorityEncodingV1.Decode(AuthorityEncodingV1.Encode(plan.CandidateInventory));
        var restoredProduction = ProductionCodecV2.Decode(ProductionCodecV2.Encode(plan.CandidateProduction));
        var stale = new RestorativeItemPlanner().Plan(restoredInventory, restoredProduction, restoredProgression,
            Delivery, Actor, Id(8201), request, before.Revision, amount);
        AssertRejected(stale, restoredInventory, restoredProduction, restoredProgression);
        Assert.Equal(expected, restoredProgression.GetActor(patient).CurrentHp);
    }

    [Theory]
    [InlineData("full")]
    [InlineData("patient-revision")]
    [InlineData("actor-overflow")]
    [InlineData("progression-overflow")]
    [InlineData("lot")]
    [InlineData("storage")]
    [InlineData("quantity")]
    [InlineData("zero-effect")]
    [InlineData("control")]
    public void Failed_composition_retains_every_original_owner_and_exposes_no_claim_or_effect(string failure)
    {
        var supply = new RestorativeSupplyFixture();
        var progression = Progression(supply.Inventory, Recipient, failure == "full" ? 60 : 7,
            failure == "actor-overflow" ? ulong.MaxValue : 0, failure == "progression-overflow" ? ulong.MaxValue : 0);
        var request = new ConsumeItemRequest(Storage, failure == "storage" ? 1UL : 0, 0, Recipient, supply.Potion,
            new(IngredientLotKind.Source, RawPotionLot, failure == "lot" ? 1UL : 0, failure == "quantity" ? 2UL : 1));
        var delivery = failure == "control" ? new TrustedCommandDelivery(Id(999), Id(998)) : Delivery;
        var plan = new RestorativeItemPlanner().Plan(supply.Inventory, supply.Production, progression,
            delivery, Actor, Id(8200), request, failure == "patient-revision" ? 1 : progression.GetActor(Recipient).Revision,
            failure == "zero-effect" ? 0U : 40U);

        AssertRejected(plan, supply.Inventory, supply.Production, progression);
        Assert.Equal(failure == "control" ? WorldActionRejection.NotAuthorized
            : failure is "patient-revision" or "storage" ? WorldActionRejection.RevisionConflict
            : WorldActionRejection.Unavailable, plan.Rejection);
        Assert.Equal(2UL, supply.Inventory.Custody.GetLot(RawPotionLot).Quantity);
    }

    [Fact]
    public void Health_and_material_owners_cannot_cross_world_or_branch_scopes()
    {
        var supply = new RestorativeSupplyFixture();
        var progression = Progression(supply.Inventory, Recipient, 7, foreignScope: true);
        Assert.Throws<ArgumentException>(() => new RestorativeItemPlanner().Plan(supply.Inventory,
            supply.Production, progression, Delivery, Actor, Id(8200), supply.Request(supply.Inventory, supply.Production), 0, 40));
        Assert.Empty(supply.Production.ConsumedItems);
    }

    [Fact]
    public void Reused_consumption_command_does_not_apply_a_second_health_effect()
    {
        var supply = new RestorativeSupplyFixture();
        var progression = Progression(supply.Inventory, Recipient, 1);
        var planner = new RestorativeItemPlanner();
        var first = planner.Plan(supply.Inventory, supply.Production, progression, Delivery, Actor, Id(8200),
            supply.Request(supply.Inventory, supply.Production), 0, 10);
        Assert.Equal(WorldActionRejection.None, first.Rejection);
        var repeat = planner.Plan(first.CandidateInventory, first.CandidateProduction, first.CandidateProgression,
            Delivery, Actor, Id(8200), supply.Request(first.CandidateInventory, first.CandidateProduction), 1, 10);
        AssertRejected(repeat, first.CandidateInventory, first.CandidateProduction, first.CandidateProgression);
        Assert.Equal(11, first.CandidateProgression.GetActor(Recipient).CurrentHp);
    }

    private static ProgressionSnapshot Progression(CountedAuthorityState inventory, EntityId patient, int hp,
        ulong patientRevision = 0, ulong revision = 0, bool foreignScope = false)
    {
        var profile = new ActorStatProfileDefinition(Id(400), "test.squire", "Squire", ActorStatProfileKind.Class,
            new(60, 10, 12, 4, 8, 8), new(9, 2, 3, 1, 2, 1), ActorStatModifiers.Zero);
        var curve = new LevelProgressionDefinition(Id(401), "test.levels", [100, 282]);
        var currency = new CurrencyDefinition(Id(402), "gil", "Gil", "gil");
        var world = foreignScope ? Id(999) : inventory.WorldId;
        var ledger = new CurrencyLedger(world, inventory.BranchId, 0, currency, 0,
            [new(Id(403), CurrencyAccountKind.Actor, Actor, 0, 0),
                new(Id(404), CurrencyAccountKind.Actor, Recipient, 0, 0)]);
        return new(world, inventory.BranchId, revision, [profile], [curve], inventory.Actors.Select(id =>
            new ActorProgressionState(id, id == patient ? patientRevision : 0, profile, curve, 1, 0,
                id == patient ? hp : 60, 3)), ledger);
    }

    private static void AssertRejected(RestorativeItemPlan plan, CountedAuthorityState inventory,
        ProductionSnapshot production, ProgressionSnapshot progression)
    {
        Assert.NotEqual(WorldActionRejection.None, plan.Rejection);
        Assert.Same(inventory, plan.CandidateInventory);
        Assert.Same(production, plan.CandidateProduction);
        Assert.Same(progression, plan.CandidateProgression);
        Assert.Null(plan.Claim);
        Assert.Null(plan.ActorTransition);
    }
}
