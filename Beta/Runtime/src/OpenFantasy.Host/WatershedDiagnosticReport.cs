using OpenFantasy.Core.Geography;

namespace OpenFantasy.Host;

/// <summary>Privileged diagnostics of retained macro constraints, not discovered player knowledge.</summary>
internal sealed class WatershedDiagnosticReport(PreparedWatershed plan)
{
    public string ContentHash { get; } = plan.ComputeContentHash().Value;
    public string BasinId { get; } = plan.BasinId.ToString();
    public string FormationId { get; } = plan.FormationId.ToString();
    public string RealmId { get; } = plan.Request.Scope.RealmId.ToString();
    public string ExternalBoundaryId { get; } = plan.Request.ExternalBoundaryId.ToString();
    public int ColumnCount { get; } = plan.Columns.Count;
    public int SharedBoundaryCount { get; } = plan.Boundaries.Count;
}
