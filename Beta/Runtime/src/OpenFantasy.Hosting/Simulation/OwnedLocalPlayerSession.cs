using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>The solo composition owns its complete host; a remote session owns only its connection.</summary>
public sealed class OwnedLocalPlayerSession(LocalWorldSession world) : IPlayerSession
{
    private readonly LocalWorldSession _world = world ?? throw new ArgumentNullException(nameof(world));
    public Task Completion => _world.Completion;
    public LocalWorldView ReadView() => _world.ReadView();
    public LocalWorldRunState ReadRunState() => _world.ReadRunState();
    public void SetReady(bool ready) => _world.SetReady(ready);
    public void SetMovement(long dx, long dy) => _world.SetMovement(dx, dy);
    public Task<WorldActionReceipt> SubmitAction(WorldActionCommand command) => _world.SubmitAction(command);
    public Task<LocalTradeInquiryView> SubmitTradeInquiryAsync(LocalTradeInquiry inquiry) => _world.SubmitTradeInquiryAsync(inquiry);
    public Task<LocalTradeInquiryView?> CancelTradeInquiryAsync() => _world.CancelTradeInquiryAsync();
    public LocalTradeInquiryView? ReadTradeInquiry() => _world.ReadTradeInquiry();
    public Task<PaidQuoteView?> InspectPaidQuoteAsync(EntityId quoteId) => _world.InspectPaidQuoteAsync(quoteId);
    public Task<MaterialProposalView?> InspectMaterialProposalAsync(EntityId proposalId) => _world.InspectMaterialProposalAsync(proposalId);
    public ValueTask DisposeAsync() => _world.DisposeAsync();
}
