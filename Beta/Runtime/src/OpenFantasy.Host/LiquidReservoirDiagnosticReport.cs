using OpenFantasy.Core.Geography;

namespace OpenFantasy.Host;

/// <summary>Privileged world-storage diagnostics; this does not expose hidden state to player projections.</summary>
internal sealed class LiquidReservoirDiagnosticReport
{
    internal LiquidReservoirDiagnosticReport(LiquidReservoirState state)
    {
        ContentHash = state.ContentHash.Value;
        CapacityHash = state.Capacity.ContentHash.Value;
        Revision = state.Capacity.Revision;
        ReservoirCount = state.Geometries.Count;
        PendingCount = state.Capacity.Pending.Count;
        ClosedCount = state.Capacity.Closed.Count;
        ActualMicrolitres = state.Projections.Aggregate(0UL, (sum, p) => checked(sum + p.ActualFill.Volume.Value));
        ReservedIncomingUnits = state.Capacity.Receivers.Aggregate(0UL, (sum, r) => checked(sum + r.IncomingUnits));
    }

    public string ContentHash { get; }
    public string CapacityHash { get; }
    public ulong Revision { get; }
    public int ReservoirCount { get; }
    public int PendingCount { get; }
    public int ClosedCount { get; }
    public ulong ActualMicrolitres { get; }
    public ulong ReservedIncomingUnits { get; }
}
