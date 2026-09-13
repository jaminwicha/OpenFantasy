using System.Buffers.Binary;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>Bounded standalone archive of exact terrain, finite calibration, admitted custody and exclusive active cells.</summary>
public static class TerrainMaterialCodecV1
{
    public const int MaximumEncodedByteLength = 5_710_000;
    internal const int MaximumBindingByteLength = 8192;
    private const int MaximumDepositByteLength = 4_110_000;
    private const int MaximumMaterialByteLength = 4096;
    private const int CellByteLength = 24;

    public static byte[] Encode(TerrainMaterialState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        byte[] cells = new byte[checked(state.ActiveCells.Count * CellByteLength)];
        for (int i = 0; i < state.ActiveCells.Count; i++)
        {
            Span<byte> row = cells.AsSpan(i * CellByteLength, CellByteLength);
            CellCoordinate cell = state.ActiveCells[i];
            BinaryPrimitives.WriteInt64BigEndian(row, cell.X);
            BinaryPrimitives.WriteInt64BigEndian(row[8..], cell.Y);
            BinaryPrimitives.WriteInt64BigEndian(row[16..], cell.Z);
        }
        byte[] bytes = CanonicalEncodingV1.Frame("geography.terrain-material-state.v1", EncodeDeposit(state.Deposit),
            AuthorityEncodingV1.Encode(state.Inventory), cells);
        if (bytes.Length > MaximumEncodedByteLength)
        {
            throw new ArgumentException("The material archive exceeds its bound.");
        }
        return bytes;
    }

    public static TerrainMaterialState Decode(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var reader = new CanonicalTerrainReader(bytes, MaximumEncodedByteLength, 4, "geography.terrain-material-state.v1"u8);
            TerrainMaterialDeposit deposit = DecodeDeposit(reader.ReadField(MaximumDepositByteLength), cancellationToken);
            CountedAuthorityState inventory = AuthorityEncodingV1.Decode(reader.ReadField(AuthorityEncodingV1.MaximumStateByteLength));
            ReadOnlySpan<byte> cells = reader.ReadField(TerrainMaterialState.MaximumActiveCells * CellByteLength);
            if (cells.Length % CellByteLength != 0)
            {
                throw new FormatException("A material cell row is truncated.");
            }
            List<CellCoordinate> active = [];
            for (int offset = 0; offset < cells.Length; offset += CellByteLength)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ReadOnlySpan<byte> row = cells.Slice(offset, CellByteLength);
                var cell = new CellCoordinate(BinaryPrimitives.ReadInt64BigEndian(row), BinaryPrimitives.ReadInt64BigEndian(row[8..]),
                    BinaryPrimitives.ReadInt64BigEndian(row[16..]));
                if (active.Count > 0 && active[^1].CompareTo(cell) >= 0)
                {
                    throw new FormatException("Material cells must be strictly canonical.");
                }
                active.Add(cell);
            }
            reader.Complete();
            var state = new TerrainMaterialState(deposit, inventory, active);
            if (!Encode(state).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The material archive is noncanonical.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            return state;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The material archive violates its calibration, funding or representation contracts.", error);
        }
    }

    private static byte[] EncodeDeposit(TerrainMaterialDeposit deposit) =>
        CanonicalEncodingV1.Frame("geography.terrain-material-deposit.v1",
            new ReadOnlyMemory<byte>[] { WatershedTerrainCodecV1.Encode(deposit.Terrain) }.Concat(BindingFields(deposit)).ToArray());

    internal static byte[] EncodeBinding(TerrainMaterialDeposit deposit) =>
        CanonicalEncodingV1.Frame("geography.terrain-material-binding.v1", BindingFields(deposit));

    private static ReadOnlyMemory<byte>[] BindingFields(TerrainMaterialDeposit deposit) => [
            deposit.MacroColumn.BasinId.ToByteArray(), CanonicalEncodingV1.Unsigned64((ulong)deposit.MacroColumn.ColumnIndex),
            CanonicalEncodingV1.Unsigned64((ulong)deposit.Depth), deposit.Material.Item.DefinitionId.ToByteArray(),
            Convert.FromHexString(deposit.Material.Item.Version.Value), MaterialRecipeCodecV1.EncodeMaterial(deposit.Material.Physical),
            CanonicalEncodingV1.Unsigned64(deposit.UnitsPerCell), deposit.Source.SourceId.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(deposit.Source.Start), CanonicalEncodingV1.Unsigned64(deposit.Source.End),
            deposit.CoarseCustodianId.ToByteArray(), deposit.FineCustodianId.ToByteArray()];

    private static TerrainMaterialDeposit DecodeDeposit(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken)
    {
        var reader = new CanonicalTerrainReader(bytes, MaximumDepositByteLength, 14, "geography.terrain-material-deposit.v1"u8);
        PreparedWatershedTerrain terrain = WatershedTerrainCodecV1.Decode(reader.ReadField(WatershedTerrainCodecV1.MaximumEncodedByteLength), cancellationToken);
        return ReadBinding(ref reader, terrain);
    }

    internal static TerrainMaterialDeposit DecodeBinding(ReadOnlySpan<byte> bytes, PreparedWatershedTerrain terrain)
    {
        var reader = new CanonicalTerrainReader(bytes, MaximumBindingByteLength, 13,
            "geography.terrain-material-binding.v1"u8);
        return ReadBinding(ref reader, terrain);
    }

    private static TerrainMaterialDeposit ReadBinding(ref CanonicalTerrainReader reader, PreparedWatershedTerrain terrain)
    {
        var column = new WatershedColumnReference(reader.ReadIdentity(), reader.ReadSmallInteger(WatershedBounds.MaximumColumns - 1));
        int depth = reader.ReadSmallInteger(TerrainMaterialDeposit.MaximumDepth);
        var item = new CountedItemDefinition(reader.ReadIdentity(), reader.ReadHash());
        MaterialDefinition physical = MaterialRecipeCodecV1.DecodeMaterial(reader.ReadField(MaximumMaterialByteLength));
        ulong units = reader.ReadUnsigned64();
        var source = new SourceInterval(reader.ReadIdentity(), reader.ReadUnsigned64(), reader.ReadUnsigned64());
        EntityId coarse = reader.ReadIdentity();
        EntityId fine = reader.ReadIdentity();
        reader.Complete();
        return new(terrain, column, depth, new(item, physical), units, source, coarse, fine);
    }
}
