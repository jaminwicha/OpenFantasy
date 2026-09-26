using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class ConsumptionRunoffCompositionTests
{
    [Fact]
    public void Consuming_actual_received_water_releases_only_its_occupied_capacity()
    {
        var state = Create();
        var request = Assert.Single(state.Pending).Request;
        var receiver = request.Terminals[0].CustodianId;
        var original = Assert.Single(state.Inventory.Custody.Lots, lot => lot.CustodianId == receiver);
        var plan = Consume(state, original, 1);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);

        var after = state.PlanReconcile(plan.CandidateInventory, plan.CandidateProduction,
            TestContext.Current.CancellationToken).ApplyTo(state);

        Assert.Equal(49UL, Assert.Single(after.Receivers).ExistingUnits);
        Assert.Equal(649UL, Assert.Single(after.Receivers).ProjectedUnits);
        Assert.Equal(new SourceInterval(original.Source.SourceId, original.Source.Start, original.Source.Start + 1),
            Assert.Single(after.Production!.ConsumedItems).Ingredient.Source);
        Assert.Equal(state.Inventory.Custody.Lots.Where(lot => lot.ReservedBy is not null),
            after.Inventory.Custody.Lots.Where(lot => lot.ReservedBy is not null));
        Assert.Equal(650UL, Assert.Single(state.Receivers).ProjectedUnits);
        Assert.Single(after.Pending);
    }

    [Theory]
    [InlineData("erase")]
    [InlineData("rewrite")]
    [InlineData("downgrade")]
    public void Runoff_successors_cannot_erase_rewrite_or_downgrade_consumed_source_history(string change)
    {
        var state = Create();
        var receiver = state.Domain.Terminals[0].CustodianId;
        var raw = Assert.Single(state.Inventory.Custody.Lots, lot => lot.CustodianId == receiver);
        var plan = Consume(state, raw, raw.Quantity);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        state = state.PlanReconcile(plan.CandidateInventory, plan.CandidateProduction,
            TestContext.Current.CancellationToken).ApplyTo(state);
        var proof = state.Production!;
        var claim = Assert.Single(proof.ConsumedItems);
        var lots = change == "rewrite" ? state.Inventory.Custody.Lots.AsEnumerable() : state.Inventory.Custody.Lots.Append(raw);
        var inventory = Rebuild(state.Inventory, lots, state.Inventory.Custody.Revision + 1);
        ConsumedItemClaim[] claims = change == "rewrite"
            ? [new(claim.ConsumptionId, claim.CommandId, claim.ActorId, claim.RecipientActorId, claim.CustodianId,
                new RetiredIngredient(Id(8501), claim.Ingredient.Revision, claim.Ingredient.Definition, claim.Ingredient.Source!))]
            : [];
        var candidate = new ProductionSnapshot(proof.WorldId, proof.BranchId, proof.Revision + 1,
            proof.Recipes, proof.Batches, proof.Lots, change == "downgrade" ? null : claims);
        candidate.ValidateForAuthority(inventory);
        // The supplied complete request is internally consistent; only retained history proves
        // that these newly supplied facts would rewrite the existing capacity owner.
        _ = Reprepare(Assert.Single(state.Pending).Request, inventory, candidate);

        Assert.Throws<ArgumentException>(() => state.PlanReconcile(inventory, candidate, TestContext.Current.CancellationToken));
        Assert.Equal(proof.ContentHash, state.Production!.ContentHash);
    }

    [Fact]
    public void Empty_consumption_capability_is_part_of_the_retained_capacity_contract()
    {
        var state = Create();
        var proof = state.Production!;
        var inventory = Rebuild(state.Inventory, state.Inventory.Custody.Lots, 1);
        var legacy = new ProductionSnapshot(proof.WorldId, proof.BranchId, 1, [], [], []);
        legacy.ValidateForAuthority(inventory);

        Assert.Throws<ArgumentException>(() => state.PlanReconcile(inventory, legacy, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void A_fully_consumed_lot_cannot_claim_the_current_or_future_runoff_custody_revision()
    {
        var state = Create();
        var request = Assert.Single(state.Pending).Request;
        var raw = Assert.Single(state.Inventory.Custody.Lots, lot => lot.CustodianId == state.Domain.Terminals[0].CustodianId);
        var plan = Consume(state, raw, raw.Quantity);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        var priorClaim = plan.Claim!;
        var claim = new ConsumedItemClaim(priorClaim.ConsumptionId, priorClaim.CommandId, priorClaim.ActorId,
            priorClaim.RecipientActorId, priorClaim.CustodianId,
            new(priorClaim.Ingredient.LotId, plan.CandidateInventory.Custody.Revision,
                priorClaim.Ingredient.Definition, priorClaim.Ingredient.Source!));
        var proof = plan.CandidateProduction;
        var invalid = new ProductionSnapshot(proof.WorldId, proof.BranchId, proof.Revision,
            proof.Recipes, proof.Batches, proof.Lots, [claim]);
        invalid.ValidateForAuthority(plan.CandidateInventory);

        Assert.Throws<ArgumentException>(() => Reprepare(request, plan.CandidateInventory, invalid));
        Assert.Throws<ArgumentException>(() => state.PlanReconcile(plan.CandidateInventory, invalid, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Outstanding_runoff_funding_cannot_be_consumed_as_unreserved_material()
    {
        var state = Create();
        var request = Assert.Single(state.Pending).Request;
        var held = Assert.Single(state.Inventory.Custody.Lots, lot => lot.ReservedBy == request.ReservationId);
        var plan = Consume(state, held, 1);

        Assert.Equal(WorldActionRejection.Unavailable, plan.Rejection);
        Assert.Same(state.Inventory, plan.CandidateInventory);
        Assert.Same(state.Production, plan.CandidateProduction);
    }

    private static WatershedRunoffCapacityState Create()
    {
        var request = WatershedRunoffCapacityTests.Create(650_000_000).First.Request;
        var production = new ProductionSnapshot(request.Inventory.WorldId, request.Inventory.BranchId, 0, [], [], [], []);
        return WatershedRunoffCapacityState.Create(Reprepare(request, request.Inventory, production));
    }

    private static ItemConsumptionPlan Consume(WatershedRunoffCapacityState state, CountedSourceLot lot, ulong quantity)
        => new ItemConsumptionPlanner().Plan(state.Inventory, state.Production!, new(Id(1), Id(2)),
            state.Domain.OwnerActorId, Id(8500), new(lot.CustodianId,
                state.Inventory.Custody.GetStorage(lot.CustodianId).Revision, state.Production!.Revision,
                state.Domain.OwnerActorId, lot.Definition, new(IngredientLotKind.Source, lot.LotId, lot.Revision, quantity)));

    private static PreparedWatershedRunoff Reprepare(WatershedRunoffRequest request, CountedAuthorityState inventory,
        ProductionSnapshot production) => new WatershedRunoffPlanner().Prepare(new(request.Network, request.Mapping,
            inventory, production, request.ReservationId, request.OwnerActorId, request.Start, request.End,
            request.Material, request.Inputs, request.Terminals), TestContext.Current.CancellationToken);

    private static CountedAuthorityState Rebuild(CountedAuthorityState basis, IEnumerable<CountedSourceLot> lots, ulong revision)
        => new(new(basis.WorldId, basis.BranchId, revision, basis.Custody.Storages, lots),
            basis.AdmittedUnitCapacity, basis.Actors, basis.Controls, basis.Policies, basis.Sources, basis.ReservationIds);

    private static EntityId Id(ulong value) => new(0x6361706163697479, value);
}
