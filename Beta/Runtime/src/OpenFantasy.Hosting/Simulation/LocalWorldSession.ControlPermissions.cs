using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

public sealed partial class LocalWorldSession
{
    internal async Task<LocalControlPermissionBoard> ReadControlPermissionsAsync(LocalPlayerConnection player)
    {
        await _serialization.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_inputs) { RequirePermissionPort(player); }
            WorldSnapshot state = ReadControlCustodyState();
            RequirePermissionWorld(state, player);
            ReconcileControlCustody(state);
            lock (_inputs)
            {
                RequirePermissionPort(player);
                var slots = state.ControlPermissions!.Slots
                    .Where(slot => slot.OwnerActorId == player.ActorId || slot.GranteeActorId == player.ActorId)
                    .Select(slot => new LocalControlPermissionSlot(slot.OwnerActorId, slot.GranteeActorId,
                        slot.Revision, slot.Status, slot.ChangedSequence, slot.OfferedSequence, slot.AcceptedSequence,
                        slot.GrantCommandId, slot.AcceptanceCommandId, slot.Terms?.Capabilities, slot.TermsHash,
                        ObserveControlAcquisition(state, player, slot))).ToArray();
                return new(state.WorldId, state.BranchId, state.Revision, player.ActorId,
                    state.Space.GetActor(player.ActorId).Revision, state.ControlPermissions.Revision,
                    state.Controllers!.Assignments.Select(row => row.ActorId).ToArray(), slots, player.LiveControlWitness);
            }
        }
        finally { _serialization.Release(); }
    }

    /// <summary>
    /// Consent authenticates the permanent home delivery independently of actor selection and readiness.
    /// Core retains its Human requirement for offer/accept and permits original-owner revoke during return.
    /// </summary>
    internal async Task<WorldActionReceipt> SubmitControlPermissionAsync(LocalPlayerConnection player, WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        await _serialization.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_inputs) { RequirePermissionPort(player); }
            if (command.Intent is not ControlPermissionIntent || command.ActorId != player.ActorId
                || command.ControlWitness is not null || command.PrincipalId != player.Delivery.PrincipalId
                || command.OriginSessionId != player.Delivery.SessionId)
                throw new AuthorityDispatchException("Consent requires this authenticated home actor's original command and delivery.");
            WorldSnapshot state = ReadControlCustodyState();
            RequirePermissionWorld(state, player);
            WorldActionReceipt? prior;
            try { prior = _authority.FindReceipt(command, player.Delivery); }
            catch (AuthorityDispatchException) { throw; }
            catch (Exception error) { Fail(error); throw; }
            if (prior is not null) return prior;

            WorldStepDecision decision;
            try
            {
                var batch = new WorldStepBatch(NewCause(command.CommandId), state.WorldId, state.BranchId,
                    state.Revision, state.AdvancingSteps, WorldBatchKind.ControlPermission, [], [command], state.ProfileVersion);
                decision = _authority.Commit(batch);
            }
            catch (AuthorityDispatchException) { throw; }
            catch (Exception error) { Fail(error); throw; }
            PublishControlCustody(decision.Candidate);
            return decision.Receipts.Single(receipt => receipt.CommandId == command.CommandId);
        }
        finally { _serialization.Release(); }
    }

    private void RequirePermissionPort(LocalPlayerConnection player)
    {
        EnsureInputAvailable();
        if (!IsCurrentPlayer(player) || !player.NetworkEstablished)
            throw new AuthorityDispatchException("Consent requires this current authenticated home transport.");
    }

    private static void RequirePermissionWorld(WorldSnapshot state, LocalPlayerConnection player)
    {
        if (state.ProfileVersion is not (WorldControlPermissionAdmission.ProfileVersion or WorldDelegatedControlAdmission.ProfileVersion or WorldPartyWithdrawalAdmission.ProfileVersion or WorldCognitionAdmission.ProfileVersion)
            || state.ControlPermissions is null || !state.Inventory.CanControl(player.Delivery, player.ActorId)
            || state.Controllers?.Assignments.Any(row => row.ActorId == player.ActorId) != true)
            throw new AuthorityDispatchException("This world and authenticated home actor do not admit control permissions.");
    }

    // The board captures terms, never a grant of possession. AcquireControlAsync rechecks the live port and Core law.
    private LocalControlAcquisition? ObserveControlAcquisition(WorldSnapshot state, LocalPlayerConnection player,
        WorldControlPermissionSlot slot)
    {
        if (state.EffectiveControl is null || state.Revision == ulong.MaxValue
            || state.EffectiveControl.Find(slot.OwnerActorId)?.Revision == ulong.MaxValue
            || !player.ControlAvailable || player.LiveControlWitness is not null
            || slot.GranteeActorId != player.ActorId || slot.Status != WorldControlPermissionStatus.Accepted
            || state.Controllers!.Get(player.ActorId).Mode != WorldControllerMode.Human
            || state.Controllers.Get(slot.OwnerActorId) is not { Mode: WorldControllerMode.Autonomous, DisconnectId: not null } owner
            || state.EffectiveControl.Find(slot.OwnerActorId)?.Witness is not null
            || state.EffectiveControl.Slots.Any(row => row.Witness?.GranteeActorId == player.ActorId)
            || _players.Values.Any(port => port.ActorId == slot.OwnerActorId && port.NetworkEstablished)
            || !WorldControllerBoundaries.CanRestoreHuman(state, slot.OwnerActorId))
            return null;
        return new(slot.OwnerActorId, slot.GrantCommandId!.Value, slot.AcceptanceCommandId!.Value,
            slot.Revision, slot.Terms!.Capabilities, slot.TermsHash!,
            state.EffectiveControl.Find(slot.OwnerActorId)?.Revision ?? 0, owner.DisconnectId!.Value, owner.Revision);
    }
}
