using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Prepares bounded canonical storage rows from one trusted, current host-selected batch.</summary>
internal sealed class WorldCommitPreparation
{
    private readonly WorldStepProcessor _processor = new();

    internal PreparedWorldCommit Prepare(WorldSnapshot state, WorldIdentityIndex genesisIdentities,
        ContentHash beforeHash, WorldStepBatch batch, Func<EntityId, bool> historicallyOccupied)
    {
        ValidateDispatch(state, batch);
        WorldStepDecision decision;
        try
        {
            decision = _processor.Decide(state, batch,
                identity => genesisIdentities.Contains(identity) || historicallyOccupied(identity));
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new AuthorityDispatchException("The selected batch cannot be committed in the current world.");
        }
        byte[] after = WorldSnapshotCodec.Encode(decision.Candidate);
        byte[] acceptedBatch = WorldStepCodec.Encode(batch);
        byte[] outcomes = WorldReceiptCodecV1.EncodeBatch(decision.Receipts);
        ulong sequence = decision.Candidate.Revision;
        if (sequence != checked(state.Revision + 1))
        {
            throw new InvalidDataException("The prepared world did not advance one commit revision.");
        }
        Dictionary<EntityId, WorldActionCommand> actions = batch.Actions.ToDictionary(action => action.CommandId);
        List<StoredWorldCommandOutcome> commands = [];
        foreach (WorldActionReceipt receipt in decision.Receipts)
        {
            if (!actions.Remove(receipt.CommandId, out WorldActionCommand? command)
                || receipt.PrincipalId != command.PrincipalId || receipt.WorldId != state.WorldId
                || receipt.BranchId != state.BranchId || receipt.CommitSequence != sequence
                || receipt.CommandHash != command.ContentHash)
            {
                throw new InvalidDataException("A prepared receipt does not match its exact actor command.");
            }
            commands.Add(new(receipt.PrincipalId, receipt.CommandId, sequence,
                WorldActionCodec.Encode(command), WorldReceiptCodecV1.Encode(receipt)));
        }
        if (actions.Count != 0)
        {
            throw new InvalidDataException("The prepared batch omitted an actor command outcome.");
        }
        StoredWorldIdentity[] identities = decision.Identities.Select(value => new StoredWorldIdentity(
            value.Identity, sequence, Map(value.Kind))).ToArray();
        return new PreparedWorldCommit(decision, after,
            new StoredWorldCommit(sequence, Map(batch.Kind), batch.CauseId, decision.Candidate.AdvancingSteps,
                acceptedBatch, outcomes, beforeHash, ContentHash.Compute(after)), commands.AsReadOnly(), identities);
    }

    private static void ValidateDispatch(WorldSnapshot state, WorldStepBatch batch)
    {
        if (batch.ProfileVersion != state.ProfileVersion || batch.WorldId != state.WorldId || batch.BranchId != state.BranchId
            || batch.ExpectedRevision != state.Revision || batch.ExpectedAdvancingSteps != state.AdvancingSteps
            || (batch.Kind == WorldBatchKind.HeldInteraction) != state.HasReadingHold)
        {
            throw new AuthorityDispatchException("The selected batch does not match the current world revision or hold.");
        }
        foreach (WorldActionCommand command in batch.Actions)
        {
            var delivery = new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId);
            if (!state.Inventory.CanControl(delivery, command.ActorId))
            {
                throw new AuthorityDispatchException("A fresh actor command requires its current origin and controller.");
            }
        }
        foreach (WorldMovementInput movement in batch.Movement)
        {
            if (!state.Inventory.CanControl(movement.Delivery, movement.ActorId))
            {
                throw new AuthorityDispatchException("A selected movement requires its current controller.");
            }
        }
    }

    private static WorldCommitKind Map(WorldBatchKind kind) => kind switch
    {
        WorldBatchKind.AdvancingStep => WorldCommitKind.AdvancingStep,
        WorldBatchKind.HeldInteraction => WorldCommitKind.HeldInteraction,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static WorldIdentityKind Map(WorldCommittedIdentityKind kind) => kind switch
    {
        WorldCommittedIdentityKind.CommitCause => WorldIdentityKind.CommitCause,
        WorldCommittedIdentityKind.Command => WorldIdentityKind.Command,
        WorldCommittedIdentityKind.Result => WorldIdentityKind.Result,
        WorldCommittedIdentityKind.SpawnedEntity => WorldIdentityKind.SpawnedEntity,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}

internal sealed record PreparedWorldCommit(WorldStepDecision Decision, byte[] CandidateBytes,
    StoredWorldCommit Stored, IReadOnlyList<StoredWorldCommandOutcome> Commands,
    IReadOnlyList<StoredWorldIdentity> Identities);
