using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>The local host's current scheduling policy, independent of which client panel is visible.</summary>
public enum LocalWorldRunState { Unavailable, Paused, Waiting, Running }

/// <summary>
/// Owns one durable local authority and its fixed-rate input cutoff. Authentication is the admitted
/// local-host profile; network authentication and global visibility require separate host adapters.
/// </summary>
public sealed class LocalWorldSession : IAsyncDisposable
{
    public static readonly TimeSpan StepInterval = TimeSpan.FromMilliseconds(50);
    public static readonly TimeSpan MovementLease = TimeSpan.FromMilliseconds(150);
    private readonly object _inputs = new();
    private readonly SemaphoreSlim _serialization = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private readonly SqliteWorldAuthority _authority;
    private readonly EntityId _playerActorId;
    private readonly TrustedCommandDelivery _delivery;
    private readonly TimeProvider _time;
    private readonly LocalNpcController _npcs = new();
    private readonly Task _loop;
    private LocalWorldView _view;
    private PendingLocalAction? _pending;
    private Exception? _failure;
    private Task? _stopping;
    private bool _ready;
    private bool _stopRequested;
    private long _movementX;
    private long _movementY;
    private long _movementAt;

    public LocalWorldSession(SqliteWorldAuthority authority, EntityId playerActorId, TrustedCommandDelivery delivery)
        : this(authority, playerActorId, delivery, TimeProvider.System) { }

    internal LocalWorldSession(SqliteWorldAuthority authority, EntityId playerActorId,
        TrustedCommandDelivery delivery, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(timeProvider);
        WorldSnapshot state = authority.ReadStateForHost();
        if (!state.Inventory.CanControl(delivery, playerActorId)
            || state.GetResident(playerActorId).ControllerKind != ResidentControllerKind.Player)
        {
            throw new AuthorityDispatchException("The local session requires its admitted player and current delivery binding.");
        }
        _authority = authority;
        _playerActorId = playerActorId;
        _delivery = delivery;
        _time = timeProvider;
        _view = new LocalWorldView(state, playerActorId);
        _loop = Task.Run(RunAsync);
    }

    /// <summary>Faults on storage, recovery or host-loop failure; input rejection is reported by its own task.</summary>
    public Task Completion => _loop;

    public LocalWorldView ReadView() => Volatile.Read(ref _view);

    public LocalWorldRunState ReadRunState()
    {
        lock (_inputs)
        {
            if (_failure is not null || _stopRequested) return LocalWorldRunState.Unavailable;
            if (!_ready) return LocalWorldRunState.Paused;
            return _view.HasReadingHold ? LocalWorldRunState.Waiting : LocalWorldRunState.Running;
        }
    }

    public void SetReady(bool ready)
    {
        lock (_inputs)
        {
            EnsureInputAvailable();
            _ready = ready;
            if (!ready)
            {
                _movementX = 0;
                _movementY = 0;
            }
        }
    }

    public void SetMovement(long dx, long dy)
    {
        bool cardinal = ((dx == 32 || dx == -32) && dy == 0) || ((dy == 32 || dy == -32) && dx == 0);
        if (!cardinal && (dx != 0 || dy != 0))
        {
            throw new ArgumentException("Movement selects one fixed cardinal sample, or zero to stop.");
        }
        lock (_inputs)
        {
            EnsureInputAvailable();
            _movementX = _ready ? dx : 0;
            _movementY = _ready ? dy : 0;
            _movementAt = _time.GetTimestamp();
        }
    }

    public async Task<WorldActionReceipt> SubmitAction(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        byte[] encoded = WorldActionCodec.Encode(command);
        Task<WorldActionReceipt> result;
        await _serialization.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_inputs) { EnsureInputAvailable(); }
            if (command.ActorId != _playerActorId)
            {
                throw new AuthorityDispatchException("This session accepts only its admitted player's actions.");
            }
            WorldActionReceipt? prior;
            try
            {
                prior = _authority.FindReceipt(command, _delivery);
            }
            catch (AuthorityDispatchException) { throw; }
            catch (Exception error) { Fail(error); throw; }
            if (prior is not null)
            {
                return prior;
            }
            if (command.Intent is CraftIntent && _authority.InspectForHost().State.ProfileVersion < 3)
            {
                throw new AuthorityDispatchException("This saved rules profile does not admit crafting actions.");
            }
            if (command.Intent is OpenFantasy.Core.Quests.DeliveryQuestIntent
                && _authority.InspectForHost().State.ProfileVersion < 4)
            {
                throw new AuthorityDispatchException("This saved profile does not support quest actions.");
            }
            if (command.Intent is OpenFantasy.Core.Spatial.ClimbIntent
                && _authority.ReadStateForHost().ProfileVersion is not 11 and not 12)
            {
                throw new AuthorityDispatchException("This saved profile does not support assisted climbing.");
            }
            if (command.Intent is OpenFantasy.Core.Terrain.ExcavateIntent
                && _authority.ReadStateForHost().ProfileVersion is not 10 and not 11 and not 12)
            {
                throw new AuthorityDispatchException("This saved profile does not support excavation.");
            }
            if (command.Intent is OpenFantasy.Core.Geography.ScheduleRunoffTransportIntent
                && _authority.ReadStateForHost().ProfileVersion != 9)
            {
                throw new AuthorityDispatchException("This saved profile does not support scheduled reservoir transport.");
            }
            if (command.Intent is OpenFantasy.Core.Geography.RunoffCommitmentIntent
                && _authority.ReadStateForHost().ProfileVersion is not 8 and not 9)
            {
                throw new AuthorityDispatchException("This saved profile does not support runoff commitments.");
            }
            if (command.Intent is WorkConstructionIntent && _authority.ReadStateForHost().ProfileVersion < 5)
            {
                throw new AuthorityDispatchException("This saved profile does not support construction work.");
            }
            lock (_inputs)
            {
                EnsureInputAvailable();
                if (_pending is { } existing)
                {
                    if (!existing.Bytes.AsSpan().SequenceEqual(encoded))
                    {
                        throw new AuthorityDispatchException("One actor action is already pending; it has not been acknowledged as durable.");
                    }
                    result = existing.Completion.Task;
                }
                else
                {
                    if (!_ready)
                    {
                        throw new AuthorityDispatchException("The local session is not ready to select a new action.");
                    }
                    // The check runs under the serialized owner, before this request joins a batch.
                    bool occupied;
                    try { occupied = _authority.IsIdentityOccupiedForHost(command.CommandId); }
                    catch (Exception error) { Fail(error); throw; }
                    if (occupied)
                    {
                        throw new AuthorityDispatchException("The command identity is already occupied.");
                    }
                    _pending = new PendingLocalAction(command, encoded);
                    result = _pending.Completion.Task;
                }
            }
        }
        finally { _serialization.Release(); }
        return await result.ConfigureAwait(false);
    }

    public Task StopAsync()
    {
        lock (_inputs)
        {
            return _stopping ??= StopCoreAsync();
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private async Task RunAsync()
    {
        long interval = checked((long)(StepInterval.TotalSeconds * _time.TimestampFrequency));
        long next = checked(_time.GetTimestamp() + interval);
        try
        {
            while (true)
            {
                long remaining = next - _time.GetTimestamp();
                if (remaining > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds((double)remaining / _time.TimestampFrequency), _time, _stop.Token)
                        .ConfigureAwait(false);
                }
                _stop.Token.ThrowIfCancellationRequested();
                await TickAsync().ConfigureAwait(false);
                long now = _time.GetTimestamp();
                next = checked(next + interval);
                if (next <= now)
                {
                    // Missed host deadlines are discarded; no catch-up simulation or offline time.
                    next = checked(now + interval);
                }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception error) { Fail(error); throw; }
        finally
        {
            lock (_inputs)
            {
                if (_failure is { } failure) { _pending?.Completion.TrySetException(failure); }
                else { _pending?.Completion.TrySetCanceled(_stop.Token); }
                _pending = null;
            }
        }
        Exception? finalFailure;
        lock (_inputs) { finalFailure = _failure; }
        if (finalFailure is not null) { ExceptionDispatchInfo.Capture(finalFailure).Throw(); }
    }

    private async Task TickAsync()
    {
        await _serialization.WaitAsync(_stop.Token).ConfigureAwait(false);
        try
        {
            PendingLocalAction? pending;
            long x;
            long y;
            lock (_inputs)
            {
                if (!_ready || _stopRequested) { return; }
                pending = _pending;
                bool fresh = _time.GetElapsedTime(_movementAt, _time.GetTimestamp()) <= MovementLease;
                x = fresh ? _movementX : 0;
                y = fresh ? _movementY : 0;
            }
            WorldSnapshot state = _authority.ReadStateForHost();
            if (state.HasReadingHold && pending is null) { return; }
            List<WorldMovementInput> movement = [];
            if (!state.HasReadingHold)
            {
                movement.AddRange(_npcs.SelectMovement(state));
                if (x != 0 || y != 0) { movement.Add(new WorldMovementInput(_delivery, _playerActorId, x, y)); }
            }
            WorldStepBatch? batch = SelectAdmissibleBatch(state, ref pending, movement);
            if (batch is null)
            {
                return;
            }
            WorldStepDecision decision = _authority.Commit(batch);
            Volatile.Write(ref _view, new LocalWorldView(decision.Candidate, _playerActorId));
            if (pending is not null)
            {
                WorldActionReceipt receipt = decision.Receipts.Single(value => value.CommandId == pending.Command.CommandId);
                CompletePending(pending, receipt, null);
            }
        }
        finally { _serialization.Release(); }
    }

    private WorldStepBatch? SelectAdmissibleBatch(WorldSnapshot state, ref PendingLocalAction? pending,
        IReadOnlyList<WorldMovementInput> movement)
    {
        WorldStepBatch batch = SelectBatch(state, pending, movement, includeNpcActions: true);
        try
        {
            if (batch.Actions.Count != 0)
            {
                _authority.Preview(batch);
            }
            return batch;
        }
        catch (AuthorityDispatchException)
        {
            // A conflicting NPC proposal must not reject a valid player request.
            batch = SelectBatch(state, pending, movement, includeNpcActions: false);
        }
        try
        {
            if (pending is not null)
            {
                _authority.Preview(batch);
            }
            return batch;
        }
        catch (AuthorityDispatchException rejected)
        {
            if (pending is null)
            {
                throw;
            }
            CompletePending(pending, null, rejected);
            pending = null;
            return state.HasReadingHold ? null : SelectBatch(state, null, movement, includeNpcActions: false);
        }
    }

    private WorldStepBatch SelectBatch(WorldSnapshot state, PendingLocalAction? pending,
        IEnumerable<WorldMovementInput> movement, bool includeNpcActions)
    {
        EntityId cause = NewCause(pending?.Command.CommandId);
        List<WorldActionCommand> actions = pending is null ? [] : [pending.Command];
        if (includeNpcActions && !state.HasReadingHold)
        {
            HashSet<EntityId> selected = [cause];
            if (pending is not null)
            {
                selected.Add(pending.Command.CommandId);
            }
            actions.AddRange(_npcs.SelectActions(state, () => AllocateSelectedIdentity(selected)));
        }
        return new WorldStepBatch(cause, state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
            state.HasReadingHold ? WorldBatchKind.HeldInteraction : WorldBatchKind.AdvancingStep,
            movement, actions, state.ProfileVersion);
    }

    private EntityId AllocateSelectedIdentity(HashSet<EntityId> selected)
    {
        for (int attempt = 0; attempt < 16; attempt++)
        {
            EntityId identity = NewCause(null);
            if (selected.Add(identity))
            {
                return identity;
            }
        }
        throw new InvalidOperationException("The host could not allocate a distinct actor action identity.");
    }

    private EntityId NewCause(EntityId? commandId)
    {
        Span<byte> bytes = stackalloc byte[EntityId.ByteLength];
        for (int attempt = 0; attempt < 16; attempt++)
        {
            RandomNumberGenerator.Fill(bytes);
            EntityId id = EntityId.FromBytes(bytes);
            if (id != commandId && !_authority.IsIdentityOccupiedForHost(id)) { return id; }
        }
        throw new InvalidOperationException("The host could not allocate an unoccupied commit cause.");
    }

    private void CompletePending(PendingLocalAction pending, WorldActionReceipt? receipt, Exception? error)
    {
        lock (_inputs)
        {
            if (ReferenceEquals(_pending, pending)) { _pending = null; }
        }
        if (error is not null) { pending.Completion.TrySetException(error); }
        else { pending.Completion.TrySetResult(receipt!); }
    }

    private void Fail(Exception error)
    {
        lock (_inputs)
        {
            _failure ??= error;
            _ready = false;
            _movementX = 0;
            _movementY = 0;
            _pending?.Completion.TrySetException(_failure);
        }
        _stop.Cancel();
    }

    private void EnsureInputAvailable()
    {
        ObjectDisposedException.ThrowIf(_stopRequested, this);
        if (_failure is not null) { throw new InvalidOperationException("The local session has failed.", _failure); }
    }

    private async Task StopCoreAsync()
    {
        _stopRequested = true;
        _ready = false;
        _movementX = 0;
        _movementY = 0;
        _stop.Cancel();
        try { await _loop.ConfigureAwait(false); }
        finally
        {
            await _serialization.WaitAsync().ConfigureAwait(false);
            try { _authority.Dispose(); }
            finally { _serialization.Release(); }
        }
    }
}
