using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

public sealed partial class LocalWorldSession
{
    /// <summary>Internal presence boundary only. Acquisition neither selects an actor nor resumes gameplay.</summary>
    internal async Task<WorldControlWitness> AcquireControlAsync(LocalPlayerConnection player, LocalControlAcquisition observed)
    {
        ArgumentNullException.ThrowIfNull(observed);
        await _serialization.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_inputs) { RequireEstablishedControlPort(player); }
            WorldSnapshot state = ReadControlCustodyState();
            ReconcileControlCustody(state);
            lock (_inputs)
            {
                RequireEstablishedControlPort(player);
                if (player.LiveControlWitness is not null
                    || _players.Values.Any(owner => owner.ActorId == observed.OwnerActorId && owner.NetworkEstablished))
                    throw new AuthorityDispatchException("Control acquisition requires an absent owner and an unassigned live grantee.");
            }
            if (state.EffectiveControl is null || !state.Inventory.CanControl(player.Delivery, player.ActorId))
                throw new AuthorityDispatchException("The world and grantee do not admit effective control.");

            EntityId cause = NewControlCause();
            WorldControlWitness witness;
            WorldStepBatch batch;
            try
            {
                witness = new WorldControlWitness(observed.OwnerActorId, player.ActorId, observed.GrantCommandId,
                    observed.AcceptanceCommandId, observed.GrantRevision, observed.Capabilities, observed.TermsHash,
                    cause, checked(observed.ExpectedAssignmentRevision + 1), checked(state.Revision + 1),
                    observed.DisconnectId, observed.OwnerControlRevision);
                batch = ControlAssignmentBatch(state, cause, new(observed.OwnerActorId,
                    WorldControlAssignmentEventKind.Acquire, observed.ExpectedAssignmentRevision, witness));
            }
            catch (Exception error) when (error is ArgumentException or OverflowException)
            {
                throw new AuthorityDispatchException("The captured control acquisition is malformed.");
            }
            WorldSnapshot committed = CommitControlAssignment(batch);
            lock (_inputs)
            {
                player.LiveControlWitness = witness;
                ClearUnacceptedControlInput(player, committed);
            }
            PublishControlCustody(committed);
            return witness;
        }
        finally { _serialization.Release(); }
    }

    internal async Task ReleaseControlAsync(LocalPlayerConnection player, WorldControlWitness observed)
    {
        ArgumentNullException.ThrowIfNull(observed);
        await _serialization.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_inputs) { RequireEstablishedControlPort(player); }
            WorldSnapshot state = ReadControlCustodyState();
            ReconcileControlCustody(state);
            lock (_inputs)
            {
                RequireEstablishedControlPort(player);
                if (player.LiveControlWitness != observed || observed.GranteeActorId != player.ActorId
                    || state.EffectiveControl?.Find(observed.OwnerActorId)?.Witness != observed)
                    throw new AuthorityDispatchException("Only this port's exact live assignment can be released.");
            }
            PublishControlCustody(ReleaseControlAssignment(state, observed));
        }
        finally { _serialization.Release(); }
    }

    private void RequireEstablishedControlPort(LocalPlayerConnection player)
    {
        EnsureInputAvailable();
        if (!IsCurrentPlayer(player) || !player.NetworkEstablished || !player.ControlAvailable)
            throw new AuthorityDispatchException("Effective control requires this current authenticated and available transport.");
    }

    private WorldSnapshot ReadControlCustodyState()
    {
        try { return _authority.ReadStateForHost(); }
        catch (Exception error) { Fail(error); throw; }
    }

    private EntityId NewControlCause()
    {
        try { return NewCause(null); }
        catch (Exception error) { Fail(error); throw; }
    }

    private void PublishControlCustody(WorldSnapshot state)
    {
        try { PublishViewIfChanged(state); }
        catch (Exception error) { Fail(error); throw; }
    }

    private WorldStepBatch ControlAssignmentBatch(WorldSnapshot state, EntityId cause, WorldControlAssignmentEvent control) =>
        new(cause, state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
            WorldBatchKind.ControlAssignment, [], [], state.ProfileVersion, controlAssignmentEvent: control);

    private WorldSnapshot CommitControlAssignment(WorldStepBatch batch)
    {
        try { return _authority.Commit(batch).Candidate; }
        catch (AuthorityDispatchException) { throw; }
        catch (Exception error) { Fail(error); throw; }
    }

    private WorldSnapshot ReleaseControlAssignment(WorldSnapshot state, WorldControlWitness witness) =>
        CommitControlAssignment(ControlAssignmentBatch(state, NewControlCause(), new(witness.OwnerActorId,
            WorldControlAssignmentEventKind.Release, witness.AssignmentRevision)));

    // Constructor and stop own the host exclusively. Neither saved consent nor a saved assignment proves presence.
    private WorldSnapshot ReleaseAllControlAssignments(WorldSnapshot state)
    {
        WorldControlWitness[] witnesses = state.EffectiveControl?.Slots
            .Where(slot => slot.Witness is not null).Select(slot => slot.Witness!).ToArray() ?? [];
        foreach (WorldControlWitness witness in witnesses)
            state = ReleaseControlAssignment(state, witness);
        return state;
    }

    // Called after final admission validation, even when accepted work postpones the owner's Human restoration.
    private WorldSnapshot ReleaseReturningOwnerAssignment(WorldSnapshot state, EntityId ownerActorId)
    {
        if (state.EffectiveControl?.Find(ownerActorId)?.Witness is not { } witness) return state;
        state = ReleaseControlAssignment(state, witness);
        PublishControlCustody(state);
        return state;
    }

    // Caller holds serialization. A stale port or stale witness never releases a replacement assignment.
    private void RelinquishControlCustody(LocalPlayerConnection player)
    {
        lock (_inputs)
        {
            if (!IsCurrentPlayer(player) || _failure is not null || _stopRequested) return;
        }
        WorldSnapshot state = ReadControlCustodyState();
        ReconcileControlCustody(state);
        WorldControlWitness? witness;
        lock (_inputs) { witness = player.LiveControlWitness; }
        if (witness is not null && state.EffectiveControl?.Find(witness.OwnerActorId)?.Witness == witness)
            PublishControlCustody(ReleaseControlAssignment(state, witness));
    }

    // Core revocation, replacement and controller events may already have released an assignment.
    // Reconcile before the projection hash shortcut, retaining custody only for the exact committed witness.
    private void ReconcileControlCustody(WorldSnapshot state)
    {
        lock (_inputs)
        {
            foreach (LocalPlayerConnection player in _players.Values)
            {
                if (player.LiveControlWitness is not { } witness
                    || state.EffectiveControl?.Find(witness.OwnerActorId)?.Witness == witness) continue;
                player.LiveControlWitness = null;
                if (player.Selection.Generation != 0)
                    ChangeActorSelection(player, state, player.ActorId, null);
                else ClearUnacceptedControlInput(player, state);
            }
        }
    }

    private static void ClearUnacceptedControlInput(LocalPlayerConnection player, WorldSnapshot state)
    {
        player.Ready = false;
        player.ClearMovement();
        if (player.Pending is not { } pending) return;
        WorldActionCommand command = pending.Command;
        bool retained = state.PendingExcavation?.Command.ContentHash == command.ContentHash
            || state.EncounterActions?.Pending.Any(accepted => accepted.ContentHash == command.ContentHash) == true
            || state.Retreat?.Active?.Command.ContentHash == command.ContentHash;
        if (retained) return;
        player.Pending = null;
        pending.Completion.TrySetException(new AuthorityDispatchException("The control boundary cleared this unaccepted input."));
    }
}
