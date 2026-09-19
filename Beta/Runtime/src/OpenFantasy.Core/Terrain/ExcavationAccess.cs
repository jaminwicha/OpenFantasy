using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Gathering;

namespace OpenFantasy.Core.Terrain;

internal static class ExcavationAccess
{
    internal static bool CanReach(LocalSpatialSnapshot space, EntityId actorId, ExcavationSite site)
    {
        if (ResourceInteractionQueries.Query(space, actorId, site.Stance) != SpatialInteraction.Reachable)
        {
            return false;
        }
        // The endpoint is one subunit outside the solid target face. The target is never removed for sight tests.
        Int128 dx = (Int128)site.Cell.X - site.Stance.Cell.X;
        Int128 dy = (Int128)site.Cell.Y - site.Stance.Cell.Y;
        var start = ((Int128)site.Stance.X * 2, (Int128)site.Stance.Y * 2, (Int128)site.Stance.Z * 2 + 256);
        var face = (start.Item1 + dx * 254, start.Item2 + dy * 254, start.Item3);
        var actor = space.GetActor(actorId);
        var actorCenter = BodyVolume.At(actor.Feet, actor.Condition.Definition.ClearanceDimensions).DoubledCenter;
        return TerrainSightline.QueryCenters(new TerrainVolumeQueries(space.Regions), actorCenter, face, GatherRulesV1.ReachSubunits, space.Structures)
            == SpatialInteraction.Reachable;
    }
}
