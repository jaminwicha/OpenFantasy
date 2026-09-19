using OpenFantasy.Core;
using OpenFantasy.Core.Geography;

namespace OpenFantasy.Host;

/// <summary>Compact privileged diagnostics for the complete retained transport owner.</summary>
internal sealed class ReservoirTransportDiagnosticReport
{
    internal ReservoirTransportDiagnosticReport(ReservoirTransportState state)
    {
        ContentHash = state.ContentHash.Value;
        ReservoirHash = state.Reservoirs.ContentHash.Value;
        Revision = state.Revision;
        Boundary = state.Boundary;
        ConnectionCount = state.Connections.Count;
        StageCount = state.Stages.Count;
        PendingCount = state.Stages.Count(stage => stage.IsPending);
        PendingUnits = state.Stages.Where(stage => stage.IsPending).Sum(stage => (decimal)stage.Units);
        Statuses = Enum.GetValues<RunoffTransportStatus>()
            .Select(status => new ReservoirTransportStatusDiagnostic(status, state.Stages)).ToArray();
        Contents = state.Reservoirs.Projections.Select(projection => new ReservoirTransportContentDiagnostic(projection)).ToArray();
    }

    public string ContentHash { get; }
    public string ReservoirHash { get; }
    public ulong Revision { get; }
    public WorldInstant Boundary { get; }
    public int ConnectionCount { get; }
    public int StageCount { get; }
    public int PendingCount { get; }
    public decimal PendingUnits { get; }
    public IReadOnlyList<ReservoirTransportStatusDiagnostic> Statuses { get; }
    public IReadOnlyList<ReservoirTransportContentDiagnostic> Contents { get; }
}

internal sealed class ReservoirTransportContentDiagnostic
{
    internal ReservoirTransportContentDiagnostic(ReservoirStorageProjection projection)
    {
        ReservoirId = projection.Geometry.ReservoirId.ToString();
        CustodianId = projection.Geometry.CustodianId.ToString();
        ActualUnits = projection.ExistingUnits;
        ReservedIncomingUnits = projection.ReservedIncomingUnits;
        ActualMicrolitres = projection.ActualFill.Volume.Value;
    }

    public string ReservoirId { get; }
    public string CustodianId { get; }
    public ulong ActualUnits { get; }
    public ulong ReservedIncomingUnits { get; }
    public ulong ActualMicrolitres { get; }
}

internal sealed class ReservoirTransportStatusDiagnostic
{
    internal ReservoirTransportStatusDiagnostic(RunoffTransportStatus status, IEnumerable<RunoffTransportStage> stages)
    {
        var matching = stages.Where(stage => stage.Status == status).ToArray();
        Status = status.ToString();
        Count = matching.Length;
        // Retained stages can count the same stock across multiple deliveries. Decimal
        // preserves exact integer totals across the bounded journal beyond UInt64.
        Units = matching.Sum(stage => (decimal)stage.Units);
    }

    public string Status { get; }
    public int Count { get; }
    public decimal Units { get; }
}
