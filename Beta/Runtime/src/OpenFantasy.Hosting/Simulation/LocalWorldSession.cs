using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>The local host's current scheduling policy, independent of which client panel is visible.</summary>
public enum LocalWorldRunState { Unavailable, Paused, Waiting, Running, Resolving }

/// <summary>
/// Owns one durable local authority and its fixed-rate input cutoff. Authentication is the admitted
/// local-host profile; network authentication and global visibility require separate host adapters.
/// </summary>
public sealed partial class LocalWorldSession : IAsyncDisposable
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
    private readonly LocalCareController _care = new();
    private readonly LocalRestorativeController _restorative = new();
    private readonly LocalMaterialProposalSelection _materialProposals = new();
    private readonly LocalMaterialGiftController _materialGifts;
    private readonly LocalTradingController _trading;
    private readonly Task _loop;
    private LocalWorldView _view;
    private ContentHash _projectedStateHash;
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

    public LocalWorldSession(SqliteWorldAuthority authority, EntityId playerActorId, TrustedCommandDelivery delivery,
        IEnumerable<LocalMaterialGiftPolicy> giftPolicies)
        : this(authority, playerActorId, delivery, TimeProvider.System, giftPolicies) { }

    /// <summary>A local-market session with explicit opt-in host merchants and shoppers, resupplied on every reopening.</summary>
    public LocalWorldSession(SqliteWorldAuthority authority, EntityId playerActorId, TrustedCommandDelivery delivery,
        LocalTradingConfiguration trading)
        : this(authority, playerActorId, delivery, TimeProvider.System, null, trading) { }

    /// <summary>A session with explicit NPC gift preferences and opt-in trading together, both resupplied on every reopening.</summary>
    public LocalWorldSession(SqliteWorldAuthority authority, EntityId playerActorId, TrustedCommandDelivery delivery,
        IEnumerable<LocalMaterialGiftPolicy> giftPolicies, LocalTradingConfiguration trading)
        : this(authority, playerActorId, delivery, TimeProvider.System, giftPolicies, trading) { }

    internal LocalWorldSession(SqliteWorldAuthority authority, EntityId playerActorId,
        TrustedCommandDelivery delivery, TimeProvider timeProvider, IEnumerable<LocalMaterialGiftPolicy>? giftPolicies = null,
        LocalTradingConfiguration? trading = null)
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
        _materialGifts = new LocalMaterialGiftController(state, giftPolicies ?? []);
        _trading = new LocalTradingController(state, playerActorId, trading ?? LocalTradingConfiguration.None);
        _view = ProjectView(state);
        _projectedStateHash = state.ContentHash;
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
            if (_view.IsResolvingConsequences) return LocalWorldRunState.Resolving;
            if (!_ready) return LocalWorldRunState.Paused;
            return _view.HasReadingHold || _view.Encounter?.HasRequiredDecisions == true || _view.Encounter?.IsOutcome == true
                ? LocalWorldRunState.Waiting : LocalWorldRunState.Running;
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
            bool acceptsMovement = _ready && !_view.IsResolvingConsequences && !_view.IsInEncounter;
            _movementX = acceptsMovement ? dx : 0;
            _movementY = acceptsMovement ? dy : 0;
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
            WorldSnapshot state = _authority.ReadStateForHost();
            _materialProposals.Validate(state, command);
            ValidateTradeSelection(state, command);
            ValidateEncounterSelection(state, command);
            bool durablePending = state.PendingExcavation is not null;
            if (state.PendingExcavation is { } accepted
                && !WorldActionCodec.Encode(accepted.Command).AsSpan().SequenceEqual(encoded))
            {
                throw new AuthorityDispatchException("The world must finish its retained consequence command before selecting another action.");
            }
            if (command.Intent is CraftIntent && state.ProfileVersion < 3)
            {
                throw new AuthorityDispatchException("This saved rules profile does not admit crafting actions.");
            }
            if (command.Intent is OpenFantasy.Core.Quests.DeliveryQuestIntent
                && state.ProfileVersion < 4)
            {
                throw new AuthorityDispatchException("This saved profile does not support quest actions.");
            }
            if (command.Intent is OpenFantasy.Core.Spatial.ClimbIntent
                && state.ProfileVersion is not 11 and not 12 and not 13 and not 14 and not 15 and not 16 and not 17 and not 18)
            {
                throw new AuthorityDispatchException("This saved profile does not support assisted climbing.");
            }
            if (command.Intent is ExcavateIntent && !LocalProfileBindings.AdmitsExcavation(state))
            {
                throw new AuthorityDispatchException("This saved profile does not support excavation.");
            }
            if (command.Intent is OpenFantasy.Core.Geography.ScheduleRunoffTransportIntent
                && state.ProfileVersion != 9)
            {
                throw new AuthorityDispatchException("This saved profile does not support scheduled reservoir transport.");
            }
            if (command.Intent is OpenFantasy.Core.Geography.RunoffCommitmentIntent
                && state.ProfileVersion is not 8 and not 9 and not 19)
            {
                throw new AuthorityDispatchException("This saved profile does not support runoff commitments.");
            }
            if (command.Intent is WorkConstructionIntent && state.ProfileVersion < 5)
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
                        throw new AuthorityDispatchException("One actor action is awaiting its terminal result.");
                    }
                    result = existing.Completion.Task;
                }
                else
                {
                    if (!_ready && !durablePending)
                    {
                        throw new AuthorityDispatchException("The local session is not ready to select a new action.");
                    }
                    // The check runs under the serialized owner, before this request joins a batch.
                    if (!durablePending)
                    {
                        bool occupied;
                        try { occupied = _authority.IsIdentityOccupiedForHost(command.CommandId); }
                        catch (Exception error) { Fail(error); throw; }
                        if (occupied)
                        {
                            throw new AuthorityDispatchException("The command identity is already occupied.");
                        }
                    }
                    _pending = new PendingLocalAction(command, encoded);
                    result = _pending.Completion.Task;
                }
            }
        }
        finally { _serialization.Release(); }
        return await result.ConfigureAwait(false);
    }

    private static void ValidateEncounterSelection(WorldSnapshot state, WorldActionCommand command)
    {
        if (command.Intent is UseRestorativeItemIntent or RevokeCarePermissionIntent
            && (!LocalProfileBindings.AdmitsOrdinaryCare(state) || state.HasReadingHold || state.PendingExcavation is not null
                || state.EncounterLifecycle?.Phase != EncounterLifecyclePhase.Exploration || state.Care?.Pending is not null))
        {
            throw new AuthorityDispatchException("Ordinary care requires its admitted unheld exploration boundary.");
        }
        if (command.Intent is AdministerRemedyIntent)
        {
            throw new AuthorityDispatchException("This local adapter selects treatment through the admitted host helper.");
        }
        if (command.Intent is AcknowledgeCareIntent acknowledgment)
        {
            if (state.Care is null || state.HasReadingHold || state.PendingExcavation is not null)
                throw new AuthorityDispatchException("Care acknowledgment requires its available care boundary.");
            var agreement = state.Care.Agreements.SingleOrDefault(value => value.AgreementId == acknowledgment.AgreementId);
            if (agreement is not null && LocalCareController.HostControl(state, agreement.HelperActorId) is null)
                throw new AuthorityDispatchException("This local care route requires an admitted host helper.");
            // Stale phase, revision and agreement choices still receive durable terminal receipts.
            return;
        }
        if (state.Care?.Pending is not null)
        {
            throw new AuthorityDispatchException("The retained care obligation excludes other local actions.");
        }
        if (command.Intent is EnterEncounterIntent or ReleaseEncounterIntent)
        {
            if (state.EncounterLifecycle is null || state.HasReadingHold)
                throw new AuthorityDispatchException("A lifecycle choice requires its admitted world without a reading hold.");
            // Exact phase, target, identity and captured revisions receive normal durable receipts.
            return;
        }
        if (state.EncounterLifecycle?.Phase is EncounterLifecyclePhase.Confrontation or EncounterLifecyclePhase.Staging
            or EncounterLifecyclePhase.Outcome)
            throw new AuthorityDispatchException("This lifecycle boundary accepts only entry or release requests.");
        if (state.Pacing?.Encounter is not null && command.Intent is not QueueEncounterWaitIntent and not QueueEncounterAbilityIntent)
            throw new AuthorityDispatchException("An active encounter accepts only its retained encounter choices.");
        if (command.Intent is QueueEncounterWaitIntent or QueueEncounterAbilityIntent && state.Pacing?.Encounter is null)
            throw new AuthorityDispatchException("An encounter choice requires an active admitted encounter.");
        if (command.Intent is QueueEncounterAbilityIntent && state.EncounterActions is null || WorldEncounterActions.IsOutcome(state))
            throw new AuthorityDispatchException("This encounter does not admit another ability or timing action.");
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
            lock (_inputs)
            {
                if (_stopRequested) { return; }
            }
            WorldSnapshot state = _authority.ReadStateForHost();
            PublishViewIfChanged(state);
            if (state.PendingExcavation is not null)
            {
                ContinueConsequences(state);
                return;
            }
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
            if (pending?.Command.Intent is AcknowledgeCareIntent || state.Care?.Pending is not null)
            {
                TickCare(state, pending);
                return;
            }
            if (pending?.Command.Intent is EnterEncounterIntent or ReleaseEncounterIntent
                || state.EncounterLifecycle?.Phase is EncounterLifecyclePhase.Confrontation or EncounterLifecyclePhase.Staging)
            {
                TickLifecycle(state, pending);
                return;
            }
            if (state.Pacing?.Encounter is not null)
            {
                TickEncounter(state, pending);
                return;
            }
            RefuseUnrepresentable(state, state.HasReadingHold ? WorldBatchKind.HeldInteraction : WorldBatchKind.AdvancingStep,
                ref pending);
            if (state.HasReadingHold && pending is null) { return; }
            List<WorldMovementInput> movement = [];
            if (SelectsConsequenceTrigger(state, pending))
            {
                ClearMovement();
            }
            else if (!state.HasReadingHold)
            {
                movement.AddRange(_npcs.SelectMovement(state));
                if (x != 0 || y != 0) { movement.Add(new WorldMovementInput(_delivery, _playerActorId, x, y)); }
            }
            WorldStepBatch? batch = SelectAdmissibleBatch(state, ref pending, movement);
            if (batch is null)
            {
                return;
            }
            CommitSelectedBatch(batch, pending);
        }
        finally { _serialization.Release(); }
    }

    private void TickCare(WorldSnapshot state, PendingLocalAction? pending)
    {
        ClearMovement();
        RefuseUnrepresentable(state, WorldBatchKind.Care, ref pending);
        WorldActionCommand? command = pending?.Command
            ?? _care.SelectTreatment(state, _authority.IsIdentityOccupiedForHost);
        if (command is null)
        {
            return;
        }
        var batch = new WorldStepBatch(NewCause(command.CommandId), state.WorldId, state.BranchId,
            state.Revision, state.AdvancingSteps, WorldBatchKind.Care, [], [command], state.ProfileVersion);
        try { _authority.Preview(batch); }
        catch (AuthorityDispatchException error) when (pending is not null)
        {
            CompletePending(pending, null, error);
            return;
        }
        // Acknowledgment and helper commitment occupy distinct serialized cutoffs and receipts.
        CommitSelectedBatch(batch, pending);
    }

    private void TickLifecycle(WorldSnapshot state, PendingLocalAction? pending)
    {
        ClearMovement();
        if (RefuseUnrepresentable(state, WorldBatchKind.EncounterLifecycle, ref pending)
            && state.EncounterLifecycle?.Phase is not (EncounterLifecyclePhase.Confrontation or EncounterLifecyclePhase.Staging))
        {
            return;
        }
        var batch = new WorldStepBatch(NewCause(pending?.Command.CommandId), state.WorldId, state.BranchId,
            state.Revision, state.AdvancingSteps, WorldBatchKind.EncounterLifecycle, [],
            pending is null ? [] : [pending.Command], state.ProfileVersion);
        try { _authority.Preview(batch); }
        catch (AuthorityDispatchException error) when (pending is not null)
        {
            CompletePending(pending, null, error);
            return;
        }
        CommitSelectedBatch(batch, pending);
    }

    private void TickEncounter(WorldSnapshot state, PendingLocalAction? pending)
    {
        ClearMovement();
        if (WorldEncounterActions.IsOutcome(state)) return;
        RefuseUnrepresentable(state, WorldBatchKind.EncounterDecision, ref pending);
        var timing = state.Pacing!.Encounter!;
        EntityId cause = NewCause(pending?.Command.CommandId);
        HashSet<EntityId> selected = [cause];
        List<WorldActionCommand> actions = [];
        if (pending is not null)
        {
            selected.Add(pending.Command.CommandId);
            actions.Add(pending.Command);
        }
        actions.AddRange(_npcs.SelectEncounterChoices(state, () => AllocateSelectedIdentity(selected)));
        WorldBatchKind kind;
        if (actions.Count != 0)
        {
            kind = WorldBatchKind.EncounterDecision;
        }
        else if (WorldEncounterActions.HasRequiredDecisions(state))
        {
            return;
        }
        else
        {
            kind = WorldEncounterActions.DueActors(state).Count == 0 ? WorldBatchKind.TacticalAdvance : WorldBatchKind.EncounterResolution;
        }
        var batch = new WorldStepBatch(cause, state.WorldId, state.BranchId, state.Revision,
            state.AdvancingSteps, kind, [], actions, state.ProfileVersion);
        try
        {
            _authority.Preview(batch);
        }
        catch (AuthorityDispatchException error) when (pending is not null)
        {
            // A refused selected request leaves this encounter at the same boundary. It cannot
            // select ordinary movement, work or exploration time as a fallback.
            CompletePending(pending, null, error);
            return;
        }
        CommitSelectedBatch(batch, pending);
    }

    private void ContinueConsequences(WorldSnapshot state)
    {
        PendingLocalAction? awaiting;
        lock (_inputs)
        {
            if (_stopRequested) { return; }
            _movementX = 0;
            _movementY = 0;
            awaiting = _pending;
        }
        if (awaiting is not null && !WorldActionCodec.Encode(state.PendingExcavation!.Command).AsSpan()
            .SequenceEqual(awaiting.Bytes))
        {
            throw new InvalidDataException("The local waiter differs from the retained consequence command.");
        }
        var batch = new WorldStepBatch(NewCause(null), state.WorldId, state.BranchId, state.Revision,
            state.AdvancingSteps, WorldBatchKind.ConsequenceContinuation, [], [], state.ProfileVersion);
        CommitSelectedBatch(batch, awaiting);
    }

    private void CommitSelectedBatch(WorldStepBatch batch, PendingLocalAction? awaiting)
    {
        WorldStepDecision decision = _authority.Commit(batch);
        // Only a committed receipt turns a merchant's selected answer into an offer.
        _trading.Observe(decision);
        PublishViewIfChanged(decision.Candidate);
        if (decision.Candidate.PendingExcavation is { } pending)
        {
            ClearMovement();
            if (awaiting is not null && !WorldActionCodec.Encode(pending.Command).AsSpan().SequenceEqual(awaiting.Bytes))
            {
                throw new InvalidDataException("The committed consequence command differs from the local waiter.");
            }
        }
        else if (awaiting is not null)
        {
            WorldActionReceipt receipt = decision.Receipts.Single(value => value.CommandId == awaiting.Command.CommandId);
            CompletePending(awaiting, receipt, null);
        }
    }

    private LocalWorldView ProjectView(WorldSnapshot state) => new(state, _playerActorId,
        state.Care?.Pending is not null && _authority.IsIdentityOccupiedForHost(LocalCareController.SelectionId(state)));

    private void ClearMovement()
    {
        lock (_inputs)
        {
            _movementX = 0;
            _movementY = 0;
        }
    }

    private static bool SelectsConsequenceTrigger(WorldSnapshot state, PendingLocalAction? pending) =>
        state.ConsequencePolicy is not null && !state.HasReadingHold && pending?.Command.Intent is ExcavateIntent;

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
        if (SelectsConsequenceTrigger(state, pending))
        {
            // Select the isolated trigger before asking NPCs for any competing work or movement.
            return new WorldStepBatch(cause, state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
                WorldBatchKind.AdvancingStep, [], [pending!.Command], state.ProfileVersion);
        }
        List<WorldActionCommand> actions = pending is null ? [] : [pending.Command];
        // A valid paid acceptance spends both participants' opportunity: neither works nor walks in its step.
        HashSet<EntityId> settling = SettlingParticipants(state, cause, pending?.Command);
        if (includeNpcActions && !state.HasReadingHold)
        {
            HashSet<EntityId> selected = [cause];
            if (pending is not null)
            {
                selected.Add(pending.Command.CommandId);
            }
            var excluded = actions.Select(action => action.ActorId).ToHashSet();
            if (_materialProposals.FindReservedSender(_authority, state, cause, pending?.Command) is { } sender)
            {
                excluded.Add(sender);
            }
            excluded.UnionWith(settling);
            LocalMaterialGiftSelection gifts = _materialGifts.SelectActions(state,
                identity => selected.Contains(identity) || _authority.IsIdentityOccupiedForHost(identity), excluded,
                command => _materialProposals.FindReservedSender(_authority, state, cause, command));
            actions.AddRange(gifts.Actions);
            selected.UnionWith(gifts.Actions.Select(action => action.CommandId));
            excluded.UnionWith(gifts.ParticipatingActors);
            // Trading precedes ordinary work, so an answering or settling merchant works again at a later cutoff.
            LocalTradingSelection trading = _trading.Select(state,
                identity => selected.Contains(identity) || _authority.IsIdentityOccupiedForHost(identity), excluded,
                command => Settles(state, cause, command));
            actions.AddRange(trading.Actions);
            selected.UnionWith(trading.Actions.Select(action => action.CommandId));
            excluded.UnionWith(trading.ParticipatingActors);
            settling.UnionWith(trading.SettlingActors);
            actions.AddRange(_npcs.SelectActions(state, () => AllocateSelectedIdentity(selected), excluded));
            excluded.UnionWith(actions.Select(action => action.ActorId));
            actions.AddRange(_restorative.SelectActions(state,
                identity => selected.Contains(identity) || _authority.IsIdentityOccupiedForHost(identity), excluded,
                _materialGifts.DeferredAssistanceActors(state).ToHashSet()));
        }
        return new WorldStepBatch(cause, state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
            state.HasReadingHold ? WorldBatchKind.HeldInteraction : WorldBatchKind.AdvancingStep,
            settling.Count == 0 ? movement : movement.Where(input => !settling.Contains(input.ActorId)).ToArray(),
            actions, state.ProfileVersion);
    }

    /// <summary>
    /// Refuses a queued request that the current boundary kind cannot carry, before any batch is built. Submission
    /// checked the boundary it saw, but another writer of the same authority may have changed it since. The request
    /// receives an ordinary refusal, nothing is published for it and the session keeps running.
    /// </summary>
    private bool RefuseUnrepresentable(WorldSnapshot state, WorldBatchKind kind, ref PendingLocalAction? pending)
    {
        if (pending is null)
        {
            return false;
        }
        try
        {
            _ = new WorldStepBatch(NewCause(pending.Command.CommandId), state.WorldId, state.BranchId, state.Revision,
                state.AdvancingSteps, kind, [], [pending.Command], state.ProfileVersion);
            return false;
        }
        catch (ArgumentException)
        {
            CompletePending(pending, null, new AuthorityDispatchException("The current world boundary cannot select this action."));
            pending = null;
            return true;
        }
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

    /// <summary>
    /// Presents the observed root even when another authority caller published it and this cutoff commits nothing.
    /// The hash stays private; unchanged paused cutoffs reuse the existing immutable projection.
    /// </summary>
    private void PublishViewIfChanged(WorldSnapshot state)
    {
        if (_projectedStateHash == state.ContentHash)
        {
            return;
        }
        Volatile.Write(ref _view, ProjectView(state));
        _projectedStateHash = state.ContentHash;
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
            try { await FinishInspectionsAsync().ConfigureAwait(false); }
            finally
            {
                await _serialization.WaitAsync().ConfigureAwait(false);
                try { _authority.Dispose(); }
                finally { _serialization.Release(); }
            }
        }
    }
}
