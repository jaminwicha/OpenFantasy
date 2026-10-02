using System.Security.Cryptography;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Simulation;

public sealed partial class LocalWorldSession
{
    /// <summary>Advancing steps a trade inquiry may wait for a merchant's answer unless the trading configuration names another bound.</summary>
    public const ulong TradeInquiryLifetimeSteps = LocalTradingConfiguration.DefaultInquiryLifetimeSteps;

    /// <summary>
    /// Admits the player's nonbinding trade inquiry. Nothing about the named market is checked or revealed here:
    /// at the next ready, unheld advancing cutoff the merchant answers only if its market is available to the
    /// player in person. One inquiry may wait; identical terms share it and different terms are refused.
    /// The inquiry is session memory only: it promises nothing, survives no restart and expires in fixed-world time.
    /// </summary>
    public Task<LocalTradeInquiryView> SubmitTradeInquiryAsync(LocalTradeInquiry inquiry) => SubmitTradeInquiryAsync(_localPlayer, inquiry);

    internal async Task<LocalTradeInquiryView> SubmitTradeInquiryAsync(LocalPlayerConnection player, LocalTradeInquiry inquiry)
    {
        ArgumentNullException.ThrowIfNull(inquiry);
        await _serialization.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_inputs) { EnsurePlayerAvailable(player); EnsureControlAvailable(player); }
            WorldSnapshot state = _authority.ReadStateForHost();
            if (state.Coordination?.Markets is null)
            {
                throw new AuthorityDispatchException("This saved profile admits no local market inquiries.");
            }
            return _trading.Admit(player.ActorId, NewRequestId(), inquiry, state.AdvancingSteps);
        }
        finally { _serialization.Release(); }
    }

    /// <summary>
    /// Cancels the player's waiting inquiry. Serialized with cutoffs, so a cancelled inquiry is never answered
    /// afterwards; an offer already issued keeps its world lifecycle and the player may still ignore it.
    /// </summary>
    public Task<LocalTradeInquiryView?> CancelTradeInquiryAsync() => CancelTradeInquiryAsync(_localPlayer);

    internal async Task<LocalTradeInquiryView?> CancelTradeInquiryAsync(LocalPlayerConnection player)
    {
        await _serialization.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_inputs) { EnsurePlayerAvailable(player); }
            return _trading.Cancel(player.ActorId);
        }
        finally { _serialization.Release(); }
    }

    /// <summary>The player's current or last inquiry; a pure in-memory read safe for every frame.</summary>
    public LocalTradeInquiryView? ReadTradeInquiry() => ReadTradeInquiry(_localPlayer);

    internal LocalTradeInquiryView? ReadTradeInquiry(LocalPlayerConnection player)
    {
        lock (_inputs) { EnsurePlayerAvailable(player); return _trading.ReadInquiry(player.ActorId); }
    }

    /// <summary>
    /// Explicit participant inspection of one paid quote, including an archived terminal outcome. Unknown and
    /// foreign quotes both give null. Runs in the background history slot, never on the caller's thread or a
    /// tick, starts no step and caches nothing; one lookup may be outstanding at a time.
    /// </summary>
    public Task<PaidQuoteView?> InspectPaidQuoteAsync(EntityId quoteId) => InspectPaidQuoteAsync(_localPlayer, quoteId);

    internal Task<PaidQuoteView?> InspectPaidQuoteAsync(LocalPlayerConnection player, EntityId quoteId)
    {
        lock (_inputs)
        {
            EnsurePlayerAvailable(player);
            return player.QuoteInspection.Admit(quoteId, subject => ReadHistoryAsync(() =>
                _authority.ReadRecordedPaidQuoteForController(subject, player.ActorId, player.Delivery,
                    (state, archived) => PaidQuoteProjection.ProjectRecorded(state, archived, subject, player.ActorId))));
        }
    }

    /// <summary>
    /// Paid and market commands exist only in their own world interpretation and only at an advancing, unheld
    /// exploration cutoff. Anything else is refused here, before it is queued, so no held or other boundary batch
    /// can ever be asked to carry one.
    /// </summary>
    private static void ValidateTradeSelection(WorldSnapshot state, WorldActionCommand command)
    {
        if (command.Intent is not (PaidExchangeIntent or MarketIntent))
        {
            return;
        }
        if (command.Intent is PaidExchangeIntent && state.Coordination?.PaidExchange is null
            || command.Intent is MarketIntent && state.Coordination?.Markets is null)
        {
            throw new AuthorityDispatchException("This saved profile admits no paid quote or market action.");
        }
        if (!LocalProfileBindings.IsUnheldExploration(state))
        {
            throw new AuthorityDispatchException("Paid and market actions require their admitted unheld exploration boundary.");
        }
    }

    /// <summary>
    /// The player and issuer of a valid pending acceptance. Its step spends both opportunities, so neither is
    /// offered other work, an answer or a walk; an invalid or foreign request reserves nobody.
    /// </summary>
    private HashSet<EntityId> SettlingParticipants(WorldSnapshot state, EntityId causeId, WorldActionCommand? command)
    {
        if (command?.Intent is not AcceptSpotQuoteIntent accept
            || state.Coordination?.PaidExchange?.FindOpen(accept.Acceptance.QuoteId) is not { } open
            || open.Quote.CounterpartyActorId != command.ActorId || !Settles(state, causeId, command))
        {
            return [];
        }
        return [command.ActorId, open.Quote.IssuerActorId];
    }

    /// <summary>Advice from complete authority evaluation of one acceptance alone; not a grant or a publication.</summary>
    private bool Settles(WorldSnapshot state, EntityId causeId, WorldActionCommand command)
    {
        try
        {
            var single = new WorldStepBatch(causeId, state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
                WorldBatchKind.AdvancingStep, [], [command], state.ProfileVersion);
            return _authority.Preview(single).Receipts.Single().Rejection == WorldActionRejection.None;
        }
        catch (Exception error) when (error is AuthorityDispatchException or ArgumentException)
        {
            return false;
        }
    }

    private static EntityId NewRequestId()
    {
        Span<byte> bytes = stackalloc byte[EntityId.ByteLength];
        RandomNumberGenerator.Fill(bytes);
        return EntityId.FromBytes(bytes);
    }
}
