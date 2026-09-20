using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Structures;

/// <summary>Reconstructs geometric work against a complete trusted world; no provisional root is trusted.</summary>
public static class ConstructionDescentAssessmentCodecV1
{
    public const int MaximumEncodedByteLength = 512;

    public static byte[] Encode(ConstructionDescentAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        return CanonicalEncodingV1.Frame("construction.descent-cursor.v1", assessment.InputPin.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(assessment.CompletedWorkUnits),
            CanonicalEncodingV1.Unsigned64(assessment.IsFailed ? 2UL : assessment.IsComplete ? 1UL : 0UL));
    }

    public static ConstructionDescentAssessment Decode(ReadOnlySpan<byte> bytes, WorldSnapshot basis,
        IEnumerable<TerrainRegionSnapshot> candidateRegions, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 4);
            reader.Tag("construction.descent-cursor.v1"u8);
            byte[] pin = reader.Exact(32).ToArray();
            ulong work = reader.Number();
            ulong status = reader.Number();
            reader.Complete();
            ConstructionDescentAssessment restored = ConstructionDescentAssessment.Begin(basis, candidateRegions);
            if (work > (ulong)restored.MaximumTotalWorkUnits || status > 2
                || !pin.AsSpan().SequenceEqual(restored.InputPin.ToByteArray()))
            {
                throw new FormatException("The descent cursor exceeds its work bounds or belongs to different complete dependencies.");
            }
            while (restored.CompletedWorkUnits < work)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (restored.IsComplete)
                {
                    throw new FormatException("The descent cursor claims work beyond its terminal assessment.");
                }
                restored = restored.Advance((int)Math.Min(16UL, work - restored.CompletedWorkUnits));
            }
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The descent cursor is not its exact canonical work prefix.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The descent cursor violates the retained world assessment contract.", error);
        }
    }
}
