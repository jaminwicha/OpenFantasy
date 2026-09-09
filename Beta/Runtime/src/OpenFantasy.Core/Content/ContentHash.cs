using System.Security.Cryptography;

namespace OpenFantasy.Core.Content;

/// <summary>An immutable SHA-256 digest with one canonical textual representation.</summary>
public sealed record ContentHash
{
    public string Value { get; }

    public ContentHash(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != SHA256.HashSizeInBytes * 2
            || value.Any(character => !(character is >= '0' and <= '9' or >= 'a' and <= 'f')))
        {
            throw new ArgumentException("A content hash requires 64 lowercase hexadecimal characters.", nameof(value));
        }
        Value = value;
    }

    public static ContentHash Compute(ReadOnlySpan<byte> content) =>
        new(Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant());

    public byte[] ToByteArray() => Convert.FromHexString(Value);

    public override string ToString() => Value;
}
