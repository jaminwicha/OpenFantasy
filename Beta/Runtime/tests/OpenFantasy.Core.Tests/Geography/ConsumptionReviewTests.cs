using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Tests.Geography;

// Independent review regressions for the material/consumption composition. Each test targets an
// invariant that the candidate's own tests left unexercised; none changes a documented behavior.
public sealed partial class TerrainMaterialRegistryTests
{
    [Fact]
    public void Legacy_registry_reconciliation_cannot_silently_acquire_the_consumption_profile()
    {
        var registry = WithProduction(Create().Registry);
        var proof = registry.Production!;
        Assert.False(proof.SupportsConsumption);
        var advanced = CopyInventory(registry.Inventory, new(proof.WorldId, proof.BranchId, registry.Revision + 1,
            registry.Inventory.Custody.Storages, registry.Inventory.Custody.Lots));
        // Same recipes, no batches, no claims: only the ownership profile differs.
        var upgraded = new ProductionSnapshot(proof.WorldId, proof.BranchId, proof.Revision + 1, proof.Recipes, [], [], []);
        upgraded.ValidateForAuthority(advanced);

        Assert.Throws<ArgumentException>(() => registry.ReconcileProduction(advanced, upgraded));

        Assert.Equal(proof.ContentHash, registry.Production!.ContentHash);
        Assert.False(registry.Production!.SupportsConsumption);
        _ = TerrainMaterialRegistryCodecV2.Encode(registry);
        Assert.Throws<ArgumentException>(() => TerrainMaterialRegistryCodecV3.Encode(registry));
    }
}

public sealed class ConsumptionReviewCapacityTests
{
    [Fact]
    public void Legacy_capacity_reconciliation_cannot_silently_acquire_the_consumption_profile()
    {
        var request = WatershedRunoffCapacityTests.Create(650_000_000).First.Request;
        var legacy = new ProductionSnapshot(request.Inventory.WorldId, request.Inventory.BranchId, 0, [], [], []);
        var state = WatershedRunoffCapacityState.Create(Reprepare(request, request.Inventory, legacy));
        Assert.False(state.Production!.SupportsConsumption);
        var inventory = Rebuild(state.Inventory, state.Inventory.Custody.Lots, state.Inventory.Custody.Revision + 1);
        var upgraded = new ProductionSnapshot(legacy.WorldId, legacy.BranchId, 1, [], [], [], []);
        upgraded.ValidateForAuthority(inventory);
        // The standalone runoff planner accepts the enabled owner; only retained history refuses the profile change.
        _ = Reprepare(request, inventory, upgraded);

        Assert.Throws<ArgumentException>(() => state.PlanReconcile(inventory, upgraded, TestContext.Current.CancellationToken));

        Assert.False(state.Production!.SupportsConsumption);
        _ = WatershedRunoffCapacityCodecV1.Encode(state);
        Assert.Throws<ArgumentException>(() => WatershedRunoffCapacityCodecV2.Encode(state));
    }

    [Fact]
    public void A_structurally_valid_claim_on_held_forcing_stock_is_refused_at_capacity_reconciliation()
    {
        var request = WatershedRunoffCapacityTests.Create(650_000_000).First.Request;
        var enabled = new ProductionSnapshot(request.Inventory.WorldId, request.Inventory.BranchId, 0, [], [], [], []);
        var state = WatershedRunoffCapacityState.Create(Reprepare(request, request.Inventory, enabled));
        var held = Assert.Single(state.Inventory.Custody.Lots, lot => lot.ReservedBy == request.ReservationId);
        EntityId command = Id(1);
        var claim = new ConsumedItemClaim(EntityId.DeriveSpawned(command, 0), command, state.Domain.OwnerActorId,
            state.Domain.OwnerActorId, held.CustodianId, new RetiredIngredient(held.LotId, held.Revision, held.Definition,
                new SourceInterval(held.Source.SourceId, held.Source.Start, held.Source.Start + 1)));
        // The forged successor retires the first held unit and keeps the remainder under the same hold.
        var remainder = new CountedSourceLot(held.LotId, held.CustodianId, held.Definition,
            new SourceInterval(held.Source.SourceId, held.Source.Start + 1, held.Source.End), held.Revision + 1,
            held.Protection, held.ReservedBy);
        var inventory = Rebuild(state.Inventory,
            state.Inventory.Custody.Lots.Select(lot => lot.LotId == held.LotId ? remainder : lot),
            state.Inventory.Custody.Revision + 1);
        var forged = new ProductionSnapshot(enabled.WorldId, enabled.BranchId, 1, [], [], [], [claim]);
        forged.ValidateForAuthority(inventory);

        Assert.Throws<ArgumentException>(() => state.PlanReconcile(inventory, forged, TestContext.Current.CancellationToken));

        Assert.Empty(state.Production!.ConsumedItems);
        Assert.Equal(held, Assert.Single(state.Inventory.Custody.Lots, lot => lot.LotId == held.LotId));
    }

    private static PreparedWatershedRunoff Reprepare(WatershedRunoffRequest request, CountedAuthorityState inventory,
        ProductionSnapshot production) => new WatershedRunoffPlanner().Prepare(new(request.Network, request.Mapping,
            inventory, production, request.ReservationId, request.OwnerActorId, request.Start, request.End,
            request.Material, request.Inputs, request.Terminals), TestContext.Current.CancellationToken);

    private static CountedAuthorityState Rebuild(CountedAuthorityState basis, IEnumerable<CountedSourceLot> lots, ulong revision)
        => new(new(basis.WorldId, basis.BranchId, revision, basis.Custody.Storages, lots),
            basis.AdmittedUnitCapacity, basis.Actors, basis.Controls, basis.Policies, basis.Sources, basis.ReservationIds);

    private static EntityId Id(ulong value) => new(0x7265766965772e63, value);
}
