using System.Text.Json;
using OpenFantasy.Hosting;

namespace OpenFantasy.Host;

/// <summary>Read-only inspection of the standalone runoff capacity owner.</summary>
internal sealed class WatershedRunoffCapacityOperator
{
    internal string Inspect(string path)
    {
        var state = LocalAuthorityFiles.ReadWatershedRunoffCapacity(path);
        return JsonSerializer.Serialize(new
        {
            Contract = "watershed-runoff-capacity-v1", Scope = "standalone-capacity-owner",
            ContentHash = state.ContentHash.Value, DomainHash = state.Domain.ContentHash.Value,
            state.Revision, CustodyRevision = state.Inventory.Custody.Revision,
            Pending = state.Pending.Select(p => new { ReservationId = p.Request.ReservationId.ToString(),
                Start = p.Request.Start.ToString(), End = p.Request.End.ToString(), p.TotalInputUnits }),
            Closed = state.Closed.Select(id => id.ToString()),
            Receivers = state.Receivers.Select(r => new { CustodianId = r.CustodianId.ToString(), r.ExistingUnits,
                r.IncomingUnits, r.ProjectedUnits, ProjectedVolumeMicrolitres = r.ProjectedVolume.Value, CapacityMicrolitres = r.Capacity.Value })
        });
    }
}
