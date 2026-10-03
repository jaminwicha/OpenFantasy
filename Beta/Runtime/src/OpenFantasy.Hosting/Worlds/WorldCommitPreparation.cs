using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Prepares bounded canonical storage rows from one trusted, current host-selected batch.</summary>
internal sealed class WorldCommitPreparation
{
    private readonly WorldStepProcessor _processor = new();

    internal PreparedWorldCommit Prepare(WorldSnapshot state, WorldIdentityIndex genesisIdentities,
        ContentHash beforeHash, WorldStepBatch batch, Func<EntityId, bool> historicallyOccupied, bool captureMaterialEvidence = false)
    {
        ValidateDispatch(state, batch);
        WorldStepDecision decision;
        try
        {
            bool Occupied(EntityId identity) => genesisIdentities.ContainsEntity(identity) || historicallyOccupied(identity);
            decision = captureMaterialEvidence
                ? _processor.DecideWithMaterialEvidence(state, batch, Occupied)
                : _processor.Decide(state, batch, Occupied);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new AuthorityDispatchException("The selected batch cannot be committed in the current world.");
        }
        WorldCanonicalFrame? frame = decision.CandidateFrame;
        if (frame is not null && !frame.IsBoundTo(decision.Candidate))
        {
            throw new InvalidDataException("A prepared canonical frame does not belong to its exact candidate.");
        }
        // Preparation that already encoded this exact root supplies its bytes; a candidate that
        // carries no frame is encoded here, where it always was. The stored row owns its own copy.
        byte[] after = frame is null ? WorldSnapshotCodec.Encode(decision.Candidate) : frame.ToArray();
        byte[] acceptedBatch = WorldStepCodec.Encode(batch);
        byte[] outcomes = WorldReceiptCodecV1.EncodeBatch(decision.Receipts);
        ulong sequence = decision.Candidate.Revision;
        if (sequence != checked(state.Revision + 1))
        {
            throw new InvalidDataException("The prepared world did not advance one commit revision.");
        }
        Dictionary<EntityId, WorldActionCommand> actions = batch.Actions.ToDictionary(action => action.CommandId);
        AccountForPendingCommand(state, batch, decision.Candidate, actions);
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
                acceptedBatch, outcomes, beforeHash, frame?.ContentHash ?? ContentHash.Compute(after)),
            commands.AsReadOnly(), identities);
    }

    private static void ValidateDispatch(WorldSnapshot state, WorldStepBatch batch)
    {
        if (batch.ProfileVersion != state.ProfileVersion || batch.WorldId != state.WorldId || batch.BranchId != state.BranchId
            || batch.ExpectedRevision != state.Revision || batch.ExpectedAdvancingSteps != state.AdvancingSteps
            || batch.Kind is not WorldBatchKind.ControllerLifecycle and not WorldBatchKind.ControlPermission and not WorldBatchKind.ControlAssignment && (batch.Kind == WorldBatchKind.HeldInteraction) != state.HasReadingHold
            || batch.Kind is not WorldBatchKind.ControllerLifecycle and not WorldBatchKind.ControlPermission and not WorldBatchKind.ControlAssignment && (batch.Kind == WorldBatchKind.ConsequenceContinuation) != (state.PendingExcavation is not null)
            || batch.Kind is not WorldBatchKind.EncounterLifecycle and not WorldBatchKind.Care and not WorldBatchKind.ControllerLifecycle and not WorldBatchKind.ControlPermission and not WorldBatchKind.ControlAssignment
                && (batch.Kind is WorldBatchKind.TacticalAdvance or WorldBatchKind.EncounterDecision or WorldBatchKind.EncounterResolution)
                    != (state.Pacing?.Encounter is not null))
        {
            throw new AuthorityDispatchException("The selected batch does not match the current world revision or hold.");
        }
        foreach (WorldActionCommand command in batch.Actions)
        {
            bool authorized = state.EffectiveControl is null
                ? state.Inventory.CanControl(new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId), command.ActorId)
                : WorldActorCommandAuthority.AuthorizeSelection(state, command) is not null;
            if (!authorized)
            {
                throw new AuthorityDispatchException("A fresh actor command requires its current origin and controller.");
            }
        }
        foreach (WorldMovementInput movement in batch.Movement)
        {
            bool authorized = state.EffectiveControl is null
                ? state.Inventory.CanControl(movement.Delivery, movement.ActorId)
                : WorldActorCommandAuthority.AuthorizeMovement(state, movement) is not null;
            if (!authorized)
            {
                throw new AuthorityDispatchException("A selected movement requires its current controller.");
            }
        }
    }

    private static void AccountForPendingCommand(WorldSnapshot state, WorldStepBatch batch, WorldSnapshot candidate,
        Dictionary<EntityId, WorldActionCommand> actions)
    {
        if (batch.Kind is WorldBatchKind.ControllerLifecycle or WorldBatchKind.ControlPermission or WorldBatchKind.ControlAssignment)
        {
            if (!ReferenceEquals(state.PendingExcavation, candidate.PendingExcavation))
                throw new InvalidDataException("A session event must preserve any pending gameplay consequence exactly.");
            return;
        }
        if (state.PendingExcavation is { } previous)
        {
            if (batch.Kind != WorldBatchKind.ConsequenceContinuation || batch.Actions.Count != 0 || batch.Movement.Count != 0)
            {
                throw new InvalidDataException("Pending consequences require an isolated continuation.");
            }
            if (candidate.PendingExcavation is { } continuing)
            {
                if (!SameCommand(previous.Command, continuing.Command)
                    || previous.TriggerCauseId != continuing.TriggerCauseId || previous.BasisRevision != continuing.BasisRevision)
                {
                    throw new InvalidDataException("A continuation changed its retained command or trigger binding.");
                }
            }
            else
            {
                // Completion owes the original accepted command its one terminal outcome.
                actions.Add(previous.Command.CommandId, previous.Command);
            }
        }
        else if (candidate.PendingExcavation is { } started)
        {
            if (state.ConsequencePolicy is null || batch.Kind != WorldBatchKind.AdvancingStep
                || batch.Actions.Count != 1 || batch.Movement.Count != 0
                || batch.Actions[0].Intent is not ExcavateIntent || !SameCommand(batch.Actions[0], started.Command)
                || started.TriggerCauseId != batch.CauseId || started.BasisRevision != state.Revision
                || !actions.Remove(started.Command.CommandId))
            {
                throw new InvalidDataException("A deferred outcome does not match one exact newly retained excavation command.");
            }
            // No receipt is eligible here: the command is now owned by the candidate's pending state.
        }
    }

    private static bool SameCommand(WorldActionCommand left, WorldActionCommand right) =>
        WorldActionCodec.Encode(left).AsSpan().SequenceEqual(WorldActionCodec.Encode(right));

    private static WorldCommitKind Map(WorldBatchKind kind) => kind switch
    {
        WorldBatchKind.AdvancingStep => WorldCommitKind.AdvancingStep,
        WorldBatchKind.HeldInteraction => WorldCommitKind.HeldInteraction,
        WorldBatchKind.ConsequenceContinuation => WorldCommitKind.ConsequenceContinuation,
        WorldBatchKind.TacticalAdvance => WorldCommitKind.TacticalAdvance,
        WorldBatchKind.EncounterDecision => WorldCommitKind.EncounterDecision,
        WorldBatchKind.EncounterResolution => WorldCommitKind.EncounterResolution,
        WorldBatchKind.EncounterLifecycle => WorldCommitKind.EncounterLifecycle,
        WorldBatchKind.Care => WorldCommitKind.Care,
        WorldBatchKind.ControllerLifecycle => WorldCommitKind.ControllerLifecycle,
        WorldBatchKind.ControlPermission => WorldCommitKind.ControlPermission,
        WorldBatchKind.ControlAssignment => WorldCommitKind.ControlAssignment,
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
    IReadOnlyList<StoredWorldIdentity> Identities)
{
    internal StoredWorldResourceArchive? ResourceArchive { get; } = Decision.SealedResourceSegment is { } segment
        ? new(segment.Reference, Stored.Sequence, segment.Encode()) : null;

    internal StoredWorldMaterialCareArchive? MaterialCareArchive { get; } = Decision.SealedMaterialCareSegment is { } segment
        ? new(segment.Reference, Stored.Sequence, segment.Encode()) : null;
}
