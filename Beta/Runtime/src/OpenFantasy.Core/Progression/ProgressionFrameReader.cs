using System.Buffers.Binary;
using System.Text;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Progression;

public sealed class ProgressionFormatException : FormatException
{
    public ProgressionFormatException(string message) : base(message) { }
}

internal ref struct ProgressionFrameReader
{
    private readonly ReadOnlySpan<byte> _bytes;
    private int _position;
    private static readonly UTF8Encoding _utf8 = new(false, true);

    internal ProgressionFrameReader(ReadOnlySpan<byte> bytes, int maximumBytes, int maximumFields)
    {
        if (bytes.Length > maximumBytes)
        {
            throw new ProgressionFormatException("The progression frame exceeds its byte limit.");
        }
        if (bytes.Length < 12 || !bytes[..4].SequenceEqual("OFC\0"u8)
            || BinaryPrimitives.ReadUInt32BigEndian(bytes[4..]) != 1)
        {
            throw new ProgressionFormatException("The progression frame header is truncated or unsupported.");
        }
        uint count = BinaryPrimitives.ReadUInt32BigEndian(bytes[8..]);
        if (count == 0 || count > maximumFields)
        {
            throw new ProgressionFormatException("The progression frame field count is invalid.");
        }
        _bytes = bytes;
        _position = 12;
        RemainingFields = (int)count;
    }

    internal int RemainingFields { get; private set; }

    internal ReadOnlySpan<byte> Field(int maximumBytes)
    {
        if (RemainingFields == 0 || _bytes.Length - _position < 4)
        {
            throw new ProgressionFormatException("A required progression field is absent or truncated.");
        }
        uint length = BinaryPrimitives.ReadUInt32BigEndian(_bytes[_position..]);
        _position += 4;
        if (length > maximumBytes || length > _bytes.Length - _position)
        {
            throw new ProgressionFormatException("A progression field is oversized or truncated.");
        }
        ReadOnlySpan<byte> result = _bytes.Slice(_position, (int)length);
        _position += (int)length;
        RemainingFields--;
        return result;
    }

    internal void Tag(ReadOnlySpan<byte> expected)
    {
        if (!Field(64).SequenceEqual(expected))
        {
            throw new ProgressionFormatException("The progression frame type is unsupported.");
        }
    }

    internal void Version()
    {
        if (Number() != 1)
        {
            throw new ProgressionFormatException("The progression payload version is unsupported.");
        }
    }

    internal ulong Number()
    {
        ReadOnlySpan<byte> field = Field(8);
        if (field.Length != 8)
        {
            throw new ProgressionFormatException("Progression numbers require exactly eight bytes.");
        }
        return BinaryPrimitives.ReadUInt64BigEndian(field);
    }

    internal int Signed()
    {
        ReadOnlySpan<byte> field = Field(8);
        if (field.Length != 8)
        {
            throw new ProgressionFormatException("Progression modifiers require exactly eight signed bytes.");
        }
        return checked((int)BinaryPrimitives.ReadInt64BigEndian(field));
    }

    internal EntityId Identity() => EntityId.FromBytes(Field(EntityId.ByteLength));
    internal ContentHash Hash()
    {
        ReadOnlySpan<byte> field = Field(32);
        if (field.Length != 32)
        {
            throw new ProgressionFormatException("A progression definition pin requires exactly 32 bytes.");
        }
        return new ContentHash(Convert.ToHexString(field).ToLowerInvariant());
    }

    internal string Text(int maximumBytes) => _utf8.GetString(Field(maximumBytes));

    internal void Complete()
    {
        if (RemainingFields != 0 || _position != _bytes.Length)
        {
            throw new ProgressionFormatException("The progression frame has extra fields or trailing bytes.");
        }
    }
}
