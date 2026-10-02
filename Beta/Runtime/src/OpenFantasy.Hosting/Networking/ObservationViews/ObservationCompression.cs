using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;

namespace OpenFantasy.Hosting.Networking.ObservationViews;

/// <summary>Versioned bounded Brotli container for canonical JSON that exceeds the plain wire-body budget.</summary>
internal static class ObservationCompression
{
    internal const int HeaderBytes = 12;
    internal const ushort Version = 1;
    internal const int Quality = 5;
    internal const int Window = 22;
    private static ReadOnlySpan<byte> Magic => "OFVB"u8;

    internal static bool IsCompressed(ReadOnlySpan<byte> bytes) => bytes.StartsWith(Magic);

    internal static byte[] Encode(ReadOnlySpan<byte> json)
    {
        if (json.Length <= PlayerViewCodec.MaximumBodyBytes || json.Length > PlayerViewCodec.MaximumExpandedJsonBytes)
        {
            throw new FormatException("Compressed observations require JSON above the plain bound and inside the expanded bound.");
        }
        byte[] encoded = new byte[PlayerViewCodec.MaximumBodyBytes];
        if (!BrotliEncoder.TryCompress(json, encoded.AsSpan(HeaderBytes), out int written, Quality, Window))
        {
            throw new FormatException("Compressed observation exceeds its unchanged wire-body bound.");
        }
        Magic.CopyTo(encoded);
        BinaryPrimitives.WriteUInt16BigEndian(encoded.AsSpan(4), Version);
        BinaryPrimitives.WriteUInt16BigEndian(encoded.AsSpan(6), 0);
        BinaryPrimitives.WriteUInt32BigEndian(encoded.AsSpan(8), checked((uint)json.Length));
        return encoded.AsSpan(0, HeaderBytes + written).ToArray();
    }

    internal static byte[] Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length <= HeaderBytes || bytes.Length > PlayerViewCodec.MaximumBodyBytes || !IsCompressed(bytes)
            || BinaryPrimitives.ReadUInt16BigEndian(bytes[4..]) != Version
            || BinaryPrimitives.ReadUInt16BigEndian(bytes[6..]) != 0)
        {
            throw new FormatException("Invalid compressed observation header.");
        }
        uint declared = BinaryPrimitives.ReadUInt32BigEndian(bytes[8..]);
        if (declared <= PlayerViewCodec.MaximumBodyBytes || declared > PlayerViewCodec.MaximumExpandedJsonBytes)
        {
            throw new FormatException("Invalid compressed observation expanded length.");
        }
        // The declared length is clamped before allocation. No decoder growth or retry is permitted.
        byte[] expanded = new byte[checked((int)declared)];
        using var decoder = new BrotliDecoder();
        OperationStatus status = decoder.Decompress(bytes[HeaderBytes..], expanded, out int consumed, out int written);
        if (status != OperationStatus.Done || consumed != bytes.Length - HeaderBytes || written != expanded.Length)
        {
            throw new FormatException("Compressed observation is truncated, trailing, malformed or exceeds its declared expansion.");
        }
        return expanded;
    }
}
