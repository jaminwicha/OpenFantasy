using Microsoft.Data.Sqlite;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

public sealed partial class SqliteWorldAuthority
{
    /// <summary>
    /// Test seam, null in every host: invoked while one recorded quote read holds the writer gate, so a test can keep
    /// a real inspection inside the gate and prove which readers wait for it and which never do.
    /// </summary>
    internal Action? RecordedQuoteReadHoldsGate { get; set; }

    /// <summary>
    /// Accepts one paid spot-exchange submission with its own independently supplied delivery. Under one
    /// lock it authorizes that delivery against the current world, returns the original receipt of an exact
    /// retry without another transfer, and otherwise selects and publishes the single-command advancing step.
    /// Inventory, production, balances, quotes, receipts and identities publish in that one SQL transaction.
    /// </summary>
    public WorldActionReceipt SubmitPaidExchangeCommand(WorldActionCommand command, TrustedCommandDelivery delivery,
        EntityId causeId) => SubmitPaidExchangeCommand(command, delivery, causeId, null);

    internal WorldActionReceipt SubmitPaidExchangeCommand(WorldActionCommand command, TrustedCommandDelivery delivery,
        EntityId causeId, Action<AuthorityCommitBoundary>? fault)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(delivery);
        lock (_gate)
        {
            EnsureAvailable();
            if (_state.ProfileVersion is not (WorldPaidExchangeAdmission.ProfileVersion or WorldMarketAdmission.ProfileVersion
                    or WorldEncounterRetreatAdmission.ProfileVersion or WorldEncounterOutcomeAdmission.ProfileVersion or WorldControllerAdmission.ProfileVersion)
                || command.Intent is not PaidExchangeIntent)
            {
                throw new AuthorityDispatchException("This boundary accepts paid exchange submissions of their own world profile.");
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

    /// <summary>
    /// Projects the current root for one quote participant only after the supplied delivery proves current
    /// control of that actor. The refusal is the same whether or not any quote would be found.
    /// </summary>
    internal T ReadPaidQuotesForController<T>(EntityId actorId, TrustedCommandDelivery delivery, Func<WorldSnapshot, T> project)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(project);
        lock (_gate)
        {
            EnsureAvailable();
            if (!_state.Inventory.CanControl(delivery, actorId))
            {
                throw new AuthorityDispatchException("The delivery cannot access this actor's quotes.");
            }
            return project(_state);
        }
    }

    /// <summary>
    /// Projects one paid quote by exact identity, including a terminal outcome that left the live segment at
    /// a coordinated cut. Current control is checked before anything is read, so a wrong delivery learns
    /// nothing. The journal stays the only authority: when the live book does not hold the identity, one
    /// complete verified replay inspects every retrieved closing root, under this lock and against the pinned
    /// live head; it is the same archive replay that serves saved proposals. Only an outcome the actor takes
    /// part in reaches the projection. Synchronous non-frame work: it must not run on a client frame or a
    /// latency-sensitive simulation callback. Damaged storage refuses and requires reopening.
    /// <para>
    /// An archived outcome is projected against the recorded root that published it, reconstructed by a second
    /// verified replay against the same pinned head. That root still holds the canonical market quote record, so the
    /// quote's market comes from history rather than from the live book, which no longer names it.
    /// </para>
    /// </summary>
    internal T ReadRecordedPaidQuoteForController<T>(EntityId quoteId, EntityId actorId,
        TrustedCommandDelivery delivery, Func<WorldSnapshot, WorldPaidQuoteOutcome?, T> project)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(project);
        lock (_gate)
        {
            EnsureAvailable();
            RecordedQuoteReadHoldsGate?.Invoke();
            if (!_state.Inventory.CanControl(delivery, actorId))
            {
                throw new AuthorityDispatchException("The delivery cannot access this actor's quotes.");
            }
            if (_state.Coordination?.PaidExchange is not { } live || _usage.MaterialCareArchiveCount == 0
                || live.FindOpen(quoteId) is not null || live.FindOutcome(quoteId) is not null)
            {
                return project(_state, null);
            }
            try
            {
                RecordedWorldPaidQuoteOutcome recorded = new WorldJournalRecovery().ReadPaidQuoteOutcome(_database, quoteId);
                if (recorded.SourceHeadRevision != _state.Revision || recorded.SourceHeadHash != _stateHash)
                {
                    throw new InvalidDataException("Quote retrieval does not match the pinned live head.");
                }
                WorldPaidQuoteOutcome? archived = recorded.Outcome;
                if (archived is null || !archived.Quote.HasParticipant(actorId))
                {
                    return project(_state, null);
                }
                RecordedWorldState publishing = new WorldJournalRecovery().ReadRecordedState(_database, archived.WorldSequence);
                if (publishing.SourceHeadRevision != _state.Revision || publishing.SourceHeadHash != _stateHash
                    || publishing.State.Coordination?.PaidExchange?.FindOutcome(quoteId) is null)
                {
                    throw new InvalidDataException("The archived quote's publishing root does not match the pinned live head.");
                }
                return project(publishing.State, archived);
            }
            catch (Exception error) when (error is IOException or InvalidDataException or SqliteException)
            {
                _requiresRecovery = true;
                throw;
            }
        }
    }
}
