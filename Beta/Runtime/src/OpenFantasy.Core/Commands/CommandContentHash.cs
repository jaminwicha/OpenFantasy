using System.Security.Cryptography;

namespace OpenFantasy.Core.Commands;

/// <summary>A complete SHA-256 command-content fingerprint, not an authentication credential.</summary>
public sealed record CommandContentHash
{
    public const int ByteLength = SHA256.HashSizeInBytes;
    private readonly string _hex;

    private CommandContentHash(string hex) => _hex = hex;

    public static CommandContentHash FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != ByteLength)
        {
            throw new ArgumentException("A content hash requires exactly 32 bytes.", nameof(bytes));
        }

        return new CommandContentHash(Convert.ToHexString(bytes).ToLowerInvariant());
    }

    public static CommandContentHash Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length != ByteLength * 2 || text.Any(character =>
                character is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
        {
            throw new FormatException("A content hash requires 64 lowercase hexadecimal characters.");
        }

        return new CommandContentHash(text);
    }

    public byte[] ToByteArray() => Convert.FromHexString(_hex);
    public override string ToString() => _hex;

    internal static CommandContentHash Compute(ReadOnlySpan<byte> canonicalBytes) =>
        FromBytes(SHA256.HashData(canonicalBytes));
}
