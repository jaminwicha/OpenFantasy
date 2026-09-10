using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Construction;

/// <summary>Exact local material geometry; one cubic millimetre is one microlitre.</summary>
public sealed record MillimetreBox
{
    public const long MaximumCoordinateMagnitude = 4_000;

    public MillimetreBox(long minX, long minY, long minZ, long maxX, long maxY, long maxZ)
    {
        if (new[] { minX, minY, minZ, maxX, maxY, maxZ }.Any(value =>
                value < -MaximumCoordinateMagnitude || value > MaximumCoordinateMagnitude)
            || minX >= maxX || minY >= maxY || minZ < 0 || minZ >= maxZ)
        {
            throw new ArgumentException("A member requires positive bounded dimensions above its local ground plane.");
        }
        MinX = minX;
        MinY = minY;
        MinZ = minZ;
        MaxX = maxX;
        MaxY = maxY;
        MaxZ = maxZ;
    }

    public long MinX { get; }
    public long MinY { get; }
    public long MinZ { get; }
    public long MaxX { get; }
    public long MaxY { get; }
    public long MaxZ { get; }
    public ulong VolumeMicrolitres => checked((ulong)(MaxX - MinX) * (ulong)(MaxY - MinY) * (ulong)(MaxZ - MinZ));

    internal bool Overlaps(MillimetreBox other) => MinX < other.MaxX && MaxX > other.MinX
        && MinY < other.MaxY && MaxY > other.MinY && MinZ < other.MaxZ && MaxZ > other.MinZ;

    internal bool SharesFace(MillimetreBox other) =>
        (MaxX == other.MinX || MinX == other.MaxX) && MinY < other.MaxY && MaxY > other.MinY && MinZ < other.MaxZ && MaxZ > other.MinZ
        || (MaxY == other.MinY || MinY == other.MaxY) && MinX < other.MaxX && MaxX > other.MinX && MinZ < other.MaxZ && MaxZ > other.MinZ
        || (MaxZ == other.MinZ || MinZ == other.MaxZ) && MinX < other.MaxX && MaxX > other.MinX && MinY < other.MaxY && MaxY > other.MinY;

    /// <summary>Conservative collision envelopes; their quantized volume is not a material quantity.</summary>
    public SubunitBox At(SubunitPosition origin) => new(
        new SubunitPosition(Translate(origin.X, MinX, false), Translate(origin.Y, MinY, false), Translate(origin.Z, MinZ, false)),
        new SubunitPosition(Translate(origin.X, MaxX, true), Translate(origin.Y, MaxY, true), Translate(origin.Z, MaxZ, true)));

    private static long Translate(long origin, long millimetres, bool roundUp)
    {
        Int128 scaled = (Int128)millimetres * SubunitPosition.SubunitsPerCell;
        Int128 whole = scaled / 1_000;
        if (roundUp && scaled % 1_000 > 0)
        {
            whole++;
        }
        else if (!roundUp && scaled % 1_000 < 0)
        {
            whole--;
        }
        return checked((long)((Int128)origin + whole));
    }
}
