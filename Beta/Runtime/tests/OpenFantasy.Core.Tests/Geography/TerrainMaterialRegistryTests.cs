using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Geography;

public sealed partial class TerrainMaterialRegistryTests
{
    private readonly TerrainMaterialRegistryPlanner _planner = new();

    [Fact]
    public void SharedInventorySplitsAndMergesTwoDisjointAllocationsWithoutDuplicatingFunding()
    {
        var fixture = Create();
        TerrainMaterialRegistry registry = fixture.Registry;
        byte[] before = TerrainMaterialRegistryCodecV1.Encode(registry);
        registry = Refine(registry, AllocationA, new(-2, 0, -1), 1);
        registry = Refine(registry, AllocationB, new(0, 0, -1), 2);
        Assert.Equal(140UL, registry.CoarseQuantity);
        Assert.Equal(20UL, registry.DetailedQuantity);
        Assert.Equal(160UL, registry.AllocatedQuantity);
        Assert.Same(registry.Inventory, registry.GetState(AllocationA).Inventory);
        Assert.Same(registry.Inventory, registry.GetState(AllocationB).Inventory);
        var neighbour = registry.GetState(AllocationB);
        CountedSourceLot neighbourLot = Assert.Single(registry.Inventory.Custody.Lots, l => l.Source.Start == 90 && l.CustodianId == Fine);
        registry = _planner.Demote(registry, AllocationA, [new(-2, 0, -1)], Id(3), registry.Revision).ApplyTo(registry);
        Assert.Equal(neighbour.ActiveCells, registry.GetState(AllocationB).ActiveCells);
        Assert.Equal(neighbourLot, registry.Inventory.Custody.GetLot(neighbourLot.LotId));
        Assert.Equal(150UL, registry.CoarseQuantity);
        Assert.Equal(before, TerrainMaterialRegistryCodecV1.Encode(fixture.Registry));
        Retain(registry, "two-allocations");
    }

    [Fact]
    public void OverlappingSpaceAndReusedSourceIntervalsRejectEvenWithDifferentAllocationIds()
    {
        var f = Create();
        TerrainMaterialDeposit first = f.Allocations[0].Deposit;
        Assert.Throws<ArgumentException>(() => TerrainMaterialRegistry.Create(f.Terrain, f.Inventory,
            [f.Allocations[0], new(Id(300), Deposit(f.Terrain, first.MacroColumn, new(Source, 90, 170)))]));
        Assert.Throws<ArgumentException>(() => TerrainMaterialRegistry.Create(f.Terrain, f.Inventory,
            [f.Allocations[0], new(AllocationB, Deposit(f.Terrain, f.Allocations[1].Deposit.MacroColumn, first.Source))]));
        Assert.Throws<ArgumentException>(() => TerrainMaterialRegistry.Create(f.Terrain, f.Inventory,
            [f.Allocations[0], new(AllocationA, f.Allocations[1].Deposit)]));
    }

    [Fact]
    public void CanonicalOrderAndExactBindingPinsAreEnforced()
    {
        var f = Create();
        var reverse = TerrainMaterialRegistry.Create(f.Terrain, f.Inventory, f.Allocations.Reverse());
        Assert.Equal(TerrainMaterialRegistryCodecV1.Encode(f.Registry), TerrainMaterialRegistryCodecV1.Encode(reverse));
        var differentWindow = new WatershedTerrainMapper().Prepare(f.Terrain.Network, f.Terrain.Mapping,
            new(new(-2, 0, -1), 2, 2, 2), TestContext.Current.CancellationToken);
        var foreign = new TerrainMaterialAllocation(AllocationB,
            Deposit(differentWindow, f.Allocations[1].Deposit.MacroColumn, f.Allocations[1].Deposit.Source));
        Assert.Throws<ArgumentException>(() => TerrainMaterialRegistry.Create(f.Terrain, f.Inventory, [f.Allocations[0], foreign]));
        MaterialDefinition original = f.Allocations[0].Deposit.Material.Physical;
        var conflicting = new MaterialDefinition(original.Package, original.DefinitionId, original.Key,
            new(original.MassPerUnit.Value + 1), original.VolumePerUnit, original.Hardness);
        var badCalibration = new TerrainMaterialAllocation(AllocationB,
            Deposit(f.Terrain, f.Allocations[1].Deposit.MacroColumn, f.Allocations[1].Deposit.Source, physical: conflicting));
        Assert.Throws<ArgumentException>(() => TerrainMaterialRegistry.Create(f.Terrain, f.Inventory, [f.Allocations[0], badCalibration]));
    }

    [Fact]
    public void AllAllocationAndPhysicalIdentitiesGuardCausesAndNewLots()
    {
        var f = Create();
        foreach (EntityId cause in new[] { AllocationB, PhysicalId })
            Assert.Throws<InvalidOperationException>(() => _planner.Refine(f.Registry, AllocationA, f.Terrain,
                [new(-2, 0, -1)], cause, f.Registry.Revision));
        EntityId generated = EntityId.DeriveSpawned(Id(1), 1);
        var occupied = TerrainMaterialRegistry.Create(f.Terrain, f.Inventory,
            [f.Allocations[0], new(generated, f.Allocations[1].Deposit)]);
        Assert.Throws<InvalidOperationException>(() => Refine(occupied, AllocationA, new(-2, 0, -1), 1));
        Assert.Throws<ArgumentException>(() => TerrainMaterialRegistry.Create(f.Terrain, f.Inventory,
            [new(Source, f.Allocations[0].Deposit)]));
    }

    [Fact]
    public void CompleteRegistryPinsRejectAnotherAllocationChangingUnderAPlan()
    {
        var f = Create();
        var plan = _planner.Refine(f.Registry, AllocationA, f.Terrain, [new(-2, 0, -1)], Id(1), f.Registry.Revision);
        var changed = Refine(f.Registry, AllocationB, new(0, 0, -1), 2);
        Assert.Throws<InvalidOperationException>(() => plan.ApplyTo(changed));
        var renamed = TerrainMaterialRegistry.Create(f.Terrain, f.Inventory,
            [f.Allocations[0], new(Id(999), f.Allocations[1].Deposit)]);
        Assert.Throws<InvalidOperationException>(() => plan.ApplyTo(renamed));
        Assert.Throws<ArgumentException>(() => _planner.Demote(f.Registry, Id(998), [new(-2, 0, -1)], Id(1), 0));
    }

    [Fact]
    public void RegistryArchiveRoundTripsAndRejectsMalformedOrNoncanonicalData()
    {
        var f = Create();
        var registry = Refine(f.Registry, AllocationA, new(-2, 0, -1), 1);
        byte[] bytes = TerrainMaterialRegistryCodecV1.Encode(registry);
        var decoded = TerrainMaterialRegistryCodecV1.Decode(bytes, TestContext.Current.CancellationToken);
        Assert.Equal(bytes, TerrainMaterialRegistryCodecV1.Encode(decoded));
        Assert.Same(decoded.Inventory, decoded.GetState(AllocationA).Inventory);
        Assert.Same(decoded.Inventory, decoded.GetState(AllocationB).Inventory);
        byte[] corrupt = (byte[])bytes.Clone(); corrupt[^1] ^= 0x80;
        foreach (byte[] bad in new[] { bytes[..^1], bytes.Concat(new byte[] { 0 }).ToArray(), corrupt })
            Assert.Throws<FormatException>(() => TerrainMaterialRegistryCodecV1.Decode(bad, TestContext.Current.CancellationToken));
    }

    private TerrainMaterialRegistry Refine(TerrainMaterialRegistry registry, EntityId allocation, CellCoordinate cell, ulong cause) =>
        _planner.Refine(registry, allocation, registry.Terrain, [cell], Id(cause), registry.Revision).ApplyTo(registry);

    private static Fixture Create()
    {
        TerrainScope scope = new(new(0, 1), new(0, 2), new(0, 3));
        PreparedWatershed basin = new WatershedPlanner().Prepare(new(scope, new(0, 4), new(-1, 0, 2, 1), new(0, 0), 0, new(0, 100)),
            TestContext.Current.CancellationToken);
        PreparedWatershedNetwork network = new WatershedNetworkPlanner().Prepare(new(0, 900), [basin],
            [WatershedOutletClosure.ToTerminal(basin.BasinId, 2)], TestContext.Current.CancellationToken);
        var terrain = new WatershedTerrainMapper().Prepare(network, new(2, 1, 0), new(new(-2, 0, -1), 4, 2, 3),
            TestContext.Current.CancellationToken);
        TerrainMaterialAllocation[] allocations = [new(AllocationA, Deposit(terrain, new(basin.BasinId, 0), new(Source, 10, 90))),
            new(AllocationB, Deposit(terrain, new(basin.BasinId, 1), new(Source, 90, 170)))];
        var custody = new CustodySnapshot(scope.WorldId, scope.BranchId, 0,
            [new(Coarse, 200, 0), new(Fine, 200, 0), new(Away, 200, 0)],
            [new(Id(106), Coarse, allocations[0].Deposit.Material.Item, new(Source, 10, 170), 0)]);
        var inventory = new CountedAuthorityState(custody, 200, [Actor], [new(Id(107), Id(108), Actor)],
            [new(Coarse, Actor, CustodyDepositRule.OwnerOnly), new(Fine, Actor, CustodyDepositRule.OwnerOnly),
                new(Away, Actor, CustodyDepositRule.OwnerOnly)], [new(Source, allocations[0].Deposit.Material.Item, 200)]);
        return new(terrain, inventory, allocations, TerrainMaterialRegistry.Create(terrain, inventory, allocations));
    }

    private static TerrainMaterialDeposit Deposit(PreparedWatershedTerrain terrain, WatershedColumnReference column,
        SourceInterval source, EntityId? coarse = null, EntityId? fine = null, MaterialDefinition? physical = null,
        int depth = 2) => new(terrain, column, depth, new(new(ItemId, ContentHash.Compute("registry-rock"u8)),
            physical ?? new(new PackageId("openfantasy.terrain"), PhysicalId, "rock", new(250_000_000), new(100_000_000), new(10))),
            10, source, coarse ?? Coarse, fine ?? Fine);

    private sealed record Fixture(PreparedWatershedTerrain Terrain, CountedAuthorityState Inventory,
        TerrainMaterialAllocation[] Allocations, TerrainMaterialRegistry Registry);
    private static EntityId Id(ulong value) => new(0x7265676973747279, value);
    private static readonly EntityId ItemId = Id(100), PhysicalId = Id(101), Actor = Id(102), Coarse = Id(103),
        Fine = Id(104), Source = Id(105), Away = Id(109), AllocationA = Id(200), AllocationB = Id(201);

    private static void Retain(TerrainMaterialRegistry registry, string name)
    {
        string? path = Environment.GetEnvironmentVariable("OPENFANTASY_MATERIAL_REGISTRY_EVIDENCE");
        if (string.IsNullOrWhiteSpace(path)) return;
        Directory.CreateDirectory(path);
        File.WriteAllBytes(Path.Combine(path, name + ".bin"), TerrainMaterialRegistryCodecV1.Encode(registry));
    }
}
