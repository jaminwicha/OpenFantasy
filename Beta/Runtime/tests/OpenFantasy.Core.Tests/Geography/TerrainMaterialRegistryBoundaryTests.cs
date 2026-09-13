using System.Buffers.Binary;
using System.Text.Json;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Geography;

public sealed partial class TerrainMaterialRegistryTests
{
    [Fact]
    public void InteriorTransferLeavesTheNeighbourAndUnrelatedProtectedStockUnchanged()
    {
        var f = Create();
        var item = f.Allocations[0].Deposit.Material.Item;
        var unrelated = new CountedSourceLot(Id(400), Away, item, new(Source, 0, 10), 0, LotProtection.Favorite, Id(401));
        var inventory = new CountedAuthorityState(new(f.Inventory.WorldId, f.Inventory.BranchId, 0, f.Inventory.Custody.Storages,
            [unrelated, new(Id(402), Coarse, item, new(Source, 10, 15), 0), new(Id(403), Coarse, item, new(Source, 15, 170), 0)]),
            200, f.Inventory.Actors, f.Inventory.Controls, f.Inventory.Policies, f.Inventory.Sources, [Id(401)]);
        var registry = TerrainMaterialRegistry.Create(f.Terrain, inventory, f.Allocations);
        registry = Refine(registry, AllocationA, new(-2, 0, -1), 1);
        registry = Refine(registry, AllocationB, new(0, 0, -1), 2);
        CountedSourceLot neighbour = Assert.Single(registry.Inventory.Custody.Lots, l => l.CustodianId == Fine && l.Source.Start == 90);
        CountedSourceLot moved = Assert.Single(registry.Inventory.Custody.Lots, l => l.CustodianId == Fine && l.Source.Start == 15);
        registry = registry.ReconcileInventory(Transfer(registry.Inventory, moved, Away, 2));
        registry = _planner.Demote(registry, AllocationA, [new(-2, 0, -1)], Id(3), registry.Revision).ApplyTo(registry);
        registry = Refine(registry, AllocationA, new(-2, 0, -1), 4);
        Assert.Equal(neighbour, registry.Inventory.Custody.GetLot(neighbour.LotId));
        Assert.Equal(unrelated, registry.Inventory.Custody.GetLot(unrelated.LotId));
        Assert.Equal(new[] { (10UL, 15UL), (17UL, 20UL) }, registry.Inventory.Custody.Lots
            .Where(l => l.CustodianId == Fine && l.Source.End <= 90).OrderBy(l => l.Source.Start).Select(l => (l.Source.Start, l.Source.End)));
        Assert.Equal(2UL, registry.OtherQuantity);
        Assert.Equal(160UL, registry.CoarseQuantity + registry.DetailedQuantity + registry.OtherQuantity);
        Retain(registry, "interior-transfer");
    }

    [Fact]
    public void DepositingIntoAnotherAllocationsCustodianCannotRebindTheSourceToItsCells()
    {
        var f = Create();
        TerrainMaterialAllocation[] allocations = [f.Allocations[0], new(AllocationB,
            Deposit(f.Terrain, f.Allocations[1].Deposit.MacroColumn, new(Source, 90, 170), coarse: Away))];
        var inventory = CopyInventory(f.Inventory, new(f.Inventory.WorldId, f.Inventory.BranchId, 0, f.Inventory.Custody.Storages,
            [new(Id(400), Coarse, allocations[0].Deposit.Material.Item, new(Source, 10, 90), 0),
                new(Id(401), Away, allocations[1].Deposit.Material.Item, new(Source, 90, 170), 0)]));
        var registry = TerrainMaterialRegistry.Create(f.Terrain, inventory, allocations);
        registry = Refine(registry, AllocationA, new(-2, 0, -1), 1);
        var lot = Assert.Single(registry.Inventory.Custody.Lots, l => l.CustodianId == Fine);
        registry = registry.ReconcileInventory(Transfer(registry.Inventory, lot, Away, 4));
        registry = Refine(registry, AllocationB, new(0, 0, -1), 2);
        Assert.Equal(4UL, registry.GetState(AllocationA).OtherQuantity);
        Assert.Equal(0UL, registry.GetState(AllocationB).OtherQuantity);
        Assert.Equal(10UL, registry.GetState(AllocationB).DetailedQuantity);
        var deposited = Assert.Single(registry.Inventory.Custody.Lots, l => l.CustodianId == Away && l.Source.Start == 10);
        Assert.Equal(new SourceInterval(Source, 10, 14), deposited.Source);
    }

    [Fact]
    public void OppositeCustodianRolesAndChangedAdmissionRejectAcrossAllAllocations()
    {
        var f = Create();
        var opposite = new TerrainMaterialAllocation(AllocationB,
            Deposit(f.Terrain, f.Allocations[1].Deposit.MacroColumn, new(Source, 90, 170), fine: Coarse, coarse: Away));
        Assert.Throws<ArgumentException>(() => TerrainMaterialRegistry.Create(f.Terrain, f.Inventory, [f.Allocations[0], opposite]));
        var changed = new CountedAuthorityState(new(f.Inventory.WorldId, f.Inventory.BranchId, 1, f.Inventory.Custody.Storages,
            f.Inventory.Custody.Lots), 201, f.Inventory.Actors, f.Inventory.Controls, f.Inventory.Policies, f.Inventory.Sources);
        Assert.Throws<ArgumentException>(() => f.Registry.ReconcileInventory(changed));
        Assert.Same(f.Registry, f.Registry.ReconcileInventory(f.Inventory));
        Assert.Throws<InvalidOperationException>(() => _planner.Refine(f.Registry, AllocationA, f.Terrain,
            [new(-2, 0, -1)], Id(1), f.Registry.Revision, id => id == EntityId.DeriveSpawned(Id(1), 0)));
    }

    [Fact]
    public void MaximumNetworkAnd64AllocationsShareMacroDataAndOneArchiveCopy()
    {
        List<PreparedWatershed> basins = [];
        TerrainScope scope = new(new(0, 1), new(0, 2), new(0, 3));
        for (int i = 0; i < 16; i++)
            basins.Add(new WatershedPlanner().Prepare(new(scope, new(0, 4), new(i % 4 * 64, i / 4 * 64, 64, 64),
                new(0, 0), 0, new(0, (ulong)(100 + i))), TestContext.Current.CancellationToken));
        PreparedWatershedNetwork network = new WatershedNetworkPlanner().Prepare(new(0, 900), basins,
            basins.Select(b => WatershedOutletClosure.ToTerminal(b.BasinId, 4096)), TestContext.Current.CancellationToken);
        var terrain = new WatershedTerrainMapper().Prepare(network, new(1, 1, 0), new(new(0, 0, 0), 32, 32, 1),
            TestContext.Current.CancellationToken);
        List<TerrainMaterialAllocation> allocations = [new(Id(300), Deposit(terrain, new(basins[0].BasinId, 0), new(Source, 10, 20), depth: 1))];
        long startBytes = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 1; i < 64; i++)
            allocations.Add(new(Id((ulong)(300 + i)), Deposit(terrain, new(basins[0].BasinId, i),
                new(Source, (ulong)(10 + i * 10), (ulong)(20 + i * 10)), depth: 1)));
        long additionalBindingBytes = GC.GetAllocatedBytesForCurrentThread() - startBytes;
        // A local allocation bound catches accidental copies of the maximum macro archive/role set per slab.
        Assert.InRange(additionalBindingBytes, 1, 4_000_000);
        var item = allocations[0].Deposit.Material.Item;
        var inventory = new CountedAuthorityState(new(scope.WorldId, scope.BranchId, 0,
            [new(Coarse, 1000, 0), new(Fine, 1000, 0)], [new(Id(106), Coarse, item, new(Source, 10, 650), 0)]),
            1000, [Actor], [], [new(Coarse, Actor, CustodyDepositRule.OwnerOnly), new(Fine, Actor, CustodyDepositRule.OwnerOnly)],
            [new(Source, item, 1000)]);
        var registry = TerrainMaterialRegistry.Create(terrain, inventory, allocations);
        byte[] bytes = TerrainMaterialRegistryCodecV1.Encode(registry);
        Assert.Equal(65536, network.ColumnCount);
        Assert.Equal(64, registry.Allocations.Count);
        Assert.True(bytes.Length < 4_000_000);
        Assert.Equal(bytes, TerrainMaterialRegistryCodecV1.Encode(TerrainMaterialRegistryCodecV1.Decode(bytes, TestContext.Current.CancellationToken)));
        Assert.Throws<ArgumentException>(() => TerrainMaterialRegistry.Create(terrain, inventory,
            allocations.Append(new(Id(500), Deposit(terrain, new(basins[0].BasinId, 64), new(Source, 650, 660), depth: 1)))));
        Retain(registry, "maximum-network");
        string? path = Environment.GetEnvironmentVariable("OPENFANTASY_MATERIAL_REGISTRY_EVIDENCE");
        if (!string.IsNullOrWhiteSpace(path))
            File.WriteAllText(Path.Combine(path, "allocation-measurement.json"), JsonSerializer.Serialize(new
                { NetworkColumns = 65536, Allocations = 64, Additional63BindingAllocatedBytes = additionalBindingBytes, EncodedBytes = bytes.Length }));
    }

    [Fact]
    public void GlobalActiveCellLimitIncludesEmptyCellsAcrossAllocations()
    {
        var f = Create();
        PreparedWatershedTerrain terrain = new WatershedTerrainMapper().Prepare(f.Terrain.Network, new(64, 1, 0),
            new(new(-64, 0, 0), 32, 32, 1), TestContext.Current.CancellationToken);
        TerrainMaterialAllocation[] allocations = [new(AllocationA, Deposit(terrain, f.Allocations[0].Deposit.MacroColumn,
            new(Source, 0, 40960), depth: 1)), new(AllocationB, Deposit(terrain, f.Allocations[1].Deposit.MacroColumn,
            new(Source, 40960, 81920), depth: 1))];
        var inventory = new CountedAuthorityState(new(f.Inventory.WorldId, f.Inventory.BranchId, 0,
            [new(Coarse, 81920, 0), new(Fine, 81920, 0), new(Away, 81920, 0)],
            [new(Id(400), Coarse, allocations[0].Deposit.Material.Item, new(Source, 0, 40960), 0),
                new(Id(401), Coarse, allocations[0].Deposit.Material.Item, new(Source, 40960, 81920), 0)]),
            81920, f.Inventory.Actors, f.Inventory.Controls, f.Inventory.Policies,
            [new(Source, allocations[0].Deposit.Material.Item, 81920)]);
        var registry = TerrainMaterialRegistry.Create(terrain, inventory, allocations);
        registry = registry.ReconcileInventory(Transfer(inventory, inventory.Custody.GetLot(Id(400)), Away, 40960));
        // Archive-boundary fixture: empty active bindings over transferred stock, not a claimed 4096-step gameplay journey.
        byte[][] outer = ReadFields(TerrainMaterialRegistryCodecV1.Encode(registry));
        byte[] rows = outer[3];
        int firstCellCount = 4 + 16 + 4 + BinaryPrimitives.ReadInt32BigEndian(rows.AsSpan(20));
        byte[] active = new byte[4096 * 24];
        for (int i = 0; i < 4096; i++)
        {
            CellCoordinate c = allocations[0].Deposit.CellForUnit((ulong)i * 10);
            BinaryPrimitives.WriteInt64BigEndian(active.AsSpan(i * 24), c.X);
            BinaryPrimitives.WriteInt64BigEndian(active.AsSpan(i * 24 + 8), c.Y);
            BinaryPrimitives.WriteInt64BigEndian(active.AsSpan(i * 24 + 16), c.Z);
        }
        byte[] count = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(count, 4096);
        outer[3] = [.. rows[..firstCellCount], .. count, .. active, .. rows[(firstCellCount + 4)..]];
        byte[] encoded = WriteFields(outer);
        registry = TerrainMaterialRegistryCodecV1.Decode(encoded, TestContext.Current.CancellationToken);
        Assert.Equal(4096, registry.ActiveCellCount);
        Assert.Equal(0UL, registry.DetailedQuantity);
        var window = new WatershedTerrainMapper().Prepare(terrain.Network, terrain.Mapping, new(new(0, 0, 0), 1, 1, 1), TestContext.Current.CancellationToken);
        Assert.Throws<ArgumentException>(() => _planner.Refine(registry, AllocationB, window, [new(0, 0, 0)], Id(1), registry.Revision));
        Assert.Equal(encoded, TerrainMaterialRegistryCodecV1.Encode(registry));
        Retain(registry, "active-cap");
    }

    [Fact]
    public void SingleDepositBytesFromThePreviousMilestoneRemainExact()
    {
        var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "terrain-material-state-v1-compatibility.json")));
        byte[] bytes = Convert.FromHexString(fixture.RootElement.GetProperty("hex").GetString()!);
        TerrainMaterialState state = TerrainMaterialCodecV1.Decode(bytes, TestContext.Current.CancellationToken);
        Assert.Equal(fixture.RootElement.GetProperty("sha256").GetString(), state.ContentHash.Value);
        Assert.Equal(bytes, TerrainMaterialCodecV1.Encode(state));
    }

    private static CountedAuthorityState Transfer(CountedAuthorityState inventory, CountedSourceLot lot, EntityId destination, ulong quantity)
    {
        var command = new CommandEnvelope(Id(9000), Id(107), Id(108), Actor, inventory.WorldId, inventory.BranchId,
            [new(RevisionTargetKind.Branch, inventory.BranchId, inventory.Custody.Revision),
                new(RevisionTargetKind.Entity, lot.LotId, lot.Revision),
                new(RevisionTargetKind.Entity, lot.CustodianId, inventory.Custody.GetStorage(lot.CustodianId).Revision),
                new(RevisionTargetKind.Entity, destination, inventory.Custody.GetStorage(destination).Revision)],
            new(lot.LotId, lot.CustodianId, destination, quantity));
        return CopyInventory(inventory, new CustodyTransferPlanner().Plan(inventory.Custody, command).ApplyTo(inventory.Custody));
    }

    private static CountedAuthorityState CopyInventory(CountedAuthorityState basis, CustodySnapshot custody) =>
        new(custody, basis.AdmittedUnitCapacity, basis.Actors, basis.Controls, basis.Policies, basis.Sources, basis.ReservationIds);

    private static byte[][] ReadFields(byte[] bytes)
    {
        var fields = new List<byte[]>();
        int offset = 12;
        while (offset < bytes.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset)); offset += 4;
            fields.Add(bytes.AsSpan(offset, length).ToArray()); offset += length;
        }
        return fields.ToArray();
    }

    [Fact]
    public void NormalizedParserRejectsIndependentCountLengthAndOrderingViolations()
    {
        var f = Create();
        byte[][] outer = ReadFields(TerrainMaterialRegistryCodecV1.Encode(f.Registry));
        byte[] rows = outer[3];
        List<byte[]> invalid = [];
        foreach (uint count in new[] { 65U, uint.MaxValue })
        {
            byte[] changed = (byte[])rows.Clone(); BinaryPrimitives.WriteUInt32BigEndian(changed, count); invalid.Add(changed);
        }
        byte[] oversized = (byte[])rows.Clone(); BinaryPrimitives.WriteInt32BigEndian(oversized.AsSpan(20), 8193); invalid.Add(oversized);
        int bindingLength = BinaryPrimitives.ReadInt32BigEndian(rows.AsSpan(20));
        int firstLength = 24 + bindingLength;
        byte[] duplicate = (byte[])rows.Clone(); rows.AsSpan(4, 16).CopyTo(duplicate.AsSpan(4 + firstLength)); invalid.Add(duplicate);
        invalid.Add([.. rows[..4], .. rows[(4 + firstLength)..], .. rows.AsSpan(4, firstLength).ToArray()]);
        foreach (byte[] malformed in invalid)
        {
            outer[3] = malformed;
            Assert.Throws<FormatException>(() => TerrainMaterialRegistryCodecV1.Decode(WriteFields(outer), TestContext.Current.CancellationToken));
        }
        byte[] cells = new byte[4096 * 24];
        for (int i = 0; i < 4096; i++) BinaryPrimitives.WriteInt64BigEndian(cells.AsSpan(i * 24), i);
        byte[] countBytes = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(countBytes, 4096);
        int firstCount = firstLength;
        byte[] expanded = [.. rows[..firstCount], .. countBytes, .. cells, .. rows[(firstCount + 4)..]];
        BinaryPrimitives.WriteInt32BigEndian(expanded.AsSpan(expanded.Length - 4), 1);
        outer[3] = expanded;
        var error = Assert.Throws<FormatException>(() => TerrainMaterialRegistryCodecV1.Decode(WriteFields(outer), TestContext.Current.CancellationToken));
        Assert.Contains("record count exceeds", error.Message, StringComparison.Ordinal);
    }

    private static byte[] WriteFields(byte[][] fields)
    {
        byte[] bytes = new byte[12 + fields.Sum(f => 4 + f.Length)];
        "OFC\0"u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8), fields.Length);
        int offset = 12;
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset), field.Length); offset += 4;
            field.CopyTo(bytes, offset); offset += field.Length;
        }
        return bytes;
    }

    [Fact]
    public void AnotherAllocationsDistinctPhysicalDefinitionAlsoReservesItsCauseIdentity()
    {
        var f = Create();
        var first = f.Allocations[0];
        var physical = new OpenFantasy.Core.Production.MaterialDefinition(new PackageId("openfantasy.terrain"), Id(452),
            "second-rock", new(250_000_000), new(100_000_000), new(10));
        var item = new CountedItemDefinition(Id(451), ContentHash.Compute("second-rock"u8));
        var second = new TerrainMaterialAllocation(AllocationB, new(f.Terrain, f.Allocations[1].Deposit.MacroColumn, 2,
            new(item, physical), 10, new(Id(450), 90, 170), Coarse, Fine));
        var inventory = new CountedAuthorityState(new(f.Inventory.WorldId, f.Inventory.BranchId, 0, f.Inventory.Custody.Storages,
            [new(Id(400), Coarse, first.Deposit.Material.Item, first.Deposit.Source, 0), new(Id(401), Coarse, item, second.Deposit.Source, 0)]),
            400, f.Inventory.Actors, f.Inventory.Controls, f.Inventory.Policies, [.. f.Inventory.Sources, new(Id(450), item, 200)]);
        var registry = TerrainMaterialRegistry.Create(f.Terrain, inventory, [first, second]);
        Assert.Throws<InvalidOperationException>(() => _planner.Refine(registry, AllocationA, f.Terrain, [new(-2, 0, -1)],
            physical.DefinitionId, registry.Revision));
    }
}
