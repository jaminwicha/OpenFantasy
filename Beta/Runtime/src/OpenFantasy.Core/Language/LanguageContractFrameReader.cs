using System.Buffers.Binary;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Language;

/// <summary>Strict bounded reader for the existing fixed language profile frames.</summary>
internal ref struct LanguageContractFrameReader
{
    private readonly ReadOnlySpan<byte> _bytes;
    private int _offset;
    private uint _remainingFields;

    internal LanguageContractFrameReader(ReadOnlySpan<byte> bytes, int maximumBytes, uint maximumFields,
        ReadOnlySpan<byte> tag)
    {
        if (bytes.Length < 12 || bytes.Length > maximumBytes || !bytes[..4].SequenceEqual("OFC\0"u8)
            || BinaryPrimitives.ReadUInt32BigEndian(bytes[4..]) != 1)
        {
            throw new FormatException("The language frame header, version or size is invalid.");
        }

        _remainingFields = BinaryPrimitives.ReadUInt32BigEndian(bytes[8..]);
        if (_remainingFields == 0 || _remainingFields > maximumFields)
        {
            throw new FormatException("The language frame field count exceeds its supported range.");
        }

        _bytes = bytes;
        _offset = 12;
        if (!ReadField(64).SequenceEqual(tag))
        {
            throw new FormatException("The language frame type is unsupported.");
        }
    }

    internal readonly uint RemainingFields => _remainingFields;

    internal ReadOnlySpan<byte> ReadField(int maximumLength)
    {
        if (_remainingFields == 0 || _bytes.Length - _offset < sizeof(uint))
        {
            throw new FormatException("A required language field is missing.");
        }

        uint length = BinaryPrimitives.ReadUInt32BigEndian(_bytes[_offset..]);
        _offset += sizeof(uint);
        if (length > maximumLength || length > _bytes.Length - _offset)
        {
            throw new FormatException("A language field is truncated or exceeds its limit.");
        }

        ReadOnlySpan<byte> result = _bytes.Slice(_offset, (int)length);
        _offset += (int)length;
        _remainingFields--;
        return result;
    }

    private ReadOnlySpan<byte> ReadExactField(int length)
    {
        ReadOnlySpan<byte> field = ReadField(length);
        if (field.Length != length)
        {
            throw new FormatException("A typed language field has the wrong byte width.");
        }

        return field;
    }

    internal EntityId ReadIdentity() => EntityId.FromBytes(ReadExactField(EntityId.ByteLength));
    internal ContentHash ReadHash() => new(Convert.ToHexString(ReadExactField(32)).ToLowerInvariant());
    internal ulong ReadUnsigned64() => BinaryPrimitives.ReadUInt64BigEndian(ReadExactField(sizeof(ulong)));

    internal void Complete()
    {
        if (_remainingFields != 0 || _offset != _bytes.Length)
        {
            throw new FormatException("The language frame has extra fields or trailing bytes.");
        }
    }
}
