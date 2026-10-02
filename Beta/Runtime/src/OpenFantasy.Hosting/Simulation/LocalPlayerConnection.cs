using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Diagnostics;
using System.Security.Cryptography;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>
/// One authenticated in-process player port of a shared world host. It owns no clock, writer or NPC
/// controller. Closing the port relinquishes uncommitted input; it never closes another player's world.
/// A network adapter must separately authenticate transport and journal its disconnect/control policy.
/// </summary>
public sealed class LocalPlayerConnection : IAsyncDisposable
{
    internal string TimingOwner { get; } = Guid.NewGuid().ToString("N");
    private readonly LocalWorldSession _host;
    private LocalWorldView _view;
    internal PlayerControlStatus ControlStatus { get; set; }

    internal LocalPlayerConnection(LocalWorldSession host, EntityId actorId, TrustedCommandDelivery delivery,
        LocalWorldView view, CargoPresentation.LocalRegionObserver? regions, bool controlAvailable = true,
        bool publishesControlStatus = false)
    {
        _host = host;
        ActorId = actorId;
        Delivery = delivery;
        _view = view;
        Regions = regions;
        ControlAvailable = controlAvailable;
        ControlStatus = !publishesControlStatus ? PlayerControlStatus.Available : new(
            controlAvailable ? PlayerControlState.Available : PlayerControlState.Pending,
            EntityId.FromBytes(RandomNumberGenerator.GetBytes(EntityId.ByteLength)), actorId, 0);
        HostTimingEvents.Log.Bind(TimingOwner, host.TimingOwner);
        if (HostTimingEvents.Log.IsEnabled()) HostTimingEvents.Log.Bind(TimingOwner, actorId.ToString());
    }

    public EntityId ActorId { get; }
    public LocalWorldView ReadView() => Volatile.Read(ref _view);
    public LocalWorldRunState ReadRunState() => _host.ReadRunState(this);
    public PlayerControlStatus ReadControlStatus() => _host.ReadControlStatus(this);
    public void Resume(PlayerControlStatus observed) => _host.Resume(this, observed);
    public Task<Networking.PlayerParticipantStatus> ReadParticipantStatusAsync() => _host.ReadParticipantStatusAsync(this);
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
    internal bool ControlAvailable { get; set; }
    internal bool NetworkEstablished { get; set; }
    internal (LocalWorldView View, PlayerControlStatus Control) ReadObservation() => _host.ReadObservation(this);
    internal void SetLegacyNetworkReady(bool ready) => _host.SetLegacyNetworkReady(this, ready);
    internal Task CompleteNetworkAdmissionAsync(CancellationToken cancellationToken, Action validateAdmission) =>
        _host.CompleteNetworkAdmissionAsync(this, cancellationToken, validateAdmission);
    internal Task ConfirmTransportDisconnectedAsync(WorldControllerDisconnectReason reason) =>
        _host.ConfirmTransportDisconnectedAsync(this, reason);
    internal PendingLocalAction? Pending { get; set; }
    internal long MovementX { get; set; }
    internal long MovementY { get; set; }
    internal long MovementAt { get; set; }
    internal LocalInspectionSlot<PaidQuoteView?> QuoteInspection { get; } = new("One quote inspection is already awaiting its result.");
    internal LocalInspectionSlot<MaterialProposalView?> ProposalInspection { get; } = new("One proposal inspection is already awaiting its result.");
    internal void Publish(LocalWorldView view) => Volatile.Write(ref _view, view);
    internal void ClearMovement() { MovementX = 0; MovementY = 0; }
}
