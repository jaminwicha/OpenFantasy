using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>
/// Optional participant control interface. Permanent home identity remains on IPlayerSession;
/// operated-character input always captures its separate selection lifetime.
/// </summary>
public interface ISelectedPlayerSession
{
    bool SupportsControlPermissions { get; }
    bool SupportsActorSelection { get; }
    LocalSelectedPlayerObservation ReadSelectedObservation();
    Task<LocalControlPermissionBoard> ReadControlPermissionsAsync();
    Task<WorldActionReceipt> SubmitControlPermissionAsync(WorldActionCommand command);
    Task<WorldControlWitness> AcquireControlAsync(LocalControlAcquisition observed);
    Task ReleaseControlAsync(WorldControlWitness observed);
    Task<LocalSelectedPlayerObservation> SelectActorAsync(LocalActorSelection observed, EntityId actorId);
    void ResumeSelected(LocalSelectedPlayerObservation observed);
    void PauseSelected(LocalActorSelection observed);
    void SetSelectedMovement(LocalActorSelection observed, long dx, long dy);
    Task<WorldActionReceipt> SubmitSelectedAction(LocalActorSelection observed, WorldActionCommand command);
    Task<LocalSelectedQueryResult<LocalTradeInquiryView>> SubmitSelectedTradeInquiryAsync(
        LocalActorSelection observed, LocalTradeInquiry inquiry);
    Task<LocalSelectedQueryResult<LocalTradeInquiryView?>> CancelSelectedTradeInquiryAsync(LocalActorSelection observed);
    Task<LocalSelectedQueryResult<LocalTradeInquiryView?>> ReadSelectedTradeInquiryAsync(LocalActorSelection observed);
    Task<LocalSelectedQueryResult<PaidQuoteView?>> InspectSelectedOpenPaidQuoteAsync(LocalActorSelection observed, EntityId subject);
    Task<LocalSelectedQueryResult<MaterialProposalView?>> InspectSelectedOpenMaterialProposalAsync(LocalActorSelection observed, EntityId subject);
    Task<LocalSelectedQueryResult<PaidQuoteView?>> InspectSelectedPaidQuoteHistoryAsync(LocalActorSelection observed, EntityId subject);
    Task<LocalSelectedQueryResult<MaterialProposalView?>> InspectSelectedMaterialProposalHistoryAsync(LocalActorSelection observed, EntityId subject);
}
