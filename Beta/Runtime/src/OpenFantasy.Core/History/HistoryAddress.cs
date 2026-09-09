namespace OpenFantasy.Core.History;

/// <summary>
/// An exact reconstruction location. Each signed coordinate is measured in 1/256
/// cell subunits; a realm is a separate identity, never an elevation value.
/// </summary>
public readonly record struct HistoryAddress(
    EntityId WorldId,
    EntityId BranchId,
    EntityId RealmId,
    long XSubunits,
    long YSubunits,
    long ZSubunits);
