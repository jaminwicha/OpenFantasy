namespace OpenFantasy.Core.Geography;

/// <summary>An authored, normally closed gravity gate across one shared column edge.</summary>
public sealed record ReservoirConnection
{
    public ReservoirConnection(EntityId connectionId, EntityId sourceReservoirId, EntityId receiverReservoirId,
        long sourceX, long sourceY, long receiverX, long receiverY, long sillZ, ulong maximumUnitsPerStep)
    {
        if (connectionId == sourceReservoirId || connectionId == receiverReservoirId || sourceReservoirId == receiverReservoirId
            || Int128.Abs((Int128)sourceX - receiverX) + Int128.Abs((Int128)sourceY - receiverY) != 1)
        {
            throw new ArgumentException("A gravity gate requires distinct identities and adjacent column ports.");
        }
        ArgumentOutOfRangeException.ThrowIfZero(maximumUnitsPerStep);
        ConnectionId = connectionId;
        SourceReservoirId = sourceReservoirId;
        ReceiverReservoirId = receiverReservoirId;
        SourceX = sourceX;
        SourceY = sourceY;
        ReceiverX = receiverX;
        ReceiverY = receiverY;
        SillZ = sillZ;
        MaximumUnitsPerStep = maximumUnitsPerStep;
    }

    public EntityId ConnectionId { get; }
    public EntityId SourceReservoirId { get; }
    public EntityId ReceiverReservoirId { get; }
    public long SourceX { get; }
    public long SourceY { get; }
    public long ReceiverX { get; }
    public long ReceiverY { get; }
    public long SillZ { get; }
    public ulong MaximumUnitsPerStep { get; }

    internal void Validate(LiquidReservoirState state)
    {
        var source = state.Geometries.SingleOrDefault(g => g.ReservoirId == SourceReservoirId);
        var receiver = state.Geometries.SingleOrDefault(g => g.ReservoirId == ReceiverReservoirId);
        if (source is null || receiver is null || !ContainsPort(source, SourceX, SourceY) || !ContainsPort(receiver, ReceiverX, ReceiverY))
        {
            throw new ArgumentException("Both gate ports must lie in their exact vessels below the spill planes.");
        }
    }

    internal bool AdmitsHead(LiquidReservoirState final)
    {
        var source = final.Projections.Single(p => p.Geometry.ReservoirId == SourceReservoirId).ActualFill;
        var receiver = final.Projections.Single(p => p.Geometry.ReservoirId == ReceiverReservoirId).ActualFill;
        // A floor-band remainder can raise some columns by one billionth of a metre. Use
        // a conservative lower bound upstream and upper bound downstream, never projected holds.
        Int128 sourceHead = (Int128)source.SurfaceMetres * ReservoirGeometry.MicrolitresPerCubicMetre + source.SurfaceBillionths;
        Int128 receiverHead = (Int128)receiver.SurfaceMetres * ReservoirGeometry.MicrolitresPerCubicMetre
            + receiver.SurfaceBillionths + (receiver.RemainderMicrolitres == 0 ? 0 : 1);
        return sourceHead >= (Int128)SillZ * ReservoirGeometry.MicrolitresPerCubicMetre && sourceHead >= receiverHead;
    }

    private bool ContainsPort(ReservoirGeometry geometry, long x, long y) => SillZ < geometry.SpillZ
        && geometry.Columns.Any(c => c.X == x && c.Y == y && c.BottomZ <= SillZ);
}
