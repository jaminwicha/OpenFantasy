namespace OpenFantasy.Core.Spatial;

public sealed partial class LocalSpatialSnapshot
{
    public const long MaximumOrdinaryStepHeightSubunits = 64;

    private static long StepHeight(ActorPresence actor) => MovementTerrainFootprint.StepHeight(actor);

    internal SpatialMoveResult PrepareWalkFrom(EntityId actorId, SubunitPosition from, long deltaX, long deltaY) =>
        _movement.PrepareWalkFrom(actorId, from, deltaX, deltaY);

    internal IEnumerable<SubunitPosition> CandidateWalkingOrigins(EntityId actorId, SubunitPosition destination,
        long deltaX, long deltaY, out bool missingTerrain) =>
        _movement.CandidateWalkingOrigins(actorId, destination, deltaX, deltaY, out missingTerrain);
}
