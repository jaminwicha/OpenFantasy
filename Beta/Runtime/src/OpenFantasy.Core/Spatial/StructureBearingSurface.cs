using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Geography;

namespace OpenFantasy.Core.Spatial;

/// <summary>Exact physical member top in 1/256 millimetre ticks, with an outward-quantized standing plane.</summary>
public sealed record StructureBearingSurface
{
    public const long TicksPerSubunit = 1000;
    public const long TicksPerMillimetre = 256;

    public StructureBearingSurface(BoundaryRectangle bounds, Int128 topTicks)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        Bounds = bounds;
        TopTicks = topTicks;
        Int128 quotient = topTicks / TicksPerSubunit;
        StandingZ = checked((long)(quotient + (topTicks % TicksPerSubunit > 0 ? 1 : 0)));
    }

    public BoundaryRectangle Bounds { get; }
    public Int128 TopTicks { get; }
    public long StandingZ { get; }

    internal static StructureBearingSurface FromMember(MillimetreBox member, SubunitPosition origin) => new(
        new BoundaryRectangle((Int128)origin.X * TicksPerSubunit + (Int128)member.MinX * TicksPerMillimetre,
            (Int128)origin.Y * TicksPerSubunit + (Int128)member.MinY * TicksPerMillimetre,
            (Int128)origin.X * TicksPerSubunit + (Int128)member.MaxX * TicksPerMillimetre,
            (Int128)origin.Y * TicksPerSubunit + (Int128)member.MaxY * TicksPerMillimetre),
        (Int128)origin.Z * TicksPerSubunit + (Int128)member.MaxZ * TicksPerMillimetre);

    internal bool Fits(SubunitBox box) => StandingZ == box.Maximum.Z
        && TopTicks > (Int128)box.Minimum.Z * TicksPerSubunit
        && Bounds.MinU >= (Int128)box.Minimum.X * TicksPerSubunit
        && Bounds.MinV >= (Int128)box.Minimum.Y * TicksPerSubunit
        && Bounds.MaxU <= (Int128)box.Maximum.X * TicksPerSubunit
        && Bounds.MaxV <= (Int128)box.Maximum.Y * TicksPerSubunit;
}
