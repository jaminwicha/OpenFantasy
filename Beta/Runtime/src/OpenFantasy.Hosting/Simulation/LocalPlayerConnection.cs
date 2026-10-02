using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>
/// One authenticated in-process player port of a shared world host. It owns no clock, writer or NPC
/// controller. Closing the port relinquishes uncommitted input; it never closes another player's world.
/// A network adapter must separately authenticate transport and journal its disconnect/control policy.
/// </summary>
public sealed class LocalPlayerConnection : IAsyncDisposable
{
    private readonly LocalWorldSession _host;
    private LocalWorldView _view;

    internal LocalPlayerConnection(LocalWorldSession host, EntityId actorId, TrustedCommandDelivery delivery,
        LocalWorldView view, CargoPresentation.LocalRegionObserver? regions)
    {
        _host = host;
        ActorId = actorId;
        Delivery = delivery;
        _view = view;
        Regions = regions;
    }

    public EntityId ActorId { get; }
    public LocalWorldView ReadView() => Volatile.Read(ref _view);
    public LocalWorldRunState ReadRunState() => _host.ReadRunState(this);
    /// <summary>Acknowledge gameplay readiness after applying the playable view; false clears held movement.</summary>
    public void SetReady(bool ready) => _host.SetReady(this, ready);
    public void SetMovement(long dx, long dy) => _host.SetMovement(this, dx, dy);
    public Task<WorldActionReceipt> SubmitAction(WorldActionCommand command) => _host.SubmitAction(this, command);
    public Task<LocalTradeInquiryView> SubmitTradeInquiryAsync(LocalTradeInquiry inquiry) => _host.SubmitTradeInquiryAsync(this, inquiry);
    public Task<LocalTradeInquiryView?> CancelTradeInquiryAsync() => _host.CancelTradeInquiryAsync(this);
    public LocalTradeInquiryView? ReadTradeInquiry() => _host.ReadTradeInquiry(this);
    public Task<PaidQuoteView?> InspectPaidQuoteAsync(EntityId quoteId) => _host.InspectPaidQuoteAsync(this, quoteId);
    public Task<MaterialProposalView?> InspectMaterialProposalAsync(EntityId proposalId) => _host.InspectMaterialProposalAsync(this, proposalId);
    public ValueTask DisposeAsync() => new(_host.ClosePlayerAsync(this));

    internal TrustedCommandDelivery Delivery { get; }
    internal Task HostCompletion => _host.Completion;
    internal CargoPresentation.LocalRegionObserver? Regions { get; }
    internal bool Connected { get; set; } = true;
    internal bool Ready { get; set; }
    internal PendingLocalAction? Pending { get; set; }
    internal long MovementX { get; set; }
    internal long MovementY { get; set; }
    internal long MovementAt { get; set; }
    internal LocalInspectionSlot<PaidQuoteView?> QuoteInspection { get; } = new("One quote inspection is already awaiting its result.");
    internal LocalInspectionSlot<MaterialProposalView?> ProposalInspection { get; } = new("One proposal inspection is already awaiting its result.");
    internal void Publish(LocalWorldView view) => Volatile.Write(ref _view, view);
    internal void ClearMovement() { MovementX = 0; MovementY = 0; }
}
