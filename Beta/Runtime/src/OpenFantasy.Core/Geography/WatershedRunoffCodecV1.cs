using System.Buffers.Binary;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>Bounded exact forcing inputs and rederived routing witnesses; never a liquid occupancy archive.</summary>
public static class WatershedRunoffCodecV1
{
    public const int MaximumEncodedByteLength = 23_500_000;
    private const int MaximumRequestByteLength = 22_900_000;
    private const int InputByteLength = 72;
    private const int TerminalByteLength = 40;

    public static byte[] Encode(PreparedWatershedRunoff runoff) => EncodeRunoff(runoff, consumption: false);

    internal static byte[] EncodeRunoff(PreparedWatershedRunoff runoff, bool consumption, bool continued = false)
    {
        ArgumentNullException.ThrowIfNull(runoff);
        byte[] columns = new byte[runoff.Request.Network.ColumnCount * sizeof(ulong)];
        int offset = 0;
        foreach (PreparedWatershed basin in runoff.Request.Network.Basins)
        {
            foreach (WatershedColumn column in basin.Columns)
            {
                BinaryPrimitives.WriteUInt64BigEndian(columns.AsSpan(offset), runoff.ContributingUnits(basin.BasinId, column.Index));
                offset += sizeof(ulong);
            }
        }
        byte[] terminals = new byte[runoff.Terminals.Count * TerminalByteLength];
        for (int i = 0; i < runoff.Terminals.Count; i++)
        {
            WatershedRunoffTerminalTotal terminal = runoff.Terminals[i];
            Span<byte> row = terminals.AsSpan(i * TerminalByteLength, TerminalByteLength);
            terminal.BasinId.WriteBytes(row);
            terminal.CustodianId.WriteBytes(row[16..]);
            BinaryPrimitives.WriteUInt64BigEndian(row[32..], terminal.IncomingUnits);
        }
        byte[] bytes = CanonicalEncodingV1.Frame(continued ? "geography.watershed.runoff.v3" : consumption ? "geography.watershed.runoff.v2" : "geography.watershed.runoff.v1", EncodeRequest(runoff.Request, consumption, continued), columns, terminals);
        if (bytes.Length > MaximumEncodedByteLength)
        {
            throw new ArgumentException("The runoff archive exceeds its finite bound.");
        }
        return bytes;
    }

    public static PreparedWatershedRunoff Decode(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken = default) =>
        DecodeRunoff(bytes, cancellationToken, consumption: false);

    internal static PreparedWatershedRunoff DecodeRunoff(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken, bool consumption,
        GeographyProductionDependencies? dependencies = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var reader = new CanonicalTerrainReader(bytes, MaximumEncodedByteLength, 4, dependencies is not null ? "geography.watershed.runoff.v3"u8 : consumption ? "geography.watershed.runoff.v2"u8 : "geography.watershed.runoff.v1"u8);
            WatershedRunoffRequest request = DecodeRequest(reader.ReadField(MaximumRequestByteLength), cancellationToken, consumption, dependencies);
            _ = reader.ReadExactField(request.Network.ColumnCount * sizeof(ulong));
            _ = reader.ReadExactField(request.Terminals.Count * TerminalByteLength);
            reader.Complete();
            PreparedWatershedRunoff candidate = new WatershedRunoffPlanner().Prepare(request, cancellationToken);
            if (!EncodeRunoff(candidate, consumption, dependencies is not null).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The runoff archive differs from canonical inputs or rederived routing quantities.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            return candidate;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The runoff archive violates its finite funding or receiver contracts.", error);
        }
    }

    internal static byte[] EncodeRequest(WatershedRunoffRequest request, bool consumption = false, bool continued = false)
    {
        byte[] inputs = EncodeInputs(request.Inputs);
        byte[] terminals = EncodeTerminals(request.Terminals);
        byte[] bytes = CanonicalEncodingV1.Frame(continued ? "geography.watershed.runoff-request.v3" : consumption ? "geography.watershed.runoff-request.v2" : "geography.watershed.runoff-request.v1", WatershedNetworkCodecV1.Encode(request.Network),
            WatershedTerrainCodecV1.EncodeMapping(request.Mapping), AuthorityEncodingV1.Encode(request.Inventory),
            GeographyProductionCodec.Encode(request.Production, consumption, continued), request.ReservationId.ToByteArray(),
            request.OwnerActorId.ToByteArray(), EncodeInstant(request.Start), EncodeInstant(request.End), request.Material.Item.DefinitionId.ToByteArray(),
            request.Material.Item.Version.ToByteArray(), MaterialRecipeCodecV1.EncodeMaterial(request.Material.Physical), inputs, terminals);
        if (bytes.Length > MaximumRequestByteLength)
        {
            throw new ArgumentException("The runoff basis exceeds its finite bound.");
        }
        return bytes;
    }

    internal static WatershedRunoffRequest DecodeRequest(ReadOnlySpan<byte> bytes, CancellationToken cancellation, bool consumption = false,
        GeographyProductionDependencies? dependencies = null)
    {
        var reader = new CanonicalTerrainReader(bytes, MaximumRequestByteLength, 14, dependencies is not null ? "geography.watershed.runoff-request.v3"u8 : consumption ? "geography.watershed.runoff-request.v2"u8 : "geography.watershed.runoff-request.v1"u8);
        var network = WatershedNetworkCodecV1.Decode(reader.ReadField(WatershedNetworkCodecV1.MaximumEncodedByteLength), cancellation);
        var mapping = WatershedTerrainCodecV1.ReadMapping(reader.ReadField(256));
        var inventory = AuthorityEncodingV1.Decode(reader.ReadField(AuthorityEncodingV1.MaximumStateByteLength));
        ReadOnlySpan<byte> productionBytes = reader.ReadField(ProductionCodecV1.MaximumSnapshotByteLength);
        var production = GeographyProductionCodec.Decode(productionBytes, consumption, dependencies);
        EntityId reservation = reader.ReadIdentity(), owner = reader.ReadIdentity();
        WorldInstant start = ReadInstant(reader.ReadExactField(12)), end = ReadInstant(reader.ReadExactField(12));
        var item = new CountedItemDefinition(reader.ReadIdentity(), reader.ReadHash());
        var physical = MaterialRecipeCodecV1.DecodeMaterial(reader.ReadField(4096));
        ReadOnlySpan<byte> inputBytes = reader.ReadExactField(network.Basins.Count * InputByteLength);
        var inputs = ReadInputs(inputBytes, cancellation);
        ReadOnlySpan<byte> terminalBytes = reader.ReadExactField(network.Terminals.Count * TerminalByteLength);
        var terminals = ReadTerminals(terminalBytes);
        reader.Complete();
        return new(network, mapping, inventory, production, reservation, owner, start, end, new(item, physical), inputs, terminals,
            dependencies?.Qualification);
    }

    internal static byte[] EncodeInputs(IReadOnlyList<WatershedRunoffInput> inputs)
    {
        byte[] bytes = new byte[inputs.Count * InputByteLength];
        for (int i = 0; i < inputs.Count; i++)
        {
            WatershedRunoffInput input = inputs[i];
            Span<byte> row = bytes.AsSpan(i * InputByteLength, InputByteLength);
            input.BasinId.WriteBytes(row);
            BinaryPrimitives.WriteUInt64BigEndian(row[16..], input.DepthMicrometres);
            if (input.Source is { } source)
            {
                source.SourceId.WriteBytes(row[24..]);
                BinaryPrimitives.WriteUInt64BigEndian(row[40..], source.Start);
                BinaryPrimitives.WriteUInt64BigEndian(row[48..], source.End);
                input.CustodianId!.Value.WriteBytes(row[56..]);
            }
        }
        return bytes;
    }

    internal static byte[] EncodeTerminals(IReadOnlyList<WatershedRunoffTerminal> terminals)
    {
        byte[] bytes = new byte[terminals.Count * TerminalByteLength];
        for (int i = 0; i < terminals.Count; i++)
        {
            WatershedRunoffTerminal terminal = terminals[i];
            Span<byte> row = bytes.AsSpan(i * TerminalByteLength, TerminalByteLength);
            terminal.BasinId.WriteBytes(row);
            terminal.CustodianId.WriteBytes(row[16..]);
            BinaryPrimitives.WriteUInt64BigEndian(row[32..], terminal.Capacity.Value);
        }
        return bytes;
    }

    internal static List<WatershedRunoffInput> ReadInputs(ReadOnlySpan<byte> inputBytes, CancellationToken cancellation)
    {
        List<WatershedRunoffInput> inputs = [];
        for (int offset = 0; offset < inputBytes.Length; offset += InputByteLength)
        {
            cancellation.ThrowIfCancellationRequested();
            ReadOnlySpan<byte> row = inputBytes.Slice(offset, InputByteLength);
            EntityId basin = EntityId.FromBytes(row[..16]);
            ulong depth = BinaryPrimitives.ReadUInt64BigEndian(row[16..]);
            if (depth == 0)
            {
                if (row[24..].ContainsAnyExcept((byte)0))
                {
                    throw new FormatException("Dry input rows cannot carry hidden funding fields.");
                }
                inputs.Add(WatershedRunoffInput.Dry(basin));
            }
            else
            {
                inputs.Add(new(basin, depth, new(EntityId.FromBytes(row.Slice(24, 16)),
                    BinaryPrimitives.ReadUInt64BigEndian(row[40..]), BinaryPrimitives.ReadUInt64BigEndian(row[48..])),
                    EntityId.FromBytes(row.Slice(56, 16))));
            }
        }
        return inputs;
    }

    internal static List<WatershedRunoffTerminal> ReadTerminals(ReadOnlySpan<byte> terminalBytes)
    {
        List<WatershedRunoffTerminal> terminals = [];
        for (int offset = 0; offset < terminalBytes.Length; offset += TerminalByteLength)
        {
            ReadOnlySpan<byte> row = terminalBytes.Slice(offset, TerminalByteLength);
            terminals.Add(new(EntityId.FromBytes(row[..16]), EntityId.FromBytes(row.Slice(16, 16)),
                new(BinaryPrimitives.ReadUInt64BigEndian(row[32..]))));
        }
        return terminals;
    }

    internal static byte[] EncodeInstant(WorldInstant instant)
    {
        byte[] bytes = new byte[12];
        BinaryPrimitives.WriteInt64BigEndian(bytes, instant.Days);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8), instant.MillisecondsWithinDay);
        return bytes;
    }

    internal static WorldInstant ReadInstant(ReadOnlySpan<byte> bytes) =>
        new(BinaryPrimitives.ReadInt64BigEndian(bytes), BinaryPrimitives.ReadInt32BigEndian(bytes[8..]));
}
