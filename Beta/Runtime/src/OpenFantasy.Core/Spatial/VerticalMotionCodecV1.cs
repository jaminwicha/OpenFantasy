using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Spatial;

/// <summary>
/// Canonical progress bound to the complete supplied scene. Restoration replays the finite retained
/// prefix with cancellation between slices; no serialized provisional placement is trusted. Normal
/// forward advancement retains its cursor and does not repeat completed geometric work.
/// </summary>
public static class VerticalMotionCodecV1
{
    public const int MaximumEncodedByteLength = 512;

    public static byte[] Encode(VerticalMotionResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        return CanonicalEncodingV1.Frame("spatial.vertical-motion-resolution.v1", resolution.InputPin.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(resolution.CompletedWorkUnits),
            CanonicalEncodingV1.Unsigned64(resolution.IsFailed ? 2UL : resolution.IsComplete ? 1UL : 0UL));
    }

    public static VerticalMotionResolution Decode(ReadOnlySpan<byte> bytes, VerticalMotionScene scene,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 4);
            reader.Tag("spatial.vertical-motion-resolution.v1"u8);
            byte[] pin = reader.Exact(32).ToArray();
            ulong work = reader.Number();
            ulong status = reader.Number();
            reader.Complete();
            if (work > (ulong)scene.MaximumTotalWorkUnits || status > 2
                || !pin.AsSpan().SequenceEqual(scene.InputPin.ToByteArray()))
            {
                throw new FormatException("The motion cursor violates its exact scene, finite work or status pin.");
            }
            VerticalMotionResolution restored = VerticalMotionResolution.Begin(scene);
            while (restored.CompletedWorkUnits < work)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (restored.IsComplete)
                {
                    throw new FormatException("The motion cursor claims work beyond its terminal result.");
                }
                int budget = (int)Math.Min((ulong)VerticalMotionResolution.DefaultWorkBudget, work - restored.CompletedWorkUnits);
                restored = restored.Advance(budget);
            }
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The motion cursor is not its canonical reconstructed work prefix.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The motion cursor violates its retained geometric contract.", error);
        }
    }
}
