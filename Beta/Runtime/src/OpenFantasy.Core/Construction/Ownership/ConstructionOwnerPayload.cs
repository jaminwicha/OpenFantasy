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

    public ConstructionOwnerKey Key { get; }
    public ContentHash Hash { get; }
    public int ByteLength => _bytes.Length;
    public byte[] CopyBytes() => _bytes.ToArray();
}
