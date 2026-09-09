namespace OpenFantasy.Core.Custody;

/// <summary>A finite counted-unit storage budget. It is neither mass/volume nor legal ownership.</summary>
public sealed record CustodianStorage(EntityId CustodianId, ulong UnitCapacity, ulong Revision);
