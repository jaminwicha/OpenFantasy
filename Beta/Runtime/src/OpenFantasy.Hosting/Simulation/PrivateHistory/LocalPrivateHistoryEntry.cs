using OpenFantasy.Core;

namespace OpenFantasy.Hosting.Simulation;

public enum LocalPrivateHistoryKind { PaidQuote = 1, MaterialProposal = 2 }

/// <summary>A participant's terminal record identity, without live terms or execution authority.</summary>
public sealed record LocalPrivateHistoryEntry(LocalPrivateHistoryKind Kind, EntityId SubjectId,
    EntityId CounterpartyActorId, ulong ClosedAtSequence, PaidQuoteStatus? PaidQuoteStatus = null,
    MaterialProposalStatus? MaterialProposalStatus = null)
{
    public void Validate()
    {
        if (ClosedAtSequence == 0)
            throw new ArgumentException("A history entry requires a terminal publication sequence.");
        bool valid = Kind switch
        {
            LocalPrivateHistoryKind.PaidQuote => MaterialProposalStatus is null
                && PaidQuoteStatus is global::OpenFantasy.Hosting.Simulation.PaidQuoteStatus.Filled or global::OpenFantasy.Hosting.Simulation.PaidQuoteStatus.Superseded
                    or global::OpenFantasy.Hosting.Simulation.PaidQuoteStatus.Withdrawn or global::OpenFantasy.Hosting.Simulation.PaidQuoteStatus.Expired,
            LocalPrivateHistoryKind.MaterialProposal => PaidQuoteStatus is null
                && MaterialProposalStatus is global::OpenFantasy.Hosting.Simulation.MaterialProposalStatus.Accepted or global::OpenFantasy.Hosting.Simulation.MaterialProposalStatus.Declined
                    or global::OpenFantasy.Hosting.Simulation.MaterialProposalStatus.Withdrawn or global::OpenFantasy.Hosting.Simulation.MaterialProposalStatus.Expired,
            _ => false
        };
        if (!valid) throw new ArgumentException("A history entry requires exactly its kind's terminal status.");
    }
}
