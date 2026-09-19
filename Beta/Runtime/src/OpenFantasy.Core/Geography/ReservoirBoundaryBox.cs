using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>Exact 1/256 millimetre coordinates; cell, spatial and authored-member scales convert without rounding.</summary>
internal sealed record ReservoirBoundaryBox(Int128 MinX, Int128 MinY, Int128 MinZ, Int128 MaxX, Int128 MaxY, Int128 MaxZ)
{
    internal const long TicksPerCell = 256_000;
    internal const long TicksPerMillimetre = 256;
    internal const long TicksPerSpatialSubunit = 1_000;

    internal static ReservoirBoundaryBox FromCell(CellCoordinate cell) => new(
        (Int128)cell.X * TicksPerCell, (Int128)cell.Y * TicksPerCell, (Int128)cell.Z * TicksPerCell,
        ((Int128)cell.X + 1) * TicksPerCell, ((Int128)cell.Y + 1) * TicksPerCell, ((Int128)cell.Z + 1) * TicksPerCell);

    internal static ReservoirBoundaryBox FromMember(SubunitPosition origin, MillimetreBox member) => new(
        Translate(origin.X, member.MinX), Translate(origin.Y, member.MinY), Translate(origin.Z, member.MinZ),
        Translate(origin.X, member.MaxX), Translate(origin.Y, member.MaxY), Translate(origin.Z, member.MaxZ));

    internal static ReservoirBoundaryBox FromBody(ActorPresence actor)
    {
        var body = BodyVolume.At(actor.Feet, actor.Condition.Definition.ClearanceDimensions);
        return new((Int128)body.MinimumX * TicksPerSpatialSubunit, (Int128)body.MinimumY * TicksPerSpatialSubunit,
            (Int128)body.MinimumZ * TicksPerSpatialSubunit, (Int128)body.MaximumX * TicksPerSpatialSubunit,
            (Int128)body.MaximumY * TicksPerSpatialSubunit, (Int128)body.MaximumZ * TicksPerSpatialSubunit);
    }

    internal bool Overlaps(ReservoirBoundaryBox other) => MinX < other.MaxX && MaxX > other.MinX
        && MinY < other.MaxY && MaxY > other.MinY && MinZ < other.MaxZ && MaxZ > other.MinZ;

    internal BoundaryRectangle Projection(ReservoirBoundarySurface surface) => surface switch
    {
        ReservoirBoundarySurface.Floor => new(MinX, MinY, MaxX, MaxY),
        ReservoirBoundarySurface.West or ReservoirBoundarySurface.East => new(MinY, MinZ, MaxY, MaxZ),
        ReservoirBoundarySurface.North or ReservoirBoundarySurface.South => new(MinX, MinZ, MaxX, MaxZ),
        _ => throw new ArgumentException("Only physical boundary surfaces have a tangential projection.")
    };

    internal bool Supports(ReservoirBoundaryBox interior, ReservoirBoundarySurface surface, uint minimumMillimetres)
    {
        Int128 thickness = (Int128)minimumMillimetres * TicksPerMillimetre;
        return surface switch
        {
            ReservoirBoundarySurface.Floor => MinZ <= interior.MinZ - thickness && MaxZ >= interior.MinZ,
            ReservoirBoundarySurface.West => MinX <= interior.MinX - thickness && MaxX >= interior.MinX,
            ReservoirBoundarySurface.East => MinX <= interior.MaxX && MaxX >= interior.MaxX + thickness,
            ReservoirBoundarySurface.North => MinY <= interior.MinY - thickness && MaxY >= interior.MinY,
            ReservoirBoundarySurface.South => MinY <= interior.MaxY && MaxY >= interior.MaxY + thickness,
            _ => false
        };
    }

    private static Int128 Translate(long origin, long millimetres) =>
        (Int128)origin * TicksPerSpatialSubunit + (Int128)millimetres * TicksPerMillimetre;
}

internal sealed record ReservoirBoundaryMember(CountedItemDefinition Material, ReservoirBoundaryBox Box);
