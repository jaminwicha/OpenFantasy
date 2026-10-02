using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Networking.Client;

public sealed partial class PlayerProtocolClient
{
    private ActionPending? _action;
    private QueryPending? _query;
    private Pending<bool>? _ping;

    /// <summary>
    /// One action may await authority while the world waits. Repeating its exact canonical command
    /// shares that task; a different concurrent action receives transient Busy feedback.
    /// </summary>
    public Task<WorldActionReceipt> SubmitActionAsync(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ActorId != Welcome.ActorId || command.PrincipalId != Welcome.Delivery.PrincipalId
            || command.OriginSessionId != Welcome.Delivery.SessionId
            || command.WorldId != Welcome.Compatibility.WorldId || command.BranchId != Welcome.Compatibility.BranchId)
            throw new ArgumentException("The command does not match the authenticated player binding.", nameof(command));
        byte[] bytes = WorldActionCodec.Encode(command);
        lock (_gate)
        {
            ThrowIfClosedLocked();
            if (_action is { } existing)
                return bytes.AsSpan().SequenceEqual(existing.Bytes) ? existing.Source.Task
                    : Task.FromException<WorldActionReceipt>(Busy());
            ulong id = EnqueueLocked(requestId => new PlayerMessage(PlayerMessageKind.Command, requestId, bytes));
            _action = new ActionPending(id, command, bytes);
            return _action.Source.Task;
        }
    }

    public Task<PlayerQueryResult> QueryAsync(PlayerQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        byte[] bytes = PlayerQueryCodec.Encode(query);
        lock (_gate)
        {
            ThrowIfClosedLocked();
            if (_query is not null) return Task.FromException<PlayerQueryResult>(Busy());
            ulong id = EnqueueLocked(requestId => new PlayerMessage(PlayerMessageKind.Query, requestId, bytes));
            _query = new QueryPending(id, query);
            return _query.Source.Task;
        }
    }

    public Task PingAsync()
    {
        lock (_gate)
        {
            ThrowIfClosedLocked();
            if (_ping is not null) return Task.FromException(Busy());
            ulong id = EnqueueLocked(requestId => new PlayerMessage(PlayerMessageKind.Ping, requestId, []));
            _ping = new Pending<bool>(id);
            return _ping.Source.Task;
        }
    }

    private static PlayerRequestRefusedException Busy() => new(PlayerRequestRefusal.Busy);

    private class Pending<T>(ulong requestId)
    {
        public ulong RequestId { get; } = requestId;
        public TaskCompletionSource<T> Source { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class ActionPending(ulong requestId, WorldActionCommand command, byte[] bytes)
        : Pending<WorldActionReceipt>(requestId)
    {
        public WorldActionCommand Command { get; } = command;
        public byte[] Bytes { get; } = bytes;
        public ContentHash Hash { get; } = ContentHash.Compute(bytes);
    }

    private sealed class QueryPending(ulong requestId, PlayerQuery query) : Pending<PlayerQueryResult>(requestId)
    {
        public PlayerQuery Query { get; } = query;
    }
}
