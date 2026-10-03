using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

public sealed partial class LocalWorldSession
{
    internal Task<LocalSelectedQueryResult<PaidQuoteView?>> InspectSelectedOpenPaidQuoteAsync(
        LocalPlayerConnection player, LocalActorSelection selection, EntityId subject) =>
        InspectSelectedAsync(player, selection, subject, LocalSelectedQueryOperation.InspectOpenPaidQuote,
            WorldControlCapabilities.Trade, player.QuoteInspection,
            () => _authority.ReadSelectedOpenPaidQuote(subject, selection.ActorId, player.Delivery, selection.ControlWitness));

    internal Task<LocalSelectedQueryResult<MaterialProposalView?>> InspectSelectedOpenMaterialProposalAsync(
        LocalPlayerConnection player, LocalActorSelection selection, EntityId subject) =>
        InspectSelectedAsync(player, selection, subject, LocalSelectedQueryOperation.InspectOpenMaterialProposal,
            WorldControlCapabilities.Inventory, player.ProposalInspection,
            () => _authority.ReadSelectedOpenMaterialProposal(subject, selection.ActorId, player.Delivery, selection.ControlWitness));

    internal Task<LocalSelectedQueryResult<PaidQuoteView?>> InspectSelectedPaidQuoteHistoryAsync(
        LocalPlayerConnection player, LocalActorSelection selection, EntityId subject) =>
        InspectSelectedAsync(player, selection, subject, LocalSelectedQueryOperation.InspectPaidQuoteHistory,
            WorldControlCapabilities.PrivateHistory, player.QuoteInspection,
            () => _authority.ReadSelectedPaidQuoteHistory(subject, selection.ActorId, player.Delivery, selection.ControlWitness));

    internal Task<LocalSelectedQueryResult<MaterialProposalView?>> InspectSelectedMaterialProposalHistoryAsync(
        LocalPlayerConnection player, LocalActorSelection selection, EntityId subject) =>
        InspectSelectedAsync(player, selection, subject, LocalSelectedQueryOperation.InspectMaterialProposalHistory,
            WorldControlCapabilities.PrivateHistory, player.ProposalInspection,
            () => _authority.ReadSelectedMaterialProposalHistory(subject, selection.ActorId, player.Delivery, selection.ControlWitness));

    private Task<LocalSelectedQueryResult<T>> InspectSelectedAsync<T>(LocalPlayerConnection player,
        LocalActorSelection selection, EntityId subject, LocalSelectedQueryOperation operation,
        WorldControlCapabilities required, LocalInspectionSlot<T> slot, Func<T> read)
    {
        ArgumentNullException.ThrowIfNull(selection);
        lock (_inputs)
        {
            RequireSelectedQuery(player, selection, required, false);
            var key = new LocalInspectionKey(subject, selection, operation);
            // Both legacy and selected reads share this bounded slot. Changing selection never abandons its worker.
            return slot.Admit(key, () => ReadHistoryAsync(() =>
            {
                ValidateSelectedQueryAuthority(player, selection, required, false);
                T value = read();
                WorldSnapshot state = ValidateSelectedQueryAuthority(player, selection, required, false);
                return new LocalSelectedQueryResult<T>(state.WorldId, state.BranchId, selection, operation, subject, value);
            }));
        }
    }

    internal Task<LocalSelectedQueryResult<LocalTradeInquiryView>> SubmitSelectedTradeInquiryAsync(
        LocalPlayerConnection player, LocalActorSelection selection, LocalTradeInquiry inquiry)
    {
        ArgumentNullException.ThrowIfNull(inquiry);
        return SelectedInquiryAsync(player, selection, LocalSelectedQueryOperation.SubmitTradeInquiry, true,
            state =>
            {
                if (state.Coordination?.Markets is null)
                    throw new AuthorityDispatchException("This saved profile admits no local market inquiries.");
                return _trading.Admit(selection.ActorId, NewRequestId(), inquiry, state.AdvancingSteps, selection, player.Delivery);
            });
    }

    internal Task<LocalSelectedQueryResult<LocalTradeInquiryView?>> CancelSelectedTradeInquiryAsync(
        LocalPlayerConnection player, LocalActorSelection selection) =>
        SelectedInquiryAsync(player, selection, LocalSelectedQueryOperation.CancelTradeInquiry, true,
            _ => _trading.Cancel(selection.ActorId, selection));

    internal Task<LocalSelectedQueryResult<LocalTradeInquiryView?>> ReadSelectedTradeInquiryAsync(
        LocalPlayerConnection player, LocalActorSelection selection) =>
        SelectedInquiryAsync(player, selection, LocalSelectedQueryOperation.ReadTradeInquiry, false,
            _ => _trading.ReadInquiry(selection.ActorId, selection));

    private async Task<LocalSelectedQueryResult<T>> SelectedInquiryAsync<T>(LocalPlayerConnection player,
        LocalActorSelection selection, LocalSelectedQueryOperation operation, bool mutation, Func<WorldSnapshot, T> apply)
    {
        ArgumentNullException.ThrowIfNull(selection);
        lock (_inputs) { RequireSelectedQuery(player, selection, WorldControlCapabilities.Trade, mutation); }
        await _serialization.WaitAsync(_stop.Token).ConfigureAwait(false);
        try
        {
            WorldSnapshot state = ValidateSelectedQueryAuthority(player, selection, WorldControlCapabilities.Trade, mutation);
            T value = apply(state);
            state = ValidateSelectedQueryAuthority(player, selection, WorldControlCapabilities.Trade, mutation);
            return new(state.WorldId, state.BranchId, selection, operation, null, value);
        }
        finally { _serialization.Release(); }
    }

    // Input lock required. Reads are allowed while paused or awaiting restoration; mutations still require control.
    private void RequireSelectedQuery(LocalPlayerConnection player, LocalActorSelection selection,
        WorldControlCapabilities required, bool mutation)
    {
        if (_stopRequested || !IsCurrentPlayer(player))
            throw new AuthorityDispatchException("The query's player port is no longer available.");
        EnsurePlayerAvailable(player);
        RequireCurrentSelection(player, selection);
        if (selection.HomeActorId != player.ActorId
            || selection.ControlWitness is { } witness && (player.LiveControlWitness != witness
                || witness.GranteeActorId != player.ActorId || witness.OwnerActorId != selection.ActorId
                || (witness.Capabilities & required) != required)
            || selection.ControlWitness is null && selection.ActorId != player.ActorId)
            throw new AuthorityDispatchException("The actor selection does not authorize this query.");
        if (mutation) EnsureControlAvailable(player);
    }

    // Serialization required; read the durable root outside the input lock, then verify its exact current witness.
    private WorldSnapshot ValidateSelectedQueryAuthority(LocalPlayerConnection player, LocalActorSelection selection,
        WorldControlCapabilities required, bool mutation)
    {
        WorldSnapshot state = _authority.ReadStateForHost();
        lock (_inputs)
        {
            RequireSelectedQuery(player, selection, required, mutation);
            bool authorized = !mutation && selection.ControlWitness is null
                ? state.Inventory.CanControl(player.Delivery, selection.ActorId)
                : WorldActorCommandAuthority.IsCurrentWitness(state, selection.ActorId, player.Delivery,
                    selection.ControlWitness, required);
            if (!authorized)
                throw new AuthorityDispatchException("The query's control permission is no longer current.");
        }
        return state;
    }
}
