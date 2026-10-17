namespace OpenFantasy.Core.Spatial;

public sealed partial class LocalSpatialSnapshot
{
    public const long MaximumOrdinaryStepHeightSubunits = 64;

    private static long StepHeight(ActorPresence actor) => MovementTerrainFootprint.StepHeight(actor);

    /// <summary>Shared exact transition for ordinary movement and search; the selected endpoint is derived, never snapped.</summary>
    internal SpatialMoveResult PrepareWalkFrom(EntityId actorId, SubunitPosition from, long deltaX, long deltaY)
    {
        ActorPresence actor = GetActor(actorId);
        try
        {
            long stepHeight = StepHeight(actor);
            SubunitPosition flat = from.Offset(deltaX, deltaY, 0);
            var dimensions = actor.Condition.Definition.ClearanceDimensions;
            BodyVolume start = BodyVolume.At(from, dimensions);
            SpatialOccupancy startStatus = QueryVolume(actorId, start);
            if (startStatus != SpatialOccupancy.Clear)
            {
                return new(WalkStatus(startStatus));
            }
            IEnumerable<long> heights = CandidateWalkingHeights(actor, flat, out bool missing);
            SpatialOccupancy failure = SpatialOccupancy.Unsupported;
            foreach (long height in heights)
            {
                var destination = new SubunitPosition(flat.X, flat.Y, height);
                BodyVolume end = BodyVolume.At(destination, dimensions);
                SpatialOccupancy endpoint = QueryVolume(actorId, end);
                if (endpoint != SpatialOccupancy.Clear)
                {
                    missing |= endpoint == SpatialOccupancy.Unavailable;
                    failure = endpoint;
                    continue;
                }
                long upper = Math.Max(from.Z, height);
                SubunitPosition corner = height > from.Z ? new(from.X, from.Y, height) : new(flat.X, flat.Y, from.Z);
                BodyVolume bend = BodyVolume.At(corner, dimensions);
                SpatialOccupancy first = QueryClearance(actorId, start.Union(bend));
                SpatialOccupancy second = QueryClearance(actorId, bend.Union(end));
                SpatialOccupancy corridor = FootingQuery.QueryBand(_terrain, Structures, start.Union(end),
                    checked(upper - stepHeight), upper);
                SpatialOccupancy blocked = new[] { first, second, corridor }.FirstOrDefault(value => value != SpatialOccupancy.Clear);
                if (blocked == SpatialOccupancy.Clear)
                {
                    return new(SpatialMoveStatus.Prepared, actor.At(destination));
                }
                missing |= blocked == SpatialOccupancy.Unavailable;
                failure = blocked;
            }
            return new(missing ? SpatialMoveStatus.Unavailable : WalkStatus(failure));
        }
        catch (OverflowException)
        {
            return new(SpatialMoveStatus.NumericLimit);
        }
    }

    /// <summary>Possible sources of a directed edge; the caller must replay the forward movement to qualify each one.</summary>
    internal IEnumerable<SubunitPosition> CandidateWalkingOrigins(EntityId actorId, SubunitPosition destination,
        long deltaX, long deltaY, out bool missingTerrain)
    {
        SubunitPosition flat = destination.Offset(checked(-deltaX), checked(-deltaY), 0);
        return CandidateWalkingHeights(GetActor(actorId), flat, out missingTerrain)
            .Select(height => new SubunitPosition(flat.X, flat.Y, height));
    }

    private IEnumerable<long> CandidateWalkingHeights(ActorPresence actor, SubunitPosition flat, out bool missingTerrain)
    {
        long stepHeight = StepHeight(actor);
        SortedSet<long> heights = FootingQuery.CandidateHeights(_terrain, Structures,
            BodyVolume.At(flat, actor.Condition.Definition.ClearanceDimensions),
            checked(flat.Z - stepHeight), checked(flat.Z + stepHeight), out missingTerrain);
        // The current height remains the preferred candidate; enumeration separately reports missing terrain.
        heights.Add(flat.Z);
        return heights.OrderBy(value => Int128.Abs((Int128)value - flat.Z)).ThenBy(value => value);
    }

    private static SpatialMoveStatus WalkStatus(SpatialOccupancy occupancy) => occupancy switch
    {
        SpatialOccupancy.Clear => SpatialMoveStatus.Prepared,
        SpatialOccupancy.BlockedTerrain => SpatialMoveStatus.BlockedTerrain,
        SpatialOccupancy.BlockedActor => SpatialMoveStatus.BlockedActor,
        SpatialOccupancy.BlockedStructure => SpatialMoveStatus.BlockedStructure,
        SpatialOccupancy.Unsupported => SpatialMoveStatus.Unsupported,
        _ => SpatialMoveStatus.Unavailable
    };
}
