using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>
/// Explicit finite basin candidate and terminal boundary. The caller must admit it before publishing dependent places.
/// An external boundary names the end of preparation; it grants no water, storage, discharge or downstream terrain.
/// </summary>
public sealed class WatershedRequest
{
    public WatershedRequest(TerrainScope scope, EntityId seed, WatershedBounds bounds, WatershedRules rules,
        int outletIndex, EntityId externalBoundaryId)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(rules);
        if (!bounds.IsBoundary(outletIndex))
        {
            throw new ArgumentException("The outlet must be on the finite basin boundary.", nameof(outletIndex));
        }
        if (externalBoundaryId == scope.WorldId || externalBoundaryId == scope.BranchId || externalBoundaryId == scope.RealmId)
        {
            throw new ArgumentException("An external boundary cannot alias the world, branch or realm.", nameof(externalBoundaryId));
        }
        Scope = scope;
        Seed = seed;
        Bounds = bounds;
        Rules = rules;
        OutletIndex = outletIndex;
        ExternalBoundaryId = externalBoundaryId;
    }

    public TerrainScope Scope { get; }
    public EntityId Seed { get; }
    public WatershedBounds Bounds { get; }
    public WatershedRules Rules { get; }
    public int OutletIndex { get; }
    public EntityId ExternalBoundaryId { get; }
}
