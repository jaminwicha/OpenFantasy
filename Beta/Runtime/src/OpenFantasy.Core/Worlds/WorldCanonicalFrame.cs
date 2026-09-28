using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// The canonical bytes of one exact immutable world root, captured once through the unchanged
/// snapshot encoder. A frame is serialization evidence only: it neither admits its world nor
/// authenticates history, retains no predecessor and can never be bound to another root.
/// </summary>
public sealed class WorldCanonicalFrame
{
    // Owned exclusively by this frame. Every export copies; no view shares this array.
    private readonly byte[] _bytes;
    private ContentHash? _contentHash;

    private WorldCanonicalFrame(WorldSnapshot world, byte[] bytes)
    {
        World = world;
        _bytes = bytes;
    }

    /// <summary>
    /// Encodes the root through the canonical snapshot encoder. Its envelope and profile refusals
    /// propagate unchanged, so a capture rejects exactly where the plain encoding would.
    /// </summary>
    public static WorldCanonicalFrame Capture(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return new WorldCanonicalFrame(world, WorldSnapshotCodec.Encode(world));
    }

    /// <summary>The exact root these bytes were encoded from.</summary>
    public WorldSnapshot World { get; }

    public int ByteLength => _bytes.Length;

    /// <summary>The digest of the retained bytes; exported copies cannot influence it.</summary>
    public ContentHash ContentHash
    {
        get
        {
            ContentHash? cached = Volatile.Read(ref _contentHash);
            if (cached is not null) return cached;
            ContentHash computed = ContentHash.Compute(_bytes);
            // Concurrent first readers may compute independently; publish one complete immutable digest.
            return Interlocked.CompareExchange(ref _contentHash, computed, null) ?? computed;
        }
    }

    /// <summary>Binding is the identity of the immutable root, never equality of selected fields or hashes.</summary>
    public bool IsBoundTo(WorldSnapshot candidate) => ReferenceEquals(World, candidate);

    /// <summary>Returns a caller-owned copy. Changing it cannot affect this frame or a later export.</summary>
    public byte[] ToArray() => (byte[])_bytes.Clone();

    /// <summary>Compares exact canonical bytes without exporting them.</summary>
    public bool Matches(ReadOnlySpan<byte> bytes) => _bytes.AsSpan().SequenceEqual(bytes);
}
