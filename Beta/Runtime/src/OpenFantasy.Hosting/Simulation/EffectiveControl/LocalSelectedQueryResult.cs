using OpenFantasy.Core;

namespace OpenFantasy.Hosting.Simulation;

public enum LocalSelectedQueryOperation
{
    SubmitTradeInquiry,
    CancelTradeInquiry,
    ReadTradeInquiry,
    InspectOpenPaidQuote,
    InspectOpenMaterialProposal,
    InspectPaidQuoteHistory,
    InspectMaterialProposalHistory
}

/// <summary>A response belongs to its captured selection, never whichever actor happens to be selected later.</summary>
public sealed record LocalSelectedQueryResult<T>(EntityId WorldId, EntityId BranchId,
    LocalActorSelection Selection, LocalSelectedQueryOperation Operation, EntityId? SubjectId, T Value)
{
    public void ValidateFor(LocalSelectedPlayerObservation observation, LocalSelectedQueryOperation operation,
        EntityId? subjectId = null)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (WorldId != observation.HomeView.WorldId || BranchId != observation.HomeView.BranchId
            || Selection != observation.Selection || Operation != operation || SubjectId != subjectId
            || Selection.HomeActorId != observation.HomeView.PlayerActorId)
            throw new AuthorityDispatchException("The query response belongs to a different actor selection or request.");
    }
}

internal sealed record LocalInspectionKey(EntityId SubjectId, LocalActorSelection? Selection = null,
    LocalSelectedQueryOperation? Operation = null);
