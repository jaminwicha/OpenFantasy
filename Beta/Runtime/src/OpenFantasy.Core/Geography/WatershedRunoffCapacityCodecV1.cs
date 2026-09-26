using System.Buffers.Binary;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>Normalized standalone reservation archive; shared authority and drainage appear exactly once.</summary>
public static class WatershedRunoffCapacityCodecV1
{
    public const int MaximumEncodedByteLength = 23_500_000;
    private const int MaximumDomainByteLength = 4_110_000;
    private const int InputByteLength = 72;
    private const int CommitmentHeaderByteLength = 40;
    private const int ReceiverByteLength = 56;

    public static byte[] Encode(WatershedRunoffCapacityState state) => EncodeCapacity(state, consumption: false);

    internal static byte[] EncodeCapacity(WatershedRunoffCapacityState state, bool consumption)
    {
        ArgumentNullException.ThrowIfNull(state);
        int stride = CommitmentHeaderByteLength + state.Domain.Network.Basins.Count * InputByteLength;
        byte[] pending = new byte[state.Pending.Count * stride];
        for (int index = 0; index < state.Pending.Count; index++)
        {
            var request = state.Pending[index].Request;
            Span<byte> row = pending.AsSpan(index * stride, stride);
            request.ReservationId.WriteBytes(row);
            WatershedRunoffCodecV1.EncodeInstant(request.Start).CopyTo(row[16..]);
            WatershedRunoffCodecV1.EncodeInstant(request.End).CopyTo(row[28..]);
            WatershedRunoffCodecV1.EncodeInputs(request.Inputs).CopyTo(row[40..]);
        }
        byte[] closed = new byte[state.Closed.Count * EntityId.ByteLength];
        for (int index = 0; index < state.Closed.Count; index++)
        {
            state.Closed[index].WriteBytes(closed.AsSpan(index * EntityId.ByteLength));
        }
        byte[] receivers = new byte[state.Receivers.Count * ReceiverByteLength];
        for (int index = 0; index < state.Receivers.Count; index++)
        {
            var receiver = state.Receivers[index];
            Span<byte> row = receivers.AsSpan(index * ReceiverByteLength, ReceiverByteLength);
            receiver.CustodianId.WriteBytes(row);
            BinaryPrimitives.WriteUInt64BigEndian(row[16..], receiver.ExistingUnits);
            BinaryPrimitives.WriteUInt64BigEndian(row[24..], receiver.IncomingUnits);
            BinaryPrimitives.WriteUInt64BigEndian(row[32..], receiver.ProjectedUnits);
            BinaryPrimitives.WriteUInt64BigEndian(row[40..], receiver.ProjectedVolume.Value);
            BinaryPrimitives.WriteUInt64BigEndian(row[48..], receiver.Capacity.Value);
        }
        var bytes = CanonicalEncodingV1.Frame(consumption ? "geography.watershed.runoff-capacity.v2" : "geography.watershed.runoff-capacity.v1", EncodeDomain(state.Domain),
            AuthorityEncodingV1.Encode(state.Inventory), GeographyProductionCodec.Encode(state.Production, consumption),
            EncodeRevision(state.Revision), pending, closed, receivers);
        if (bytes.Length > MaximumEncodedByteLength)
        {
            throw new ArgumentException("The runoff capacity archive exceeds its finite bound.");
        }
        return bytes;
    }

    public static WatershedRunoffCapacityState Decode(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken = default) =>
        DecodeCapacity(bytes, cancellationToken, consumption: false);

    internal static WatershedRunoffCapacityState DecodeCapacity(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken, bool consumption)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var reader = new CanonicalTerrainReader(bytes, MaximumEncodedByteLength, 8, consumption ? "geography.watershed.runoff-capacity.v2"u8 : "geography.watershed.runoff-capacity.v1"u8);
            var domain = DecodeDomain(reader.ReadField(MaximumDomainByteLength), cancellationToken);
            var inventory = AuthorityEncodingV1.Decode(reader.ReadField(AuthorityEncodingV1.MaximumStateByteLength));
            var productionBytes = reader.ReadField(ProductionCodecV1.MaximumSnapshotByteLength);
            var production = GeographyProductionCodec.Decode(productionBytes, consumption);
            ulong revision = reader.ReadUnsigned64();
            int stride = CommitmentHeaderByteLength + domain.Network.Basins.Count * InputByteLength;
            var pendingBytes = reader.ReadField(WatershedRunoffCapacityState.MaximumPending * stride);
            var closedBytes = reader.ReadField(WatershedRunoffCapacityState.MaximumClosed * EntityId.ByteLength);
            _ = reader.ReadExactField(domain.Terminals.Select(t => t.CustodianId).Distinct().Count() * ReceiverByteLength);
            reader.Complete();
            if (pendingBytes.Length % stride != 0 || closedBytes.Length % EntityId.ByteLength != 0)
            {
                throw new FormatException("Capacity commitment or closed-identity rows have invalid lengths.");
            }
            List<PreparedWatershedRunoff> pending = [];
            var planner = new WatershedRunoffPlanner();
            for (int offset = 0; offset < pendingBytes.Length; offset += stride)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = pendingBytes.Slice(offset, stride);
                var request = new WatershedRunoffRequest(domain.Network, domain.Mapping, inventory, production,
                    EntityId.FromBytes(row[..16]), domain.OwnerActorId, WatershedRunoffCodecV1.ReadInstant(row.Slice(16, 12)),
                    WatershedRunoffCodecV1.ReadInstant(row.Slice(28, 12)), domain.Material,
                    WatershedRunoffCodecV1.ReadInputs(row[40..], cancellationToken), domain.Terminals);
                pending.Add(planner.Prepare(request, cancellationToken));
            }
            List<EntityId> closed = [];
            for (int offset = 0; offset < closedBytes.Length; offset += EntityId.ByteLength)
            {
                closed.Add(EntityId.FromBytes(closedBytes.Slice(offset, EntityId.ByteLength)));
            }
            var state = new WatershedRunoffCapacityState(domain, inventory, production, revision, pending, closed);
            if (!EncodeCapacity(state, consumption).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The capacity archive differs from its canonical state and rederived obligations.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            return state;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The capacity archive violates its ownership or funding contract.", error);
        }
    }

    private static byte[] EncodeRevision(ulong revision)
    {
        byte[] bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, revision);
        return bytes;
    }

    internal static byte[] EncodeDomain(WatershedRunoffDomain domain) =>
        CanonicalEncodingV1.Frame("geography.watershed.runoff-domain.v1", WatershedNetworkCodecV1.Encode(domain.Network),
            WatershedTerrainCodecV1.EncodeMapping(domain.Mapping), domain.OwnerActorId.ToByteArray(),
            domain.Material.Item.DefinitionId.ToByteArray(), domain.Material.Item.Version.ToByteArray(),
            MaterialRecipeCodecV1.EncodeMaterial(domain.Material.Physical), WatershedRunoffCodecV1.EncodeTerminals(domain.Terminals));

    private static WatershedRunoffDomain DecodeDomain(ReadOnlySpan<byte> bytes, CancellationToken cancellation)
    {
        var reader = new CanonicalTerrainReader(bytes, MaximumDomainByteLength, 8, "geography.watershed.runoff-domain.v1"u8);
        var network = WatershedNetworkCodecV1.Decode(reader.ReadField(WatershedNetworkCodecV1.MaximumEncodedByteLength), cancellation);
        var mapping = WatershedTerrainCodecV1.ReadMapping(reader.ReadField(256));
        EntityId owner = reader.ReadIdentity();
        var item = new CountedItemDefinition(reader.ReadIdentity(), reader.ReadHash());
        var material = MaterialRecipeCodecV1.DecodeMaterial(reader.ReadField(4096));
        var terminals = WatershedRunoffCodecV1.ReadTerminals(reader.ReadExactField(network.Terminals.Count * 40));
        reader.Complete();
        return new(network, mapping, owner, new(item, material), terminals);
    }
}
