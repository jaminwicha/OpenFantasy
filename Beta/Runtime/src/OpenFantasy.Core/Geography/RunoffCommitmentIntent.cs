using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Geography;

/// <summary>A request against one exact physical owner, never permission to replace its domain or material authority.</summary>
public abstract record RunoffCommitmentIntent : WorldActionIntent
{
    protected RunoffCommitmentIntent(ContentHash expectedReservoirHash, ulong expectedCapacityRevision, EntityId reservationId)
    {
        ArgumentNullException.ThrowIfNull(expectedReservoirHash);
        ExpectedReservoirHash = expectedReservoirHash;
        ExpectedCapacityRevision = expectedCapacityRevision;
        ReservationId = reservationId;
    }

    public ContentHash ExpectedReservoirHash { get; }
    public ulong ExpectedCapacityRevision { get; }
    public EntityId ReservationId { get; }
}

/// <summary>Releases only a pending receiving obligation; source stock and its hold remain unchanged.</summary>
public sealed record CancelRunoffIntent : RunoffCommitmentIntent
{
    public CancelRunoffIntent(ContentHash expectedReservoirHash, ulong expectedCapacityRevision, EntityId reservationId)
        : base(expectedReservoirHash, expectedCapacityRevision, reservationId)
    {
    }
}

/// <summary>Requests a finite future commitment backed by source stock already reserved under an admitted identity.</summary>
public sealed record AdmitRunoffIntent : RunoffCommitmentIntent
{
    public AdmitRunoffIntent(ContentHash expectedReservoirHash, ulong expectedCapacityRevision, EntityId reservationId,
        WorldInstant start, WorldInstant end, IEnumerable<WatershedRunoffInput> inputs)
        : base(expectedReservoirHash, expectedCapacityRevision, reservationId)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (end <= start)
        {
            throw new ArgumentException("A runoff commitment requires a nonempty half-open interval.");
        }
        var selected = new SortedDictionary<EntityId, WatershedRunoffInput>();
        foreach (var input in inputs)
        {
            ArgumentNullException.ThrowIfNull(input);
            if (selected.Count == PreparedWatershedNetwork.MaximumBasins || !selected.TryAdd(input.BasinId, input))
            {
                throw new ArgumentException("Runoff input selections must be unique and bounded.");
            }
        }
        if (selected.Count == 0)
        {
            throw new ArgumentException("A runoff commitment must explicitly select its basins, including dry ones.");
        }
        Start = start;
        End = end;
        Inputs = Array.AsReadOnly(selected.Values.ToArray());
    }

    public WorldInstant Start { get; }
    public WorldInstant End { get; }
    public ReadOnlyCollection<WatershedRunoffInput> Inputs { get; }
}
