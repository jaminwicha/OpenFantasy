using System.Text.Json;
using OpenFantasy.Core.Geography;

namespace OpenFantasy.Host;

/// <summary>Read-only inspection of an immutable finite preparation archive, not a live world or fluid authority.</summary>
internal sealed class WatershedOperator
{
    internal string Inspect(string path)
    {
        using FileStream stream = File.OpenRead(path);
        if (stream.Length > WatershedCodecV1.MaximumEncodedByteLength)
        {
            throw new InvalidDataException("Watershed input exceeds its byte limit.");
        }
        byte[] bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1)
        {
            throw new InvalidDataException("Watershed input changed length during inspection.");
        }
        PreparedWatershed plan = WatershedCodecV1.Decode(bytes);
        return JsonSerializer.Serialize(new
        {
            Contract = "watershed-preparation-v1", Scope = "finite-macro-candidate",
            ContentHash = plan.ComputeContentHash().Value, BasinId = plan.BasinId.ToString(),
            FormationId = plan.FormationId.ToString(), WorldId = plan.Request.Scope.WorldId.ToString(),
            BranchId = plan.Request.Scope.BranchId.ToString(), RealmId = plan.Request.Scope.RealmId.ToString(),
            ExternalBoundaryId = plan.Request.ExternalBoundaryId.ToString(),
            ColumnCount = plan.Columns.Count, SharedBoundaryCount = plan.Boundaries.Count,
            plan.Request.OutletIndex, OutletElevation = plan.Request.Rules.OutletElevation,
            OutletContributingColumns = plan.Columns[plan.Request.OutletIndex].ContributingColumns
        });
    }
}
