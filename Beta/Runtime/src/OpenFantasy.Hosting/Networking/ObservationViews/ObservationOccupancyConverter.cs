using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenFantasy.Hosting.Networking.ObservationViews;

/// <summary>Exact row-major occupancy, packed low bit first; count prevents padding from becoming known cells.</summary>
internal sealed class ObservationOccupancyConverter : JsonConverter<ReadOnlyCollection<bool>>
{
    public override ReadOnlyCollection<bool> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        PackedOccupancy packed = JsonSerializer.Deserialize<PackedOccupancy>(ref reader, options)
            ?? throw new JsonException("Missing packed occupancy.");
        if (packed.Count is < 0 or > PlayerViewCodec.MaximumCollectionItems || packed.Bits is null
            || packed.Bits.Length != (packed.Count + 7) / 8)
        {
            throw new FormatException("Packed occupancy requires its bounded count and exact byte length.");
        }
        int remainder = packed.Count % 8;
        if (remainder != 0 && (packed.Bits[^1] & ~((1 << remainder) - 1)) != 0)
        {
            throw new FormatException("Unused occupancy padding bits must be zero.");
        }
        bool[] cells = new bool[packed.Count];
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i] = (packed.Bits[i / 8] & (1 << (i % 8))) != 0;
        }
        return Array.AsReadOnly(cells);
    }

    public override void Write(Utf8JsonWriter writer, ReadOnlyCollection<bool> value, JsonSerializerOptions options)
    {
        if (value.Count > PlayerViewCodec.MaximumCollectionItems)
        {
            throw new FormatException("Occupancy exceeds its cell count bound.");
        }
        byte[] bits = new byte[(value.Count + 7) / 8];
        for (int i = 0; i < value.Count; i++)
        {
            if (value[i]) bits[i / 8] |= (byte)(1 << (i % 8));
        }
        JsonSerializer.Serialize(writer, new PackedOccupancy(value.Count, bits), options);
    }

    private sealed record PackedOccupancy(int Count, byte[] Bits);
}
