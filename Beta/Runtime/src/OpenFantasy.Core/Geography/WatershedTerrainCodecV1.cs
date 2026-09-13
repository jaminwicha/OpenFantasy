using System.Buffers.Binary;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>Exact network, transform and window plus rederived column witnesses. This is not the lived terrain codec.</summary>
public static class WatershedTerrainCodecV1
{
    public const int MaximumEncodedByteLength = 4_100_000;
    private const int ColumnByteLength = 68;
    private const int MaximumMappingByteLength = 256;
    private const int MaximumBoundsByteLength = 256;

    public static byte[] Encode(PreparedWatershedTerrain window)
    {
        ArgumentNullException.ThrowIfNull(window);
        byte[] rows = new byte[checked(window.Columns.Count * ColumnByteLength)];
        for (int i = 0; i < window.Columns.Count; i++)
        {
            WatershedTerrainColumn column = window.Columns[i];
            Span<byte> row = rows.AsSpan(i * ColumnByteLength, ColumnByteLength);
            column.MacroColumn.BasinId.WriteBytes(row);
            BinaryPrimitives.WriteUInt32BigEndian(row[16..], (uint)column.MacroColumn.ColumnIndex);
            column.FormationId.WriteBytes(row[20..]);
            column.SubstrateParcelId.WriteBytes(row[36..]);
            BinaryPrimitives.WriteInt64BigEndian(row[52..], column.SurfaceZ);
            BinaryPrimitives.WriteUInt32BigEndian(row[60..], (uint)column.OffsetX);
            BinaryPrimitives.WriteUInt32BigEndian(row[64..], (uint)column.OffsetY);
        }
        byte[] bytes = CanonicalEncodingV1.Frame("geography.watershed.terrain-window.v1",
            WatershedNetworkCodecV1.Encode(window.Network), EncodeMapping(window.Mapping), EncodeBounds(window.Bounds), rows);
        if (bytes.Length > MaximumEncodedByteLength)
        {
            throw new ArgumentException("The mapped terrain candidate exceeds its archive bound.", nameof(window));
        }
        return bytes;
    }

    public static PreparedWatershedTerrain Decode(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var reader = new CanonicalTerrainReader(bytes, MaximumEncodedByteLength, 5, "geography.watershed.terrain-window.v1"u8);
            PreparedWatershedNetwork network = WatershedNetworkCodecV1.Decode(
                reader.ReadField(WatershedNetworkCodecV1.MaximumEncodedByteLength), cancellationToken);
            WatershedTerrainMapping mapping = ReadMapping(reader.ReadField(MaximumMappingByteLength));
            TerrainRegionBounds bounds = ReadBounds(reader.ReadField(MaximumBoundsByteLength));
            _ = reader.ReadExactField(checked(bounds.Width * bounds.Depth * ColumnByteLength));
            reader.Complete();
            PreparedWatershedTerrain candidate = new WatershedTerrainMapper().Prepare(network, mapping, bounds, cancellationToken);
            if (!Encode(candidate).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The terrain window differs from its pinned macro mapping.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            return candidate;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The terrain mapping contains invalid inputs or dependencies.", error);
        }
    }

    internal static byte[] EncodeMapping(WatershedTerrainMapping mapping) =>
        CanonicalEncodingV1.Frame("geography.watershed.terrain-mapping.v1",
            CanonicalEncodingV1.Unsigned64((ulong)mapping.CellsPerColumn),
            CanonicalEncodingV1.Unsigned64((ulong)mapping.CellsPerElevationUnit),
            TerrainRegionCodecV1.Signed64(mapping.ElevationDatumZ));

    private static byte[] EncodeBounds(TerrainRegionBounds bounds) =>
        CanonicalEncodingV1.Frame("geography.watershed.terrain-bounds.v1",
            TerrainRegionCodecV1.Signed64(bounds.Minimum.X), TerrainRegionCodecV1.Signed64(bounds.Minimum.Y),
            TerrainRegionCodecV1.Signed64(bounds.Minimum.Z), CanonicalEncodingV1.Unsigned64((ulong)bounds.Width),
            CanonicalEncodingV1.Unsigned64((ulong)bounds.Depth), CanonicalEncodingV1.Unsigned64((ulong)bounds.Height));

    private static WatershedTerrainMapping ReadMapping(ReadOnlySpan<byte> bytes)
    {
        var reader = new CanonicalTerrainReader(bytes, MaximumMappingByteLength, 4, "geography.watershed.terrain-mapping.v1"u8);
        var result = new WatershedTerrainMapping(reader.ReadSmallInteger(WatershedTerrainMapping.MaximumScale),
            reader.ReadSmallInteger(WatershedTerrainMapping.MaximumScale), reader.ReadSigned64());
        reader.Complete();
        return result;
    }

    private static TerrainRegionBounds ReadBounds(ReadOnlySpan<byte> bytes)
    {
        var reader = new CanonicalTerrainReader(bytes, MaximumBoundsByteLength, 7, "geography.watershed.terrain-bounds.v1"u8);
        var result = new TerrainRegionBounds(new(reader.ReadSigned64(), reader.ReadSigned64(), reader.ReadSigned64()),
            reader.ReadSmallInteger(TerrainRegionBounds.MaximumWidth), reader.ReadSmallInteger(TerrainRegionBounds.MaximumDepth),
            reader.ReadSmallInteger(TerrainRegionBounds.MaximumHeight));
        reader.Complete();
        return result;
    }
}
