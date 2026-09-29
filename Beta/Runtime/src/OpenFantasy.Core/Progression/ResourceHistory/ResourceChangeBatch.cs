using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Progression.ResourceHistory;

/// <summary>Ordered resource effects of one command; commands without resource revisions need no batch.</summary>
public sealed class ResourceChangeBatch
{
    public const int MaximumChanges = 128;
    private byte[]? _encodingV1;

    public ResourceChangeBatch(ulong worldSequence, uint commandOrdinal, EntityId commandId,
        IEnumerable<ActorResourceChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        List<ActorResourceChange> retained = [];
        foreach (var change in changes)
        {
            ArgumentNullException.ThrowIfNull(change);
            if (retained.Count == MaximumChanges || change.Ordinal != retained.Count)
                throw new ArgumentException("Resource changes require bounded, contiguous effect ordinals in their declared order.");
            retained.Add(change);
        }
        if (retained.Count == 0)
            throw new ArgumentException("A resource batch requires an actual actor revision.");
        WorldSequence = worldSequence;
        CommandOrdinal = commandOrdinal;
        CommandId = commandId;
        Changes = retained.AsReadOnly();
    }

    public ulong WorldSequence { get; }
    public uint CommandOrdinal { get; }
    public EntityId CommandId { get; }
    public ReadOnlyCollection<ActorResourceChange> Changes { get; }

    // The codec copies this exact immutable batch's bytes into the enclosing frame. It never
    // exposes the backing array, shares by command identity or retains an earlier history root.
    // Capture must remain a pure function of deeply immutable batch fields, independent of history context.
    internal ReadOnlyMemory<byte> EncodingV1 => Volatile.Read(ref _encodingV1) ?? CaptureEncodingV1();

    private byte[] CaptureEncodingV1()
    {
        byte[] encoded = ActorResourceHistoryCodecV1.EncodeBatch(this);
        return Interlocked.CompareExchange(ref _encodingV1, encoded, null) ?? encoded;
    }
}
