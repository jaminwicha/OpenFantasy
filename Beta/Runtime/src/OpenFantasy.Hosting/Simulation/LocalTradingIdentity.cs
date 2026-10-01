using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>
/// Stable semantic identities of scheduled trading decisions. Wall clocks, polling and refusals never select a
/// replacement: one answer per inquiry, one acceptance per quote, and one showing and one inquiry per host
/// shopper attempt. An occupied decision therefore stays decided instead of becoming a new world command on every
/// cutoff; only a new attempt (see <see cref="LocalTradingController"/>) is a new decision with new identities.
/// </summary>
internal static class LocalTradingIdentity
{
    internal static EntityId Answer(WorldSnapshot state, EntityId merchantActorId, EntityId shopperActorId, EntityId requestId) =>
        Derive(state, "local.trade-answer.v1", [merchantActorId, shopperActorId, requestId]);

    internal static EntityId Acceptance(WorldSnapshot state, EntityId shopperActorId, EntityId quoteId) =>
        Derive(state, "local.trade-acceptance.v1", [shopperActorId, quoteId]);

    /// <summary>A host shopper's own circumstances for one desire: market, direction, holding and balance; no merchant state.</summary>
    internal static EntityId Circumstances(WorldSnapshot state, EntityId shopperActorId, EntityId marketId,
        LocalTradeDirection direction, ulong holding, ulong balance) =>
        Derive(state, "local.trade-circumstances.v1", [shopperActorId, marketId], [(ulong)direction, holding, balance]);

    /// <summary>
    /// The one showing of an attempt. The attempt's starting step makes each attempt, including the first after a
    /// restart, a new decision instead of an occupied repetition of an earlier one.
    /// </summary>
    internal static EntityId Showing(WorldSnapshot state, EntityId shopperActorId, EntityId marketId,
        EntityId circumstances, ulong attemptStep) =>
        Derive(state, "local.trade-showing.v3", [shopperActorId, marketId, circumstances], [attemptStep]);

    /// <summary>The one inquiry of an attempt; the merchant's answer identity derives from it.</summary>
    internal static EntityId Request(WorldSnapshot state, EntityId shopperActorId, LocalTradeInquiry inquiry,
        EntityId circumstances, ulong attemptStep) =>
        Derive(state, "local.trade-request.v2", [shopperActorId, inquiry.MarketId, inquiry.OwnShownLotId ?? default, circumstances],
            [(ulong)inquiry.Direction, inquiry.Quantity, attemptStep]);

    private static EntityId Derive(WorldSnapshot state, string domain, EntityId[] identities, ulong[]? numbers = null)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.ASCII.GetBytes(domain));
        hash.AppendData(state.WorldId.ToByteArray());
        hash.AppendData(state.BranchId.ToByteArray());
        foreach (EntityId identity in identities)
        {
            hash.AppendData(identity.ToByteArray());
        }
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        foreach (ulong number in numbers ?? [])
        {
            BinaryPrimitives.WriteUInt64BigEndian(bytes, number);
            hash.AppendData(bytes);
        }
        return EntityId.FromBytes(hash.GetHashAndReset().AsSpan(0, EntityId.ByteLength));
    }
}
