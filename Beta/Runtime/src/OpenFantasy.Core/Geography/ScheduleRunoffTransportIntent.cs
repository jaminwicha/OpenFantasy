using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Geography;

/// <summary>Schedules one already-funded indivisible arrival through named retained gravity gates.</summary>
public sealed record ScheduleRunoffTransportIntent : WorldActionIntent
{
    public ScheduleRunoffTransportIntent(ContentHash expectedTransportHash, ulong expectedTransportRevision,
        EntityId reservationId, WorldInstant dueAt, IEnumerable<RunoffTransportRoute> routes)
    {
        ArgumentNullException.ThrowIfNull(expectedTransportHash);
        ArgumentNullException.ThrowIfNull(routes);
        var selected = routes.Take(PreparedWatershedNetwork.MaximumBasins + 1).ToArray();
        if (selected.Length > PreparedWatershedNetwork.MaximumBasins || selected.Any(r => r is null)
            || selected.Select(r => r.BasinId).Distinct().Count() != selected.Length)
        {
            throw new ArgumentException("Transport route selections must be unique and bounded.");
        }
        ExpectedTransportHash = expectedTransportHash;
        ExpectedTransportRevision = expectedTransportRevision;
        ReservationId = reservationId;
        DueAt = dueAt;
        Routes = selected.OrderBy(r => r.BasinId).ToList().AsReadOnly();
    }

    public ContentHash ExpectedTransportHash { get; }
    public ulong ExpectedTransportRevision { get; }
    public EntityId ReservationId { get; }
    public WorldInstant DueAt { get; }
    public ReadOnlyCollection<RunoffTransportRoute> Routes { get; }
}
