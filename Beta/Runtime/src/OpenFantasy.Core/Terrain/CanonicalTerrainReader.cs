using System.Buffers.Binary;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Terrain;

/// <summary>Fixed-schema frame reader; lengths and field counts are checked before slices or allocations.</summary>
internal ref struct CanonicalTerrainReader
{
    private readonly ReadOnlySpan<byte> _bytes;
    private int _offset;
    private uint _remaining;

    internal CanonicalTerrainReader(ReadOnlySpan<byte> bytes, int maximumBytes, uint expectedFields,
        ReadOnlySpan<byte> expectedTag)
    {
        if (bytes.Length is < 12 || bytes.Length > maximumBytes || !bytes[..4].SequenceEqual("OFC\0"u8)
            || BinaryPrimitives.ReadUInt32BigEndian(bytes[4..]) != 1
            || BinaryPrimitives.ReadUInt32BigEndian(bytes[8..]) != expectedFields)
        {
            throw new FormatException("The terrain frame size, header or field count is invalid.");
        }
        _bytes = bytes;
        _offset = 12;
        _remaining = expectedFields;
        if (!ReadExactField(expectedTag.Length).SequenceEqual(expectedTag))
        {
            throw new FormatException("The terrain contract tag is unsupported.");
        }
    }

    internal ReadOnlySpan<byte> ReadField(int maximumLength)
    {
        if (_remaining == 0 || _bytes.Length - _offset < sizeof(uint))
        {
            throw new FormatException("A terrain field length is missing.");
        }
        uint length = BinaryPrimitives.ReadUInt32BigEndian(_bytes[_offset..]);
        _offset += sizeof(uint);
        if (length > maximumLength || length > _bytes.Length - _offset)
        {
            throw new FormatException("A terrain field is truncated or exceeds its bound.");
        }
        ReadOnlySpan<byte> result = _bytes.Slice(_offset, (int)length);
        _offset += (int)length;
        _remaining--;
        return result;
    }

    internal ReadOnlySpan<byte> ReadExactField(int length)
    {
        ReadOnlySpan<byte> result = ReadField(length);
        if (result.Length != length)
        {
            throw new FormatException("A typed terrain field has the wrong length.");
        }
        return result;
    }

    internal EntityId ReadIdentity() => EntityId.FromBytes(ReadExactField(EntityId.ByteLength));
    internal ContentHash ReadHash() => new(Convert.ToHexString(ReadExactField(32)).ToLowerInvariant());
    internal long ReadSigned64() => BinaryPrimitives.ReadInt64BigEndian(ReadExactField(sizeof(long)));
    internal ulong ReadUnsigned64() => BinaryPrimitives.ReadUInt64BigEndian(ReadExactField(sizeof(ulong)));

    internal int ReadSmallInteger(int maximum)
    {
        ulong value = ReadUnsigned64();
        if (value > (ulong)maximum)
        {
            throw new FormatException("A terrain integer exceeds its supported bound.");
        }
        return (int)value;
    }

    internal void Complete()
    {
        if (_remaining != 0 || _offset != _bytes.Length)
        {
            throw new FormatException("The terrain frame has trailing or unconsumed data.");
        }
    }
}
