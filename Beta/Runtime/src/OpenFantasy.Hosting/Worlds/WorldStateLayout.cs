using System.Buffers.Binary;
using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>
/// Read-only accounting of actual top-level canonical fields. Field reuse is a byte measurement,
/// not proof of independent ownership, partial admission, a new storage format or saved I/O.
/// </summary>
internal sealed class WorldStateLayout
{
    private const int HeaderBytes = 12;
    private const int FieldLengthBytes = sizeof(uint);
    private readonly byte[] _bytes;
    private readonly (int Offset, int Length)[] _ranges;

    internal WorldStateLayout(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        WorldId = world.WorldId;
        BranchId = world.BranchId;
        ProfileVersion = world.ProfileVersion;
        Revision = world.Revision;
        _bytes = WorldSnapshotCodec.Encode(world);
        if (_bytes.Length < HeaderBytes || !_bytes.AsSpan(0, 4).SequenceEqual("OFC\0"u8)
            || BinaryPrimitives.ReadUInt32BigEndian(_bytes.AsSpan(4)) != 1)
        {
            throw new InvalidDataException("Layout accounting requires canonical framing version one.");
        }
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(_bytes.AsSpan(8)));
        _ranges = new (int, int)[count];
        var fields = new List<WorldFieldFootprint>(count);
        int offset = HeaderBytes;
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(_bytes.AsSpan(offset)));
            offset = checked(offset + FieldLengthBytes);
            _ranges[index] = (offset, length);
            fields.Add(new(index, length, ContentHash.Compute(_bytes.AsSpan(offset, length))));
            offset = checked(offset + length);
        }
        if (offset != _bytes.Length)
        {
            throw new InvalidDataException("Canonical layout accounting did not cover the complete root.");
        }
        Fields = fields.AsReadOnly();
        FramingBytes = checked(HeaderBytes + count * FieldLengthBytes);
        ContentHash = ContentHash.Compute(_bytes);
    }

    internal EntityId WorldId { get; }
    internal EntityId BranchId { get; }
    internal ushort ProfileVersion { get; }
    internal ulong Revision { get; }
    internal int ByteLength => _bytes.Length;
    internal int FramingBytes { get; }
    internal ContentHash ContentHash { get; }
    internal ReadOnlyCollection<WorldFieldFootprint> Fields { get; }

    /// <summary>
    /// Compares exact fields at identical positions in the same interpretation and world branch.
    /// Counts exclude hypothetical references, indexes, metadata, compression and transaction costs.
    /// </summary>
    internal WorldFieldReuse CompareTo(WorldStateLayout previous)
    {
        ArgumentNullException.ThrowIfNull(previous);
        if (WorldId != previous.WorldId || BranchId != previous.BranchId
            || ProfileVersion != previous.ProfileVersion || _ranges.Length != previous._ranges.Length)
        {
            throw new ArgumentException("Field reuse requires the same world, branch and root interpretation.", nameof(previous));
        }
        var changed = new List<int>();
        int retainedBytes = 0;
        int changedBytes = 0;
        for (int index = 0; index < _ranges.Length; index++)
        {
            var current = _ranges[index];
            var old = previous._ranges[index];
            if (_bytes.AsSpan(current.Offset, current.Length)
                .SequenceEqual(previous._bytes.AsSpan(old.Offset, old.Length)))
            {
                retainedBytes = checked(retainedBytes + current.Length);
            }
            else
            {
                changed.Add(index);
                changedBytes = checked(changedBytes + current.Length);
            }
        }
        return new(retainedBytes, changedBytes, changed.AsReadOnly());
    }
}

internal sealed record WorldFieldFootprint(int Index, int PayloadBytes, ContentHash ContentHash);
internal sealed record WorldFieldReuse(int IdenticalPayloadBytes, int ChangedPayloadBytes,
    ReadOnlyCollection<int> ChangedFields);
