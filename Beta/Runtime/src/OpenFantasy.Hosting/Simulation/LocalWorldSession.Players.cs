using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Diagnostics;

namespace OpenFantasy.Hosting.Simulation;

public sealed partial class LocalWorldSession
{
    public const int MaximumPlayerConnections = 4;

    /// <summary>The original local client. Legacy local methods forward to this same input owner.</summary>
    public LocalPlayerConnection Player => _localPlayer;

    /// <summary>
    /// Attach an already admitted human controller to this host, initially not ready. This validates the
    /// trusted binding; it does not authenticate a network socket or invent/reassign a resident's control.
    /// At most one live port owns an actor, and at most four ports share the sole scheduler.
    /// </summary>
    public Task<LocalPlayerConnection> ConnectPlayerAsync(EntityId actorId, TrustedCommandDelivery delivery,
        CargoPresentation.LocalRegionObserver? regions = null) => ConnectPlayerCoreAsync(actorId, delivery, regions, null, default);

    private async Task<LocalPlayerConnection> ConnectPlayerCoreAsync(EntityId actorId, TrustedCommandDelivery delivery,
        CargoPresentation.LocalRegionObserver? regions, Networking.PlayerWorldCompatibility? compatibility,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        HostTimingEvents.Log.Stage(TimingOwner, "attach.wait");
        await _serialization.WaitAsync(cancellationToken).ConfigureAwait(false);
        HostTimingEvents.Log.Stage(TimingOwner, "attach.begin");
        try
        {
            lock (_inputs) { EnsureInputAvailable(); }
            WorldSnapshot state = _authority.ReadStateForHost();
            if (compatibility is not null && !compatibility.Matches(state)) throw new Networking.PlayerAdmissionException();
            if (!state.Inventory.CanControl(delivery, actorId)
                || state.GetResident(actorId).ControllerKind != ResidentControllerKind.Player)
            {
                throw new AuthorityDispatchException("A player connection requires its admitted human and current delivery binding.");
            }
            lock (_inputs)
            {
                EnsureInputAvailable();
                if (_players.ContainsKey(actorId) || _players.Count >= MaximumPlayerConnections)
                    throw new AuthorityDispatchException("The actor is already connected or all four player slots are occupied.");
                var player = new LocalPlayerConnection(this, actorId, delivery, ProjectView(state, actorId, regions), regions,
                    state.Controllers is null || state.Controllers.Get(actorId).Mode == WorldControllerMode.Human,
                    state.Controllers is not null);
                _players.Add(actorId, player);
                return player;
            }
        }
        finally
        {
            HostTimingEvents.Log.Stage(TimingOwner, "attach.end");
            _serialization.Release();
        }
    }

    internal LocalWorldRunState ReadRunState(LocalPlayerConnection player)
    {
        lock (_inputs)
        {
            if (_failure is not null || _stopRequested || !player.Connected) return LocalWorldRunState.Unavailable;
            LocalWorldView view = player.ReadView();
            if (view.IsResolvingConsequences) return LocalWorldRunState.Resolving;
            if (!player.Ready) return LocalWorldRunState.Paused;
            return view.HasReadingHold || view.Encounter?.HasRequiredDecisions == true || view.Encounter?.IsOutcome == true
                ? LocalWorldRunState.Waiting : LocalWorldRunState.Running;
        }
    }

    /// <summary>
    /// Reads one participant's run state and inquiry without advancing. The existing host serialization
    /// gate makes the published view and trading read coherent; input readiness is captured under its lock.
    /// History work may delay this asynchronous query, so callers must not block their frame thread on it.
    /// </summary>
    internal async Task<Networking.PlayerParticipantStatus> ReadParticipantStatusAsync(LocalPlayerConnection player)
    {
        await _serialization.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_inputs)
            {
                EnsurePlayerAvailable(player);
                return new Networking.PlayerParticipantStatus(ReadRunState(player), _trading.ReadInquiry(player.ActorId));
            }
        }
        finally { _serialization.Release(); }
    }

    internal void SetReady(LocalPlayerConnection player, bool ready)
    {
        lock (_inputs)
        {
            EnsurePlayerAvailable(player);
            player.Ready = ready && player.ControlAvailable;
            // Direct in-process calls are synchronous with the port boundary and retain their admitted API.
            if (player.Ready && player.ControlStatus.State == PlayerControlState.Restored)
                player.ControlStatus = player.ControlStatus with { State = PlayerControlState.Available };
            HostTimingEvents.Log.Stage(player.TimingOwner, "ready", value: player.Ready ? 1 : 0);
            if (!player.Ready) player.ClearMovement();
        }
    }

    internal void SetMovement(LocalPlayerConnection player, long dx, long dy)
    {
        bool cardinal = ((dx == 32 || dx == -32) && dy == 0) || ((dy == 32 || dy == -32) && dx == 0);
        if (!cardinal && (dx != 0 || dy != 0))
            throw new ArgumentException("Movement selects one fixed cardinal sample, or zero to stop.");
        lock (_inputs)
        {
            EnsurePlayerAvailable(player);
            LocalWorldView view = player.ReadView();
            bool acceptsMovement = player.Ready && !view.IsResolvingConsequences && !view.IsInEncounter;
            player.MovementX = acceptsMovement ? dx : 0;
            player.MovementY = acceptsMovement ? dy : 0;
            player.MovementAt = _time.GetTimestamp();
            HostTimingEvents.Log.Stage(player.TimingOwner, acceptsMovement ? "movement.accepted" : "movement.rejected", dx, dy);
        }
    }

    private void EnsurePlayerAvailable(LocalPlayerConnection player)
    {
        EnsureInputAvailable();
        if (!player.Connected || !_players.TryGetValue(player.ActorId, out LocalPlayerConnection? current)
            || !ReferenceEquals(current, player))
            throw new ObjectDisposedException(nameof(LocalPlayerConnection));
    }

    /// <summary>Stable rotating service, bounded to four outstanding player requests; callers hold the input lock.</summary>
    private PendingLocalAction? SelectPendingAction(WorldSnapshot state)
    {
        LocalPlayerConnection[] waiting = _players.Values.Where(player => player.Ready && player.Pending is not null).ToArray();
        if (state.Care?.Pending is { } acknowledged)
        {
            // A queued terminal decision competes before automatic treatment. Refusal gives the helper
            // the remainder of this cutoff; legacy stale acknowledgment retains its one-receipt window.
            LocalPlayerConnection[] decisions = LocalProfileBindings.AdmitsCareDispositions(state)
                ? waiting.Where(player => IsCareDecision(player.Pending!.Command.Intent)).ToArray() : [];
            if (decisions.Length != 0)
            {
                LocalPlayerConnection[] lawful = decisions.Where(player =>
                    IsLawfulPendingCareDecision(state, player.Pending!.Command)).ToArray();
                waiting = lawful.Length != 0 ? lawful : decisions;
            }
            else
            {
                if (state.Revision != acknowledged.WorldSequence) return null;
                EntityId patient = state.Care.GetAgreement(acknowledged.AgreementId).PatientActorId;
                waiting = waiting.Where(player => player.ActorId == patient
                    && player.Pending!.Command.Intent is AcknowledgeCareIntent care
                    && care.AgreementId == acknowledged.AgreementId
                    && care.EncounterId == acknowledged.EncounterId).ToArray();
            }
        }
        else
        {
            // A ready patient's already-queued care choice precedes outcome completion or another reader's traffic.
            LocalPlayerConnection[] careChoices = waiting.Where(player => IsCurrentCareChoice(state, player.Pending!.Command)).ToArray();
            if (careChoices.Length != 0) waiting = careChoices;
        }
        LocalPlayerConnection? next = waiting.FirstOrDefault(player => _lastServedActor is null
            || player.ActorId.CompareTo(_lastServedActor.Value) > 0) ?? waiting.FirstOrDefault();
        if (next is null) return null;
        _lastServedActor = next.ActorId;
        return next.Pending;
    }

    private List<WorldMovementInput> SamplePlayerMovement()
    {
        List<WorldMovementInput> movement = [];
        long now = _time.GetTimestamp();
        foreach (LocalPlayerConnection player in _players.Values)
        {
            if (HostTimingEvents.Log.IsEnabled() && (player.MovementX != 0 || player.MovementY != 0))
                HostTimingEvents.Log.Stage(player.TimingOwner, "movement.age", value: _time.GetElapsedTime(player.MovementAt, now).Ticks);
            if (!player.Ready || _time.GetElapsedTime(player.MovementAt, now) > MovementLease
                || player.MovementX == 0 && player.MovementY == 0) continue;
            HostTimingEvents.Log.Stage(player.TimingOwner, "movement.selected", player.MovementX, player.MovementY);
            movement.Add(new WorldMovementInput(player.Delivery, player.ActorId, player.MovementX, player.MovementY));
        }
        return movement;
    }

    internal async Task ClosePlayerAsync(LocalPlayerConnection player)
    {
        await _serialization.WaitAsync().ConfigureAwait(false);
        try
        {
            RelinquishControlCustody(player);
            DetachPlayer(player);
        }
        finally { _serialization.Release(); }
        await AwaitPlayerInspectionsAsync(player).ConfigureAwait(false);
    }

    // Caller holds serialization; stale cleanup never cancels the replacement port's inquiry.
    private void DetachPlayer(LocalPlayerConnection player)
    {
        lock (_inputs)
        {
            if (!IsCurrentPlayer(player)) return;
            player.Connected = false;
            player.Ready = false;
            player.ClearMovement();
            player.LiveControlWitness = null;
            player.Pending?.Completion.TrySetCanceled();
            player.Pending = null;
            _players.Remove(player.ActorId);
        }
        _trading.Cancel(player.ActorId);
        _trading.ForgetInquiry(player.ActorId);
    }

    private static async Task AwaitPlayerInspectionsAsync(LocalPlayerConnection player)
    {
        // Reads already admitted may finish, but no new query can enter through the closed port.
        foreach (Task? inspection in new Task?[] { player.ProposalInspection.Latest, player.QuoteInspection.Latest })
        {
            if (inspection is null) continue;
            try { await inspection.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (AuthorityDispatchException) { }
        }
    }
}
