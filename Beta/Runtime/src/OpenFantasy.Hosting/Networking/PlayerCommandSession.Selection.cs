using System.Security.Cryptography;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking.Control;
using OpenFantasy.Hosting.Networking.Selection;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking;

public sealed partial class PlayerCommandSession
{
    private readonly object _selectionGate = new();
    private readonly EntityId _selectionStream = EntityId.FromBytes(RandomNumberGenerator.GetBytes(EntityId.ByteLength));
    private ulong _selectionRevision;
    private int _selectedMode;
    private int _administrationBusy;
    private int _selectionBusy;
    private Task _administration = Task.CompletedTask;
    private Task _selectionChange = Task.CompletedTask;
    private PlayerSelectedObservation? _selectedResumeBasis;
    private LocalSelectedPlayerObservation? _lastSelectedObservation;
    private readonly Dictionary<PlayerMessage, PlayerSelectedObservation> _pendingSelectedDeliveries = [];

    private bool RouteExtendedInput(PlayerCommandInput input, CancellationToken cancellationToken)
    {
        ulong request = input.Message.RequestId;
        if (input.Message.Kind == PlayerMessageKind.SelectionSubscribe)
        {
            if (_channel.ProfileVersion != WorldDelegatedControlAdmission.ProfileVersion
                || Volatile.Read(ref _observationRequested) != 0 || Interlocked.CompareExchange(ref _selectedMode, 1, 0) != 0)
                throw new FormatException("Selected observations require explicit initial subscription in their supported profile.");
            PublishSelectedObservation(request, true);
            Volatile.Write(ref _observationRequested, 1);
            return true;
        }
        if (input.Message.Kind == PlayerMessageKind.ControlRequest)
        {
            if (_channel.ProfileVersion is not (WorldControlPermissionAdmission.ProfileVersion or WorldDelegatedControlAdmission.ProfileVersion))
                Enqueue(Refusal(request, PlayerRequestRefusal.NotAdmitted));
            else if (Interlocked.CompareExchange(ref _administrationBusy, 1, 0) != 0)
                Enqueue(Refusal(request, PlayerRequestRefusal.Busy));
            else _administration = CompleteAdministrationAsync(request, input.Administration!, cancellationToken);
            return true;
        }
        if (input.Message.Kind != PlayerMessageKind.SelectionInput) return false;
        if (Volatile.Read(ref _selectedMode) == 0)
        {
            Enqueue(Refusal(request, PlayerRequestRefusal.NotAdmitted));
            return true;
        }
        PlayerSelectedInput selected = input.Selected!;
        switch (selected.Kind)
        {
            case PlayerSelectedInputKind.SelectActor:
                if (Interlocked.CompareExchange(ref _selectionBusy, 1, 0) != 0)
                    Enqueue(Refusal(request, PlayerRequestRefusal.Busy));
                else _selectionChange = CompleteSelectionAsync(request, selected, cancellationToken);
                break;
            case PlayerSelectedInputKind.Command:
                if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
                    Enqueue(Refusal(request, PlayerRequestRefusal.Busy));
                else _action = CompleteSelectedActionAsync(request, selected, cancellationToken);
                break;
            case PlayerSelectedInputKind.Query:
                if (Interlocked.CompareExchange(ref _queryBusy, 1, 0) != 0)
                    Enqueue(Refusal(request, PlayerRequestRefusal.Busy));
                else _query = CompleteSelectedQueryAsync(request, selected, cancellationToken);
                break;
            default:
                try
                {
                    ApplySelectedControl(selected);
                    Enqueue(new(PlayerMessageKind.Pong, request, []));
                }
                catch (AuthorityDispatchException) { Enqueue(Refusal(request, PlayerRequestRefusal.NotAdmitted)); }
                break;
        }
        return true;
    }

    private void ApplySelectedControl(PlayerSelectedInput input)
    {
        switch (input.Kind)
        {
            case PlayerSelectedInputKind.Pause:
                _channel.Player.PauseSelected(input.Selection);
                break;
            case PlayerSelectedInputKind.Movement:
                _channel.Player.SetSelectedMovement(input.Selection, input.DeltaX, input.DeltaY);
                break;
            case PlayerSelectedInputKind.Resume:
                lock (_selectionGate)
                {
                    if (_selectedResumeBasis is not { } basis || basis.StreamId != input.StreamId
                        || basis.Revision != input.Revision || basis.Observation.Selection != input.Selection)
                        throw new AuthorityDispatchException("Resume requires the delivered observation of this selection lifetime.");
                    _channel.Player.ResumeSelected(basis.Observation);
                }
                break;
            default: throw new FormatException("Unsupported selected control.");
        }
    }

    private bool PublishSelectedObservation(ulong requestId, bool force)
    {
        lock (_selectionGate)
        {
            LocalSelectedPlayerObservation observation;
            try { observation = _channel.Player.ReadSelectedObservation(); }
            catch (AuthorityDispatchException) when (requestId == 0) { return false; }
            if (!force && requestId == 0 && observation == _lastSelectedObservation) return false;
            var frame = new PlayerSelectedObservation(_selectionStream, checked(++_selectionRevision), observation)
                .WithConstruction().WithCarePermissionDescriptions();
            byte[] bytes = PlayerSelectedObservationCodec.Encode(frame);
            var message = new PlayerMessage(PlayerMessageKind.SelectionObservation, requestId, bytes);
            // The response queue bounds this table to its capacity plus the one in-flight send.
            _pendingSelectedDeliveries.Add(message, frame);
            try { Enqueue(message); }
            catch
            {
                _pendingSelectedDeliveries.Remove(message);
                throw;
            }
            _lastSelectedObservation = observation;
            return true;
        }
    }

    private void ConfirmSelectedDelivery(PlayerMessage message)
    {
        if (message.Kind != PlayerMessageKind.SelectionObservation) return;
        lock (_selectionGate)
        {
            if (!_pendingSelectedDeliveries.Remove(message, out var frame))
                throw new InvalidOperationException("A selected send lost its captured observation.");
            if (_selectedResumeBasis is { } basis && !NewResumeLifetime(basis.Observation, frame.Observation)) return;
            _selectedResumeBasis = frame;
            // A receiving client can run before SendAsync's continuation. It waits for this proof
            // before offering Resume, so that genuine input cannot race the server's installation.
            Enqueue(new(PlayerMessageKind.SelectionDelivered, 0,
                PlayerSelectionDeliveryCodec.Encode(frame.StreamId, frame.Revision)));
        }
    }

    internal static bool NewResumeLifetime(LocalSelectedPlayerObservation old, LocalSelectedPlayerObservation current) =>
        old.Selection != current.Selection || old.ControlStatus.PortId != current.ControlStatus.PortId
        || old.ControlStatus.Generation != current.ControlStatus.Generation
        || current.ControlStatus.State is PlayerControlState.Pending or PlayerControlState.Restored
            && current.ControlStatus.State != old.ControlStatus.State;

    private Task CompleteSelectionAsync(ulong request, PlayerSelectedInput input, CancellationToken stop) =>
        CompleteExtendedAsync(async () =>
        {
            await _channel.Player.SelectActorAsync(input.Selection, input.TargetActorId!.Value).ConfigureAwait(false);
            if (!stop.IsCancellationRequested) PublishSelectedObservation(request, true);
        }, request, () => Volatile.Write(ref _selectionBusy, 0), stop);

    private Task CompleteSelectedActionAsync(ulong request, PlayerSelectedInput input, CancellationToken stop) =>
        CompleteExtendedAsync(async () =>
        {
            WorldActionReceipt receipt = await _channel.Player.SubmitSelectedAction(input.Selection, input.Action!).ConfigureAwait(false);
            if (!stop.IsCancellationRequested) Enqueue(new(PlayerMessageKind.Receipt, request, WorldReceiptCodecV1.Encode(receipt)));
        }, request, () => Volatile.Write(ref _busy, 0), stop);

    private Task CompleteSelectedQueryAsync(ulong request, PlayerSelectedInput input, CancellationToken stop) =>
        CompleteExtendedAsync(async () =>
        {
            LocalPlayerConnection player = _channel.Player;
            PlayerSelectedQueryResponse result = input.Operation switch
            {
                LocalSelectedQueryOperation.SubmitTradeInquiry => PlayerSelectedQueryResponse.From(await player.SubmitSelectedTradeInquiryAsync(input.Selection, input.Inquiry!).ConfigureAwait(false)),
                LocalSelectedQueryOperation.CancelTradeInquiry => PlayerSelectedQueryResponse.From(await player.CancelSelectedTradeInquiryAsync(input.Selection).ConfigureAwait(false)),
                LocalSelectedQueryOperation.ReadTradeInquiry => PlayerSelectedQueryResponse.From(await player.ReadSelectedTradeInquiryAsync(input.Selection).ConfigureAwait(false)),
                LocalSelectedQueryOperation.InspectOpenPaidQuote => PlayerSelectedQueryResponse.From(await player.InspectSelectedOpenPaidQuoteAsync(input.Selection, input.SubjectId!.Value).ConfigureAwait(false)),
                LocalSelectedQueryOperation.InspectOpenMaterialProposal => PlayerSelectedQueryResponse.From(await player.InspectSelectedOpenMaterialProposalAsync(input.Selection, input.SubjectId!.Value).ConfigureAwait(false)),
                LocalSelectedQueryOperation.InspectPaidQuoteHistory => PlayerSelectedQueryResponse.From(await player.InspectSelectedPaidQuoteHistoryAsync(input.Selection, input.SubjectId!.Value).ConfigureAwait(false)),
                LocalSelectedQueryOperation.InspectMaterialProposalHistory => PlayerSelectedQueryResponse.From(await player.InspectSelectedMaterialProposalHistoryAsync(input.Selection, input.SubjectId!.Value).ConfigureAwait(false)),
                _ => throw new FormatException("Unknown selected query.")
            };
            if (!stop.IsCancellationRequested) Enqueue(new(PlayerMessageKind.SelectionQueryResult, request, PlayerSelectedQueryResponseCodec.Encode(result)));
        }, request, () => Volatile.Write(ref _queryBusy, 0), stop);

    private Task CompleteAdministrationAsync(ulong requestId, PlayerControlRequest request, CancellationToken stop) =>
        CompleteExtendedAsync(async () =>
        {
            LocalPlayerConnection player = _channel.Player;
            var scope = player.ReadView();
            var result = new PlayerControlResponse(scope.WorldId, scope.BranchId, player.ActorId, request.Kind);
            result = request.Kind switch
            {
                PlayerControlRequestKind.ReadPermissions => result with { Board = await player.ReadControlPermissionsAsync().ConfigureAwait(false) },
                PlayerControlRequestKind.SubmitPermission => result with { Receipt = await player.SubmitControlPermissionAsync(request.Command!).ConfigureAwait(false) },
                PlayerControlRequestKind.Acquire => result with { Witness = await player.AcquireControlAsync(request.Acquisition!).ConfigureAwait(false) },
                PlayerControlRequestKind.Release => await ReleaseAsync(),
                _ => throw new FormatException("Unknown control administration.")
            };
            if (!stop.IsCancellationRequested) Enqueue(new(PlayerMessageKind.ControlResult, requestId, PlayerControlResponseCodec.Encode(result)));
            async Task<PlayerControlResponse> ReleaseAsync()
            {
                await player.ReleaseControlAsync(request.Witness!).ConfigureAwait(false);
                return result;
            }
        }, requestId, () => Volatile.Write(ref _administrationBusy, 0), stop);

    private async Task CompleteExtendedAsync(Func<Task> operation, ulong request, Action release, CancellationToken stop)
    {
        try { await operation().ConfigureAwait(false); }
        catch (AuthorityDispatchException)
        {
            try { if (!stop.IsCancellationRequested) Enqueue(Refusal(request, PlayerRequestRefusal.NotAdmitted)); }
            catch (Exception error) { _requestFailure.TrySetResult(error); }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested || _channel.Player.ReadRunState() == LocalWorldRunState.Unavailable) { }
        catch (ObjectDisposedException) when (stop.IsCancellationRequested) { }
        catch (Exception error) { _requestFailure.TrySetResult(error); }
        finally { release(); }
    }
}
