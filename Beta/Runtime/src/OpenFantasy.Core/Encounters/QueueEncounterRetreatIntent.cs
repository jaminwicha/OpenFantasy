using System.Collections.ObjectModel;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Encounters;

/// <summary>One ordinary 32-subunit cardinal movement sample; north decreases Y.</summary>
public enum EncounterRetreatDirection
{
    North = 1,
    East = 2,
    South = 3,
    West = 4
}

/// <summary>
/// Requests a committed retreat over captured legal ground. The route is advice captured by the caller; the
/// world revalidates control, body, every sample, occupancy and contact at queue time and at each sample.
/// It is neither a Wait, an outcome release nor a relocation: each sample is walked and charged separately.
/// </summary>
public sealed record QueueEncounterRetreatIntent : WorldActionIntent
{
    public QueueEncounterRetreatIntent(EntityId encounterId, ulong expectedTimingRevision, SubunitPosition destination,
        IEnumerable<EncounterRetreatDirection> route)
    {
        ArgumentNullException.ThrowIfNull(route);
        EncounterRetreatDirection[] samples = route.ToArray();
        if (samples.Length is 0 or > EncounterRetreatRules.MaximumRouteSamples || samples.Any(value => !Enum.IsDefined(value)))
        {
            throw new ArgumentException("A retreat route names one to the bounded number of cardinal samples.", nameof(route));
        }
        EncounterId = encounterId;
        ExpectedTimingRevision = expectedTimingRevision;
        Destination = destination;
        Route = Array.AsReadOnly(samples);
    }

    public EntityId EncounterId { get; }
    public ulong ExpectedTimingRevision { get; }
    public SubunitPosition Destination { get; }
    public ReadOnlyCollection<EncounterRetreatDirection> Route { get; }

    /// <summary>Two requests are the same choice when every captured field and every route sample agree.</summary>
    public bool Equals(QueueEncounterRetreatIntent? other) => other is not null && EncounterId == other.EncounterId
        && ExpectedTimingRevision == other.ExpectedTimingRevision && Destination == other.Destination
        && Route.SequenceEqual(other.Route);

    public override int GetHashCode() => HashCode.Combine(EncounterId, ExpectedTimingRevision, Destination, Route.Count);

    public static (long X, long Y) Displacement(EncounterRetreatDirection direction) => direction switch
    {
        EncounterRetreatDirection.North => (0, -EncounterRetreatRules.SampleSubunits),
        EncounterRetreatDirection.East => (EncounterRetreatRules.SampleSubunits, 0),
        EncounterRetreatDirection.South => (0, EncounterRetreatRules.SampleSubunits),
        EncounterRetreatDirection.West => (-EncounterRetreatRules.SampleSubunits, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };

    /// <summary>The flat endpoint of the route from an origin; elevation is never changed by a retreat sample.</summary>
    public static SubunitPosition Endpoint(SubunitPosition origin, IEnumerable<EncounterRetreatDirection> route)
    {
        ArgumentNullException.ThrowIfNull(route);
        SubunitPosition position = origin;
        foreach (EncounterRetreatDirection direction in route)
        {
            (long x, long y) = Displacement(direction);
            position = position.Offset(x, y, 0);
        }
        return position;
    }
}
