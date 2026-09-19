using System.Collections.ObjectModel;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Geography;

public sealed record ReservoirColumnFill(ReservoirColumn Column, VolumeMicrolitres Volume);

/// <summary>Equilibrium distribution witness. Volumes are projections of existing custody, never new material claims.</summary>
public sealed class ReservoirFill
{
    internal ReservoirFill(VolumeMicrolitres volume, long surfaceMetres, uint surfaceBillionths,
        ulong remainderMicrolitres, IEnumerable<ReservoirColumnFill> columns)
    {
        Volume = volume;
        SurfaceMetres = surfaceMetres;
        SurfaceBillionths = surfaceBillionths;
        RemainderMicrolitres = remainderMicrolitres;
        Columns = columns.ToList().AsReadOnly();
    }

    public VolumeMicrolitres Volume { get; }
    public long SurfaceMetres { get; }
    public uint SurfaceBillionths { get; }
    public ulong RemainderMicrolitres { get; }
    public ReadOnlyCollection<ReservoirColumnFill> Columns { get; }
}
