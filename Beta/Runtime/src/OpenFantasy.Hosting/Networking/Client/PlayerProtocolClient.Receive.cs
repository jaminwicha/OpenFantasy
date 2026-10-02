using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Diagnostics;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking.Client;

public sealed partial class PlayerProtocolClient
{
    private async Task ReceiveLoopAsync()
    {
        while (true)
        {
            byte[]? bytes = await _transport.ReceiveAsync(_lifetime.Token).ConfigureAwait(false);
            _lifetime.Token.ThrowIfCancellationRequested();
            if (bytes is null) throw new EndOfStreamException("The player connection closed.");
            ProcessMessage(PlayerMessageCodec.Decode(bytes));
        }
    }

    private void ProcessMessage(PlayerMessage message)
    {
        if (message.Kind == PlayerMessageKind.ControlStatus)
        {
            if (message.RequestId != 0) throw new FormatException("Control status requires request identity zero.");
            PlayerControlStatus status = PlayerControlCodec.Decode(message.Payload);
            lock (_gate)
            {
                if (_closed) return;
                if (status.ActorId != Welcome.ActorId || !HasAppliedControlObservation(status)
                    || _controlStatus != PlayerControlStatus.Available &&
                        (status.PortId != _controlStatus.PortId || status.Generation < _controlStatus.Generation))
                    throw new FormatException("Control status does not match the applied player observation and port.");
                if (status.State == PlayerControlState.Restored)
                {
                    _resumeBinding = status;
                    _ready = false;
                    _movementX = 0; _movementY = 0;
                }
                else if (status.State == PlayerControlState.Pending)
                {
                    _ready = false;
                    _movementX = 0; _movementY = 0;
                }
                _controlStatus = status;
                _firstControl.TrySetResult(true);
            }
            return;
        }
        if (message.Kind == PlayerMessageKind.Observation)
        {
            HostTimingEvents.Log.Stage(_timingOwner, "replica.begin", value: message.Payload.Length);
            if (message.RequestId != 0) throw new FormatException("Observations require request identity zero.");
            PlayerObservationFrame frame = PlayerObservationFrameCodec.Decode(message.Payload);
            PlayerObservationApplyResult result = _replica.Apply(frame);
            HostTimingEvents.Log.Stage(_timingOwner, "replica.end", value: frame.IsFull ? 1 : 0);
            lock (_gate)
            {
                if (_closed) return;
                if (result == PlayerObservationApplyResult.RequiresFull) RequestFullLocked();
                else if (frame.IsFull)
                {
                    _fullRequested = false;
                    _firstFull.TrySetResult(true);
                }
            }
            return;
        }

        lock (_gate)
        {
            if (_closed) return;
            if (message.RequestId == 0) throw new FormatException("A player reply requires its request identity.");
            switch (message.Kind)
            {
                case PlayerMessageKind.Receipt:
                {
                    ActionPending pending = _action is { } action && action.RequestId == message.RequestId
                        ? action : throw UnexpectedReply();
                    WorldActionReceipt receipt = WorldReceiptCodecV1.Decode(message.Payload);
                    if (receipt.CommandId != pending.Command.CommandId
                        || receipt.PrincipalId != pending.Command.PrincipalId
                        || receipt.WorldId != Welcome.Compatibility.WorldId
                        || receipt.BranchId != Welcome.Compatibility.BranchId
                        || receipt.CommandHash != pending.Hash)
                        throw new FormatException("The receipt does not match its exact command and authenticated scope.");
                    _action = null;
                    pending.Source.TrySetResult(receipt);
                    break;
                }
                case PlayerMessageKind.QueryResult:
                {
                    QueryPending pending = _query is { } query && query.RequestId == message.RequestId
                        ? query : throw UnexpectedReply();
                    PlayerQueryResult result = PlayerQueryResultCodec.Decode(message.Payload);
                    result.ValidateFor(pending.Query, Welcome.Compatibility.WorldId, Welcome.Compatibility.BranchId, Welcome.ActorId);
                    _query = null;
                    pending.Source.TrySetResult(result);
                    break;
                }
                case PlayerMessageKind.Pong:
                {
                    Pending<bool> pending = _ping is { } ping && ping.RequestId == message.RequestId
                        ? ping : throw UnexpectedReply();
                    if (message.Payload.Length != 0) throw new FormatException("A pong must have no payload.");
                    _ping = null;
                    pending.Source.TrySetResult(true);
                    break;
                }
                case PlayerMessageKind.Refusal:
                {
                    var refusal = new PlayerRequestRefusedException(PlayerGameplayCodec.ReadRefusal(message));
                    if (_action is { } action && action.RequestId == message.RequestId)
                    {
                        _action = null;
                        action.Source.TrySetException(refusal);
                    }
                    else if (_query is { } query && query.RequestId == message.RequestId)
                    {
                        _query = null;
                        query.Source.TrySetException(refusal);
                    }
                    else if (_ping is { } ping && ping.RequestId == message.RequestId)
                    {
                        _ping = null;
                        ping.Source.TrySetException(refusal);
                    }
                    else throw UnexpectedReply();
                    break;
                }
                default:
                    throw UnexpectedReply();
            }
        }
    }

    private static FormatException UnexpectedReply() =>
        new("The player reply is unknown, duplicated, or does not match an outstanding request.");
}
