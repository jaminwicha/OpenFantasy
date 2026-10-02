using System.Buffers;

namespace OpenFantasy.Hosting.Networking;

/// <summary>A fixed bounded writer; reservation space is never accepted as output beyond the 16 KiB body cap.</summary>
internal sealed class PlayerQueryJsonBuffer : IBufferWriter<byte>
{
    private const int ReservationBytes = 32 * 1024;
    private readonly byte[] _bytes = new byte[PlayerQueryResultCodec.MaximumBodyBytes + ReservationBytes];
    private int _written;

    internal ReadOnlySpan<byte> WrittenSpan => _bytes.AsSpan(0, _written);

    public void Advance(int count)
    {
        if (count < 0 || count > PlayerQueryResultCodec.MaximumBodyBytes - _written)
            throw new FormatException("Query result exceeds its byte bound.");
        _written += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        ValidateReservation(sizeHint);
        return _bytes.AsMemory(_written);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        ValidateReservation(sizeHint);
        return _bytes.AsSpan(_written);
    }

    private void ValidateReservation(int sizeHint)
    {
        if (sizeHint < 0 || sizeHint > _bytes.Length - _written)
            throw new FormatException("Query result writer exceeds its bounded reservation.");
    }
}
