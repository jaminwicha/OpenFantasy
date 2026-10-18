using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Construction.Ownership;

/// <summary>Immutable canonical owner image. Hash is its storage revision, not the actor or custody counter.</summary>
public sealed class ConstructionOwnerPayload
{
    private readonly byte[] _bytes;

    internal ConstructionOwnerPayload(ConstructionOwnerKey key, byte[] bytes)
    {
        Key = key;
        _bytes = bytes.ToArray();
        Hash = ContentHash.Compute(_bytes);
    }

    internal static ConstructionOwnerPayload Create(ConstructionOwnerKey key,
        OpenFantasy.Core.Terrain.TerrainScope scope, params byte[][] fields)
    {
        var header = new byte[][] { CanonicalEncodingV1.Unsigned64(1),
            CanonicalEncodingV1.Unsigned64((ulong)key.Kind), scope.WorldId.ToByteArray(),
            scope.BranchId.ToByteArray(), key.SubjectId.ToByteArray() };
        return new(key, CanonicalEncodingV1.Frame("world.construction-owner.v1",
            header.Concat(fields).Select(value => (ReadOnlyMemory<byte>)value).ToArray()));
    }

    public ConstructionOwnerKey Key { get; }
    public ContentHash Hash { get; }
    public int ByteLength => _bytes.Length;
    public byte[] CopyBytes() => _bytes.ToArray();
}
