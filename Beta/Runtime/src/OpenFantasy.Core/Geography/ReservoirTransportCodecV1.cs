using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Geography;

/// <summary>Canonical complete transport owner; original physical and funding formats remain unchanged.</summary>
public static class ReservoirTransportCodecV1
{
    public const int MaximumEncodedByteLength = 29_000_000;
    private const int ConnectionRowBytes = 96;
    private const int MaximumStageBytes = 1024;
    private const int MaximumJournalBytes = 4_300_000;

    public static byte[] Encode(ReservoirTransportState state) => EncodeTransport(state, consumption: false);

    internal static byte[] EncodeTransport(ReservoirTransportState state, bool consumption)
    {
        ArgumentNullException.ThrowIfNull(state);
        byte[] bytes = CanonicalEncodingV1.Frame(consumption ? "geography.reservoir-transport.v2" : "geography.reservoir-transport.v1", Number(state.Revision),
            WatershedRunoffCodecV1.EncodeInstant(state.Boundary), LiquidReservoirCodecV1.EncodeReservoirs(state.Reservoirs, consumption),
            EncodeConnections(state.Connections), CanonicalEncodingV1.Frame("geography.transport-stages.v1",
                state.Stages.Select(s => (ReadOnlyMemory<byte>)EncodeStage(s)).ToArray()));
        if (bytes.Length > MaximumEncodedByteLength)
        {
            throw new ArgumentException("The finite transport owner exceeds its canonical archive bound.");
        }
        return bytes;
    }

    public static ReservoirTransportState Decode(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken = default) =>
        DecodeTransport(bytes, cancellationToken, consumption: false);

    internal static ReservoirTransportState DecodeTransport(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken, bool consumption)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumEncodedByteLength, 6);
            reader.Tag(consumption ? "geography.reservoir-transport.v2"u8 : "geography.reservoir-transport.v1"u8);
            ulong revision = reader.Number();
            WorldInstant boundary = WatershedRunoffCodecV1.ReadInstant(reader.Exact(12));
            var physical = LiquidReservoirCodecV1.DecodeReservoirs(reader.Field(LiquidReservoirCodecV1.MaximumEncodedByteLength), cancellationToken, consumption);
            var connections = DecodeConnections(reader.Field(ReservoirTransportState.MaximumConnections * ConnectionRowBytes));
            var stages = new List<RunoffTransportStage>();
            var journal = new WorldFrameReader(reader.Field(MaximumJournalBytes), MaximumJournalBytes, ReservoirTransportState.MaximumStages + 1);
            journal.Tag("geography.transport-stages.v1"u8);
            while (journal.Remaining != 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                stages.Add(DecodeStage(journal.Field(MaximumStageBytes)));
            }
            journal.Complete();
            reader.Complete();
            var restored = new ReservoirTransportState(physical, connections, stages, revision, boundary);
            if (!EncodeTransport(restored, consumption).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The transport owner is not canonical.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The transport owner violates its finite physical and stage contract.", error);
        }
    }

    internal static byte[] EncodeRoutes(IEnumerable<RunoffTransportRoute> routes)
    {
        using var bytes = new MemoryStream();
        foreach (var route in routes)
        {
            bytes.Write(route.BasinId.ToByteArray());
            bytes.Write(route.ConnectionId.ToByteArray());
        }
        return bytes.ToArray();
    }

    internal static RunoffTransportRoute[] DecodeRoutes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > PreparedWatershedNetwork.MaximumBasins * 32 || bytes.Length % 32 != 0)
        {
            throw new FormatException("Transport route rows must be complete and bounded.");
        }
        var routes = new List<RunoffTransportRoute>();
        for (int offset = 0; offset < bytes.Length; offset += 32)
        {
            routes.Add(new(EntityId.FromBytes(bytes.Slice(offset, 16)), EntityId.FromBytes(bytes.Slice(offset + 16, 16))));
        }
        return routes.ToArray();
    }

    private static byte[] EncodeConnections(IEnumerable<ReservoirConnection> connections)
    {
        using var bytes = new MemoryStream();
        foreach (var connection in connections)
        {
            bytes.Write(connection.ConnectionId.ToByteArray());
            bytes.Write(connection.SourceReservoirId.ToByteArray());
            bytes.Write(connection.ReceiverReservoirId.ToByteArray());
            bytes.Write(Signed(connection.SourceX));
            bytes.Write(Signed(connection.SourceY));
            bytes.Write(Signed(connection.ReceiverX));
            bytes.Write(Signed(connection.ReceiverY));
            bytes.Write(Signed(connection.SillZ));
            bytes.Write(Number(connection.MaximumUnitsPerStep));
        }
        return bytes.ToArray();
    }

    private static List<ReservoirConnection> DecodeConnections(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0 || bytes.Length % ConnectionRowBytes != 0)
        {
            throw new FormatException("Gate rows must be complete and nonempty.");
        }
        var result = new List<ReservoirConnection>();
        for (int offset = 0; offset < bytes.Length; offset += ConnectionRowBytes)
        {
            var row = bytes.Slice(offset, ConnectionRowBytes);
            result.Add(new(EntityId.FromBytes(row[..16]), EntityId.FromBytes(row.Slice(16, 16)), EntityId.FromBytes(row.Slice(32, 16)),
                BinaryPrimitives.ReadInt64BigEndian(row[48..]), BinaryPrimitives.ReadInt64BigEndian(row[56..]),
                BinaryPrimitives.ReadInt64BigEndian(row[64..]), BinaryPrimitives.ReadInt64BigEndian(row[72..]),
                BinaryPrimitives.ReadInt64BigEndian(row[80..]), BinaryPrimitives.ReadUInt64BigEndian(row[88..])));
        }
        return result;
    }

    private static byte[] EncodeStage(RunoffTransportStage stage) => CanonicalEncodingV1.Frame("geography.transport-stage.v1",
        stage.StageId.ToByteArray(), stage.ReservationId.ToByteArray(), WatershedRunoffCodecV1.EncodeInstant(stage.DueAt),
        stage.FundingHash.ToByteArray(), Number(stage.Units), EncodeRoutes(stage.Routes), Number((ulong)stage.Status),
        stage.ResolvedAt is { } resolved ? WatershedRunoffCodecV1.EncodeInstant(resolved) : []);

    private static RunoffTransportStage DecodeStage(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumStageBytes, 9);
        reader.Tag("geography.transport-stage.v1"u8);
        EntityId stage = reader.Identity(), reservation = reader.Identity();
        WorldInstant due = WatershedRunoffCodecV1.ReadInstant(reader.Exact(12));
        var funding = new ContentHash(Convert.ToHexString(reader.Exact(32)).ToLowerInvariant());
        ulong units = reader.Number();
        var routes = DecodeRoutes(reader.Field(PreparedWatershedNetwork.MaximumBasins * 32));
        ulong status = reader.Number();
        if (status > (ulong)RunoffTransportStatus.BlockedByMaterial)
        {
            throw new FormatException("Unknown transport stage status.");
        }
        var resolved = reader.Field(12);
        WorldInstant? instant = resolved.Length == 0 ? null : WatershedRunoffCodecV1.ReadInstant(resolved);
        reader.Complete();
        return new(stage, reservation, due, funding, units, routes, (RunoffTransportStatus)status, instant);
    }

    private static byte[] Signed(long value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] Number(ulong number) => CanonicalEncodingV1.Unsigned64(number);
}
