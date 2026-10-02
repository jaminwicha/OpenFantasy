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

/// <summary>A player port's scheduling state; other ready ports can keep their shared host running.</summary>
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
    private readonly TimeProvider _time;
    private readonly LocalNpcController _npcs = new();
    private readonly LocalAutonomousEncounterController _autonomousEncounter = new();
    private readonly LocalCareController _care = new();
    private readonly LocalRestorativeController _restorative = new();
    private readonly LocalMaterialProposalSelection _materialProposals = new();
    private readonly LocalMaterialGiftController _materialGifts;
    private readonly LocalTradingController _trading;
    private readonly Retreat.LocalRetreatConfiguration _retreat;
    private readonly Task _loop;
    private readonly LocalPlayerConnection _localPlayer;
    private readonly SortedDictionary<EntityId, LocalPlayerConnection> _players = [];
    private EntityId? _lastServedActor;
    private ContentHash _projectedStateHash;
    private Exception? _failure;
    private Task? _stopping;
    private bool _stopRequested;

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

    /// <summary>
    /// A trading session presented regionally: every published view also carries the player's bounded terrain and water
    /// observation from that view's own root, so frames read one coherent public value and never the authority.
    /// </summary>
    public LocalWorldSession(SqliteWorldAuthority authority, EntityId playerActorId, TrustedCommandDelivery delivery,
        IEnumerable<LocalMaterialGiftPolicy> giftPolicies, LocalTradingConfiguration trading,
        CargoPresentation.LocalRegionObserver regions)
        : this(authority, playerActorId, delivery, TimeProvider.System, giftPolicies, trading,
            regions ?? throw new ArgumentNullException(nameof(regions))) { }

    /// <summary>
    /// A regional trading session of the explicit retreat world. Opt-in NPC retreat preferences are host advice,
    /// resupplied on every opening and never saved; players and NPCs share the same retreat admission and costs.
    /// </summary>
    public LocalWorldSession(SqliteWorldAuthority authority, EntityId playerActorId, TrustedCommandDelivery delivery,
        IEnumerable<LocalMaterialGiftPolicy> giftPolicies, LocalTradingConfiguration trading,
        CargoPresentation.LocalRegionObserver? regions, Retreat.LocalRetreatConfiguration retreat)
        : this(authority, playerActorId, delivery, TimeProvider.System, giftPolicies, trading, regions,
            retreat ?? throw new ArgumentNullException(nameof(retreat))) { }

    internal LocalWorldSession(SqliteWorldAuthority authority, EntityId playerActorId,
        TrustedCommandDelivery delivery, TimeProvider timeProvider, IEnumerable<LocalMaterialGiftPolicy>? giftPolicies = null,
        LocalTradingConfiguration? trading = null, CargoPresentation.LocalRegionObserver? regions = null,
        Retreat.LocalRetreatConfiguration? retreat = null)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _retreat = retreat ?? Retreat.LocalRetreatConfiguration.None;
        WorldSnapshot state = authority.ReadStateForHost();
        if (!state.Inventory.CanControl(delivery, playerActorId)
            || state.GetResident(playerActorId).ControllerKind != ResidentControllerKind.Player)
        {
            throw new AuthorityDispatchException("The local session requires its admitted player and current delivery binding.");
        }
        _authority = authority;
        _time = timeProvider;
        _materialGifts = new LocalMaterialGiftController(state, giftPolicies ?? []);
        _trading = new LocalTradingController(state, playerActorId, trading ?? LocalTradingConfiguration.None);
        _localPlayer = new LocalPlayerConnection(this, playerActorId, delivery, ProjectView(state, playerActorId, regions), regions,
            state.Controllers is null || state.Controllers.Get(playerActorId).Mode == WorldControllerMode.Human);
        _players.Add(playerActorId, _localPlayer);
        _projectedStateHash = state.ContentHash;
        _loop = Task.Run(RunAsync);
    }

    /// <summary>Faults on storage, recovery or host-loop failure; input rejection is reported by its own task.</summary>
    public Task Completion => _loop;

    public LocalWorldView ReadView() => _localPlayer.ReadView();

    public LocalWorldRunState ReadRunState() => ReadRunState(_localPlayer);

    public void SetReady(bool ready) => SetReady(_localPlayer, ready);

    public void SetMovement(long dx, long dy) => SetMovement(_localPlayer, dx, dy);

    public Task<WorldActionReceipt> SubmitAction(WorldActionCommand command) => SubmitAction(_localPlayer, command);

    internal async Task<WorldActionReceipt> SubmitAction(LocalPlayerConnection player, WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        byte[] encoded = WorldActionCodec.Encode(command);
        Task<WorldActionReceipt> result;
        await _serialization.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_inputs) { EnsurePlayerAvailable(player); }
            if (command.ActorId != player.ActorId)
            {
                throw new AuthorityDispatchException("This session accepts only its admitted player's actions.");
            }
            WorldActionReceipt? prior;
            try
            {
                prior = _authority.FindReceipt(command, player.Delivery);
            }
            catch (AuthorityDispatchException) { throw; }
            catch (Exception error) { Fail(error); throw; }
            if (prior is not null)
            {
                return prior;
            }
            lock (_inputs) { EnsureControlAvailable(player); }
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
                EnsurePlayerAvailable(player);
                if (player.Pending is { } existing)
                {
                    if (!existing.Bytes.AsSpan().SequenceEqual(encoded))
                    {
                        throw new AuthorityDispatchException("One actor action is awaiting its terminal result.");
                    }
                    result = existing.Completion.Task;
                }
                else
                {
                    if (!player.Ready && !durablePending)
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
                    if (_players.Values.Any(other => !ReferenceEquals(other, player)
                        && other.Pending?.Command.CommandId == command.CommandId))
                        throw new AuthorityDispatchException("Another player already queued this command identity.");
                    player.Pending = new PendingLocalAction(command, encoded);
                    result = player.Pending.Completion.Task;
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
            // Stale revision and agreement choices still receive durable terminal receipts.
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
            if (command.Intent is ReleaseEncounterIntent && LocalProfileBindings.AdmitsOutcomeCompletion(state))
                throw new AuthorityDispatchException("This world completes an outcome after it is read; it has no initiator release.");
            // Exact target, identity and captured revisions receive normal durable receipts.
            return;
        }
        if (command.Intent is AcknowledgeEncounterOutcomeIntent)
        {
            if (!LocalProfileBindings.AdmitsOutcomeCompletion(state) || state.HasReadingHold)
                throw new AuthorityDispatchException("An outcome acknowledgment requires the outcome-completion world without a reading hold.");
            // Exact phase, episode, archive and controller receive normal durable receipts.
            return;
        }
        if (state.EncounterLifecycle?.Phase is EncounterLifecyclePhase.Confrontation or EncounterLifecyclePhase.Staging
            or EncounterLifecyclePhase.Outcome)
            throw new AuthorityDispatchException("This lifecycle boundary accepts only entry or release requests.");
        if (command.Intent is QueueEncounterRetreatIntent && !LocalProfileBindings.AdmitsRetreat(state))
            throw new AuthorityDispatchException("This saved profile does not admit a committed retreat.");
        if (state.Pacing?.Encounter is not null && command.Intent is not QueueEncounterWaitIntent and not QueueEncounterAbilityIntent
            and not QueueEncounterRetreatIntent)
            throw new AuthorityDispatchException("An active encounter accepts only its retained encounter choices.");
        if (command.Intent is QueueEncounterWaitIntent or QueueEncounterAbilityIntent or QueueEncounterRetreatIntent
            && state.Pacing?.Encounter is null)
            throw new AuthorityDispatchException("An encounter choice requires an active admitted encounter.");
        if (command.Intent is QueueEncounterAbilityIntent or QueueEncounterRetreatIntent && state.EncounterActions is null
            || WorldEncounterActions.IsOutcome(state))
            throw new AuthorityDispatchException("This encounter does not admit another ability or timing action.");
        if (command.Intent is QueueEncounterWaitIntent or QueueEncounterAbilityIntent or QueueEncounterRetreatIntent
            && (WorldEncounterActions.DueActors(state).All(actor => actor.ActorId != command.ActorId)
                || state.Pacing!.Encounter!.Decisions.Any(decision => decision.ActorId == command.ActorId)
                || WorldEncounterRetreats.HoldsActor(state, command.ActorId)))
            throw new AuthorityDispatchException("Only an eligible participant awaiting its own decision may queue a combat choice.");
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
                foreach (LocalPlayerConnection player in _players.Values)
                {
                    if (_failure is { } failure) { player.Pending?.Completion.TrySetException(failure); }
                    else { player.Pending?.Completion.TrySetCanceled(_stop.Token); }
                    player.Pending = null;
                }
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
            WorldSnapshot state = TickControllers(_authority.ReadStateForHost());
            PublishViewIfChanged(state);
            if (state.PendingExcavation is not null)
            {
                ContinueConsequences(state);
                return;
            }
            PendingLocalAction? pending;
            List<WorldMovementInput> playerMovement;
            lock (_inputs)
            {
                if (_stopRequested || !_players.Values.Any(player => player.Ready) && !CanContinueAcceptedControlReturn(state)) { return; }
                pending = SelectPendingAction(state);
                playerMovement = SamplePlayerMovement();
            }
            if (pending?.Command.Intent is AcknowledgeCareIntent || state.Care?.Pending is not null)
            {
                TickCare(state, pending);
                if (state.Care?.Pending is not null)
                {
                    WorldSnapshot afterCare = _authority.ReadStateForHost();
                    // Only a published receipt earns the patient's separate cutoff. A failed preflight
                    // changes no durable revision and must not defer the already accepted helper action.
                    if (pending is not null && afterCare.Revision == state.Revision)
                        TickCare(afterCare, null);
                    return;
                }
                if (!WasRefused(pending) || pending is not null && IsCurrentCareChoice(state, pending.Command))
                    return;
                // A patient's care offer is adjudicated unchanged, including after its encounter ends.
                // Outside the genuine outcome choice, a refusal cannot stop the due world boundary.
                state = _authority.ReadStateForHost();
                pending = null;
            }
            // Profile 35: a reading is the player's own lifecycle command; completion is the world's own boundary once
            // every required human has read the outcome and no care is pending. Neither earns time.
            if (pending?.Command.Intent is EnterEncounterIntent or ReleaseEncounterIntent or AcknowledgeEncounterOutcomeIntent
                || state.EncounterLifecycle?.Phase is EncounterLifecyclePhase.Confrontation or EncounterLifecyclePhase.Staging
                || WorldEncounterOutcomes.IsReadyToFinalize(state))
            {
                TickLifecycle(state, pending);
                if (!WasRefused(pending) || pending is not null && IsLegacyInitiatorRelease(state, pending.Command))
                    return;
                // A rejected zero-time request is a receipt, not an extra pause entitlement. Continue the
                // already-due world cutoff once, without retrying or replacing the player's command.
                state = _authority.ReadStateForHost();
                pending = null;
                if (state.EncounterLifecycle?.Phase is EncounterLifecyclePhase.Confrontation or EncounterLifecyclePhase.Staging
                    || WorldEncounterOutcomes.IsReadyToFinalize(state))
                {
                    TickLifecycle(state, null);
                    return;
                }
            }
            if (state.Pacing?.Encounter is not null)
            {
                TickEncounter(state, pending);
                return;
            }
            // An eligible hostile entry owns this exploration cutoff even when an ordinary request is waiting, so
            // submissions cannot keep movement carrying the player through the initiator's reach.
            if (TickHostileEntry(state, pending))
            {
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
                movement.AddRange(playerMovement);
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

    private static bool IsCurrentCareChoice(WorldSnapshot state, WorldActionCommand command) =>
        command.Intent is AcknowledgeCareIntent care && state.EncounterLifecycle?.Phase == EncounterLifecyclePhase.Outcome
        && state.EncounterActions?.Scope.EncounterId == care.EncounterId
        && state.Care?.Agreements.Any(agreement => agreement.AgreementId == care.AgreementId
            && agreement.PatientActorId == command.ActorId) == true
        && state.EncounterActions.Combatants.Any(actor => actor.ActorId == command.ActorId && actor.Side != 0);

    private static bool IsLegacyInitiatorRelease(WorldSnapshot state, WorldActionCommand command) =>
        command.Intent is ReleaseEncounterIntent release
        && state.EncounterLifecycle?.Phase is EncounterLifecyclePhase.Confrontation or EncounterLifecyclePhase.Staging
        && state.EncounterActions?.Scope.EncounterId == release.EncounterId
        && state.EncounterLifecycle.Scenarios.Single(scenario => scenario.DefinitionId == state.EncounterLifecycle.ScenarioId)
            .InitiatingActorId == command.ActorId;

    private static bool WasRefused(PendingLocalAction? pending) => pending is not null
        && (pending.Completion.Task.IsFaulted || pending.Completion.Task.IsCanceled
            || pending.Completion.Task.IsCompletedSuccessfully
                && pending.Completion.Task.Result.Rejection != WorldActionRejection.None);

    private void TickCare(WorldSnapshot state, PendingLocalAction? pending)
    {
        if (state.Pacing?.Encounter is not null) ClearMovement();
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
        if (RefuseUnrepresentable(state, WorldBatchKind.EncounterLifecycle, ref pending)
            && state.EncounterLifecycle?.Phase is not (EncounterLifecyclePhase.Confrontation or EncounterLifecyclePhase.Staging)
            && !WorldEncounterOutcomes.IsReadyToFinalize(state))
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
        if (_authority.ReadStateForHost().Pacing?.Encounter is not null) ClearMovement();
    }

    /// <summary>
    /// A host resident's admitted hostile opportunity begins at its own lifecycle cutoff, before any exploration step,
    /// as an ordinary authenticated entry under that resident's control. A proposal the authority would refuse is not
    /// committed, so a near miss or stale view publishes nothing; the decision is re-derived at the next cutoff.
    /// The holds that precede this call (pause, retained consequence, care, the player's own lifecycle choice and an
    /// active encounter) keep their priority, and a reading hold is not an unheld exploration boundary. A waiting
    /// ordinary request was selected for an exploration step that the committed entry replaced: it receives the
    /// ordinary boundary refusal at this same cutoff, so nothing of it executes against the encounter and its waiter
    /// never hangs.
    /// </summary>
    private bool TickHostileEntry(WorldSnapshot state, PendingLocalAction? pending)
    {
        WorldActionCommand? entry = RoadThreat.LocalRoadThreatController.SelectPlayerEntry(state, () => NewCause(null));
        if (entry is null)
        {
            return false;
        }
        var batch = new WorldStepBatch(NewCause(entry.CommandId), state.WorldId, state.BranchId, state.Revision,
            state.AdvancingSteps, WorldBatchKind.EncounterLifecycle, [], [entry], state.ProfileVersion);
        WorldStepDecision preview;
        try { preview = _authority.Preview(batch); }
        catch (AuthorityDispatchException) { return false; }
        if (preview.Receipts.Any(receipt => receipt.Rejection != WorldActionRejection.None))
        {
            return false;
        }
        ClearMovement();
        CommitSelectedBatch(batch, (PendingLocalAction?)null);
        if (pending is not null)
        {
            CompletePending(pending, null,
                new AuthorityDispatchException("A hostile encounter began at this boundary; the exploration action was not selected."));
        }
        return true;
    }

    private void TickEncounter(WorldSnapshot state, PendingLocalAction? pending)
    {
        ClearMovement();
        List<PendingLocalAction> awaiting = [];
        bool maySelectAutonomousChoices;
        lock (_inputs)
        {
            // The zero-ready exception completes accepted returning-player work only. It does
            // not authorize fresh autonomous choices for other disconnected characters.
            maySelectAutonomousChoices = _players.Values.Any(player => player.Ready);
            awaiting.AddRange(_players.Values.Where(player => player.Ready && player.Pending is not null)
                .Select(player => player.Pending!));
        }
        foreach (PendingLocalAction request in awaiting.ToArray())
        {
            PendingLocalAction? candidate = request;
            if (WorldEncounterActions.IsOutcome(state))
            {
                CompletePending(request, null, new AuthorityDispatchException("The encounter now awaits outcome reading."));
                awaiting.Remove(request);
            }
            else if (RefuseUnrepresentable(state, WorldBatchKind.EncounterDecision, ref candidate))
            {
                awaiting.Remove(request);
            }
        }
        if (WorldEncounterActions.IsOutcome(state)) return;
        EntityId cause = NewCause(pending?.Command.CommandId);
        HashSet<EntityId> selected = [cause];
        List<WorldActionCommand> actions = awaiting.Select(request => request.Command).ToList();
        selected.UnionWith(actions.Select(command => command.CommandId));
        actions.AddRange(_npcs.SelectEncounterChoices(state, () => AllocateSelectedIdentity(selected), _retreat));
        if (maySelectAutonomousChoices)
            actions.AddRange(_autonomousEncounter.SelectChoices(state, actions, () => AllocateSelectedIdentity(selected)));
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
        catch (AuthorityDispatchException error) when (awaiting.Count != 0)
        {
            // A refused selected request leaves this encounter at the same boundary. It cannot
            // select ordinary movement, work or exploration time as a fallback.
            foreach (PendingLocalAction request in awaiting) CompletePending(request, null, error);
            return;
        }
        CommitSelectedBatch(batch, awaiting);
    }

    private void ContinueConsequences(WorldSnapshot state)
    {
        PendingLocalAction? awaiting;
        lock (_inputs)
        {
            if (_stopRequested) { return; }
            foreach (LocalPlayerConnection player in _players.Values) player.ClearMovement();
            awaiting = _players.GetValueOrDefault(state.PendingExcavation!.Command.ActorId)?.Pending;
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

    private void CommitSelectedBatch(WorldStepBatch batch, PendingLocalAction? awaiting) =>
        CommitSelectedBatch(batch, awaiting is null ? Array.Empty<PendingLocalAction>() : [awaiting]);

    private void CommitSelectedBatch(WorldStepBatch batch, IReadOnlyList<PendingLocalAction> awaiting)
    {
        WorldStepDecision decision = _authority.Commit(batch);
        _trading.Observe(decision);
        PublishViewIfChanged(decision.Candidate);
        if (decision.Candidate.PendingExcavation is { } pending)
        {
            ClearMovement();
            if (awaiting.Count > 1 || awaiting.Count == 1
                && !WorldActionCodec.Encode(pending.Command).AsSpan().SequenceEqual(awaiting[0].Bytes))
                throw new InvalidDataException("The committed consequence command differs from its player waiter.");
        }
        else
        {
            foreach (PendingLocalAction request in awaiting)
            {
                WorldActionReceipt receipt = decision.Receipts.Single(value => value.CommandId == request.Command.CommandId);
                CompletePending(request, receipt, null);
            }
        }
    }

    /// <summary>Runs at publication on the serialized host owner, never in a frame; the regional cache is reused.</summary>
    private LocalWorldView ProjectView(WorldSnapshot state, EntityId actorId,
        CargoPresentation.LocalRegionObserver? regions) => new(state, actorId,
        state.Care?.Pending is not null && _authority.IsIdentityOccupiedForHost(LocalCareController.SelectionId(state)), regions);

    private void ClearMovement()
    {
        lock (_inputs)
        {
            foreach (LocalPlayerConnection player in _players.Values) player.ClearMovement();
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
            bool queued;
            lock (_inputs) { queued = _players.Values.Any(player => player.Pending?.Command.CommandId == id); }
            if (id != commandId && !queued && !_authority.IsIdentityOccupiedForHost(id)) { return id; }
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
        foreach (LocalPlayerConnection player in _players.Values)
        {
            player.Publish(ProjectView(state, player.ActorId, player.Regions));
        }
        _projectedStateHash = state.ContentHash;
    }

    private void CompletePending(PendingLocalAction pending, WorldActionReceipt? receipt, Exception? error)
    {
        lock (_inputs)
        {
            if (_players.TryGetValue(pending.Command.ActorId, out LocalPlayerConnection? player)
                && ReferenceEquals(player.Pending, pending)) player.Pending = null;
        }
        if (error is not null) { pending.Completion.TrySetException(error); }
        else { pending.Completion.TrySetResult(receipt!); }
    }

    private void Fail(Exception error)
    {
        lock (_inputs)
        {
            _failure ??= error;
            foreach (LocalPlayerConnection player in _players.Values)
            {
                player.Ready = false;
                player.ClearMovement();
                player.Pending?.Completion.TrySetException(_failure);
            }
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
        foreach (LocalPlayerConnection player in _players.Values)
        {
            player.Ready = false;
            player.ClearMovement();
        }
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
