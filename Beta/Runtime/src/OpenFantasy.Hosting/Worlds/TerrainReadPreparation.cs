using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Loads a bounded set of exact terrain archives without owning or publishing world state.</summary>
internal sealed class TerrainReadPreparation
{
    private readonly Func<TerrainRegionReference, int, byte[]?> _read;

    internal TerrainReadPreparation(Func<TerrainRegionReference, int, byte[]?> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        _read = read;
    }

    internal TerrainReadSet Load(TerrainReadPlan plan, int maximumEncodedBytes, int maximumDecodedCells)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (maximumEncodedBytes < 1) throw new ArgumentOutOfRangeException(nameof(maximumEncodedBytes));
        if (maximumDecodedCells < 1) throw new ArgumentOutOfRangeException(nameof(maximumDecodedCells));
        if (plan.TotalLoadedCells > maximumDecodedCells)
            throw new InvalidOperationException("Terrain dependencies exceed the declared decoded-cell budget.");
        int remainingBytes = maximumEncodedBytes;
        var regions = new List<TerrainRegionSnapshot>();
        foreach (TerrainRegionReference reference in plan.References)
        {
            if (remainingBytes == 0)
                throw new InvalidDataException("The declared terrain read budget is exhausted.");
            int limit = Math.Min(remainingBytes, TerrainRegionCodecV1.MaximumEncodedByteLength);
            byte[] bytes = _read(reference, limit)
                ?? throw new InvalidDataException("A required terrain archive is unavailable.");
            if (bytes.Length > limit)
                throw new InvalidDataException("Terrain archive bytes exceed their declared read budget.");
            remainingBytes -= bytes.Length;
            try
            {
                regions.Add(TerrainRegionCodecV1.Decode(bytes, reference));
            }
            catch (FormatException error)
            {
                throw new InvalidDataException("Terrain archive data does not match its exact dependency.", error);
            }
        }
        return plan.Resolve(regions);
    }
}
