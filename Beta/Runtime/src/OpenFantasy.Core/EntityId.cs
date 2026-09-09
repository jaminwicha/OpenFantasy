using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;

namespace OpenFantasy.Core;

/// <summary>
/// A 128-bit identity ordered by its canonical big-endian bytes. All bit patterns
/// are valid. Runtime hash codes are never persistence keys or ordering keys.
/// </summary>
public readonly record struct EntityId(ulong High, ulong Low) : IComparable<EntityId>
{
    public const int ByteLength = 16;

    private const int WordByteLength = sizeof(ulong);
    private const int WordHexLength = WordByteLength * 2;
    private const int HexLength = ByteLength * 2;

    public static EntityId FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != ByteLength)
        {
            throw new ArgumentException("An identity requires exactly 16 bytes.", nameof(bytes));
        }

        return new EntityId(BinaryPrimitives.ReadUInt64BigEndian(bytes),
            BinaryPrimitives.ReadUInt64BigEndian(bytes[WordByteLength..]));
    }

    /// <summary>Writes the first 16 bytes of the destination; trailing bytes remain untouched.</summary>
    public void WriteBytes(Span<byte> destination)
    {
        if (destination.Length < ByteLength)
        {
            throw new ArgumentException("The destination requires at least 16 bytes.", nameof(destination));
        }

        BinaryPrimitives.WriteUInt64BigEndian(destination, High);
        BinaryPrimitives.WriteUInt64BigEndian(destination[WordByteLength..], Low);
    }

    public byte[] ToByteArray()
    {
        byte[] bytes = new byte[ByteLength];
        WriteBytes(bytes);
        return bytes;
    }

    /// <summary>Accepts only the canonical 32 lowercase hexadecimal characters.</summary>
    public static EntityId Parse(string? text) => TryParse(text, out EntityId result)
        ? result
        : throw new FormatException("An identity must contain exactly 32 lowercase hexadecimal characters.");

    public static bool TryParse(string? text, out EntityId result)
    {
        result = default;
        if (text is null || text.Length != HexLength)
        {
            return false;
        }

        foreach (char character in text)
        {
            if (character is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
            {
                return false;
            }
        }

        result = new EntityId(
            ulong.Parse(text.AsSpan(0, WordHexLength), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture),
            ulong.Parse(text.AsSpan(WordHexLength, WordHexLength), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));
        return true;
    }

    /// <summary>
    /// Derives an identity from stable generation inputs using the v1 encoding.
    /// The inserting authority must still reject any detected identity collision.
    /// </summary>
    public static EntityId DeriveGenerated(ReadOnlySpan<byte> worldSeed, string generatorVersion,
        string sourceAddress, string role, ulong ordinal)
    {
        if (worldSeed.IsEmpty)
        {
            throw new ArgumentException("A world seed cannot be empty.", nameof(worldSeed));
        }

        byte[] payload = CanonicalEncodingV1.Frame("entity.generated", worldSeed.ToArray(),
            CanonicalEncodingV1.Text(generatorVersion, nameof(generatorVersion)),
            CanonicalEncodingV1.Text(sourceAddress, nameof(sourceAddress)),
            CanonicalEncodingV1.Text(role, nameof(role)), CanonicalEncodingV1.Unsigned64(ordinal));
        return HashIdentity(payload);
    }

    /// <summary>Derives an identity from an accepted command/event identity and spawn ordinal.</summary>
    public static EntityId DeriveSpawned(EntityId acceptedCauseId, ulong ordinal) => HashIdentity(
        CanonicalEncodingV1.Frame("entity.spawned", acceptedCauseId.ToByteArray(), CanonicalEncodingV1.Unsigned64(ordinal)));

    private static EntityId HashIdentity(ReadOnlySpan<byte> payload)
    {
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(payload, digest);
        return FromBytes(digest[..ByteLength]);
    }

    public int CompareTo(EntityId other)
    {
        int highOrder = High.CompareTo(other.High);
        return highOrder != 0 ? highOrder : Low.CompareTo(other.Low);
    }

    public override string ToString() => High.ToString("x16", CultureInfo.InvariantCulture)
        + Low.ToString("x16", CultureInfo.InvariantCulture);

    public static bool operator <(EntityId left, EntityId right) => left.CompareTo(right) < 0;
    public static bool operator >(EntityId left, EntityId right) => left.CompareTo(right) > 0;
    public static bool operator <=(EntityId left, EntityId right) => left.CompareTo(right) <= 0;
    public static bool operator >=(EntityId left, EntityId right) => left.CompareTo(right) >= 0;
}
