using System.Buffers.Binary;

namespace OpenFantasy.Core.History;

/// <summary>Reads only the fixed field types permitted by the history request format.</summary>
internal ref struct HistoryRequestReader
{
    private const int HeaderByteLength = 12;
    private readonly ReadOnlySpan<byte> _bytes;
    private int _offset;
    private uint _remainingFields;

    internal HistoryRequestReader(ReadOnlySpan<byte> bytes, int maximumBytes, uint expectedFields)
    {
        if (bytes.Length > maximumBytes || bytes.Length < HeaderByteLength)
        {
            throw new FormatException("The history request is outside its encoded byte limits.");
        }

        if (!bytes[..4].SequenceEqual("OFC\0"u8)
            || BinaryPrimitives.ReadUInt32BigEndian(bytes[4..]) != 1)
        {
            throw new FormatException("The canonical frame header is unsupported.");
        }

        _remainingFields = BinaryPrimitives.ReadUInt32BigEndian(bytes[8..]);
        if (_remainingFields != expectedFields)
        {
            throw new FormatException("The history request must contain exactly its declared fields.");
        }

        _bytes = bytes;
        _offset = HeaderByteLength;
    }

    internal ReadOnlySpan<byte> ReadField(int maximumLength)
    {
        if (_remainingFields == 0 || _bytes.Length - _offset < sizeof(uint))
        {
            throw new FormatException("A required field length is absent or incomplete.");
        }

        uint length = BinaryPrimitives.ReadUInt32BigEndian(_bytes[_offset..]);
        _offset += sizeof(uint);
        if (length > maximumLength || length > _bytes.Length - _offset)
        {
            throw new FormatException("A required field exceeds its size or is incomplete.");
        }

        ReadOnlySpan<byte> result = _bytes.Slice(_offset, (int)length);
        _offset += (int)length;
        _remainingFields--;
        return result;
    }

    internal ReadOnlySpan<byte> ReadExactField(int length)
    {
        ReadOnlySpan<byte> bytes = ReadField(length);
        if (bytes.Length != length)
        {
            throw new FormatException("A typed history field has the wrong byte length.");
        }

        return bytes;
    }

    internal void ExpectTag(ReadOnlySpan<byte> expectedTag)
    {
        if (!ReadExactField(expectedTag.Length).SequenceEqual(expectedTag))
        {
            throw new FormatException("The history request contract tag is unsupported.");
        }
    }

    internal EntityId ReadIdentity() => EntityId.FromBytes(ReadExactField(EntityId.ByteLength));
    internal long ReadSigned64() => BinaryPrimitives.ReadInt64BigEndian(ReadExactField(sizeof(long)));
    internal ulong ReadUnsigned64() => BinaryPrimitives.ReadUInt64BigEndian(ReadExactField(sizeof(ulong)));

    internal void Complete()
    {
        if (_remainingFields != 0 || _offset != _bytes.Length)
        {
            throw new FormatException("The history request contains unconsumed fields or trailing bytes.");
        }
    }
}
