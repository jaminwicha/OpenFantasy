using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation.CargoPresentation;

namespace OpenFantasy.Hosting.Simulation;

public sealed partial class LocalWorldSession
{
    internal LocalSelectedPlayerObservation ReadSelectedObservation(LocalPlayerConnection player)
    {
        lock (_inputs)
        {
            EnsurePlayerAvailable(player);
            if (player.SelectionNeedsProjection)
                throw new AuthorityDispatchException("The new actor selection is awaiting its coherent observation.");
            return new(player.Selection, player.ReadView(), player.ControlledView, player.ControlStatus, player.Ready);
        }
    }

    internal async Task<LocalSelectedPlayerObservation> SelectActorAsync(LocalPlayerConnection player,
        LocalActorSelection observed, EntityId actorId)
    {
        ArgumentNullException.ThrowIfNull(observed);
        await _serialization.WaitAsync().ConfigureAwait(false);
        try
        {
            WorldSnapshot state = ReadControlCustodyState();
            ReconcileControlCustody(state);
            lock (_inputs)
            {
                RequireEstablishedControlPort(player);
                RequireCurrentSelection(player, observed);
                if (state.ProfileVersion != WorldDelegatedControlAdmission.ProfileVersion)
                    throw new AuthorityDispatchException("Actor selection requires its explicit saved interpretation.");
                WorldControlWitness? witness = actorId == player.ActorId ? null : player.LiveControlWitness;
                if (actorId != player.ActorId && (witness is null || witness.OwnerActorId != actorId
                    || !WorldActorCommandAuthority.IsCurrentWitness(state, actorId, player.Delivery, witness, witness.Capabilities)))
                    throw new AuthorityDispatchException("The selected actor requires this port's exact live assignment.");
                if (player.Selection.ActorId != actorId || player.Selection.ControlWitness != witness)
                    ChangeActorSelection(player, state, actorId, witness);
            }
            PublishViewIfChanged(state);
            return ReadSelectedObservation(player);
        }
        finally { _serialization.Release(); }
    }

    internal void ResumeSelected(LocalPlayerConnection player, LocalSelectedPlayerObservation observed)
    {
        ArgumentNullException.ThrowIfNull(observed);
        lock (_inputs)
        {
            EnsurePlayerAvailable(player);
            RequireCurrentSelection(player, observed.Selection);
            EnsureControlAvailable(player);
            PlayerControlStatus current = player.ControlStatus;
            if (observed.ControlStatus.State is not (PlayerControlState.Available or PlayerControlState.Restored)
                || observed.ControlStatus.PortId != current.PortId || observed.ControlStatus.ActorId != current.ActorId
                || observed.ControlStatus.Generation != current.Generation
                || observed.HomeView.PlayerActorId != player.ActorId
                || observed.HomeView.WorldId != player.ReadView().WorldId || observed.HomeView.BranchId != player.ReadView().BranchId
                || !ObservationMatchesSelection(observed))
                throw new AuthorityDispatchException("Resume requires the coherent current actor-selection observation.");
            player.ControlStatus = current with { State = PlayerControlState.Available };
            ApplyReady(player, true);
        }
    }

    internal void PauseSelected(LocalPlayerConnection player, LocalActorSelection observed)
    {
        lock (_inputs)
        {
            EnsurePlayerAvailable(player);
            RequireCurrentSelection(player, observed);
            ApplyReady(player, false);
        }
    }

    internal void SetSelectedMovement(LocalPlayerConnection player, LocalActorSelection observed, long dx, long dy)
    {
        ValidateMovementSample(dx, dy);
        lock (_inputs)
        {
            EnsurePlayerAvailable(player);
            RequireCurrentSelection(player, observed);
            if (observed.ControlWitness is { } witness && (witness.Capabilities & WorldControlCapabilities.Traversal) == 0)
                throw new AuthorityDispatchException("This actor selection does not permit traversal.");
            ApplyMovement(player, dx, dy);
        }
    }

    internal Task<WorldActionReceipt> SubmitSelectedAction(LocalPlayerConnection player, LocalActorSelection observed,
        WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(observed);
        return SubmitActionCore(player, command, observed);
    }

    private static bool ObservationMatchesSelection(LocalSelectedPlayerObservation observed)
    {
        if (observed.Selection.ControlWitness is not { } witness) return observed.ControlledView is null;
        return observed.ControlledView is { } view && view.ActorId == observed.Selection.ActorId
            && view.WorldId == observed.HomeView.WorldId && view.BranchId == observed.HomeView.BranchId
            && view.Capabilities == witness.Capabilities;
    }

    // Input lock required. Refuse stale zero/false as well: they could otherwise stop a new selection.
    private static void RequireCurrentSelection(LocalPlayerConnection player, LocalActorSelection observed)
    {
        if (player.SelectionNeedsProjection || player.Selection != observed)
            throw new AuthorityDispatchException("Actor selection has changed; read the current observation before choosing input.");
    }

    private static void RequireUnboundHomeInput(LocalPlayerConnection player)
    {
        if (player.Selection.Generation != 0)
            throw new AuthorityDispatchException("This port requires generation-bound actor input after its first selection transition.");
    }

    // Serialization and input lock required. Durable accepted obligations survive changing the input owner.
    private void ChangeActorSelection(LocalPlayerConnection player, WorldSnapshot state, EntityId actorId,
        WorldControlWitness? witness)
    {
        ulong generation = checked(player.Selection.Generation + 1);
        ClearUnacceptedControlInput(player, state);
        _trading.CancelSelection(player.Selection);
        player.Selection = player.Selection with { ActorId = actorId, Generation = generation, ControlWitness = witness };
        player.ControlledView = null;
        player.SelectedRegions = actorId != player.ActorId && player.Regions is not null ? new LocalRegionObserver(actorId) : null;
        player.SelectionNeedsProjection = true;
    }

    private static void ValidateActionSelection(LocalPlayerConnection player, LocalActorSelection? observed,
        WorldSnapshot state, WorldActionCommand command)
    {
        EnsureControlAvailable(player);
        if (observed is null)
        {
            RequireUnboundHomeInput(player);
            if (command.ActorId != player.ActorId || command.ControlWitness is not null)
                throw new AuthorityDispatchException("Unbound input addresses only the port's permanent home actor.");
            return;
        }
        RequireCurrentSelection(player, observed);
        if (command.ActorId != observed.ActorId || command.ControlWitness != observed.ControlWitness
            || WorldActorCommandAuthority.AuthorizeSelection(state, command) is null)
            throw new AuthorityDispatchException("The command does not have this current selection's actor authority.");
    }
}
