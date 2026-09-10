using System.Buffers.Binary;
using System.Text;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Hosting.Content;

/// <summary>Exact bounded user text and canonical domain separation for a new farm profile.</summary>
internal sealed class GatheringFarmInput
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);

    private GatheringFarmInput(string baseSeedText) => BaseSeedText = baseSeedText;

    internal string BaseSeedText { get; }

    internal static GatheringFarmInput Create(string seedText, string playerName)
    {
        byte[] seed = Validate(seedText, FarmWorldAdmission.MaximumSeedByteLength, nameof(seedText));
        Validate(playerName, FarmWorldAdmission.MaximumPlayerNameByteLength, nameof(playerName));
        ReadOnlySpan<byte> domain = "farm-work.seed.v1"u8;
        // Exact two-field OFC v1 frame; the portable core's generic framing helper is internal.
        byte[] frame = new byte[checked(20 + domain.Length + seed.Length)];
        "OFC\0"u8.CopyTo(frame);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(8), 2);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(12), (uint)domain.Length);
        domain.CopyTo(frame.AsSpan(16));
        int seedLengthOffset = 16 + domain.Length;
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(seedLengthOffset), (uint)seed.Length);
        seed.CopyTo(frame.AsSpan(seedLengthOffset + 4));
        return new GatheringFarmInput(ContentHash.Compute(frame).Value);
    }

    private static byte[] Validate(string value, int maximumBytes, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > maximumBytes || value.Any(char.IsControl))
        {
            throw new ArgumentException("Farm input text exceeds its bound or contains control characters.", parameterName);
        }
        byte[] bytes = _strictUtf8.GetBytes(value);
        if (bytes.Length > maximumBytes)
        {
            throw new ArgumentException("Farm input text exceeds its UTF-8 byte bound.", parameterName);
        }
        return bytes;
    }
}
