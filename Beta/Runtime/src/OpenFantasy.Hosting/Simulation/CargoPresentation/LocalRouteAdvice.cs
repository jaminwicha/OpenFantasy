using OpenFantasy.Core;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Simulation.CargoPresentation;

/// <summary>
/// Read-only travel advice for a walker that steers with ordinary movement input: the bounded ground route from the
/// player's actual feet to one target, planned with the unchanged world search. It moves nothing, submits nothing and
/// is never saved; every step the walker takes is still an ordinary movement sample the world validates. A route that
/// cannot be planned returns null with the search status, which is not proof that no route exists.
/// </summary>
public sealed class LocalRouteAdvice
{
    private readonly SqliteWorldAuthority _authority;
    private readonly EntityId _playerActorId;

    public LocalRouteAdvice(SqliteWorldAuthority authority, EntityId playerActorId)
    {
        ArgumentNullException.ThrowIfNull(authority);
        _authority = authority;
        _playerActorId = playerActorId;
    }

    /// <summary>The player's current public feet in the live head.</summary>
    public SubunitPosition PlayerFeet => _authority.ReadStateForHost().Space.GetActor(_playerActorId).Feet;

    /// <summary>Route waypoints from the actual feet to the target, excluding the start, or null with the search status.</summary>
    public (IReadOnlyList<SubunitPosition>? Waypoints, GroundRouteStatus Status) Plan(SubunitPosition target)
    {
        WorldSnapshot state = _authority.ReadStateForHost();
        SubunitPosition feet = state.Space.GetActor(_playerActorId).Feet;
        GroundRouteResult route = state.Space.QueryGroundRoute(_playerActorId, feet, target);
        if (route.Status != GroundRouteStatus.Reachable)
        {
            return (null, route.Status);
        }
        return (route.Waypoints.Where(point => point != feet).ToArray(), route.Status);
    }
}
