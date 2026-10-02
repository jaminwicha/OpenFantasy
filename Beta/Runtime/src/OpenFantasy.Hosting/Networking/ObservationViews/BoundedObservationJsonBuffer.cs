using System.Buffers;

namespace OpenFantasy.Hosting.Networking.ObservationViews;

/// <summary>
/// Bounds the serializer's destination while it writes. A small, fixed reservation margin accommodates the JSON
/// writer's requested working span; Advance still refuses even one output byte beyond the expanded JSON limit.
/// </summary>
internal sealed class BoundedObservationJsonBuffer : IBufferWriter<byte>, IDisposable
{
    internal const int MaximumReservationBytes = 128 * 1024;
    private byte[] _buffer = new byte[4096];
    private int _written;

    internal ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, _written);

    public void Advance(int count)
    {
        if (count < 0 || count > _buffer.Length - _written
            || count > PlayerViewCodec.MaximumExpandedJsonBytes - _written)
        {
            throw new FormatException("Observation JSON exceeds its expanded byte bound.");
        }
        _written += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsMemory(_written);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        EnsureCapacity(sizeHint);
        return _buffer.AsSpan(_written);
    }

    private void EnsureCapacity(int sizeHint)
    {
        if (sizeHint < 0) throw new ArgumentOutOfRangeException(nameof(sizeHint));
        int required = checked(_written + Math.Max(sizeHint, 1));
        const int maximumCapacity = PlayerViewCodec.MaximumExpandedJsonBytes + MaximumReservationBytes;
        if (required > maximumCapacity)
        {
            throw new FormatException("Observation JSON writer reservation exceeds its bounded capacity.");
        }
        if (required > _buffer.Length)
        {
            int capacity = Math.Min(maximumCapacity, Math.Max(required, checked(_buffer.Length * 2)));
            Array.Resize(ref _buffer, capacity);
        }
    }

    public void Dispose()
    {
        _buffer = [];
        _written = 0;
    }
}
