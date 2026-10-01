using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Exact local-market submissions: a rule-priced quote issuance, an in-person price observation and a
/// seller's lot disclosure or its withdrawal. The quote reuses the spot-exchange frame. Decoding validates
/// shape only; only profile 33 admits these intents.
/// </summary>
public static class WorldActionCodecV18
{
    public const int MaximumCommandByteLength = 2_048;
    private const int MaximumIntentByteLength = 1_536;

    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        byte[] intent = command.Intent switch
        {
            IssueMarketQuoteIntent issue => CanonicalEncodingV1.Frame("world.action.market-quote-issue", Number(1),
                issue.MarketId.ToByteArray(), SpotExchangeCodecV1.Encode(issue.Quote)),
            ObserveMarketIntent observe => CanonicalEncodingV1.Frame("world.action.market-observe", Number(1),
                observe.MarketId.ToByteArray(), Number(observe.ExpectedMerchantRevision)),
            DiscloseMarketLotIntent disclose => CanonicalEncodingV1.Frame("world.action.market-lot-disclose", Number(1),
                disclose.MarketId.ToByteArray(), Number((ulong)disclose.LotKind), disclose.LotId.ToByteArray(),
                disclose.Definition.DefinitionId.ToByteArray(), disclose.Definition.Version.ToByteArray(),
                Number(disclose.ExpectedLotRevision), Number(disclose.Quantity), disclose.SellerAccountId.ToByteArray(),
                Number(disclose.ExpiresAtStep)),
            WithdrawMarketLotDisclosureIntent withdraw => CanonicalEncodingV1.Frame("world.action.market-lot-withdraw",
                Number(1), withdraw.MarketId.ToByteArray()),
            _ => throw new ArgumentException("This command version requires a market intent.", nameof(command))
        };
        byte[] bytes = CanonicalEncodingV1.Frame("world.actor-action", Number(18), command.CommandId.ToByteArray(),
            command.PrincipalId.ToByteArray(), command.OriginSessionId.ToByteArray(), command.ActorId.ToByteArray(),
            command.WorldId.ToByteArray(), command.BranchId.ToByteArray(), Number(command.ExpectedActorRevision), intent);
        if (bytes.Length > MaximumCommandByteLength) throw new ArgumentException("Market command exceeds its bounded frame.");
        return bytes;
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumCommandByteLength, 10);
            reader.Tag("world.actor-action"u8);
            if (reader.Number() != 18) throw new FormatException("Unsupported market command version.");
            EntityId command = reader.Identity(), principal = reader.Identity(), session = reader.Identity();
            EntityId actor = reader.Identity(), world = reader.Identity(), branch = reader.Identity();
            ulong revision = reader.Number();
            var payload = new WorldFrameReader(reader.Field(MaximumIntentByteLength), MaximumIntentByteLength, 11);
            ReadOnlySpan<byte> tag = payload.Field(64);
            payload.Version();
            WorldActionIntent intent;
            if (tag.SequenceEqual("world.action.market-quote-issue"u8))
            {
                EntityId market = payload.Identity();
                intent = new IssueMarketQuoteIntent(market,
                    SpotExchangeCodecV1.DecodeQuote(payload.Field(SpotExchangeCodecV1.MaximumQuoteByteLength)));
            }
            else if (tag.SequenceEqual("world.action.market-observe"u8))
            {
                EntityId market = payload.Identity();
                intent = new ObserveMarketIntent(market, payload.Number());
            }
            else if (tag.SequenceEqual("world.action.market-lot-disclose"u8))
            {
                EntityId market = payload.Identity();
                ulong kind = payload.Number();
                EntityId lot = payload.Identity();
                var definition = new CountedItemDefinition(payload.Identity(),
                    new ContentHash(Convert.ToHexString(payload.Exact(32)).ToLowerInvariant()));
                ulong lotRevision = payload.Number(), quantity = payload.Number();
                EntityId account = payload.Identity();
                intent = new DiscloseMarketLotIntent(market, checked((SpotLotKind)(int)kind), lot, definition, lotRevision,
                    quantity, account, payload.Number());
            }
            else if (tag.SequenceEqual("world.action.market-lot-withdraw"u8))
            {
                intent = new WithdrawMarketLotDisclosureIntent(payload.Identity());
            }
            else throw new FormatException("Unsupported market intent.");
            payload.Complete();
            reader.Complete();
            var restored = new WorldActionCommand(command, principal, session, actor, world, branch, revision, intent);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes)) throw new FormatException("Market command is not canonical.");
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException
            or AuthorityFormatException)
        {
            throw new FormatException("Market command violates its invariants.", error);
        }
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
}
