using OpenFantasy.Core;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Optional bounded discovery and typed terminal inspection for an explicitly captured participant selection.</summary>
public interface IPrivateHistoryPlayerSession
{
    bool SupportsPrivateHistoryDirectory { get; }
    LocalSelectedPlayerObservation ReadPrivateHistoryObservation();
    Task<LocalSelectedQueryResult<LocalPrivateHistoryPage>> ReadSelectedPrivateHistoryAsync(
        LocalActorSelection observed, LocalPrivateHistoryPageRequest request);
    Task<LocalSelectedQueryResult<PaidQuoteView?>> InspectSelectedPaidQuoteHistoryAsync(
        LocalActorSelection observed, EntityId subject);
    Task<LocalSelectedQueryResult<MaterialProposalView?>> InspectSelectedMaterialProposalHistoryAsync(
        LocalActorSelection observed, EntityId subject);
}
