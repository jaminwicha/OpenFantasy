using System.Buffers.Binary;
using System.Text;

namespace OpenFantasy.Core.Actors;

/// <summary>Bounded reader of the existing body frames; no normalization or optional-field defaults.</summary>
internal ref struct BodyContractFrameReader
{
    private static readonly UTF8Encoding _strictUtf8 = new(false, true);
    private readonly ReadOnlySpan<byte> _bytes;
    private int _offset;
    private uint _remainingFields;

    internal BodyContractFrameReader(ReadOnlySpan<byte> bytes, int maximumBytes, uint maximumFields,
        ReadOnlySpan<byte> expectedTag)
    {
        if (bytes.Length < 12 || bytes.Length > maximumBytes || !bytes[..4].SequenceEqual("OFC\0"u8)
            || BinaryPrimitives.ReadUInt32BigEndian(bytes[4..]) != 1)
        {
            throw new FormatException("The body frame header, version or byte count is invalid.");
        }

        _remainingFields = BinaryPrimitives.ReadUInt32BigEndian(bytes[8..]);
        if (_remainingFields == 0 || _remainingFields > maximumFields)
        {
            throw new FormatException("The body frame field count is outside its limit.");
        }

        _bytes = bytes;
        _offset = 12;
        if (!ReadField(64).SequenceEqual(expectedTag))
        {
            throw new FormatException("The body frame type is unsupported.");
        }
    }

    internal readonly uint RemainingFields => _remainingFields;

    internal ReadOnlySpan<byte> ReadField(int maximumLength)
    {
        if (_remainingFields == 0 || _bytes.Length - _offset < sizeof(uint))
        {
            throw new FormatException("A required body field is absent.");
        }

        uint length = BinaryPrimitives.ReadUInt32BigEndian(_bytes[_offset..]);
        _offset += sizeof(uint);
        if (length > maximumLength || length > _bytes.Length - _offset)
        {
            throw new FormatException("A body field is truncated or exceeds its bound.");
        }

        ReadOnlySpan<byte> result = _bytes.Slice(_offset, (int)length);
        _offset += (int)length;
        _remainingFields--;
        return result;
    }

    internal ReadOnlySpan<byte> ReadExactField(int length)
    {
        ReadOnlySpan<byte> result = ReadField(length);
        if (result.Length != length)
        {
            throw new FormatException("A typed body field has the wrong byte width.");
        }

        return result;
    }

    internal EntityId ReadIdentity() => EntityId.FromBytes(ReadExactField(EntityId.ByteLength));

    internal EntityId? ReadOptionalIdentity()
    {
        ReadOnlySpan<byte> result = ReadField(EntityId.ByteLength);
        return result.Length switch
        {
            0 => null,
            EntityId.ByteLength => EntityId.FromBytes(result),
            _ => throw new FormatException("An optional body identity must be absent or sixteen bytes.")
        };
    }

    internal ulong ReadUnsigned64() => BinaryPrimitives.ReadUInt64BigEndian(ReadExactField(sizeof(ulong)));
    internal long ReadSigned64() => BinaryPrimitives.ReadInt64BigEndian(ReadExactField(sizeof(long)));

    internal void ExpectVersion()
    {
        if (ReadUnsigned64() != BodyContractEncodingV1.ContractVersion)
        {
            throw new FormatException("The body contract version is unsupported.");
        }
    }

    internal BodySlotKey ReadSlot() => new(_strictUtf8.GetString(ReadField(BodySlotKey.MaximumLength)));

    internal BodySlotKey? ReadOptionalSlot()
    {
        ReadOnlySpan<byte> result = ReadField(BodySlotKey.MaximumLength);
        return result.IsEmpty ? null : new BodySlotKey(_strictUtf8.GetString(result));
    }

    internal void Complete()
    {
        if (_remainingFields != 0 || _offset != _bytes.Length)
        {
            throw new FormatException("The body frame has extra fields or trailing bytes.");
        }
    }
}
