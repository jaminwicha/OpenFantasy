using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Structures;

/// <summary>
/// Event-ordered cursor bound to complete retained dependencies. Restoration repeats the bounded real
/// work prefix with cancellation between slices; cached placements are never accepted as authority.
/// The event scene determines its own finite work limit, independently of the common-trajectory bound.
/// </summary>
public static class ConstructionSettlementResolutionCodecV3
{
    public const int MaximumEncodedByteLength = 512;

    public static byte[] Encode(ConstructionSettlementResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        if (!resolution.IsEventOrdered)
        {
            throw new ArgumentException("This codec requires an event-ordered settlement cursor.", nameof(resolution));
        }
        return CanonicalEncodingV1.Frame("construction.settlement-resolution.v3", resolution.InputPin.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(resolution.CompletedWorkUnits),
            CanonicalEncodingV1.Unsigned64(resolution.IsFailed ? 2UL : resolution.IsComplete ? 1UL : 0UL));
    }

    public static ConstructionSettlementResolution Decode(ReadOnlySpan<byte> bytes, WorldSnapshot basis,
        IEnumerable<TerrainRegionSnapshot> candidateRegions, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 4);
            reader.Tag("construction.settlement-resolution.v3"u8);
            byte[] inputPin = reader.Exact(32).ToArray();
            ulong work = reader.Number();
            ulong status = reader.Number();
            reader.Complete();
            if (basis.DescentMode != ConstructionDescentMode.EventOrdered || basis.ProfileVersion != 18 || status > 2)
            {
                throw new FormatException("The event continuation requires its owned world interpretation and valid status.");
            }
            ConstructionSettlementResolution restored = ConstructionSettlementResolution.BeginEventOrdered(basis, candidateRegions);
            if (work > (ulong)restored.MaximumTotalWorkUnits
                || !inputPin.AsSpan().SequenceEqual(restored.InputPin.ToByteArray()))
            {
                throw new FormatException("The event continuation violates its exact dependency pin or actual scene work bound.");
            }
            while (restored.CompletedWorkUnits < work)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (restored.IsComplete)
                {
                    throw new FormatException("The event continuation claims work beyond its terminal result.");
                }
                int budget = (int)Math.Min((ulong)ConstructionSettlementResolution.DefaultWorkBudget, work - restored.CompletedWorkUnits);
                restored = restored.Advance(budget);
            }
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The event continuation is not its canonical reconstructed work prefix.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The event continuation violates its retained geometric contract.", error);
        }
    }
}
