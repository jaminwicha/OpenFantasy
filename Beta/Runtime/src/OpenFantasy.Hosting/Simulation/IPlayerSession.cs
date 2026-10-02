using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Participant input and visible state. A session grants no direct world or history-writer access.</summary>
public interface IPlayerSession : IAsyncDisposable
{
    Task Completion { get; }
    LocalWorldView ReadView();
    LocalWorldRunState ReadRunState();
    void SetReady(bool ready);
    void SetMovement(long dx, long dy);
    Task<WorldActionReceipt> SubmitAction(WorldActionCommand command);
    Task<LocalTradeInquiryView> SubmitTradeInquiryAsync(LocalTradeInquiry inquiry);
    Task<LocalTradeInquiryView?> CancelTradeInquiryAsync();
    LocalTradeInquiryView? ReadTradeInquiry();
    Task<PaidQuoteView?> InspectPaidQuoteAsync(EntityId quoteId);
    Task<MaterialProposalView?> InspectMaterialProposalAsync(EntityId proposalId);
}
