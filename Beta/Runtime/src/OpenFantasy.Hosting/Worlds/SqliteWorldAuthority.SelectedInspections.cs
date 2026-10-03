using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Worlds;

public sealed partial class SqliteWorldAuthority
{
    /// <summary>Current, actionable participant terms only; this read never visits archived history.</summary>
    internal PaidQuoteView? ReadSelectedOpenPaidQuote(EntityId subject, EntityId actor,
        TrustedCommandDelivery delivery, WorldControlWitness? witness)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        lock (_gate)
        {
            EnsureAvailable();
            RequireSelectedInspection(actor, delivery, witness, WorldControlCapabilities.Trade);
            if (_state.Coordination?.PaidExchange?.FindOpen(subject) is not { } open
                || !open.HasParticipant(actor) || open.IsDue(_state.AdvancingSteps))
            {
                return null;
            }
            var view = PaidQuoteProjection.ProjectRecorded(_state, null, subject, actor);
            return view is { Status: PaidQuoteStatus.Open, ClosedAtSequence: null } ? view : null;
        }
    }

    /// <summary>Current, actionable participant terms only; this read never visits archived history.</summary>
    internal MaterialProposalView? ReadSelectedOpenMaterialProposal(EntityId subject, EntityId actor,
        TrustedCommandDelivery delivery, WorldControlWitness? witness)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        lock (_gate)
        {
            EnsureAvailable();
            RequireSelectedInspection(actor, delivery, witness, WorldControlCapabilities.Inventory);
            if (_state.Coordination?.Proposals?.FindOpen(subject) is not { } open
                || !open.HasParticipant(actor) || open.IsDue(_state.AdvancingSteps))
            {
                return null;
            }
            var view = MaterialProposalService.ProjectRecorded(_state, null, subject, actor);
            return view is { Status: MaterialProposalStatus.Open, ClosedAtSequence: null } ? view : null;
        }
    }

    /// <summary>
    /// Terminal participant history under its independent permission. Archived results require the same
    /// complete authenticated replay and pinned publishing root as the original controller reader.
    /// </summary>
    internal PaidQuoteView? ReadSelectedPaidQuoteHistory(EntityId subject, EntityId actor,
        TrustedCommandDelivery delivery, WorldControlWitness? witness)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        lock (_gate)
        {
            EnsureAvailable();
            RequireSelectedInspection(actor, delivery, witness, WorldControlCapabilities.PrivateHistory);
            RecordedQuoteReadHoldsGate?.Invoke();
            // A due offer can project as Expired without a terminal journal outcome. It is not history yet.
            if (_state.Coordination?.PaidExchange?.FindOpen(subject) is not null) return null;
            return ReadRecordedPaidQuoteUnderGate(subject, actor, (state, archived) =>
            {
                var outcome = state.Coordination?.PaidExchange?.FindOutcome(subject) ?? archived;
                if (outcome is null || outcome.Quote.QuoteId != subject || !outcome.Quote.HasParticipant(actor)) return null;
                var view = PaidQuoteProjection.ProjectRecorded(state, outcome, subject, actor);
                return view is { ClosedAtSequence: not null } && view.Status != PaidQuoteStatus.Open ? view : null;
            });
        }
    }

    /// <summary>Terminal participant history without granting current inventory or sender-presence authority.</summary>
    internal MaterialProposalView? ReadSelectedMaterialProposalHistory(EntityId subject, EntityId actor,
        TrustedCommandDelivery delivery, WorldControlWitness? witness)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        lock (_gate)
        {
            EnsureAvailable();
            RequireSelectedInspection(actor, delivery, witness, WorldControlCapabilities.PrivateHistory);
            if (_state.Coordination?.Proposals?.FindOpen(subject) is not null) return null;
            return ReadRecordedProposalUnderGate(subject, actor, (state, archived) =>
            {
                var outcome = state.Coordination?.Proposals?.FindOutcome(subject) ?? archived;
                if (outcome is null || outcome.Proposal.ProposalId != subject || !outcome.Proposal.HasParticipant(actor)) return null;
                var view = MaterialProposalService.ProjectRecorded(state, outcome, subject, actor);
                return view is { ClosedAtSequence: not null } && view.Status != MaterialProposalStatus.Open
                    ? view with { SenderPresenceRevision = null } : null;
            });
        }
    }

    private void RequireSelectedInspection(EntityId actor, TrustedCommandDelivery delivery,
        WorldControlWitness? witness, WorldControlCapabilities required)
    {
        bool authorized = witness is null
            ? _state.Inventory.CanControl(delivery, actor)
            : WorldActorCommandAuthority.IsCurrentWitness(_state, actor, delivery, witness, required);
        if (!authorized)
        {
            throw new AuthorityDispatchException("The delivery cannot inspect this actor under the supplied control and permission.");
        }
    }
}
