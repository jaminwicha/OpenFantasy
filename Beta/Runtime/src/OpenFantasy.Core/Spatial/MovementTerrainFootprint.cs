using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Spatial;

/// <summary>
/// Conservative terrain coverage for one bounded cardinal move, including every possible ordinary
/// stair candidate and its support probes. This is not actor/structure completeness or action admission.
/// </summary>
public sealed class MovementTerrainFootprint
{
    private MovementTerrainFootprint(ActorPresence actor, GroundSupportMode groundSupport,
        long deltaX, long deltaY, TerrainRegionBounds bounds)
    {
        Actor = actor;
        GroundSupport = groundSupport;
        DeltaX = deltaX;
        DeltaY = deltaY;
        Bounds = bounds;
    }

    public ActorPresence Actor { get; }
    public GroundSupportMode GroundSupport { get; }
    public long DeltaX { get; }
    public long DeltaY { get; }
    public TerrainRegionBounds Bounds { get; }

    public static MovementTerrainFootprint Create(ActorPresence actor, GroundSupportMode groundSupport,
        long deltaX, long deltaY)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!Enum.IsDefined(groundSupport))
        {
            throw new ArgumentOutOfRangeException(nameof(groundSupport));
        }
        if ((deltaX == 0) == (deltaY == 0)
            || deltaX < -LocalSpatialSnapshot.MaximumMoveSubunits || deltaX > LocalSpatialSnapshot.MaximumMoveSubunits
            || deltaY < -LocalSpatialSnapshot.MaximumMoveSubunits || deltaY > LocalSpatialSnapshot.MaximumMoveSubunits)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaX), "Movement requires a bounded nonzero cardinal displacement.");
        }
        var dimensions = actor.Condition.Definition.ClearanceDimensions;
        if (dimensions.WidthSubunits > LocalSpatialSnapshot.MaximumBodyAxisSubunits
            || dimensions.DepthSubunits > LocalSpatialSnapshot.MaximumBodyAxisSubunits
            || dimensions.HeightSubunits > LocalSpatialSnapshot.MaximumBodyAxisSubunits)
        {
            throw new ArgumentException("Terrain preparation requires the admitted local body-size envelope.", nameof(actor));
        }
        long step = groundSupport == GroundSupportMode.SteppedTerrainAndConstruction ? StepHeight(actor) : 0;
        BodyVolume start = BodyVolume.At(actor.Feet, dimensions);
        SubunitPosition flat = actor.Feet.Offset(deltaX, deltaY, 0);
        BodyVolume swept = start.Union(BodyVolume.At(flat.Offset(0, 0, -step), dimensions))
            .Union(BodyVolume.At(flat.Offset(0, 0, step), dimensions));
        // A descending endpoint's own support band reaches one further step below its feet.
        long supportCell = groundSupport == GroundSupportMode.TerrainOnly
            ? start.MinimumCell.Z - 1
            : CeilingCell(checked(actor.Feet.Z - 2 * step)) - 1;
        CellCoordinate minimum = swept.MinimumCell;
        CellCoordinate maximum = swept.MaximumCell;
        long minimumZ = Math.Min(minimum.Z, supportCell);
        var bounds = new TerrainRegionBounds(new(minimum.X, minimum.Y, minimumZ),
            checked((int)(maximum.X - minimum.X + 1)), checked((int)(maximum.Y - minimum.Y + 1)),
            checked((int)(maximum.Z - minimumZ + 1)));
        return new(actor, groundSupport, deltaX, deltaY, bounds);
    }

    internal static long StepHeight(ActorPresence actor) => Math.Min(LocalSpatialSnapshot.MaximumOrdinaryStepHeightSubunits,
        actor.Condition.Definition.ClearanceDimensions.HeightSubunits / 4);

    private static long CeilingCell(long subunits) => SubunitPosition.CellOf(subunits)
        + (subunits % SubunitPosition.SubunitsPerCell == 0 ? 0 : 1);
}
