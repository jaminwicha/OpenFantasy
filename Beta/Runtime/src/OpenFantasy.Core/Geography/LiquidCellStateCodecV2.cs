using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Geography;

/// <summary>Placement bound to externally rederived exact terrain topology and the conserved reservoir owner.</summary>
public static class LiquidCellStateCodecV2
{
    public const int MaximumEncodedByteLength = LiquidCellStateCodecV1.MaximumEncodedByteLength;
    private const int PortionRowByteLength = 80;

    public static byte[] Encode(LiquidCellState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        TerrainLiquidTopology topology = state.Topology
            ?? throw new ArgumentException("The terrain liquid archive requires its exact topology.", nameof(state));
        return CanonicalEncodingV1.Frame("geography.liquid-cells.v2", CanonicalEncodingV1.Unsigned64(state.Revision),
            LiquidCellStateCodecV1.Signed(state.Boundary.Days), CanonicalEncodingV1.Unsigned64((ulong)state.Boundary.MillisecondsWithinDay),
            state.ReservoirHash.ToByteArray(), LiquidCellStateCodecV1.EncodeProfile(state.Profile),
            topology.ContentHash.ToByteArray(), LiquidCellStateCodecV1.EncodeRows(state));
    }

    public static LiquidCellState Decode(ReadOnlySpan<byte> bytes, LiquidReservoirState reservoirs,
        TerrainLiquidTopology topology)
    {
        ArgumentNullException.ThrowIfNull(reservoirs);
        ArgumentNullException.ThrowIfNull(topology);
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 8);
            reader.Tag("geography.liquid-cells.v2"u8);
            ulong revision = reader.Number();
            var boundary = new WorldInstant(reader.Signed(), checked((int)reader.Number()));
            if (!reader.Exact(32).SequenceEqual(reservoirs.ContentHash.ToByteArray()))
            {
                throw new FormatException("Liquid placement belongs to a different reservoir owner.");
            }
            LiquidFlowProfile profile = LiquidCellStateCodecV1.DecodeProfile(reader.Field(128));
            if (!reader.Exact(32).SequenceEqual(topology.ContentHash.ToByteArray()))
            {
                throw new FormatException("Liquid placement belongs to different proved terrain topology.");
            }
            ReadOnlySpan<byte> rows = reader.Field(LiquidCellState.MaximumPortions * PortionRowByteLength);
            reader.Complete();
            if (rows.Length % PortionRowByteLength != 0)
            {
                throw new FormatException("Liquid placement rows must be complete.");
            }
            var portions = new List<LiquidCellPortion>();
            for (int offset = 0; offset < rows.Length; offset += PortionRowByteLength)
            {
                var row = rows.Slice(offset, PortionRowByteLength);
                var cell = new LiquidCellKey(EntityId.FromBytes(row[..16]), new(BinaryPrimitives.ReadInt64BigEndian(row[16..]),
                    BinaryPrimitives.ReadInt64BigEndian(row[24..]), BinaryPrimitives.ReadInt64BigEndian(row[32..])));
                ulong origin = BinaryPrimitives.ReadUInt64BigEndian(row[40..]);
                if (origin is not 1 and not 2)
                {
                    throw new FormatException("Unknown liquid stock origin.");
                }
                portions.Add(new(cell, (LiquidStockOrigin)origin, EntityId.FromBytes(row.Slice(48, 16)),
                    BinaryPrimitives.ReadUInt64BigEndian(row[64..]), BinaryPrimitives.ReadUInt64BigEndian(row[72..])));
            }
            var restored = new LiquidCellState(reservoirs, profile, boundary, revision, portions, topology);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("Terrain liquid placement is not canonical.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            throw new FormatException("Invalid terrain liquid placement.", error);
        }
    }
}
