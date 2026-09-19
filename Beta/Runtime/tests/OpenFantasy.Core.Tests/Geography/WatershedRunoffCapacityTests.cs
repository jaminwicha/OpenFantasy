using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class WatershedRunoffCapacityTests
{
    private static EntityId Id(ulong value) => new(0x6361706163697479, value);

    [Fact]
    public void SeparateValidBudgetsCannotPromiseTheSameReceivingHeadroom()
    {
        var (first, second) = Create(1_000_000_000);
        var state = WatershedRunoffCapacityState.Create(first);
        Assert.Equal(650UL, Assert.Single(state.Receivers).ProjectedUnits);
        Assert.Equal(650UL, Assert.Single(second.Receivers).ProjectedUnits);
        Assert.Throws<ArgumentException>(() => state.PlanAdmit(second));
        Assert.Single(state.Pending);
        var roomy = Create(1_250_000_000);
        var basis = roomy.First.Request.Inventory;
        EntityId receiver = roomy.First.Request.Terminals[0].CustodianId;
        var countLimited = new CountedAuthorityState(new(basis.WorldId, basis.BranchId, 0,
            basis.Custody.Storages.Select(s => s.CustodianId == receiver ? s with { UnitCapacity = 1000 } : s), basis.Custody.Lots),
            basis.AdmittedUnitCapacity, basis.Actors, basis.Controls, basis.Policies, basis.Sources, basis.ReservationIds);
        var countState = WatershedRunoffCapacityState.Create(Reprepare(roomy.First.Request, countLimited, roomy.First.Request.Production));
        Assert.Throws<ArgumentException>(() => countState.PlanAdmit(Reprepare(roomy.Second.Request, countLimited, roomy.Second.Request.Production)));
    }

    [Fact]
    public void ActualDeliveryCannotConsumeAnOutstandingCapacityHold()
    {
        var (first, _) = Create(650_000_000);
        var state = WatershedRunoffCapacityState.Create(first);
        var production = state.Production!;
        var inventory = state.Inventory;
        var delivery = new SourceDeliveryPlanner().Plan(inventory, production, new(Id(1), Id(2)),
            first.Request.OwnerActorId, Id(90), new(first.Request.Inputs[0].CustodianId!.Value, 0,
                first.Request.Terminals[0].CustodianId, first.Request.Material.Item, 100, Id(4),
                [new(Id(20), 0, 100)]));
        Assert.Equal(WorldActionRejection.None, delivery.Rejection);
        Assert.Throws<ArgumentException>(() => state.PlanReconcile(delivery.Candidate, production, TestContext.Current.CancellationToken));
        Assert.Equal(inventory.ContentHash, state.Inventory.ContentHash);
        var canceled = state.PlanCancel(first.Request.ReservationId).ApplyTo(state);
        var accepted = canceled.PlanReconcile(delivery.Candidate, production, TestContext.Current.CancellationToken).ApplyTo(canceled);
        Assert.Equal(150UL, Assert.Single(accepted.Receivers).ExistingUnits);
        Assert.Empty(accepted.Pending);
    }

    [Fact]
    public void CapacityMutationsHaveTheirOwnRevisionAndCannotBeReplayed()
    {
        var (first, second) = Create(1_250_000_000);
        var initial = WatershedRunoffCapacityState.Create(first);
        var admit = initial.PlanAdmit(second);
        var staleCancel = initial.PlanCancel(first.Request.ReservationId);
        var both = admit.ApplyTo(initial);
        Assert.Equal(initial.Inventory.ContentHash, both.Inventory.ContentHash);
        Assert.Equal(1UL, both.Revision);
        Assert.Equal(1250UL, Assert.Single(both.Receivers).ProjectedUnits);
        Assert.Throws<InvalidOperationException>(() => admit.ApplyTo(both));
        Assert.Throws<InvalidOperationException>(() => staleCancel.ApplyTo(both));
        Assert.Throws<ArgumentException>(() => both.PlanAdmit(first));
        var canceled = both.PlanCancel(first.Request.ReservationId).ApplyTo(both);
        Assert.Equal(650UL, Assert.Single(canceled.Receivers).ProjectedUnits);
        Assert.Equal(both.Inventory.ContentHash, canceled.Inventory.ContentHash);
        Assert.Contains(canceled.Inventory.Custody.Lots, l => l.ReservedBy == first.Request.ReservationId);
        Assert.Throws<ArgumentException>(() => canceled.PlanCancel(first.Request.ReservationId));
        Assert.Throws<ArgumentException>(() => canceled.PlanAdmit(first));
        Retain(both, "two-held");
        Retain(canceled, "one-canceled");
    }

    [Fact]
    public void ReconciliationChecksAggregateHoldsNotOnlyEachStandaloneBudget()
    {
        var (first, second) = Create(1_250_000_000);
        var initial = WatershedRunoffCapacityState.Create(first);
        var state = initial.PlanAdmit(second).ApplyTo(initial);
        var delivery = new SourceDeliveryPlanner().Plan(state.Inventory, state.Production!, new(Id(1), Id(2)),
            first.Request.OwnerActorId, Id(90), new(first.Request.Inputs[0].CustodianId!.Value, 0,
                first.Request.Terminals[0].CustodianId, first.Request.Material.Item, 100, Id(4), [new(Id(20), 0, 100)]));
        Assert.Equal(WorldActionRejection.None, delivery.Rejection);
        foreach (var pending in state.Pending)
        {
            Assert.Equal(750UL, Assert.Single(Reprepare(pending.Request, delivery.Candidate, state.Production).Receivers).ProjectedUnits);
        }
        Assert.Throws<ArgumentException>(() => state.PlanReconcile(delivery.Candidate, state.Production, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AdmissionPinsInventoryProductionAndPhysicalCapacity()
    {
        var (first, second) = Create(1_250_000_000);
        var state = WatershedRunoffCapacityState.Create(first);
        var request = second.Request;
        Assert.Throws<ArgumentException>(() => state.PlanAdmit(Reprepare(request, request.Inventory, null)));
        var advanced = Rebuild(request.Inventory, request.Inventory.Custody.Lots, 1);
        Assert.Throws<ArgumentException>(() => state.PlanAdmit(Reprepare(request, advanced, request.Production)));
        var changed = new WatershedRunoffPlanner().Prepare(new(request.Network, request.Mapping, request.Inventory,
            request.Production, request.ReservationId, request.OwnerActorId, request.Start, request.End, request.Material,
            request.Inputs, request.Terminals.Select(t => new WatershedRunoffTerminal(t.BasinId, t.CustodianId, new(1_250_000_001)))),
            TestContext.Current.CancellationToken);
        Assert.Throws<ArgumentException>(() => state.PlanAdmit(changed));
    }

    [Fact]
    public void ReconciliationPreservesSourceHoldsAndRejectsAuthorityRollbackOrOmittedProduction()
    {
        var (first, _) = Create(1_250_000_000);
        var state = WatershedRunoffCapacityState.Create(first);
        var changed = Rebuild(state.Inventory, state.Inventory.Custody.Lots.Select(l => l.ReservedBy == first.Request.ReservationId
            ? new CountedSourceLot(l.LotId, l.CustodianId, l.Definition, l.Source, 1, l.Protection) : l), 1);
        Assert.Throws<ArgumentException>(() => state.PlanReconcile(changed, state.Production, TestContext.Current.CancellationToken));
        var advanced = Rebuild(state.Inventory, state.Inventory.Custody.Lots, 1);
        Assert.Throws<ArgumentException>(() => state.PlanReconcile(advanced, null, TestContext.Current.CancellationToken));
        var next = state.PlanReconcile(advanced, state.Production, TestContext.Current.CancellationToken).ApplyTo(state);
        Assert.Throws<ArgumentException>(() => next.PlanReconcile(state.Inventory, state.Production, TestContext.Current.CancellationToken));
        Assert.Same(next, next.PlanReconcile(advanced, next.Production, TestContext.Current.CancellationToken).ApplyTo(next));
    }

    [Fact]
    public void ActualCraftedOccupancyAndRetainedProductionHistorySurviveReconciliation()
    {
        var (first, _) = Create(650_000_000);
        var request = first.Request;
        var material = request.Material;
        var physical = new MaterialRecipeDefinition(material.Physical.Package, Id(800), "repack_water", new(1), null,
            new MaterialCatalog([material.Physical]), [new(RecipeMaterialRole.Input, material.Physical.Pin(), 50),
                new(RecipeMaterialRole.Output, material.Physical.Pin(), 50)]);
        var recipe = new CraftRecipeDefinition(Id(801), physical, [material], [new(material.Item, 0, material.Physical.MassPerUnit)]);
        var production = new ProductionSnapshot(request.Inventory.WorldId, request.Inventory.BranchId, 0, [recipe], [], []);
        var state = WatershedRunoffCapacityState.Create(Reprepare(request, request.Inventory, production));
        var receiver = request.Terminals[0].CustodianId;
        var lot = request.Inventory.Custody.Lots.Single(l => l.CustodianId == receiver);
        var craft = new InstantCraftPlanner().Plan(state.Inventory, production, new(Id(1), Id(2)), request.OwnerActorId, Id(802),
            new(recipe.DefinitionId, recipe.Version, 0, receiver, 0, [new(IngredientLotKind.Source, lot.LotId, 0, 50)]));
        Assert.Equal(WorldActionRejection.None, craft.Rejection);
        var next = state.PlanReconcile(craft.CandidateInventory, craft.CandidateProduction, TestContext.Current.CancellationToken).ApplyTo(state);
        Assert.Equal(650UL, Assert.Single(next.Receivers).ProjectedUnits);
        Assert.Single(next.Production!.Lots);
        var advanced = Rebuild(next.Inventory, next.Inventory.Custody.Lots, 2);
        var erased = new ProductionSnapshot(production.WorldId, production.BranchId, 2, [recipe], [], []);
        Assert.Throws<ArgumentException>(() => next.PlanReconcile(advanced, erased, TestContext.Current.CancellationToken));
        Retain(next, "crafted-held");
    }

    [Fact]
    public void ArchivesRetainEmptyOwnerCalibrationAndClosedIdentities()
    {
        var (first, _) = Create(650_000_000);
        var initial = WatershedRunoffCapacityState.Create(first);
        var state = initial.PlanCancel(first.Request.ReservationId).ApplyTo(initial);
        byte[] bytes = WatershedRunoffCapacityCodecV1.Encode(state);
        var restored = WatershedRunoffCapacityCodecV1.Decode(bytes, TestContext.Current.CancellationToken);
        Assert.Equal(bytes, WatershedRunoffCapacityCodecV1.Encode(restored));
        Assert.Equal(state.Domain.ContentHash, restored.Domain.ContentHash);
        Assert.Equal(50UL, Assert.Single(restored.Receivers).ProjectedUnits);
        Assert.Throws<ArgumentException>(() => restored.PlanAdmit(first));
        var releasedInventory = Rebuild(restored.Inventory, restored.Inventory.Custody.Lots.Select(l =>
            l.ReservedBy == first.Request.ReservationId
                ? new CountedSourceLot(l.LotId, l.CustodianId, l.Definition, l.Source, 1, l.Protection) : l), 1);
        var released = restored.PlanReconcile(releasedInventory, restored.Production, TestContext.Current.CancellationToken).ApplyTo(restored);
        Assert.DoesNotContain(released.Inventory.Custody.Lots, l => l.ReservedBy == first.Request.ReservationId);
        Assert.Equal(restored.Closed, released.Closed);
        byte[] corrupt = (byte[])bytes.Clone(); corrupt[^1] ^= 1;
        foreach (byte[] bad in new[] { corrupt, bytes[..^1], bytes.Concat(new byte[] { 0 }).ToArray(),
            new byte[WatershedRunoffCapacityCodecV1.MaximumEncodedByteLength + 1] })
        {
            Assert.Throws<FormatException>(() => WatershedRunoffCapacityCodecV1.Decode(bad, TestContext.Current.CancellationToken));
        }
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => WatershedRunoffCapacityCodecV1.Decode(bytes, cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => state.PlanReconcile(state.Inventory, state.Production, cancellation.Token));
        Retain(restored, "empty-closed");
    }

    [Fact]
    public void CompletePendingProfileSharesOneAuthorityAndNetworkArchive()
    {
        var (first, _) = Create(100_000_000_000);
        var request = first.Request;
        var basis = request.Inventory;
        EntityId source = request.Inputs[0].Source!.SourceId, custodian = request.Inputs[0].CustodianId!.Value;
        var ids = Enumerable.Range(0, 17).Select(i => Id((ulong)(10000 + i))).ToArray();
        var lots = basis.Custody.Lots.Where(l => l.CustodianId != custodian).Concat(ids.Select((id, i) =>
            new CountedSourceLot(Id((ulong)(20000 + i)), custodian, request.Material.Item,
                new(source, (ulong)i * 1000, (ulong)i * 1000 + 600), 0, LotProtection.None, id)));
        var inventory = new CountedAuthorityState(new(basis.WorldId, basis.BranchId, 0,
            basis.Custody.Storages.Select(s => s with { UnitCapacity = 100000 }), lots), 100000, basis.Actors, basis.Controls,
            basis.Policies, [new(source, request.Material.Item, 100000)], ids);
        var pending = ids.Select((id, i) => new WatershedRunoffPlanner().Prepare(new(request.Network, request.Mapping,
            inventory, request.Production, id, request.OwnerActorId, request.Start, request.End, request.Material,
            request.Inputs.Select(input => input.Source is { } value ? new WatershedRunoffInput(input.BasinId, input.DepthMicrometres,
                new(value.SourceId, value.Start + (ulong)i * 1000, value.End + (ulong)i * 1000), custodian) : input), request.Terminals),
            TestContext.Current.CancellationToken)).ToArray();
        var state = WatershedRunoffCapacityState.Create(pending[0]);
        foreach (var runoff in pending.Skip(1).Take(15)) state = state.PlanAdmit(runoff).ApplyTo(state);
        Assert.Equal(16, state.Pending.Count);
        Assert.Equal(9650UL, Assert.Single(state.Receivers).ProjectedUnits);
        Assert.Throws<ArgumentException>(() => state.PlanAdmit(pending[16]));
        byte[] bytes = WatershedRunoffCapacityCodecV1.Encode(state);
        Assert.Equal(bytes, WatershedRunoffCapacityCodecV1.Encode(WatershedRunoffCapacityCodecV1.Decode(bytes, TestContext.Current.CancellationToken)));
        Assert.True(bytes.Length < pending.Take(16).Sum(p => WatershedRunoffCodecV1.Encode(p).Length));
        var reordered = WatershedRunoffCapacityState.Create(pending[15]);
        foreach (var runoff in pending.Take(15).Reverse()) reordered = reordered.PlanAdmit(runoff).ApplyTo(reordered);
        Assert.Equal(bytes, WatershedRunoffCapacityCodecV1.Encode(reordered));
        Retain(state, "maximum-pending");
    }

    private static PreparedWatershedRunoff Reprepare(WatershedRunoffRequest request, CountedAuthorityState inventory,
        ProductionSnapshot? production) => new WatershedRunoffPlanner().Prepare(new(request.Network, request.Mapping, inventory,
            production, request.ReservationId, request.OwnerActorId, request.Start, request.End, request.Material, request.Inputs, request.Terminals),
            TestContext.Current.CancellationToken);

    private static CountedAuthorityState Rebuild(CountedAuthorityState basis, IEnumerable<CountedSourceLot> lots, ulong revision) =>
        new(new(basis.WorldId, basis.BranchId, revision, basis.Custody.Storages, lots), basis.AdmittedUnitCapacity,
            basis.Actors, basis.Controls, basis.Policies, basis.Sources, basis.ReservationIds);

    private static void Retain(WatershedRunoffCapacityState state, string name)
    {
        string? path = Environment.GetEnvironmentVariable("OPENFANTASY_RUNOFF_CAPACITY_EVIDENCE");
        if (string.IsNullOrWhiteSpace(path)) return;
        Directory.CreateDirectory(path);
        File.WriteAllBytes(Path.Combine(path, name + ".bin"), WatershedRunoffCapacityCodecV1.Encode(state));
    }

    internal static (PreparedWatershedRunoff First, PreparedWatershedRunoff Second) Create(ulong volumeCapacity)
    {
        var basis = WatershedRunoffTests.Create();
        var original = basis.Inventory;
        EntityId sourceCustodian = basis.Inputs[0].CustodianId!.Value;
        EntityId source = basis.Inputs[0].Source!.SourceId;
        var lots = original.Custody.Lots.Concat(new[] {
            new CountedSourceLot(Id(10), sourceCustodian, basis.Material.Item, new(source, 1000, 1600), 0, LotProtection.None, Id(3)),
            new CountedSourceLot(Id(20), sourceCustodian, basis.Material.Item, new(source, 2000, 2100), 0) });
        var inventory = new CountedAuthorityState(new(original.WorldId, original.BranchId, 0,
            original.Custody.Storages.Select(s => s with { UnitCapacity = 3000 }), lots), 3000, original.Actors,
            [new(Id(1), Id(2), basis.OwnerActorId)], original.Policies,
            [new(source, basis.Material.Item, 3000)], [basis.ReservationId, Id(3), Id(4)]);
        var production = new ProductionSnapshot(inventory.WorldId, inventory.BranchId, 0, [], [], []);
        var terminals = basis.Terminals.Select(t => new WatershedRunoffTerminal(t.BasinId, t.CustodianId, new(volumeCapacity))).ToArray();
        var first = new WatershedRunoffRequest(basis.Network, basis.Mapping, inventory, production, basis.ReservationId,
            basis.OwnerActorId, basis.Start, basis.End, basis.Material, basis.Inputs, terminals);
        var second = new WatershedRunoffRequest(basis.Network, basis.Mapping, inventory, production, Id(3), basis.OwnerActorId,
            basis.End, basis.End.AddMilliseconds(1), basis.Material, basis.Inputs.Select(i => i.Source is { } input
                ? new WatershedRunoffInput(i.BasinId, i.DepthMicrometres, new(input.SourceId, input.Start + 1000, input.End + 1000), i.CustodianId!.Value)
                : i), terminals);
        var planner = new WatershedRunoffPlanner();
        return (planner.Prepare(first, TestContext.Current.CancellationToken), planner.Prepare(second, TestContext.Current.CancellationToken));
    }
}
