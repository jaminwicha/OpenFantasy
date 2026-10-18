using System.Buffers.Binary;
using System.Text;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Construction.Ownership;

/// <summary>Bounded storage envelopes; gameplay admission belongs to the reconstructed domain owners.</summary>
internal sealed class StoredConstructionOwners
{
    private const int MaximumEntries = 16_384;
    private readonly Dictionary<ConstructionOwnerKey, byte[][]> _fields = [];

    internal StoredConstructionOwners(byte[] directory, ContentHash expectedHash, Func<ContentHash, byte[]> readOwner)
    {
        if (directory.Length > ConstructionOwnerDirectory.MaximumDirectoryByteLength
            || ContentHash.Compute(directory) != expectedHash)
            throw new FormatException("The owner directory does not match the selected history binding.");
        var reader = new WorldFrameReader(directory, ConstructionOwnerDirectory.MaximumDirectoryByteLength, 9);
        reader.Tag("world.construction-owners.v1"u8);
        reader.Version();
        if (reader.Number() != 5) throw new FormatException("Unsupported owner gameplay interpretation.");
        Scope = new(reader.Identity(), reader.Identity(), reader.Identity());
        Revision = reader.Number();
        Terrain = ReadTerrain(reader.Field(ConstructionOwnerDirectory.MaximumDirectoryByteLength));
        var entries = new WorldFrameReader(reader.Field(ConstructionOwnerDirectory.MaximumDirectoryByteLength),
            ConstructionOwnerDirectory.MaximumDirectoryByteLength, MaximumEntries + 1);
        entries.Tag("world.owner-entries"u8);
        List<(ConstructionOwnerKey Key, ContentHash Hash)> keys = [];
        while (entries.Remaining != 0)
        {
            var entry = new WorldFrameReader(entries.Field(256), 256, 4);
            entry.Tag("world.owner-entry.v1"u8);
            var kind = (ConstructionOwnerKind)checked((int)entry.Number());
            if (!Enum.IsDefined(kind)) throw new FormatException("Unknown owner role.");
            var key = new ConstructionOwnerKey(kind, entry.Identity());
            var hash = Hash(entry.Exact(32));
            entry.Complete();
            if (keys.Count != 0 && (key.Kind < keys[^1].Key.Kind
                || key.Kind == keys[^1].Key.Kind && key.SubjectId <= keys[^1].Key.SubjectId))
                throw new FormatException("Owner keys must be unique and strictly ordered.");
            keys.Add((key, hash));
        }
        entries.Complete();
        reader.Complete();
        long bytes = 0;
        foreach (var (key, hash) in keys)
        {
            byte[] payload = readOwner(hash) ?? throw new FormatException("A referenced owner is missing.");
            bytes = checked(bytes + payload.Length);
            if (payload.Length > ConstructionOwnerDirectory.MaximumOwnerPayloadByteLength
                || bytes > ConstructionOwnerDirectory.MaximumTotalPayloadByteLength
                || ContentHash.Compute(payload) != hash)
                throw new FormatException("An owner image violates its hash or byte envelope.");
            var owner = new WorldFrameReader(payload, ConstructionOwnerDirectory.MaximumOwnerPayloadByteLength, 12);
            owner.Tag("world.construction-owner.v1"u8);
            owner.Version();
            if (owner.Number() != (ulong)key.Kind || owner.Identity() != Scope.WorldId
                || owner.Identity() != Scope.BranchId || owner.Identity() != key.SubjectId)
                throw new FormatException("Owner role, identity or scope differs from its directory entry.");
            List<byte[]> fields = [];
            while (owner.Remaining != 0) fields.Add(owner.Field(ConstructionOwnerDirectory.MaximumOwnerPayloadByteLength).ToArray());
            owner.Complete();
            _fields.Add(key, fields.ToArray());
        }
    }

    internal TerrainScope Scope { get; }
    internal ulong Revision { get; }
    internal IReadOnlyList<TerrainRegionReference> Terrain { get; }

    internal byte[][] Singleton(ConstructionOwnerKind kind, int count)
    {
        var matches = Owners(kind, count).ToArray();
        if (matches.Length != 1 || matches[0].Key.SubjectId != Scope.WorldId)
            throw new FormatException("A world singleton owner is absent or has the wrong subject.");
        return matches[0].Fields;
    }

    internal IEnumerable<(ConstructionOwnerKey Key, byte[][] Fields)> Owners(ConstructionOwnerKind kind, int count)
    {
        foreach (var entry in _fields.Where(value => value.Key.Kind == kind).OrderBy(value => value.Key.SubjectId))
        {
            if (entry.Value.Length != count) throw new FormatException("An owner has the wrong field count.");
            yield return (entry.Key, entry.Value);
        }
    }

    internal static List<byte[]> Collection(byte[] bytes, string tag, int maximumCount)
    {
        var reader = new WorldFrameReader(bytes, ConstructionOwnerDirectory.MaximumOwnerPayloadByteLength, maximumCount + 1);
        reader.Tag(Encoding.UTF8.GetBytes(tag));
        List<byte[]> fields = [];
        while (reader.Remaining != 0) fields.Add(reader.Field(ConstructionOwnerDirectory.MaximumOwnerPayloadByteLength).ToArray());
        reader.Complete();
        return fields;
    }

    internal static ulong Number(byte[] bytes)
    {
        if (bytes.Length != 8) throw new FormatException("Owner number requires eight bytes.");
        return BinaryPrimitives.ReadUInt64BigEndian(bytes);
    }

    internal static long Signed(byte[] bytes)
    {
        if (bytes.Length != 8) throw new FormatException("Owner instant requires eight bytes.");
        return BinaryPrimitives.ReadInt64BigEndian(bytes);
    }

    internal static ContentHash Hash(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 32) throw new FormatException("Owner content hash requires thirty-two bytes.");
        return new(Convert.ToHexString(bytes).ToLowerInvariant());
    }

    private IReadOnlyList<TerrainRegionReference> ReadTerrain(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, ConstructionOwnerDirectory.MaximumDirectoryByteLength,
            LocalSpatialSnapshot.MaximumRegions + 1);
        reader.Tag("world.owner-terrain"u8);
        List<TerrainRegionReference> references = [];
        while (reader.Remaining != 0)
        {
            var entry = new WorldFrameReader(reader.Field(256), 256, 9);
            entry.Tag("world.owner-terrain.v1"u8);
            var minimum = new CellCoordinate(entry.Signed(), entry.Signed(), entry.Signed());
            var bounds = new TerrainRegionBounds(minimum, checked((int)entry.Number()),
                checked((int)entry.Number()), checked((int)entry.Number()));
            references.Add(new(Scope, bounds, entry.Number(), Hash(entry.Exact(32))));
            entry.Complete();
        }
        reader.Complete();
        return references.AsReadOnly();
    }
}
