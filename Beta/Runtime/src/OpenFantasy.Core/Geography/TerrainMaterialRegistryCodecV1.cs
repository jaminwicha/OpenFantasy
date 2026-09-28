using System.Buffers.Binary;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>Normalized finite registry archive: terrain and inventory occur once, allocation bindings reference that exact basis.</summary>
public static class TerrainMaterialRegistryCodecV1
{
    public const int MaximumEncodedByteLength = 6_250_000;
    private const int MaximumBindingByteLength = TerrainMaterialCodecV1.MaximumBindingByteLength;
    private const int MaximumAllocationsByteLength = 640_000;
    private const int CellByteLength = 24;

    public static byte[] Encode(TerrainMaterialRegistry registry) => EncodeRegistry(registry, withProduction: false);

    internal static byte[] EncodeRegistry(TerrainMaterialRegistry registry, bool withProduction, bool consumption = false, bool continued = false)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (withProduction != (registry.Production is not null))
        {
            throw new ArgumentException("The registry archive version must retain its exact production proof profile.");
        }
        List<ReadOnlyMemory<byte>> fields = [WatershedTerrainCodecV1.Encode(registry.Terrain),
            AuthorityEncodingV1.Encode(registry.Inventory), EncodeAllocations(registry)];
        if (withProduction)
        {
            fields.Add(continued ? ProductionCodecV6.Encode(registry.Production!) : GeographyProductionCodec.Encode(registry.Production, consumption));
        }
        byte[] bytes = CanonicalEncodingV1.Frame(continued ? "geography.terrain-material-registry.v4" : consumption ? "geography.terrain-material-registry.v3" : withProduction ? "geography.terrain-material-registry.v2"
            : "geography.terrain-material-registry.v1", fields.ToArray());
        if (bytes.Length > (withProduction ? TerrainMaterialRegistryCodecV2.MaximumEncodedByteLength : MaximumEncodedByteLength))
        {
            throw new ArgumentException("The material registry exceeds its archive limit.");
        }
        return bytes;
    }

    public static TerrainMaterialRegistry Decode(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken = default) =>
        DecodeRegistry(bytes, withProduction: false, cancellationToken);

    internal static TerrainMaterialRegistry DecodeRegistry(ReadOnlySpan<byte> bytes, bool withProduction, CancellationToken cancellationToken, bool consumption = false,
        TerrainProductionDependencies? dependencies = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var reader = new CanonicalTerrainReader(bytes,
                withProduction ? TerrainMaterialRegistryCodecV2.MaximumEncodedByteLength : MaximumEncodedByteLength,
                withProduction ? 5U : 4U, dependencies is not null ? "geography.terrain-material-registry.v4"u8 : consumption ? "geography.terrain-material-registry.v3"u8 : withProduction ? "geography.terrain-material-registry.v2"u8 : "geography.terrain-material-registry.v1"u8);
            PreparedWatershedTerrain terrain = WatershedTerrainCodecV1.Decode(reader.ReadField(WatershedTerrainCodecV1.MaximumEncodedByteLength), cancellationToken);
            CountedAuthorityState inventory = AuthorityEncodingV1.Decode(reader.ReadField(AuthorityEncodingV1.MaximumStateByteLength));
            ReadOnlySpan<byte> rows = reader.ReadField(MaximumAllocationsByteLength);
            ProductionSnapshot? production = null;
            if (withProduction)
            {
                var productionBytes = reader.ReadField(ProductionCodecV1.MaximumSnapshotByteLength);
                production = dependencies is not null ? dependencies.Decode(productionBytes)
                    : GeographyProductionCodec.Decode(productionBytes, consumption);
            }
            reader.Complete();
            var registry = DecodeAllocations(rows, terrain, inventory, cancellationToken, production);
            if (!EncodeRegistry(registry, withProduction, consumption, dependencies is not null).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The material registry is not canonical.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            return registry;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The material registry violates its allocation or shared-custody contracts.", error);
        }
    }

    private static byte[] EncodeAllocations(TerrainMaterialRegistry registry)
    {
        byte[][] bindings = registry.Allocations.Select(a => TerrainMaterialCodecV1.EncodeBinding(a.Deposit)).ToArray();
        int length = checked(4 + bindings.Sum(b => 24 + b.Length) + registry.ActiveCellCount * CellByteLength);
        if (length > MaximumAllocationsByteLength || bindings.Any(b => b.Length > MaximumBindingByteLength))
        {
            throw new ArgumentException("Material allocation records exceed their archive limits.");
        }
        byte[] bytes = new byte[length];
        BinaryPrimitives.WriteInt32BigEndian(bytes, registry.Allocations.Count);
        int offset = 4;
        for (int i = 0; i < bindings.Length; i++)
        {
            TerrainMaterialAllocation allocation = registry.Allocations[i];
            allocation.AllocationId.WriteBytes(bytes.AsSpan(offset));
            offset += EntityId.ByteLength;
            BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset), bindings[i].Length);
            offset += 4;
            bindings[i].CopyTo(bytes, offset);
            offset += bindings[i].Length;
            var cells = registry.GetState(allocation.AllocationId).ActiveCells;
            BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset), cells.Count);
            offset += 4;
            foreach (CellCoordinate cell in cells)
            {
                BinaryPrimitives.WriteInt64BigEndian(bytes.AsSpan(offset), cell.X);
                BinaryPrimitives.WriteInt64BigEndian(bytes.AsSpan(offset + 8), cell.Y);
                BinaryPrimitives.WriteInt64BigEndian(bytes.AsSpan(offset + 16), cell.Z);
                offset += CellByteLength;
            }
        }
        return bytes;
    }

    private static TerrainMaterialRegistry DecodeAllocations(ReadOnlySpan<byte> bytes, PreparedWatershedTerrain terrain,
        CountedAuthorityState inventory, CancellationToken cancellationToken, ProductionSnapshot? production)
    {
        int offset = 0;
        int count = ReadCount(bytes, ref offset, TerrainMaterialRegistry.MaximumAllocations);
        List<TerrainMaterialAllocation> allocations = [];
        Dictionary<EntityId, CellCoordinate[]> active = [];
        int totalCells = 0;
        for (int i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireRemaining(bytes, offset, EntityId.ByteLength);
            EntityId identity = EntityId.FromBytes(bytes.Slice(offset, EntityId.ByteLength));
            offset += EntityId.ByteLength;
            if (i > 0 && allocations[^1].AllocationId >= identity)
            {
                throw new FormatException("Allocation identities must be strictly increasing.");
            }
            int bindingLength = ReadCount(bytes, ref offset, MaximumBindingByteLength);
            RequireRemaining(bytes, offset, bindingLength);
            TerrainMaterialDeposit deposit = TerrainMaterialCodecV1.DecodeBinding(bytes.Slice(offset, bindingLength), terrain);
            offset += bindingLength;
            int cellCount = ReadCount(bytes, ref offset, TerrainMaterialRegistry.MaximumActiveCells - totalCells);
            totalCells += cellCount;
            RequireRemaining(bytes, offset, cellCount * CellByteLength);
            CellCoordinate[] cells = new CellCoordinate[cellCount];
            for (int c = 0; c < cells.Length; c++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                cells[c] = new(BinaryPrimitives.ReadInt64BigEndian(bytes[offset..]),
                    BinaryPrimitives.ReadInt64BigEndian(bytes[(offset + 8)..]), BinaryPrimitives.ReadInt64BigEndian(bytes[(offset + 16)..]));
                offset += CellByteLength;
                if (c > 0 && cells[c - 1].CompareTo(cells[c]) >= 0)
                {
                    throw new FormatException("Active cells must be strictly canonical within each allocation.");
                }
            }
            allocations.Add(new(identity, deposit));
            active.Add(identity, cells);
        }
        if (offset != bytes.Length)
        {
            throw new FormatException("The allocation list has trailing data.");
        }
        return new(terrain, inventory, allocations, active, production);
    }

    private static int ReadCount(ReadOnlySpan<byte> bytes, ref int offset, int maximum)
    {
        RequireRemaining(bytes, offset, sizeof(uint));
        uint count = BinaryPrimitives.ReadUInt32BigEndian(bytes[offset..]);
        offset += sizeof(uint);
        if (count > maximum)
        {
            throw new FormatException("The material registry record count exceeds its limit.");
        }
        return (int)count;
    }

    private static void RequireRemaining(ReadOnlySpan<byte> bytes, int offset, int length)
    {
        if (length > bytes.Length - offset)
        {
            throw new FormatException("The material registry record is truncated.");
        }
    }
}
