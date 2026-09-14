using System.Text.Json;
using OpenFantasy.Core.Geography;
using OpenFantasy.Hosting;

namespace OpenFantasy.Host;

/// <summary>Privileged read-only inspection of funded runoff potential, outside world or hydraulic admission.</summary>
internal sealed class WatershedRunoffOperator
{
    internal string Inspect(string path)
    {
        PreparedWatershedRunoff runoff = LocalAuthorityFiles.ReadWatershedRunoff(path);
        WatershedRunoffRequest request = runoff.Request;
        return JsonSerializer.Serialize(new
        {
            Contract = "watershed-runoff-v1", Scope = "standalone-funded-runoff-potential",
            ContentHash = runoff.ContentHash.Value, RequestHash = request.ContentHash.Value,
            NetworkHash = request.Network.ComputeContentHash().Value, request.Network.ColumnCount,
            Basins = request.Network.Basins.Count, Revision = request.Inventory.Custody.Revision,
            ReservationId = request.ReservationId.ToString(), Start = request.Start.ToString(), End = request.End.ToString(),
            runoff.TotalInputUnits, TotalInputVolumeMicrolitres = runoff.TotalInputVolume.Value,
            Terminals = runoff.Terminals.Select(t => new { BasinId = t.BasinId.ToString(), CustodianId = t.CustodianId.ToString(), t.IncomingUnits }),
            Receivers = runoff.Receivers.Select(r => new { CustodianId = r.CustodianId.ToString(), r.ExistingUnits, r.IncomingUnits,
                r.ProjectedUnits, ProjectedVolumeMicrolitres = r.ProjectedVolume.Value, CapacityMicrolitres = r.Capacity.Value })
        });
    }
}
