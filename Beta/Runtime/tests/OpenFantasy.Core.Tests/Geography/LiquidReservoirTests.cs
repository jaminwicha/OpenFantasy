using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class LiquidReservoirTests
{
    private static EntityId Id(ulong value) => new(0x7265736572766f69, value);

    [Fact]
    public void GeometryFundsSourcesAndDistinguishesActualFromHeldReceivingVolume()
    {
        var (state, second) = Create();
        Assert.Equal(1_300_000_000UL, state.Projections.Single(p => p.Geometry.ReservoirId == Id(1)).ActualFill.Volume.Value);
        var receiver = state.Projections.Single(p => p.Geometry.ReservoirId == Id(2));
        Assert.Equal(50_000_000UL, receiver.ActualFill.Volume.Value);
        Assert.Equal(650_000_000UL, receiver.ProjectedFill.Volume.Value);
        byte[] inventory = AuthorityEncodingV1.Encode(state.Capacity.Inventory);
        var next = state.PlanApply(state.Capacity.PlanAdmit(second)).ApplyTo(state);
        Assert.Equal(1_250_000_000UL, next.Projections.Single(p => p.Geometry.ReservoirId == Id(2)).ProjectedFill.Volume.Value);
        Assert.Equal(inventory, AuthorityEncodingV1.Encode(next.Capacity.Inventory));
        Retain(next, "two-held");
    }

    [Fact]
    public void SourcesCountAllContentsAndReceiverWitnessMustMatchGeometryExactly()
    {
        var (state, _) = Create();
        var source = state.Geometries.Single(g => g.ReservoirId == Id(1));
        var receiver = state.Geometries.Single(g => g.ReservoirId == Id(2));
        Assert.Throws<ArgumentException>(() => LiquidReservoirState.Create(state.Capacity, [receiver]));
        Assert.Throws<ArgumentOutOfRangeException>(() => LiquidReservoirState.Create(state.Capacity,
            [new(source.Scope, source.ReservoirId, source.CustodianId, 1, [new(-1, 0, 0)]), receiver]));
        Assert.Throws<ArgumentException>(() => LiquidReservoirState.Create(state.Capacity,
            [source, new(receiver.Scope, receiver.ReservoirId, receiver.CustodianId, 3, receiver.Columns)]));
    }

    [Fact]
    public void StackedStorageIsSeparateButOverlappingOrAliasedStorageRejects()
    {
        var (state, _) = Create();
        var source = state.Geometries[0]; var receiver = state.Geometries[1];
        Assert.Throws<ArgumentException>(() => LiquidReservoirState.Create(state.Capacity,
            [source, new(receiver.Scope, receiver.ReservoirId, receiver.CustodianId, 2, source.Columns)]));
        var stacked = LiquidReservoirState.Create(state.Capacity,
            [source, new(receiver.Scope, receiver.ReservoirId, receiver.CustodianId, 4, [new(-1, 0, 2)])]);
        Assert.Equal(2, stacked.Geometries.Count);
        Assert.Throws<ArgumentException>(() => LiquidReservoirState.Create(state.Capacity,
            [new(source.Scope, state.Capacity.Domain.OwnerActorId, source.CustodianId, source.SpillZ, source.Columns), receiver]));
        Assert.Throws<ArgumentException>(() => LiquidReservoirState.Create(state.Capacity,
            [new(source.Scope with { RealmId = Id(50) }, source.ReservoirId, source.CustodianId, source.SpillZ, source.Columns), receiver]));
        Retain(stacked, "stacked");
    }

    [Fact]
    public void CanceledSourcesRemainPhysicalAndActualDeliveryCannotOverfillThem()
    {
        var (initial, _) = Create();
        var request = initial.Capacity.Pending[0].Request; var basis = request.Inventory;
        EntityId buffer = Id(60), lotId = Id(61);
        var inventory = new CountedAuthorityState(new(basis.WorldId, basis.BranchId, 0,
            basis.Custody.Storages.Append(new(buffer, 1000, 0)), basis.Custody.Lots.Append(new(lotId, buffer,
                request.Material.Item, new(request.Inputs[0].Source!.SourceId, 2200, 3000), 0))),
            basis.AdmittedUnitCapacity, basis.Actors, basis.Controls,
            basis.Policies.Append(new(buffer, request.OwnerActorId, CustodyDepositRule.OwnerOnly)), basis.Sources, basis.ReservationIds);
        var capacity = WatershedRunoffCapacityState.Create(Reprepare(request, inventory, request.Production));
        var state = LiquidReservoirState.Create(capacity, initial.Geometries.Append(new(request.Network.Scope, Id(62), buffer, 1, [new(10, 0, 0)])));
        state = state.PlanApply(state.Capacity.PlanCancel(request.ReservationId)).ApplyTo(state);
        Assert.Empty(state.Capacity.Pending);
        var control = inventory.Controls[0];
        var spareReservation = inventory.ReservationIds.Single(id => id != request.ReservationId
            && !inventory.Custody.Lots.Any(l => l.ReservedBy == id));
        var delivery = new SourceDeliveryPlanner().Plan(inventory, request.Production!, new(control.PrincipalId, control.SessionId),
            request.OwnerActorId, Id(63), new(buffer, 0, request.Inputs[0].CustodianId!.Value, request.Material.Item,
                800, spareReservation, [new(lotId, 0, 800)]));
        Assert.Equal(WorldActionRejection.None, delivery.Rejection);
        var capacityPlan = state.Capacity.PlanReconcile(delivery.Candidate, request.Production, TestContext.Current.CancellationToken);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.PlanApply(capacityPlan));
        Assert.Equal(3, state.Geometries.Count);
        Retain(state, "canceled-buffer");
    }

    [Fact]
    public void WrapperPlansPinGeometryAsWellAsTheCapacityOwner()
    {
        var (state, second) = Create();
        var plan = state.PlanApply(state.Capacity.PlanAdmit(second));
        var moved = LiquidReservoirState.Create(state.Capacity, state.Geometries.Select(g =>
            new ReservoirGeometry(g.Scope, g.ReservoirId, g.CustodianId, g.SpillZ,
                g.Columns.Select(c => c with { Y = 100 }))));
        Assert.Throws<InvalidOperationException>(() => plan.ApplyTo(moved));
        var next = plan.ApplyTo(state);
        Assert.Throws<InvalidOperationException>(() => plan.ApplyTo(next));
        Assert.Throws<InvalidOperationException>(() => next.PlanApply(state.Capacity.PlanCancel(state.Capacity.Pending[0].Request.ReservationId)));
    }

    [Fact]
    public void RealDerivedSourceContentsRemainInPhysicalVolumeAccounting()
    {
        var (initial, _) = Create(); var request = initial.Capacity.Pending[0].Request;
        var material = request.Material;
        var physical = new MaterialRecipeDefinition(material.Physical.Package, Id(70), "repack_liquid", new(1), null,
            new MaterialCatalog([material.Physical]), [new(RecipeMaterialRole.Input, material.Physical.Pin(), 100),
                new(RecipeMaterialRole.Output, material.Physical.Pin(), 100)]);
        var recipe = new CraftRecipeDefinition(Id(71), physical, [material], [new(material.Item, 0, material.Physical.MassPerUnit)]);
        var production = new ProductionSnapshot(request.Inventory.WorldId, request.Inventory.BranchId, 0, [recipe], [], []);
        var state = LiquidReservoirState.Create(WatershedRunoffCapacityState.Create(Reprepare(request, request.Inventory, production)), initial.Geometries);
        EntityId source = request.Inputs[0].CustodianId!.Value;
        var lot = request.Inventory.Custody.Lots.Single(l => l.CustodianId == source && l.ReservedBy is null);
        var control = request.Inventory.Controls[0];
        var craft = new InstantCraftPlanner().Plan(request.Inventory, production, new(control.PrincipalId, control.SessionId), request.OwnerActorId, Id(72),
            new(recipe.DefinitionId, recipe.Version, 0, source, 0, [new(IngredientLotKind.Source, lot.LotId, 0, 100)]));
        Assert.Equal(WorldActionRejection.None, craft.Rejection);
        var next = state.PlanApply(state.Capacity.PlanReconcile(craft.CandidateInventory, craft.CandidateProduction,
            TestContext.Current.CancellationToken)).ApplyTo(state);
        Assert.Equal(1_300_000_000UL, next.Projections.Single(p => p.Geometry.CustodianId == source).ActualFill.Volume.Value);
        Assert.Single(next.Capacity.Production!.Lots);
        Retain(next, "crafted-source");
    }

    [Fact]
    public void ArchiveBoundsWitnessesAndGlobalColumnBudgetAreEnforced()
    {
        var (initial, _) = Create(); var source = initial.Geometries[0]; var receiver = initial.Geometries[1];
        var maximum = LiquidReservoirState.Create(initial.Capacity, [new(source.Scope, source.ReservoirId, source.CustodianId,
            source.SpillZ, Enumerable.Range(0, 4095).Select(i => new ReservoirColumn(i, 20, 0))), receiver]);
        byte[] bytes = LiquidReservoirCodecV1.Encode(maximum);
        Assert.Equal(bytes, LiquidReservoirCodecV1.Encode(LiquidReservoirCodecV1.Decode(bytes, TestContext.Current.CancellationToken)));
        Assert.Throws<ArgumentException>(() => LiquidReservoirState.Create(initial.Capacity, [new(source.Scope, source.ReservoirId,
            source.CustodianId, source.SpillZ, Enumerable.Range(0, 4096).Select(i => new ReservoirColumn(i, 20, 0))), receiver]));
        byte[] corrupt = (byte[])bytes.Clone(); corrupt[^1] ^= 1;
        foreach (var bad in new[] { bytes[..^1], bytes.Concat(new byte[] { 0 }).ToArray(), corrupt,
            new byte[LiquidReservoirCodecV1.MaximumEncodedByteLength + 1] })
        {
            Assert.Throws<FormatException>(() => LiquidReservoirCodecV1.Decode(bad, TestContext.Current.CancellationToken));
        }
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => LiquidReservoirCodecV1.Decode(bytes, canceled.Token));
        Retain(maximum, "maximum-columns");
    }

    [Fact]
    public void UnmappedNewSourceCannotBypassThePhysicalOwnerAtAdmission()
    {
        var (initial, second) = Create(); var first = initial.Capacity.Pending[0].Request;
        var basis = first.Inventory; EntityId other = Id(90);
        var inventory = new CountedAuthorityState(new(basis.WorldId, basis.BranchId, 0,
            basis.Custody.Storages.Append(new(other, 1000, 0)), basis.Custody.Lots.Select(l =>
                l.ReservedBy == second.Request.ReservationId ? new CountedSourceLot(l.LotId, other, l.Definition, l.Source,
                    l.Revision, l.Protection, l.ReservedBy) : l)), basis.AdmittedUnitCapacity, basis.Actors, basis.Controls,
            basis.Policies.Append(new(other, first.OwnerActorId, CustodyDepositRule.OwnerOnly)), basis.Sources, basis.ReservationIds);
        var state = LiquidReservoirState.Create(WatershedRunoffCapacityState.Create(Reprepare(first, inventory, first.Production)), initial.Geometries);
        var request = second.Request;
        var rerouted = new WatershedRunoffPlanner().Prepare(new(request.Network, request.Mapping, inventory, request.Production,
            request.ReservationId, request.OwnerActorId, request.Start, request.End, request.Material,
            request.Inputs.Select(i => i.Source is { } source ? new WatershedRunoffInput(i.BasinId, i.DepthMicrometres, source, other) : i),
            request.Terminals), TestContext.Current.CancellationToken);
        Assert.Throws<ArgumentException>(() => state.PlanApply(state.Capacity.PlanAdmit(rerouted)));
    }

    [Fact]
    public void UnrelatedUncalibratedSourceCargoCannotDisappearFromPhysicalAccounting()
    {
        var (initial, _) = Create(); var request = initial.Capacity.Pending[0].Request; var basis = request.Inventory;
        var other = new CountedItemDefinition(Id(91), ContentHash.Compute("unknown-cargo"u8));
        var inventory = new CountedAuthorityState(new(basis.WorldId, basis.BranchId, 0, basis.Custody.Storages,
            basis.Custody.Lots.Append(new(Id(92), request.Inputs[0].CustodianId!.Value, other, new(Id(93), 0, 1), 0))),
            basis.AdmittedUnitCapacity + 1, basis.Actors, basis.Controls, basis.Policies,
            basis.Sources.Append(new(Id(93), other, 1)), basis.ReservationIds);
        var capacity = WatershedRunoffCapacityState.Create(Reprepare(request, inventory, request.Production));
        Assert.Throws<ArgumentException>(() => LiquidReservoirState.Create(capacity, initial.Geometries));
    }

    private static (LiquidReservoirState State, PreparedWatershedRunoff Second) Create()
    {
        var (first, second) = WatershedRunoffCapacityTests.Create(2_000_000_000);
        var request = first.Request;
        return (LiquidReservoirState.Create(WatershedRunoffCapacityState.Create(first),
            [new(request.Network.Scope, Id(1), request.Inputs[0].CustodianId!.Value, 2, [new(-1, 0, 0)]),
                new(request.Network.Scope, Id(2), request.Terminals[0].CustodianId, 2, [new(0, 0, 0)])]), second);
    }

    private static PreparedWatershedRunoff Reprepare(WatershedRunoffRequest request, CountedAuthorityState inventory,
        ProductionSnapshot? production) => new WatershedRunoffPlanner().Prepare(new(request.Network, request.Mapping, inventory,
            production, request.ReservationId, request.OwnerActorId, request.Start, request.End, request.Material, request.Inputs, request.Terminals),
            TestContext.Current.CancellationToken);

    private static void Retain(LiquidReservoirState state, string name)
    {
        string? path = Environment.GetEnvironmentVariable("OPENFANTASY_RESERVOIR_EVIDENCE");
        if (string.IsNullOrWhiteSpace(path)) return;
        Directory.CreateDirectory(path);
        File.WriteAllBytes(Path.Combine(path, name + ".bin"), LiquidReservoirCodecV1.Encode(state));
    }
}
