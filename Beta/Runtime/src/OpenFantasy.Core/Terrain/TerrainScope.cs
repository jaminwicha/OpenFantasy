namespace OpenFantasy.Core.Terrain;

/// <summary>A region belongs to exactly one world, branch and realm; this is not an authorization.</summary>
public sealed record TerrainScope(EntityId WorldId, EntityId BranchId, EntityId RealmId);
