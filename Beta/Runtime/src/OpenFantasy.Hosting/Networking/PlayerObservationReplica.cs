using OpenFantasy.Core;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking;

public enum PlayerObservationApplyResult { Applied, Unchanged, RequiresFull }

/// <summary>One immutable presentation value and its connection-local version, published together.</summary>
public sealed class PlayerObservationSnapshot
{
    private readonly byte[] _bytes;
    internal PlayerObservationSnapshot(EntityId streamId, ulong revision, LocalWorldView view, byte[] bytes)
    {
        StreamId = streamId;
        Revision = revision;
        View = view;
        _bytes = bytes;
    }
    public EntityId StreamId { get; }
    public ulong Revision { get; }
    public LocalWorldView View { get; }
    internal ReadOnlySpan<byte> Bytes => _bytes;
}

/// <summary>One receiver publishes immutable views. Invalid/incomplete updates retain the last good snapshot.</summary>
public sealed class PlayerObservationReplica(EntityId worldId, EntityId branchId, EntityId actorId)
{
    private PlayerObservationSnapshot? _current;
    private int _applying;
    public ulong Revision => ReadSnapshot()?.Revision ?? 0;
    public LocalWorldView? ReadView() => ReadSnapshot()?.View;
    public PlayerObservationSnapshot? ReadSnapshot() => Volatile.Read(ref _current);

    public PlayerObservationApplyResult Apply(PlayerObservationFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (Interlocked.CompareExchange(ref _applying, 1, 0) != 0)
            throw new InvalidOperationException("Only one observation receiver may publish at a time.");
        try { return ApplyComplete(frame); }
        finally { Volatile.Write(ref _applying, 0); }
    }

    private PlayerObservationApplyResult ApplyComplete(PlayerObservationFrame frame)
    {
        PlayerObservationSnapshot? current = ReadSnapshot();
        if (current is not null && frame.StreamId != current.StreamId)
            throw new FormatException("An observation belongs to another connection stream.");
        ulong revision = current?.Revision ?? 0;
        if (frame.Revision < revision) throw new FormatException("A stale observation cannot replace a newer view.");
        if (!frame.IsFull && (current is null || frame.BaseRevision != revision)) return PlayerObservationApplyResult.RequiresFull;
        byte[] bytes = frame.Reconstruct(current is null ? [] : current.Bytes);
        LocalWorldView view = PlayerViewCodec.Decode(bytes);
        if (view.WorldId != worldId || view.BranchId != branchId || view.PlayerActorId != actorId)
            throw new FormatException("The observation does not match the authenticated player scope.");
        if (current is not null && frame.Revision == revision)
        {
            if (!bytes.AsSpan().SequenceEqual(current.Bytes)) throw new FormatException("Conflicting observation contents at one revision.");
            return PlayerObservationApplyResult.Unchanged;
        }
        Volatile.Write(ref _current, new PlayerObservationSnapshot(frame.StreamId, frame.Revision, view, bytes));
        return PlayerObservationApplyResult.Applied;
    }
}
