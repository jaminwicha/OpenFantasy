using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;

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
    public async Task<LocalPlayerConnection> ConnectPlayerAsync(EntityId actorId, TrustedCommandDelivery delivery,
        CargoPresentation.LocalRegionObserver? regions = null)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        await _serialization.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_inputs) { EnsureInputAvailable(); }
            WorldSnapshot state = _authority.ReadStateForHost();
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
                var player = new LocalPlayerConnection(this, actorId, delivery, ProjectView(state, actorId, regions), regions);
                _players.Add(actorId, player);
                return player;
            }
        }
        finally { _serialization.Release(); }
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

    internal void SetReady(LocalPlayerConnection player, bool ready)
    {
        lock (_inputs)
        {
            EnsurePlayerAvailable(player);
            player.Ready = ready;
            if (!ready) player.ClearMovement();
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
            // Preserve one separate stale-offer receipt for the original patient. Its durable revision
            // closes this window across reconnects and reloads; later requests cannot starve treatment.
            if (state.Revision != acknowledged.WorldSequence) return null;
            EntityId patient = state.Care.GetAgreement(acknowledged.AgreementId).PatientActorId;
            waiting = waiting.Where(player => player.ActorId == patient
                && player.Pending!.Command.Intent is AcknowledgeCareIntent care
                && care.AgreementId == acknowledged.AgreementId
                && care.EncounterId == acknowledged.EncounterId).ToArray();
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
            if (!player.Ready || _time.GetElapsedTime(player.MovementAt, now) > MovementLease
                || player.MovementX == 0 && player.MovementY == 0) continue;
            movement.Add(new WorldMovementInput(player.Delivery, player.ActorId, player.MovementX, player.MovementY));
        }
        return movement;
    }

    internal async Task ClosePlayerAsync(LocalPlayerConnection player)
    {
        await _serialization.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_inputs)
            {
                if (!player.Connected) return;
                player.Connected = false;
                player.Ready = false;
                player.ClearMovement();
                player.Pending?.Completion.TrySetCanceled();
                player.Pending = null;
                _players.Remove(player.ActorId);
            }
            _trading.Cancel(player.ActorId);
            _trading.ForgetInquiry(player.ActorId);
        }
        finally { _serialization.Release(); }
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
