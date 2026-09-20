using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Structures;

/// <summary>
/// Canonical cursor bound to complete fixed dependencies. Restoration repeats the retained prefix
/// (at most 192 geometric units plus preparation/admission), checking cancellation between slices.
/// Serialized provisional placements cannot become authority. Forward Advance does not replay work.
/// </summary>
public static class ConstructionSettlementResolutionCodecV1
{
    public const int MaximumEncodedByteLength = 512;

    public static byte[] Encode(ConstructionSettlementResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        if (resolution.IsCoupled) { throw new ArgumentException("Coupled settlement requires its versioned codec.", nameof(resolution)); }
        return CanonicalEncodingV1.Frame("construction.settlement-resolution.v1", resolution.InputPin.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(resolution.CompletedWorkUnits),
            CanonicalEncodingV1.Unsigned64(resolution.IsFailed ? 2UL : resolution.IsComplete ? 1UL : 0UL));
    }

    public static ConstructionSettlementResolution Decode(ReadOnlySpan<byte> bytes, LocalSpatialSnapshot space,
        IEnumerable<TerrainRegionSnapshot> candidateRegions, ConstructionPhysicalSnapshot physical,
        StructuralSettlementPolicy policy, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 4);
            reader.Tag("construction.settlement-resolution.v1"u8);
            byte[] inputPin = reader.Exact(32).ToArray();
            ulong work = reader.Number();
            ulong status = reader.Number();
            reader.Complete();
            if (work > ConstructionSettlementResolution.MaximumWorkUnits || status > 2)
            {
                throw new FormatException("The settlement continuation exceeds its finite work or status bounds.");
            }
            ConstructionSettlementResolution restored = ConstructionSettlementResolution.Begin(space, candidateRegions, physical, policy);
            if (!inputPin.AsSpan().SequenceEqual(restored.InputPin.ToByteArray()))
            {
                throw new FormatException("The settlement continuation belongs to different exact retained dependencies.");
            }
            while (restored.CompletedWorkUnits < work)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (restored.IsComplete)
                {
                    throw new FormatException("The settlement continuation claims work beyond its terminal result.");
                }
                int budget = (int)Math.Min((ulong)ConstructionSettlementResolution.DefaultWorkBudget, work - restored.CompletedWorkUnits);
                restored = restored.Advance(budget);
            }
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The settlement continuation is not its canonical reconstructed work prefix.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The settlement continuation violates its retained work contract.", error);
        }
    }
}
