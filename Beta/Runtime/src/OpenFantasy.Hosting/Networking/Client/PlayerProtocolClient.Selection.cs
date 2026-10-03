using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking.Control;
using OpenFantasy.Hosting.Networking.Selection;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking.Client;

public sealed partial class PlayerProtocolClient
{
    private PlayerSelectedObservation? _selectedFrame;
    private PlayerSelectedObservation? _selectedResumeBasis;
    private bool _selectedBasisConfirmed;
    private bool _selectionReplyReceived;
    private readonly HashSet<ulong> _pendingSelectedProofs = [];
    private ulong _selectionSubscribeId;
    private Pending<LocalSelectedPlayerObservation>? _selectionChange;
    private SelectedQueryPending? _selectedQuery;
    private AdministrationPending? _administration;
    private readonly Dictionary<ulong, LocalActorSelection> _selectedControls = [];

    public bool SupportsControlPermissions => Welcome.Compatibility.ProfileVersion is
        WorldControlPermissionAdmission.ProfileVersion or WorldDelegatedControlAdmission.ProfileVersion;
    public bool SupportsActorSelection => Welcome.Compatibility.ProfileVersion == WorldDelegatedControlAdmission.ProfileVersion;

    public LocalSelectedPlayerObservation ReadSelectedObservation()
    {
        lock (_gate)
        {
            ThrowIfClosedLocked();
            return _selectedFrame?.Observation ?? throw new InvalidOperationException("A selected observation has not been delivered.");
        }
    }

    public Task<LocalSelectedPlayerObservation> SelectActorAsync(LocalActorSelection observed, EntityId actorId)
    {
        var input = PlayerSelectedInput.SelectActor(observed, actorId);
        byte[] bytes = PlayerSelectedInputCodec.Encode(input);
        lock (_gate)
        {
            RequireSelectedLocked(observed);
            if (_selectionChange is not null) return Task.FromException<LocalSelectedPlayerObservation>(Busy());
            ulong id = EnqueueLocked(request => new(PlayerMessageKind.SelectionInput, request, bytes));
            _selectionChange = new(id);
            return _selectionChange.Source.Task;
        }
    }

    public void ResumeSelected(LocalSelectedPlayerObservation observed)
    {
        ArgumentNullException.ThrowIfNull(observed);
        lock (_gate)
        {
            RequireSelectedLocked(observed.Selection);
            if (!_selectedBasisConfirmed || _selectedResumeBasis is not { } basis || _selectedFrame is not { } current
                || basis.Observation.Selection != observed.Selection
                || observed.HomeView.WorldId != Welcome.Compatibility.WorldId || observed.HomeView.BranchId != Welcome.Compatibility.BranchId
                || observed.HomeView.PlayerActorId != Welcome.ActorId || observed.ControlStatus != current.Observation.ControlStatus
                || observed.ControlStatus.State == PlayerControlState.Pending)
                throw new SelectedInputUnavailableException("Read this selection's delivered, available observation before explicitly choosing Resume.");
            SendSelectedControlLocked(PlayerSelectedInput.Resume(observed.Selection, basis.StreamId, basis.Revision));
            _ready = true;
        }
    }

    public void PauseSelected(LocalActorSelection observed)
    {
        lock (_gate)
        {
            RequireSelectedLocked(observed);
            SendSelectedControlLocked(PlayerSelectedInput.Pause(observed));
            _ready = false;
            _movementX = 0; _movementY = 0;
        }
    }

    public void SetSelectedMovement(LocalActorSelection observed, long dx, long dy)
    {
        PlayerSelectedInput input = PlayerSelectedInput.Movement(observed, checked((int)dx), checked((int)dy));
        // Validate even when rate coalescing subsequently avoids a redundant transmission.
        _ = PlayerSelectedInputCodec.Encode(input);
        lock (_gate)
        {
            RequireSelectedLocked(observed);
            long now = _time.GetTimestamp();
            if (dx == _movementX && dy == _movementY
                && (dx == 0 && dy == 0 || _time.GetElapsedTime(_lastMovement, now) < MovementRenewalInterval)) return;
            SendSelectedControlLocked(input);
            _movementX = checked((int)dx); _movementY = checked((int)dy); _lastMovement = now;
        }
    }

    private void SendSelectedControlLocked(PlayerSelectedInput input)
    {
        if (_selectedControls.Count >= OutputCapacity) throw Busy();
        byte[] bytes = PlayerSelectedInputCodec.Encode(input);
        ulong id = EnqueueLocked(request => new(PlayerMessageKind.SelectionInput, request, bytes));
        _selectedControls.Add(id, input.Selection);
    }

    public Task<WorldActionReceipt> SubmitSelectedAction(LocalActorSelection observed, WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateOwnDelivery(command);
        byte[] bytes = WorldActionCodec.Encode(command);
        byte[] input = PlayerSelectedInputCodec.Encode(PlayerSelectedInput.Command(observed, command));
        lock (_gate)
        {
            ThrowIfClosedLocked();
            // An already submitted exact action keeps its original waiter across selection changes.
            if (_action is { } existing)
                return bytes.AsSpan().SequenceEqual(existing.Bytes) ? existing.Source.Task
                    : Task.FromException<WorldActionReceipt>(Busy());
            // A past port/generation may retrieve this authenticated submitter's exact receipt.
            // The host checks durable receipt identity first and requires CURRENT selection for every fresh command.
            if (!SupportsActorSelection || observed.HomeActorId != Welcome.ActorId)
                throw new InvalidOperationException("The captured action must belong to this authenticated home participant.");
            ulong id = EnqueueLocked(request => new(PlayerMessageKind.SelectionInput, request, input));
            _action = new(id, command, bytes);
            return _action.Source.Task;
        }
    }

    public Task<PlayerSelectedQueryResponse> QuerySelectedAsync(PlayerSelectedInput input)
    {
        if (input.Kind != PlayerSelectedInputKind.Query) throw new ArgumentException("A selected query is required.", nameof(input));
        byte[] bytes = PlayerSelectedInputCodec.Encode(input);
        lock (_gate)
        {
            RequireSelectedLocked(input.Selection);
            if (_query is not null || _selectedQuery is not null) return Task.FromException<PlayerSelectedQueryResponse>(Busy());
            ulong id = EnqueueLocked(request => new(PlayerMessageKind.SelectionInput, request, bytes));
            _selectedQuery = new(id, input);
            return _selectedQuery.Source.Task;
        }
    }

    public Task<PlayerControlResponse> AdministerControlAsync(PlayerControlRequest request)
    {
        if (!SupportsControlPermissions) throw new InvalidOperationException("This saved profile does not support participant consent.");
        if (request.Command is { } command) ValidateOwnDelivery(command);
        byte[] bytes = PlayerControlRequestCodec.Encode(request);
        lock (_gate)
        {
            ThrowIfClosedLocked();
            if (_administration is { } existing)
                return bytes.AsSpan().SequenceEqual(existing.Bytes) ? existing.Source.Task
                    : Task.FromException<PlayerControlResponse>(Busy());
            ulong id = EnqueueLocked(value => new(PlayerMessageKind.ControlRequest, value, bytes));
            _administration = new(id, request, bytes);
            return _administration.Source.Task;
        }
    }

    private void ValidateOwnDelivery(WorldActionCommand command)
    {
        if (command.PrincipalId != Welcome.Delivery.PrincipalId || command.OriginSessionId != Welcome.Delivery.SessionId
            || command.WorldId != Welcome.Compatibility.WorldId || command.BranchId != Welcome.Compatibility.BranchId)
            throw new ArgumentException("The command must retain this connection's original delivery and world.", nameof(command));
    }

    private void RequireSelectedLocked(LocalActorSelection observed)
    {
        ThrowIfClosedLocked();
        if (!SupportsActorSelection) throw new InvalidOperationException("This connection does not support actor selection.");
        if (_selectedFrame?.Observation.Selection != observed)
            throw new SelectedInputUnavailableException("Read the current actor selection before choosing input.");
    }

    private bool ProcessSelectedMessage(PlayerMessage message)
    {
        if (message.Kind == PlayerMessageKind.SelectionObservation)
        {
            if (!SupportsActorSelection) throw new FormatException("Selected observations were not negotiated.");
            PlayerSelectedObservation frame = PlayerSelectedObservationCodec.Decode(message.Payload);
            lock (_gate)
            {
                if (_closed) return true;
                bool subscribed = message.RequestId != 0 && message.RequestId == _selectionSubscribeId;
                bool selected = message.RequestId != 0 && message.RequestId == _selectionChange?.RequestId;
                if (message.RequestId != 0 && !subscribed && !selected) throw UnexpectedReply();
                if (frame.Observation.HomeView.WorldId != Welcome.Compatibility.WorldId
                    || frame.Observation.HomeView.BranchId != Welcome.Compatibility.BranchId
                    || frame.Observation.Selection.HomeActorId != Welcome.ActorId
                    || _selectedFrame is { } old && (frame.StreamId != old.StreamId || frame.Revision <= old.Revision
                        || frame.Observation.Selection.PortId != old.Observation.Selection.PortId
                        || frame.Observation.Selection.Generation < old.Observation.Selection.Generation
                        || frame.Observation.Selection.Generation == old.Observation.Selection.Generation
                            && frame.Observation.Selection != old.Observation.Selection
                        || frame.Observation.ControlStatus.PortId != old.Observation.ControlStatus.PortId
                        || frame.Observation.ControlStatus.Generation < old.Observation.ControlStatus.Generation))
                    throw new FormatException("Selected observation violates its connection, stream or generation binding.");
                if (_selectedResumeBasis is not { } basis || PlayerCommandSession.NewResumeLifetime(basis.Observation, frame.Observation))
                {
                    _selectedResumeBasis = frame;
                    _selectedBasisConfirmed = false;
                    if (_pendingSelectedProofs.Count >= OutputCapacity)
                        throw new FormatException("Too many unconfirmed selection lifetimes.");
                    _pendingSelectedProofs.Add(frame.Revision);
                    _ready = false;
                    _movementX = 0; _movementY = 0;
                }
                _selectedFrame = frame;
                _controlStatus = frame.Observation.ControlStatus;
                if (subscribed) _selectionSubscribeId = 0;
                if (selected) _selectionReplyReceived = true;
                CompleteDeliveredSelectionLocked();
            }
            return true;
        }
        lock (_gate)
        {
            if (_closed) return true;
            if (message.Kind == PlayerMessageKind.SelectionDelivered)
            {
                var proof = PlayerSelectionDeliveryCodec.Decode(message.Payload);
                if (!SupportsActorSelection || message.RequestId != 0 || _selectedFrame?.StreamId != proof.StreamId
                    || !_pendingSelectedProofs.Remove(proof.Revision))
                    throw new FormatException("Selection delivery proof does not match a pending observed lifetime.");
                if (_selectedResumeBasis?.Revision == proof.Revision)
                {
                    _selectedBasisConfirmed = true;
                    CompleteDeliveredSelectionLocked();
                }
                return true;
            }
            if (message.Kind == PlayerMessageKind.SelectionQueryResult)
            {
                var pending = _selectedQuery is { } query && query.RequestId == message.RequestId ? query : throw UnexpectedReply();
                PlayerSelectedQueryResponse response = PlayerSelectedQueryResponseCodec.Decode(message.Payload);
                // First authenticate correlation to the original request; a stale but genuine result is a caller refusal.
                response.ValidateFor(pending.Input, Welcome.Compatibility.WorldId, Welcome.Compatibility.BranchId, pending.Input.Selection);
                _selectedQuery = null;
                if (_selectedFrame?.Observation.Selection != pending.Input.Selection)
                    pending.Source.TrySetException(new InvalidOperationException("The selected query completed after its actor selection changed."));
                else pending.Source.TrySetResult(response);
                return true;
            }
            if (message.Kind == PlayerMessageKind.ControlResult)
            {
                var pending = _administration is { } admin && admin.RequestId == message.RequestId ? admin : throw UnexpectedReply();
                PlayerControlResponse response = PlayerControlResponseCodec.Decode(message.Payload);
                ValidateAdministrationResponse(pending.Request, response);
                _administration = null;
                pending.Source.TrySetResult(response);
                return true;
            }
            if (message.Kind == PlayerMessageKind.Pong && _selectedControls.Remove(message.RequestId))
            {
                if (!message.Payload.IsEmpty) throw new FormatException("Selected input acknowledgment must be empty.");
                return true;
            }
            if (message.Kind == PlayerMessageKind.Refusal)
            {
                var refusal = new PlayerRequestRefusedException(PlayerGameplayCodec.ReadRefusal(message));
                if (_selectedQuery?.RequestId == message.RequestId) { _selectedQuery.Source.TrySetException(refusal); _selectedQuery = null; }
                else if (_selectionChange?.RequestId == message.RequestId) { _selectionChange.Source.TrySetException(refusal); _selectionChange = null; _selectionReplyReceived = false; }
                else if (_administration?.RequestId == message.RequestId) { _administration.Source.TrySetException(refusal); _administration = null; }
                else if (_selectedControls.Remove(message.RequestId, out var owner))
                {
                    if (_selectedFrame?.Observation.Selection == owner) { _ready = false; _movementX = 0; _movementY = 0; }
                }
                else return false;
                return true;
            }
            if (SupportsActorSelection && message.Kind is PlayerMessageKind.Observation or PlayerMessageKind.ControlStatus)
                throw new FormatException("Legacy observations cannot replace a negotiated selected stream.");
        }
        return false;
    }

    private void CompleteDeliveredSelectionLocked()
    {
        if (!_selectedBasisConfirmed || _selectedFrame is not { } frame) return;
        if (_selectionSubscribeId == 0)
        {
            _firstFull.TrySetResult(true);
            _firstControl.TrySetResult(true);
        }
        if (_selectionReplyReceived && _selectionChange is { } pending)
        {
            _selectionChange = null;
            _selectionReplyReceived = false;
            pending.Source.TrySetResult(frame.Observation);
        }
    }

    private void ValidateAdministrationResponse(PlayerControlRequest request, PlayerControlResponse response)
    {
        if (response.WorldId != Welcome.Compatibility.WorldId || response.BranchId != Welcome.Compatibility.BranchId
            || response.HomeActorId != Welcome.ActorId || response.Kind != request.Kind)
            throw new FormatException("Control response does not match its authenticated home request.");
        if (request.Command is { } command && (response.Receipt is not { } receipt
            || receipt.CommandId != command.CommandId || receipt.PrincipalId != command.PrincipalId
            || receipt.CommandHash != command.ContentHash))
            throw new FormatException("Permission receipt does not match the original command.");
        if (request.Acquisition is { } acquired && (response.Witness is not { } witness
            || witness.OwnerActorId != acquired.OwnerActorId || witness.GranteeActorId != Welcome.ActorId
            || witness.GrantCommandId != acquired.GrantCommandId || witness.AcceptanceCommandId != acquired.AcceptanceCommandId
            || witness.GrantRevision != acquired.GrantRevision || witness.Capabilities != acquired.Capabilities
            || witness.TermsHash != acquired.TermsHash || witness.AssignmentRevision != checked(acquired.ExpectedAssignmentRevision + 1)
            || witness.DisconnectId != acquired.DisconnectId || witness.OwnerControlRevision != acquired.OwnerControlRevision))
            throw new FormatException("Acquisition response does not match the observed permission and episode.");
    }

    private void CloseSelectedLocked(Exception ended)
    {
        _selectionChange?.Source.TrySetException(ended);
        _selectedQuery?.Source.TrySetException(ended);
        _administration?.Source.TrySetException(ended);
        _selectionChange = null; _selectedQuery = null; _administration = null;
        _selectedControls.Clear();
        _pendingSelectedProofs.Clear();
        _selectionReplyReceived = false;
        _selectedBasisConfirmed = false;
    }

    private sealed class SelectedQueryPending(ulong id, PlayerSelectedInput input) : Pending<PlayerSelectedQueryResponse>(id)
    {
        internal PlayerSelectedInput Input { get; } = input;
    }
    private sealed class AdministrationPending(ulong id, PlayerControlRequest request, byte[] bytes) : Pending<PlayerControlResponse>(id)
    {
        internal PlayerControlRequest Request { get; } = request;
        internal byte[] Bytes { get; } = bytes;
    }
}
