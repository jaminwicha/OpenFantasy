using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Geography;

/// <summary>Context-bound placement archive; decoding requires the exact conserved reservoir owner.</summary>
public static class LiquidCellStateCodecV1
{
    public const int MaximumEncodedByteLength = 1_400_000;
    private const int MaximumProfileByteLength = 128;
    private const int PortionRowByteLength = 80;

    public static byte[] Encode(LiquidCellState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        byte[] rows = new byte[state.Portions.Count * PortionRowByteLength];
        for (int index = 0; index < state.Portions.Count; index++)
        {
            LiquidCellPortion portion = state.Portions[index];
            Span<byte> row = rows.AsSpan(index * PortionRowByteLength, PortionRowByteLength);
            portion.Cell.ReservoirId.WriteBytes(row);
            BinaryPrimitives.WriteInt64BigEndian(row[16..], portion.Cell.Coordinate.X);
            BinaryPrimitives.WriteInt64BigEndian(row[24..], portion.Cell.Coordinate.Y);
            BinaryPrimitives.WriteInt64BigEndian(row[32..], portion.Cell.Coordinate.Z);
            BinaryPrimitives.WriteUInt64BigEndian(row[40..], (ulong)portion.Origin);
            portion.OriginId.WriteBytes(row[48..]);
            BinaryPrimitives.WriteUInt64BigEndian(row[64..], portion.Start);
            BinaryPrimitives.WriteUInt64BigEndian(row[72..], portion.End);
        }
        return CanonicalEncodingV1.Frame("geography.liquid-cells.v1", CanonicalEncodingV1.Unsigned64(state.Revision),
            Signed(state.Boundary.Days), CanonicalEncodingV1.Unsigned64((ulong)state.Boundary.MillisecondsWithinDay),
            state.ReservoirHash.ToByteArray(), EncodeProfile(state.Profile), rows);
    }

    public static LiquidCellState Decode(ReadOnlySpan<byte> bytes, LiquidReservoirState reservoirs)
    {
        ArgumentNullException.ThrowIfNull(reservoirs);
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 7);
            reader.Tag("geography.liquid-cells.v1"u8);
            ulong revision = reader.Number();
            var boundary = new WorldInstant(reader.Signed(), checked((int)reader.Number()));
            if (!reader.Exact(32).SequenceEqual(reservoirs.ContentHash.ToByteArray()))
            {
                throw new FormatException("Liquid placement belongs to a different reservoir owner.");
            }
            LiquidFlowProfile profile = DecodeProfile(reader.Field(MaximumProfileByteLength));
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
            var restored = new LiquidCellState(reservoirs, profile, boundary, revision, portions);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("Liquid placement is not canonical.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            throw new FormatException("Invalid detailed liquid placement.", error);
        }
    }

    internal static byte[] EncodeProfile(LiquidFlowProfile profile) => CanonicalEncodingV1.Frame("geography.liquid-flow-profile.v1",
        CanonicalEncodingV1.Unsigned64(profile.DownwardUnitsPerFace), CanonicalEncodingV1.Unsigned64(profile.LateralUnitsPerFace));

    private static byte[] Signed(long value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }

    private static LiquidFlowProfile DecodeProfile(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumProfileByteLength, 3);
        reader.Tag("geography.liquid-flow-profile.v1"u8);
        var profile = new LiquidFlowProfile(reader.Number(), reader.Number());
        reader.Complete();
        if (!EncodeProfile(profile).AsSpan().SequenceEqual(bytes))
        {
            throw new FormatException("Liquid flow profile is not canonical.");
        }
        return profile;
    }
}
