using System.Buffers.Binary;
using Microsoft.Data.Sqlite;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Hosting.Worlds;

internal static class WorldStorageBinary
{
    internal static byte[] Number(ulong value)
    {
        byte[] bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    internal static ulong ReadNumber(SqliteDataReader reader, int index) =>
        BinaryPrimitives.ReadUInt64BigEndian(ReadBlob(reader, index, sizeof(ulong), sizeof(ulong)));

    internal static EntityId ReadIdentity(SqliteDataReader reader, int index) =>
        EntityId.FromBytes(ReadBlob(reader, index, EntityId.ByteLength, EntityId.ByteLength));

    internal static ContentHash ReadHash(SqliteDataReader reader, int index) =>
        new(Convert.ToHexString(ReadBlob(reader, index, 32, 32)).ToLowerInvariant());

    internal static byte[] ReadState(SqliteDataReader reader, int stateIndex, int hashIndex)
    {
        byte[] state = ReadBlob(reader, stateIndex, 1, WorldDatabase.MaximumStateByteLength);
        if (ContentHash.Compute(state) != ReadHash(reader, hashIndex))
        {
            throw new InvalidDataException("Stored world state does not match its checksum.");
        }
        return state;
    }

    internal static byte[] ReadBlob(SqliteDataReader reader, int index, int minimum, int maximum)
    {
        if (reader.GetFieldType(index) != typeof(byte[]))
        {
            throw new InvalidDataException("A stored world field is not binary.");
        }
        long length = reader.GetBytes(index, 0, null, 0, 0);
        if (length < minimum || length > maximum)
        {
            throw new InvalidDataException("A stored world field exceeds its format bounds.");
        }
        return reader.GetFieldValue<byte[]>(index);
    }

    internal static void RequireBytes(byte[] bytes, int maximum, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(bytes, parameterName);
        if (bytes.Length == 0 || bytes.Length > maximum)
        {
            throw new ArgumentException("Stored world bytes exceed their declared format bounds.", parameterName);
        }
    }

    internal static void RequirePositiveSequence(ulong sequence)
    {
        if (sequence == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "A world commit requires a positive sequence.");
        }
    }
}
