using System.Buffers.Binary;

namespace OpenFantasy.Core.Authority;

internal ref struct AuthorityFrameReader
{
    private const int HeaderByteLength = 12;
    private readonly ReadOnlySpan<byte> _bytes;
    private int _offset;
    private uint _remainingFields;

    internal AuthorityFrameReader(ReadOnlySpan<byte> bytes, int maximumBytes, uint maximumFields)
    {
        if (bytes.Length > maximumBytes)
        {
            throw Failure(AuthorityFormatFailure.LimitExceeded, "Authority frame exceeds its byte limit.");
        }
        if (bytes.Length < HeaderByteLength)
        {
            throw Failure(AuthorityFormatFailure.Truncated, "Authority frame header is incomplete.");
        }
        if (!bytes[..4].SequenceEqual("OFC\0"u8))
        {
            throw Failure(AuthorityFormatFailure.InvalidFrame, "Authority frame magic is invalid.");
        }
        if (BinaryPrimitives.ReadUInt32BigEndian(bytes[4..]) != 1)
        {
            throw Failure(AuthorityFormatFailure.UnsupportedVersion, "Canonical framing version is unsupported.");
        }

        _remainingFields = BinaryPrimitives.ReadUInt32BigEndian(bytes[8..]);
        if (_remainingFields == 0 || _remainingFields > maximumFields)
        {
            throw Failure(AuthorityFormatFailure.LimitExceeded, "Authority field count is outside its limit.");
        }

        _bytes = bytes;
        _offset = HeaderByteLength;
    }

    internal readonly uint RemainingFields => _remainingFields;

    internal ReadOnlySpan<byte> ReadField(int maximumBytes)
    {
        if (_remainingFields == 0 || _bytes.Length - _offset < sizeof(uint))
        {
            throw Failure(AuthorityFormatFailure.Truncated, "A required authority field is missing.");
        }

        uint length = BinaryPrimitives.ReadUInt32BigEndian(_bytes[_offset..]);
        _offset += sizeof(uint);
        if (length > maximumBytes)
        {
            throw Failure(AuthorityFormatFailure.LimitExceeded, "Authority field exceeds its byte limit.");
        }
        if (length > _bytes.Length - _offset)
        {
            throw Failure(AuthorityFormatFailure.Truncated, "Authority field content is incomplete.");
        }

        ReadOnlySpan<byte> result = _bytes.Slice(_offset, (int)length);
        _offset += (int)length;
        _remainingFields--;
        return result;
    }

    internal void ExpectTag(ReadOnlySpan<byte> tag)
    {
        if (!ReadField(64).SequenceEqual(tag))
        {
            throw Failure(AuthorityFormatFailure.UnknownType, "Authority frame type is unsupported.");
        }
    }

    internal EntityId ReadIdentity()
    {
        ReadOnlySpan<byte> bytes = ReadField(EntityId.ByteLength);
        if (bytes.Length != EntityId.ByteLength)
        {
            throw Failure(AuthorityFormatFailure.InvalidValue, "Authority identity requires sixteen bytes.");
        }

        return EntityId.FromBytes(bytes);
    }

    internal ulong ReadUnsigned64()
    {
        ReadOnlySpan<byte> bytes = ReadField(sizeof(ulong));
        if (bytes.Length != sizeof(ulong))
        {
            throw Failure(AuthorityFormatFailure.InvalidValue, "Authority integer requires eight bytes.");
        }

        return BinaryPrimitives.ReadUInt64BigEndian(bytes);
    }

    internal void Complete()
    {
        if (_remainingFields != 0)
        {
            throw Failure(AuthorityFormatFailure.NonCanonical, "Authority frame has unexpected fields.");
        }
        if (_offset != _bytes.Length)
        {
            throw Failure(AuthorityFormatFailure.TrailingData, "Authority frame has trailing bytes.");
        }
    }

    internal static AuthorityFormatException Failure(AuthorityFormatFailure failure, string message) => new(failure, message);
}
