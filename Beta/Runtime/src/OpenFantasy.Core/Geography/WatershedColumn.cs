namespace OpenFantasy.Core.Geography;

/// <summary>
/// Prepared substrate parcel and potential spill route. Spill elevation does not raise the ground or create a lake.
/// Contributing columns measure catchment topology, not available water or mineral quantity.
/// </summary>
public sealed record WatershedColumn(int Index, long NaturalElevation, long SpillElevation,
    int? DownstreamIndex, int DrainageRank, int ContributingColumns, EntityId SubstrateParcelId);

/// <summary>One canonical face read identically from either side of a prepared basin's local partition.</summary>
public sealed record WatershedBoundary(EntityId BoundaryId, int FirstIndex, int SecondIndex,
    long FirstNaturalElevation, long SecondNaturalElevation, int? DrainsFromIndex);
