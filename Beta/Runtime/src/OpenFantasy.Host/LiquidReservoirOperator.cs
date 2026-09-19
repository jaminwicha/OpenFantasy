using System.Text.Json;
using OpenFantasy.Core.Geography;
using OpenFantasy.Hosting;

namespace OpenFantasy.Host;

/// <summary>Privileged read-only inspection of standalone physical reservoir support and volume projections.</summary>
internal sealed class LiquidReservoirOperator
{
    internal string Inspect(string path)
    {
        var state = LocalAuthorityFiles.ReadLiquidReservoirs(path);
        return JsonSerializer.Serialize(new
        {
            Contract = "liquid-reservoirs-v1", Scope = "standalone-watertight-open-vessels",
            ContentHash = state.ContentHash.Value, CapacityHash = state.Capacity.ContentHash.Value,
            state.Capacity.Revision, TotalColumns = state.Geometries.Sum(g => g.Columns.Count),
            Reservoirs = state.Projections.Select(p => new
            {
                ReservoirId = p.Geometry.ReservoirId.ToString(), CustodianId = p.Geometry.CustodianId.ToString(),
                p.Geometry.SpillZ, Columns = p.Geometry.Columns.Count, CapacityMicrolitres = p.Geometry.Capacity.Value,
                p.ExistingUnits, p.ReservedIncomingUnits, Actual = Describe(p.ActualFill), Projected = Describe(p.ProjectedFill)
            })
        });
    }

    private static object Describe(ReservoirFill fill) => new
    {
        VolumeMicrolitres = fill.Volume.Value, fill.SurfaceMetres, fill.SurfaceBillionths, fill.RemainderMicrolitres
    };
}
