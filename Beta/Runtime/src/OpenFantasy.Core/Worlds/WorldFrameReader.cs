using System.Buffers.Binary;
using System.Text;

namespace OpenFantasy.Core.Worlds;

/// <summary>Bounds-first reader for the composed-world formats; constructors validate semantic invariants.</summary>
internal ref struct WorldFrameReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly ReadOnlySpan<byte> _bytes;
    private int _offset;
    private uint _remaining;

    internal WorldFrameReader(ReadOnlySpan<byte> bytes, int maximumBytes, int maximumFields)
    {
        if (bytes.Length < 12 || bytes.Length > maximumBytes || !bytes[..4].SequenceEqual("OFC\0"u8)
            || BinaryPrimitives.ReadUInt32BigEndian(bytes[4..]) != 1)
        {
            throw new FormatException("Invalid world frame header or byte bound.");
        }
        _bytes = bytes;
        _offset = 12;
        _remaining = BinaryPrimitives.ReadUInt32BigEndian(bytes[8..]);
        if (_remaining == 0 || _remaining > maximumFields || _remaining > (bytes.Length - 12) / 4)
        {
            throw new FormatException("Invalid world frame field count.");
        }
    }

    internal uint Remaining => _remaining;

    internal ReadOnlySpan<byte> Field(int maximum)
    {
        if (_remaining == 0 || _bytes.Length - _offset < 4)
        {
            throw new FormatException("A world frame is truncated.");
        }
        uint length = BinaryPrimitives.ReadUInt32BigEndian(_bytes[_offset..]);
        _offset += 4;
        if (length > maximum || length > _bytes.Length - _offset)
        {
            throw new FormatException("A world field is truncated or excessive.");
        }
        ReadOnlySpan<byte> result = _bytes.Slice(_offset, checked((int)length));
        _offset += checked((int)length);
        _remaining--;
        return result;
    }

    internal void Tag(ReadOnlySpan<byte> expected)
    {
        if (!Field(expected.Length).SequenceEqual(expected))
        {
            throw new FormatException("Unexpected world frame tag.");
        }
    }

    internal EntityId Identity() => EntityId.FromBytes(Exact(16));
    internal ulong Number() => BinaryPrimitives.ReadUInt64BigEndian(Exact(8));
    internal long Signed() => BinaryPrimitives.ReadInt64BigEndian(Exact(8));
    internal ReadOnlySpan<byte> Exact(int length)
    {
        ReadOnlySpan<byte> value = Field(length);
        if (value.Length != length)
        {
            throw new FormatException("A world field has the wrong fixed length.");
        }
        return value;
    }

    internal string Text(int maximumBytes)
    {
        try
        {
            return StrictUtf8.GetString(Field(maximumBytes));
        }
        catch (DecoderFallbackException error)
        {
            throw new FormatException("A world text field is not strict UTF-8.", error);
        }
    }

    internal void Version()
    {
        if (Number() != 1)
        {
            throw new FormatException("Unsupported world format version.");
        }
    }

    internal void Complete()
    {
        if (_remaining != 0 || _offset != _bytes.Length)
        {
            throw new FormatException("A world frame has trailing fields or bytes.");
        }
    }
}
