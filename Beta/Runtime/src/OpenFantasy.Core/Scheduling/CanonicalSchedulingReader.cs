using System.Buffers.Binary;

namespace OpenFantasy.Core.Scheduling;

/// <summary>A bounded reader for the existing canonical frame, with no transport or authority state.</summary>
internal ref struct CanonicalSchedulingReader
{
    private const int HeaderByteLength = 12;
    private readonly ReadOnlySpan<byte> _bytes;
    private int _offset;
    private uint _remainingFields;

    internal CanonicalSchedulingReader(ReadOnlySpan<byte> bytes, int maximumBytes, uint maximumFields)
    {
        if (bytes.Length > maximumBytes)
        {
            throw Failure(SchedulingFormatFailure.LimitExceeded, "The frame exceeds its byte limit.");
        }

        if (bytes.Length < HeaderByteLength)
        {
            throw Failure(SchedulingFormatFailure.Truncated, "The frame header is incomplete.");
        }

        if (!bytes[..4].SequenceEqual("OFC\0"u8))
        {
            throw Failure(SchedulingFormatFailure.InvalidFrame, "The frame magic is invalid.");
        }

        if (BinaryPrimitives.ReadUInt32BigEndian(bytes[4..]) != 1)
        {
            throw Failure(SchedulingFormatFailure.UnsupportedVersion, "The canonical encoding version is unsupported.");
        }

        _remainingFields = BinaryPrimitives.ReadUInt32BigEndian(bytes[8..]);
        if (_remainingFields == 0 || _remainingFields > maximumFields)
        {
            throw Failure(SchedulingFormatFailure.LimitExceeded, "The frame field count is outside its limit.");
        }

        _bytes = bytes;
        _offset = HeaderByteLength;
    }

    internal readonly uint RemainingFields => _remainingFields;

    internal ReadOnlySpan<byte> ReadField(int maximumLength)
    {
        if (_remainingFields == 0)
        {
            throw Failure(SchedulingFormatFailure.Truncated, "A required field is absent.");
        }

        if (_bytes.Length - _offset < sizeof(uint))
        {
            throw Failure(SchedulingFormatFailure.Truncated, "The field length is incomplete.");
        }

        uint length = BinaryPrimitives.ReadUInt32BigEndian(_bytes[_offset..]);
        _offset += sizeof(uint);
        if (length > maximumLength)
        {
            throw Failure(SchedulingFormatFailure.LimitExceeded, "The field exceeds its byte limit.");
        }

        if (length > _bytes.Length - _offset)
        {
            throw Failure(SchedulingFormatFailure.Truncated, "The field content is incomplete.");
        }

        ReadOnlySpan<byte> field = _bytes.Slice(_offset, (int)length);
        _offset += (int)length;
        _remainingFields--;
        return field;
    }

    internal void ExpectTag(ReadOnlySpan<byte> expectedTag)
    {
        if (!ReadField(64).SequenceEqual(expectedTag))
        {
            throw Failure(SchedulingFormatFailure.UnknownType, "The frame tag is unsupported.");
        }
    }

    internal ulong ReadUnsigned64()
    {
        ReadOnlySpan<byte> bytes = ReadField(sizeof(ulong));
        if (bytes.Length != sizeof(ulong))
        {
            throw Failure(SchedulingFormatFailure.InvalidValue, "An integer must contain exactly eight bytes.");
        }

        return BinaryPrimitives.ReadUInt64BigEndian(bytes);
    }

    internal long ReadSigned64()
    {
        ReadOnlySpan<byte> bytes = ReadField(sizeof(long));
        if (bytes.Length != sizeof(long))
        {
            throw Failure(SchedulingFormatFailure.InvalidValue, "An integer must contain exactly eight bytes.");
        }
        return BinaryPrimitives.ReadInt64BigEndian(bytes);
    }

    internal void ExpectVersion(ulong expectedVersion)
    {
        if (ReadUnsigned64() != expectedVersion)
        {
            throw Failure(SchedulingFormatFailure.UnsupportedVersion, "The contract version is unsupported.");
        }
    }

    internal EntityId ReadIdentity()
    {
        ReadOnlySpan<byte> bytes = ReadField(EntityId.ByteLength);
        if (bytes.Length != EntityId.ByteLength)
        {
            throw Failure(SchedulingFormatFailure.InvalidValue, "An identity must contain exactly sixteen bytes.");
        }

        return EntityId.FromBytes(bytes);
    }

    internal void Complete()
    {
        if (_remainingFields != 0)
        {
            throw Failure(SchedulingFormatFailure.NonCanonical, "The frame contains unexpected fields.");
        }

        if (_offset != _bytes.Length)
        {
            throw Failure(SchedulingFormatFailure.TrailingData, "Bytes remain after the frame's last field.");
        }
    }

    internal static SchedulingFormatException Failure(SchedulingFormatFailure failure, string message) =>
        new(failure, message);
}
