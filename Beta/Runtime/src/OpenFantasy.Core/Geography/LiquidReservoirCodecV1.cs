using System.Buffers.Binary;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>Exact capacity owner plus finite reservoir support and rederived actual/projected fill witnesses.</summary>
public static class LiquidReservoirCodecV1
{
    public const int MaximumEncodedByteLength = 24_000_000;
    private const int MaximumGeometryBytes = 100_000;
    private const int MaximumCollectionBytes = 160_000;
    private const int MaximumWitnessBytes = 100_000;
    private const int ColumnByteLength = 24;

    public static byte[] Encode(LiquidReservoirState state) => EncodeReservoirs(state, consumption: false);

    internal static byte[] EncodeReservoirs(LiquidReservoirState state, bool consumption, bool continued = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        using var geometries = new MemoryStream();
        WriteUnsigned32(geometries, (uint)state.Geometries.Count);
        foreach (var geometry in state.Geometries)
        {
            byte[] bytes = EncodeGeometry(geometry);
            WriteUnsigned32(geometries, (uint)bytes.Length);
            geometries.Write(bytes);
        }
        using var witnesses = new MemoryStream();
        foreach (var projection in state.Projections)
        {
            witnesses.Write(projection.Geometry.ReservoirId.ToByteArray());
            witnesses.Write(Unsigned(projection.ExistingUnits));
            witnesses.Write(Unsigned(projection.ReservedIncomingUnits));
            WriteFill(witnesses, projection.ActualFill);
            WriteFill(witnesses, projection.ProjectedFill);
        }
        byte[] encoded = CanonicalEncodingV1.Frame(continued ? "geography.liquid-reservoirs.v3" : consumption ? "geography.liquid-reservoirs.v2" : "geography.liquid-reservoirs.v1", WatershedRunoffCapacityCodecV1.EncodeCapacity(state.Capacity, consumption, continued),
            geometries.ToArray(), witnesses.ToArray());
        if (geometries.Length > MaximumCollectionBytes || witnesses.Length > MaximumWitnessBytes || encoded.Length > MaximumEncodedByteLength)
        {
            throw new ArgumentException("The reservoir archive exceeds its finite profile.");
        }
        return encoded;
    }

    public static LiquidReservoirState Decode(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken = default) =>
        DecodeReservoirs(bytes, cancellationToken, consumption: false);

    internal static LiquidReservoirState DecodeReservoirs(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken, bool consumption,
        GeographyProductionDependencies? dependencies = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var reader = new CanonicalTerrainReader(bytes, MaximumEncodedByteLength, 4, dependencies is not null ? "geography.liquid-reservoirs.v3"u8 : consumption ? "geography.liquid-reservoirs.v2"u8 : "geography.liquid-reservoirs.v1"u8);
            var capacity = WatershedRunoffCapacityCodecV1.DecodeCapacity(reader.ReadField(WatershedRunoffCapacityCodecV1.MaximumEncodedByteLength), cancellationToken, consumption, dependencies);
            var collection = reader.ReadField(MaximumCollectionBytes);
            _ = reader.ReadField(MaximumWitnessBytes);
            reader.Complete();
            if (collection.Length < 4)
            {
                throw new FormatException("The reservoir collection lacks its count.");
            }
            uint count = BinaryPrimitives.ReadUInt32BigEndian(collection);
            if (count is 0 or > LiquidReservoirState.MaximumReservoirs)
            {
                throw new FormatException("The reservoir count is outside its supported profile.");
            }
            List<ReservoirGeometry> geometries = [];
            int offset = 4, totalColumns = 0;
            for (uint index = 0; index < count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (collection.Length - offset < 4)
                {
                    throw new FormatException("A reservoir length is missing.");
                }
                uint length = BinaryPrimitives.ReadUInt32BigEndian(collection[offset..]);
                offset += 4;
                if (length > MaximumGeometryBytes || length > collection.Length - offset)
                {
                    throw new FormatException("A reservoir geometry is truncated or oversized.");
                }
                var geometry = DecodeGeometry(collection.Slice(offset, (int)length));
                offset += (int)length;
                totalColumns += geometry.Columns.Count;
                if (totalColumns > LiquidReservoirState.MaximumTotalColumns)
                {
                    throw new FormatException("The reservoir collection exceeds total support bounds.");
                }
                geometries.Add(geometry);
            }
            if (offset != collection.Length)
            {
                throw new FormatException("The reservoir collection has trailing bytes.");
            }
            var state = dependencies is null ? LiquidReservoirState.Create(capacity, geometries)
                : LiquidReservoirState.RestoreContinued(capacity, geometries, dependencies);
            if (!EncodeReservoirs(state, consumption, dependencies is not null).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("Reservoir geometry or fill witnesses differ from canonical reconstruction.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            return state;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The reservoir archive violates physical storage ownership.", error);
        }
    }

    internal static byte[] EncodeGeometry(ReservoirGeometry geometry)
    {
        byte[] columns = new byte[geometry.Columns.Count * ColumnByteLength];
        for (int index = 0; index < geometry.Columns.Count; index++)
        {
            var column = geometry.Columns[index];
            Span<byte> row = columns.AsSpan(index * ColumnByteLength, ColumnByteLength);
            BinaryPrimitives.WriteInt64BigEndian(row, column.X);
            BinaryPrimitives.WriteInt64BigEndian(row[8..], column.Y);
            BinaryPrimitives.WriteInt64BigEndian(row[16..], column.BottomZ);
        }
        return CanonicalEncodingV1.Frame("geography.reservoir-geometry.v1", geometry.Scope.WorldId.ToByteArray(),
            geometry.Scope.BranchId.ToByteArray(), geometry.Scope.RealmId.ToByteArray(), geometry.ReservoirId.ToByteArray(),
            geometry.CustodianId.ToByteArray(), Signed(geometry.SpillZ), columns);
    }

    private static ReservoirGeometry DecodeGeometry(ReadOnlySpan<byte> bytes)
    {
        var reader = new CanonicalTerrainReader(bytes, MaximumGeometryBytes, 8, "geography.reservoir-geometry.v1"u8);
        var scope = new TerrainScope(reader.ReadIdentity(), reader.ReadIdentity(), reader.ReadIdentity());
        EntityId identity = reader.ReadIdentity(), custodian = reader.ReadIdentity();
        long spill = reader.ReadSigned64();
        var encoded = reader.ReadField(ReservoirGeometry.MaximumColumns * ColumnByteLength);
        reader.Complete();
        if (encoded.Length % ColumnByteLength != 0)
        {
            throw new FormatException("Reservoir column rows have an invalid length.");
        }
        List<ReservoirColumn> columns = [];
        for (int offset = 0; offset < encoded.Length; offset += ColumnByteLength)
        {
            var row = encoded.Slice(offset, ColumnByteLength);
            columns.Add(new(BinaryPrimitives.ReadInt64BigEndian(row), BinaryPrimitives.ReadInt64BigEndian(row[8..]),
                BinaryPrimitives.ReadInt64BigEndian(row[16..])));
        }
        return new(scope, identity, custodian, spill, columns);
    }

    private static void WriteFill(Stream target, ReservoirFill fill)
    {
        target.Write(Unsigned(fill.Volume.Value));
        target.Write(Signed(fill.SurfaceMetres));
        target.Write(Unsigned(fill.SurfaceBillionths));
        target.Write(Unsigned(fill.RemainderMicrolitres));
        foreach (var column in fill.Columns)
        {
            target.Write(Unsigned(column.Volume.Value));
        }
    }

    private static byte[] Unsigned(ulong value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] Signed(long value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }

    private static void WriteUnsigned32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        stream.Write(bytes);
    }
}
