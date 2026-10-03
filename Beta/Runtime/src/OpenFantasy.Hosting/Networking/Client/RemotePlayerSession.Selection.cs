using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking.Control;
using OpenFantasy.Hosting.Networking.Selection;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking.Client;

public sealed partial class RemotePlayerSession
{
    public bool SupportsControlPermissions => _client.SupportsControlPermissions;
    public bool SupportsActorSelection => _client.SupportsActorSelection;
    public LocalSelectedPlayerObservation ReadSelectedObservation() => _client.ReadSelectedObservation();
    public async Task<LocalControlPermissionBoard> ReadControlPermissionsAsync() =>
        (await _client.AdministerControlAsync(new(PlayerControlRequestKind.ReadPermissions)).ConfigureAwait(false)).Board!;
    public async Task<WorldActionReceipt> SubmitControlPermissionAsync(WorldActionCommand command) =>
        (await _client.AdministerControlAsync(new(PlayerControlRequestKind.SubmitPermission, Command: command)).ConfigureAwait(false)).Receipt!;
    public async Task<WorldControlWitness> AcquireControlAsync(LocalControlAcquisition observed) =>
        (await _client.AdministerControlAsync(new(PlayerControlRequestKind.Acquire, Acquisition: observed)).ConfigureAwait(false)).Witness!;
    public async Task ReleaseControlAsync(WorldControlWitness observed) =>
        _ = await _client.AdministerControlAsync(new(PlayerControlRequestKind.Release, Witness: observed)).ConfigureAwait(false);
    public Task<LocalSelectedPlayerObservation> SelectActorAsync(LocalActorSelection observed, EntityId actorId) =>
        _client.SelectActorAsync(observed, actorId);
    public void ResumeSelected(LocalSelectedPlayerObservation observed) => SendControl(() => _client.ResumeSelected(observed));
    public void PauseSelected(LocalActorSelection observed) => SendControl(() => _client.PauseSelected(observed));
    public void SetSelectedMovement(LocalActorSelection observed, long dx, long dy) =>
        SendControl(() => _client.SetSelectedMovement(observed, dx, dy));
    public Task<WorldActionReceipt> SubmitSelectedAction(LocalActorSelection observed, WorldActionCommand command) =>
        _client.SubmitSelectedAction(observed, command);

    public Task<LocalSelectedQueryResult<LocalTradeInquiryView>> SubmitSelectedTradeInquiryAsync(
        LocalActorSelection observed, LocalTradeInquiry inquiry) =>
        SelectedForeground(PlayerSelectedInput.Query(observed, LocalSelectedQueryOperation.SubmitTradeInquiry, inquiry), value => value.Inquiry!);
    public Task<LocalSelectedQueryResult<LocalTradeInquiryView?>> CancelSelectedTradeInquiryAsync(LocalActorSelection observed) =>
        SelectedForeground(PlayerSelectedInput.Query(observed, LocalSelectedQueryOperation.CancelTradeInquiry), value => value.Inquiry);
    public Task<LocalSelectedQueryResult<LocalTradeInquiryView?>> ReadSelectedTradeInquiryAsync(LocalActorSelection observed) =>
        SelectedForeground(PlayerSelectedInput.Query(observed, LocalSelectedQueryOperation.ReadTradeInquiry), value => value.Inquiry);
    public Task<LocalSelectedQueryResult<PaidQuoteView?>> InspectSelectedOpenPaidQuoteAsync(LocalActorSelection observed, EntityId subject) =>
        SelectedForeground(PlayerSelectedInput.Query(observed, LocalSelectedQueryOperation.InspectOpenPaidQuote, subjectId: subject), value => value.PaidQuote);
    public Task<LocalSelectedQueryResult<MaterialProposalView?>> InspectSelectedOpenMaterialProposalAsync(LocalActorSelection observed, EntityId subject) =>
        SelectedForeground(PlayerSelectedInput.Query(observed, LocalSelectedQueryOperation.InspectOpenMaterialProposal, subjectId: subject), value => value.MaterialProposal);
    public Task<LocalSelectedQueryResult<PaidQuoteView?>> InspectSelectedPaidQuoteHistoryAsync(LocalActorSelection observed, EntityId subject) =>
        SelectedForeground(PlayerSelectedInput.Query(observed, LocalSelectedQueryOperation.InspectPaidQuoteHistory, subjectId: subject), value => value.PaidQuote);
    public Task<LocalSelectedQueryResult<MaterialProposalView?>> InspectSelectedMaterialProposalHistoryAsync(LocalActorSelection observed, EntityId subject) =>
        SelectedForeground(PlayerSelectedInput.Query(observed, LocalSelectedQueryOperation.InspectMaterialProposalHistory, subjectId: subject), value => value.MaterialProposal);

    public bool SupportsPrivateHistoryDirectory => SupportsActorSelection;
    public LocalSelectedPlayerObservation ReadPrivateHistoryObservation() => ReadSelectedObservation();
    public Task<LocalSelectedQueryResult<LocalPrivateHistoryPage>> ReadSelectedPrivateHistoryAsync(
        LocalActorSelection observed, LocalPrivateHistoryPageRequest request) =>
        SelectedForeground(PlayerSelectedInput.ReadPrivateHistory(observed, request), value => value.Directory!);

    private Task<LocalSelectedQueryResult<T>> SelectedForeground<T>(PlayerSelectedInput input, Func<PlayerSelectedQueryResponse, T> project)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            if (_foreground is { IsCompleted: false })
                return Task.FromException<LocalSelectedQueryResult<T>>(new PlayerRequestRefusedException(PlayerRequestRefusal.Busy));
            var task = ExecuteSelectedForegroundAsync(input, project);
            _foreground = task;
            return task;
        }
    }

    private async Task<LocalSelectedQueryResult<T>> ExecuteSelectedForegroundAsync<T>(PlayerSelectedInput input,
        Func<PlayerSelectedQueryResponse, T> project)
    {
        await _querySlot.WaitAsync(_stop.Token).ConfigureAwait(false);
        try
        {
            PlayerSelectedQueryResponse result = await _client.QuerySelectedAsync(input).ConfigureAwait(false);
            return new(result.WorldId, result.BranchId, result.Selection, result.Operation, result.SubjectId, project(result));
        }
        finally { _querySlot.Release(); }
    }
}
