using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Tests.Client.SelectedGifts;

internal sealed class SelectedGiftPort(LocalPlayerConnection player) : IPlayerSession, ISelectedPlayerSession
    {
        public bool SupportsControlPermissions => true;
        public bool SupportsActorSelection => true;
        public Task Completion => player.HostCompletion;
        public LocalWorldView ReadView() => player.ReadView();
        public LocalWorldRunState ReadRunState() => player.ReadRunState();
        public PlayerControlStatus ReadControlStatus() => player.ReadControlStatus();
        public void SetReady(bool ready) => throw new InvalidOperationException("Unbound readiness is forbidden by this fixture.");
        public void SetMovement(long dx, long dy) => throw new InvalidOperationException("Unbound movement is forbidden by this fixture.");
        public Task<WorldActionReceipt> SubmitAction(WorldActionCommand command) => throw new InvalidOperationException("Unbound action is forbidden by this fixture.");
        public Task<LocalTradeInquiryView> SubmitTradeInquiryAsync(LocalTradeInquiry inquiry) => throw new InvalidOperationException();
        public Task<LocalTradeInquiryView?> CancelTradeInquiryAsync() => throw new InvalidOperationException();
        public LocalTradeInquiryView? ReadTradeInquiry() => player.ReadTradeInquiry();
        public Task<PaidQuoteView?> InspectPaidQuoteAsync(EntityId id) => player.InspectPaidQuoteAsync(id);
        public Task<MaterialProposalView?> InspectMaterialProposalAsync(EntityId id) => player.InspectMaterialProposalAsync(id);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public LocalSelectedPlayerObservation ReadSelectedObservation() => player.ReadSelectedObservation();
        public Task<LocalControlPermissionBoard> ReadControlPermissionsAsync() => player.ReadControlPermissionsAsync();
        public Task<WorldActionReceipt> SubmitControlPermissionAsync(WorldActionCommand command) => player.SubmitControlPermissionAsync(command);
        public Task<WorldControlWitness> AcquireControlAsync(LocalControlAcquisition observed) => player.AcquireControlAsync(observed);
        public Task ReleaseControlAsync(WorldControlWitness observed) => player.ReleaseControlAsync(observed);
        public Task<LocalSelectedPlayerObservation> SelectActorAsync(LocalActorSelection observed, EntityId actorId) => player.SelectActorAsync(observed, actorId);
        public void ResumeSelected(LocalSelectedPlayerObservation observed) => player.ResumeSelected(observed);
        public void PauseSelected(LocalActorSelection observed) => player.PauseSelected(observed);
        public void SetSelectedMovement(LocalActorSelection observed, long dx, long dy) => player.SetSelectedMovement(observed, dx, dy);
        public Task<WorldActionReceipt> SubmitSelectedAction(LocalActorSelection observed, WorldActionCommand command) => player.SubmitSelectedAction(observed, command);
        public Task<LocalSelectedQueryResult<LocalTradeInquiryView>> SubmitSelectedTradeInquiryAsync(LocalActorSelection observed, LocalTradeInquiry inquiry) => player.SubmitSelectedTradeInquiryAsync(observed, inquiry);
        public Task<LocalSelectedQueryResult<LocalTradeInquiryView?>> CancelSelectedTradeInquiryAsync(LocalActorSelection observed) => player.CancelSelectedTradeInquiryAsync(observed);
        public Task<LocalSelectedQueryResult<LocalTradeInquiryView?>> ReadSelectedTradeInquiryAsync(LocalActorSelection observed) => player.ReadSelectedTradeInquiryAsync(observed);
        public Task<LocalSelectedQueryResult<PaidQuoteView?>> InspectSelectedOpenPaidQuoteAsync(LocalActorSelection observed, EntityId id) => player.InspectSelectedOpenPaidQuoteAsync(observed, id);
        public Task<LocalSelectedQueryResult<MaterialProposalView?>> InspectSelectedOpenMaterialProposalAsync(LocalActorSelection observed, EntityId id) => player.InspectSelectedOpenMaterialProposalAsync(observed, id);
        public Task<LocalSelectedQueryResult<PaidQuoteView?>> InspectSelectedPaidQuoteHistoryAsync(LocalActorSelection observed, EntityId id) => player.InspectSelectedPaidQuoteHistoryAsync(observed, id);
        public Task<LocalSelectedQueryResult<MaterialProposalView?>> InspectSelectedMaterialProposalHistoryAsync(LocalActorSelection observed, EntityId id) => player.InspectSelectedMaterialProposalHistoryAsync(observed, id);
    }
