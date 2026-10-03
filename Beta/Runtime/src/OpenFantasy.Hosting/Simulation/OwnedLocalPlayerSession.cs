using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>The solo composition owns its complete host; a remote session owns only its connection.</summary>
public sealed class OwnedLocalPlayerSession(LocalWorldSession world) : IPlayerSession, IPrivateHistoryPlayerSession
{
    private readonly LocalWorldSession _world = world ?? throw new ArgumentNullException(nameof(world));
    public Task Completion => _world.Completion;
    public LocalWorldView ReadView() => _world.ReadView();
    public LocalWorldRunState ReadRunState() => _world.ReadRunState();
    public PlayerControlStatus ReadControlStatus() => _world.Player.ReadControlStatus();
    public void Resume(PlayerControlStatus observed) => _world.Player.Resume(observed);
    public void SetReady(bool ready) => _world.SetReady(ready);
    public void SetMovement(long dx, long dy) => _world.SetMovement(dx, dy);
    public Task<WorldActionReceipt> SubmitAction(WorldActionCommand command) => _world.SubmitAction(command);
    public Task<LocalTradeInquiryView> SubmitTradeInquiryAsync(LocalTradeInquiry inquiry) => _world.SubmitTradeInquiryAsync(inquiry);
    public Task<LocalTradeInquiryView?> CancelTradeInquiryAsync() => _world.CancelTradeInquiryAsync();
    public LocalTradeInquiryView? ReadTradeInquiry() => _world.ReadTradeInquiry();
    public Task<PaidQuoteView?> InspectPaidQuoteAsync(EntityId quoteId) => _world.InspectPaidQuoteAsync(quoteId);
    public Task<MaterialProposalView?> InspectMaterialProposalAsync(EntityId proposalId) => _world.InspectMaterialProposalAsync(proposalId);
    public bool SupportsPrivateHistoryDirectory => true;
    public LocalSelectedPlayerObservation ReadPrivateHistoryObservation() => _world.Player.ReadSelectedObservation();
    public Task<LocalSelectedQueryResult<LocalPrivateHistoryPage>> ReadSelectedPrivateHistoryAsync(
        LocalActorSelection observed, LocalPrivateHistoryPageRequest request) =>
        _world.Player.ReadSelectedPrivateHistoryAsync(observed, request);
    public Task<LocalSelectedQueryResult<PaidQuoteView?>> InspectSelectedPaidQuoteHistoryAsync(
        LocalActorSelection observed, EntityId subject) => _world.Player.InspectSelectedPaidQuoteHistoryAsync(observed, subject);
    public Task<LocalSelectedQueryResult<MaterialProposalView?>> InspectSelectedMaterialProposalHistoryAsync(
        LocalActorSelection observed, EntityId subject) => _world.Player.InspectSelectedMaterialProposalHistoryAsync(observed, subject);
    public ValueTask DisposeAsync() => _world.DisposeAsync();
}
