using Microsoft.Data.Sqlite;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

public sealed partial class SqliteWorldAuthority
{
    /// <summary>
    /// Accepts one saved-proposal submission with its own independently supplied delivery. Under one
    /// lock it authorizes that delivery against the current world, returns the original outcome of an
    /// exact retry, and otherwise selects and publishes the single-command advancing step. The
    /// delivery is a local trusted input; this boundary performs no transport handshake.
    /// </summary>
    public WorldActionReceipt SubmitMaterialProposalCommand(WorldActionCommand command,
        TrustedCommandDelivery delivery, EntityId causeId) =>
        SubmitMaterialProposalCommand(command, delivery, causeId, null);

    internal WorldActionReceipt SubmitMaterialProposalCommand(WorldActionCommand command,
        TrustedCommandDelivery delivery, EntityId causeId, Action<AuthorityCommitBoundary>? fault)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(delivery);
        lock (_gate)
        {
            EnsureAvailable();
            if (_state.ProfileVersion is not (30 or WorldPaidExchangeAdmission.ProfileVersion or WorldMarketAdmission.ProfileVersion
                    or WorldEncounterRetreatAdmission.ProfileVersion or WorldEncounterOutcomeAdmission.ProfileVersion or WorldControllerAdmission.ProfileVersion or WorldRecoveryAdmission.ProfileVersion)
                || command.Intent is not MaterialProposalIntent)
            {
                throw new AuthorityDispatchException("This boundary accepts saved proposal submissions of their own world profile.");
            }
            // Authorization precedes every disclosure, including whether an earlier outcome exists.
            WorldActionReceipt? prior = FindReceipt(command, delivery);
            if (prior is not null)
            {
                return prior;
            }
            WorldStepBatch batch = SelectMaterialProposalBatch(_state, command, causeId);
            WorldStepDecision decision = Commit(batch, fault);
            return decision.Receipts.Single(receipt => receipt.CommandId == command.CommandId);
        }
    }

    /// <summary>The exact batch this boundary selects for one submission against the given root.</summary>
    internal static WorldStepBatch SelectMaterialProposalBatch(WorldSnapshot state, WorldActionCommand command,
        EntityId causeId)
    {
        try
        {
            return new WorldStepBatch(causeId, state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
                WorldBatchKind.AdvancingStep, [], [command], state.ProfileVersion);
        }
        catch (ArgumentException)
        {
            throw new AuthorityDispatchException("The submission cannot be selected in the current world.");
        }
    }

    /// <summary>
    /// Projects the current root for one actor only after the supplied delivery proves current
    /// control of that actor. The refusal is the same whether or not anything would be found.
    /// </summary>
    internal T ReadForController<T>(EntityId actorId, TrustedCommandDelivery delivery, Func<WorldSnapshot, T> project)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(project);
        lock (_gate)
        {
            EnsureAvailable();
            if (!_state.Inventory.CanControl(delivery, actorId))
            {
                throw new AuthorityDispatchException("The delivery cannot access this actor's proposals.");
            }
            return project(_state);
        }
    }

    /// <summary>
    /// Projects one proposal by exact identity, including a terminal outcome that left the live
    /// segment at a coordinated cut. Current control is checked before anything is read, so a wrong
    /// delivery learns nothing. The journal stays the only authority: when the live owner does not
    /// hold the identity, one complete verified replay inspects every retrieved closing root, under
    /// this lock and against the pinned live head. Only an outcome the actor takes part in reaches
    /// the projection. Synchronous non-frame work: it must not run on a client frame or a
    /// latency-sensitive simulation callback. Damaged storage refuses and requires reopening.
    /// </summary>
    internal T ReadRecordedProposalForController<T>(EntityId proposalId, EntityId actorId,
        TrustedCommandDelivery delivery, Func<WorldSnapshot, WorldMaterialProposalOutcome?, T> project)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(project);
        lock (_gate)
        {
            EnsureAvailable();
            if (!_state.Inventory.CanControl(delivery, actorId))
            {
                throw new AuthorityDispatchException("The delivery cannot access this actor's proposals.");
            }
            if (_state.Coordination?.Proposals is not { } live || _usage.MaterialCareArchiveCount == 0
                || live.FindOpen(proposalId) is not null || live.FindOutcome(proposalId) is not null)
            {
                return project(_state, null);
            }
            try
            {
                RecordedWorldMaterialProposalOutcome recorded =
                    new WorldJournalRecovery().ReadMaterialProposalOutcome(_database, proposalId);
                if (recorded.SourceHeadRevision != _state.Revision || recorded.SourceHeadHash != _stateHash)
                {
                    throw new InvalidDataException("Proposal retrieval does not match the pinned live head.");
                }
                WorldMaterialProposalOutcome? archived = recorded.Outcome;
                return project(_state, archived is not null && archived.Proposal.HasParticipant(actorId) ? archived : null);
            }
            catch (Exception error) when (error is IOException or InvalidDataException or SqliteException)
            {
                _requiresRecovery = true;
                throw;
            }
        }
    }
}
