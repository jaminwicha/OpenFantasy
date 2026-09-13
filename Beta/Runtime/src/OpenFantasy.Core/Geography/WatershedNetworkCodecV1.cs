using System.Buffers.Binary;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>Canonical bounded archive retaining exact member bytes and verifying all composed facts by rederivation.</summary>
public static class WatershedNetworkCodecV1
{
    public const int MaximumEncodedByteLength = 4_000_000;
    private const int MaximumMembersBytes = 4 + PreparedWatershedNetwork.MaximumBasins * (4 + WatershedCodecV1.MaximumEncodedByteLength);
    private const int ClosureBytes = 44;
    private const int FaceBytes = 76;

    public static byte[] Encode(PreparedWatershedNetwork network)
    {
        ArgumentNullException.ThrowIfNull(network);
        byte[] counts = new byte[checked(network.ColumnCount * sizeof(uint))];
        int offset = 0;
        foreach (PreparedWatershed basin in network.Basins)
        {
            foreach (WatershedColumn column in basin.Columns)
            {
                BinaryPrimitives.WriteInt32BigEndian(counts.AsSpan(offset), network.ContributingColumns(basin.BasinId, column.Index));
                offset += sizeof(uint);
            }
        }
        return CanonicalEncodingV1.Frame("geography.watershed.network.prepared.v1",
            network.Scope.WorldId.ToByteArray(), network.Scope.BranchId.ToByteArray(), network.Scope.RealmId.ToByteArray(),
            network.Seed.ToByteArray(), network.ProvinceId.ToByteArray(), EncodeMembers(network.Basins),
            EncodeClosures(network.Closures), counts, EncodeFaces(network.SharedFaces));
    }

    public static PreparedWatershedNetwork Decode(ReadOnlySpan<byte> bytes, CancellationToken cancellation = default)
    {
        try
        {
            cancellation.ThrowIfCancellationRequested();
            var reader = new CanonicalTerrainReader(bytes, MaximumEncodedByteLength, 10,
                "geography.watershed.network.prepared.v1"u8);
            var scope = new TerrainScope(reader.ReadIdentity(), reader.ReadIdentity(), reader.ReadIdentity());
            EntityId seed = reader.ReadIdentity();
            EntityId province = reader.ReadIdentity();
            PreparedWatershed[] basins = ReadMembers(reader.ReadField(MaximumMembersBytes), cancellation);
            WatershedOutletClosure[] closures = ReadClosures(reader.ReadExactField(checked(basins.Length * ClosureBytes)));
            _ = reader.ReadExactField(checked(basins.Sum(basin => basin.Columns.Count) * sizeof(uint)));
            ReadOnlySpan<byte> faces = reader.ReadField(PreparedWatershedNetwork.MaximumSharedFaces * FaceBytes);
            if (faces.Length % FaceBytes != 0)
            {
                throw new FormatException("A composed face has an incomplete record.");
            }
            reader.Complete();
            PreparedWatershedNetwork network = new WatershedNetworkPlanner().Prepare(province, basins, closures, cancellation);
            if (network.Scope != scope || network.Seed != seed || !Encode(network).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The network archive differs from its exact canonical composition.");
            }
            return network;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException)
        {
            throw new FormatException("The watershed network contains invalid inputs or composition.", error);
        }
    }

    private static byte[] EncodeMembers(IReadOnlyList<PreparedWatershed> basins)
    {
        byte[][] members = basins.Select(WatershedCodecV1.Encode).ToArray();
        byte[] bytes = new byte[checked(4 + members.Sum(member => 4 + member.Length))];
        BinaryPrimitives.WriteInt32BigEndian(bytes, members.Length);
        int offset = 4;
        foreach (byte[] member in members)
        {
            BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(offset), member.Length);
            member.CopyTo(bytes, offset + 4);
            offset += 4 + member.Length;
        }
        return bytes;
    }

    private static PreparedWatershed[] ReadMembers(ReadOnlySpan<byte> bytes, CancellationToken cancellation)
    {
        if (bytes.Length < 4)
        {
            throw new FormatException("The network member count is missing.");
        }
        uint count = BinaryPrimitives.ReadUInt32BigEndian(bytes);
        if (count is < 1 or > PreparedWatershedNetwork.MaximumBasins)
        {
            throw new FormatException("The network member count exceeds its finite envelope.");
        }
        var basins = new PreparedWatershed[(int)count];
        int offset = 4;
        for (int index = 0; index < basins.Length; index++)
        {
            cancellation.ThrowIfCancellationRequested();
            if (bytes.Length - offset < 4)
            {
                throw new FormatException("A network member length is missing.");
            }
            uint length = BinaryPrimitives.ReadUInt32BigEndian(bytes[offset..]);
            offset += 4;
            if (length > WatershedCodecV1.MaximumEncodedByteLength || length > bytes.Length - offset)
            {
                throw new FormatException("A network member is truncated or exceeds its bound.");
            }
            basins[index] = WatershedCodecV1.Decode(bytes.Slice(offset, (int)length));
            offset += (int)length;
        }
        if (offset != bytes.Length)
        {
            throw new FormatException("The network member list contains trailing data.");
        }
        return basins;
    }

    private static byte[] EncodeClosures(IReadOnlyList<WatershedOutletClosure> closures)
    {
        byte[] bytes = new byte[checked(closures.Count * ClosureBytes)];
        for (int index = 0; index < closures.Count; index++)
        {
            WatershedOutletClosure closure = closures[index];
            Span<byte> row = bytes.AsSpan(index * ClosureBytes, ClosureBytes);
            closure.BasinId.WriteBytes(row);
            BinaryPrimitives.WriteUInt32BigEndian(row[16..], (uint)closure.Kind);
            (closure.ReceivingBasinId ?? default).WriteBytes(row[20..]);
            BinaryPrimitives.WriteInt32BigEndian(row[36..], closure.ReceivingColumnIndex ?? 0);
            BinaryPrimitives.WriteInt32BigEndian(row[40..], closure.TerminalColumnCapacity ?? 0);
        }
        return bytes;
    }

    private static WatershedOutletClosure[] ReadClosures(ReadOnlySpan<byte> bytes)
    {
        var closures = new WatershedOutletClosure[bytes.Length / ClosureBytes];
        for (int index = 0; index < closures.Length; index++)
        {
            ReadOnlySpan<byte> row = bytes.Slice(index * ClosureBytes, ClosureBytes);
            EntityId basinId = EntityId.FromBytes(row[..16]);
            uint kind = BinaryPrimitives.ReadUInt32BigEndian(row[16..]);
            EntityId receiver = EntityId.FromBytes(row.Slice(20, 16));
            int targetIndex = checked((int)BinaryPrimitives.ReadUInt32BigEndian(row[36..]));
            int capacity = checked((int)BinaryPrimitives.ReadUInt32BigEndian(row[40..]));
            closures[index] = kind switch
            {
                1 when capacity == 0 => WatershedOutletClosure.ToBasin(basinId, receiver, targetIndex),
                2 when receiver == default && targetIndex == 0 => WatershedOutletClosure.ToTerminal(basinId, capacity),
                _ => throw new FormatException("The outlet closure kind or unused fields are invalid.")
            };
        }
        return closures;
    }

    private static byte[] EncodeFaces(IReadOnlyList<WatershedNetworkFace> faces)
    {
        byte[] bytes = new byte[checked(faces.Count * FaceBytes)];
        for (int index = 0; index < faces.Count; index++)
        {
            WatershedNetworkFace face = faces[index];
            Span<byte> row = bytes.AsSpan(index * FaceBytes, FaceBytes);
            face.BoundaryId.WriteBytes(row);
            face.First.BasinId.WriteBytes(row[16..]);
            BinaryPrimitives.WriteInt32BigEndian(row[32..], face.First.ColumnIndex);
            face.Second.BasinId.WriteBytes(row[36..]);
            BinaryPrimitives.WriteInt32BigEndian(row[52..], face.Second.ColumnIndex);
            BinaryPrimitives.WriteInt64BigEndian(row[56..], face.FirstNaturalElevation);
            BinaryPrimitives.WriteInt64BigEndian(row[64..], face.SecondNaturalElevation);
            BinaryPrimitives.WriteUInt32BigEndian(row[72..], face.DrainsFromBasinId is null ? 0u
                : face.DrainsFromBasinId == face.First.BasinId ? 1u : 2u);
        }
        return bytes;
    }
}
