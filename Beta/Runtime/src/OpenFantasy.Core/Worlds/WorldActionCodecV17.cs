using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Markets;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Exact paid spot-exchange submissions: quote issuance, acceptance and the issuer's withdrawal. The terms
/// reuse the spot-exchange contract frames. Decoding validates shape only; no step profile but the paid
/// world admits these intents.
/// </summary>
public static class WorldActionCodecV17
{
    public const int MaximumCommandByteLength = 2_048;
    private const int MaximumIntentByteLength = 1_536;

    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ControlWitness is not null) throw new ArgumentException("Delegated attribution requires action codec 23.", nameof(command));
        byte[] intent = command.Intent switch
        {
            IssueSpotQuoteIntent issue => CanonicalEncodingV1.Frame("world.action.spot-quote-issue", Number(1),
                SpotExchangeCodecV1.Encode(issue.Quote)),
            AcceptSpotQuoteIntent accept => CanonicalEncodingV1.Frame("world.action.spot-quote-accept", Number(1),
                SpotExchangeCodecV1.Encode(accept.Acceptance), Number(accept.ExpectedIssuerRevision)),
            WithdrawSpotQuoteIntent withdraw => CanonicalEncodingV1.Frame("world.action.spot-quote-withdraw", Number(1),
                withdraw.QuoteId.ToByteArray()),
            _ => throw new ArgumentException("This command version requires a paid exchange intent.", nameof(command))
        };
        byte[] bytes = CanonicalEncodingV1.Frame("world.actor-action", Number(17), command.CommandId.ToByteArray(),
            command.PrincipalId.ToByteArray(), command.OriginSessionId.ToByteArray(), command.ActorId.ToByteArray(),
            command.WorldId.ToByteArray(), command.BranchId.ToByteArray(), Number(command.ExpectedActorRevision), intent);
        if (bytes.Length > MaximumCommandByteLength) throw new ArgumentException("Paid exchange command exceeds its bounded frame.");
        return bytes;
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumCommandByteLength, 10);
            reader.Tag("world.actor-action"u8);
            if (reader.Number() != 17) throw new FormatException("Unsupported paid exchange command version.");
            EntityId command = reader.Identity(), principal = reader.Identity(), session = reader.Identity();
            EntityId actor = reader.Identity(), world = reader.Identity(), branch = reader.Identity();
            ulong revision = reader.Number();
            var payload = new WorldFrameReader(reader.Field(MaximumIntentByteLength), MaximumIntentByteLength, 5);
            ReadOnlySpan<byte> tag = payload.Field(64);
            payload.Version();
            WorldActionIntent intent;
            if (tag.SequenceEqual("world.action.spot-quote-issue"u8))
            {
                intent = new IssueSpotQuoteIntent(
                    SpotExchangeCodecV1.DecodeQuote(payload.Field(SpotExchangeCodecV1.MaximumQuoteByteLength)));
            }
            else if (tag.SequenceEqual("world.action.spot-quote-accept"u8))
            {
                intent = new AcceptSpotQuoteIntent(
                    SpotExchangeCodecV1.DecodeAcceptance(payload.Field(SpotExchangeCodecV1.MaximumAcceptanceByteLength)),
                    payload.Number());
            }
            else if (tag.SequenceEqual("world.action.spot-quote-withdraw"u8))
            {
                intent = new WithdrawSpotQuoteIntent(payload.Identity());
            }
            else throw new FormatException("Unsupported paid exchange intent.");
            payload.Complete();
            reader.Complete();
            var restored = new WorldActionCommand(command, principal, session, actor, world, branch, revision, intent);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes)) throw new FormatException("Paid exchange command is not canonical.");
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException
            or AuthorityFormatException)
        {
            throw new FormatException("Paid exchange command violates its invariants.", error);
        }
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
}
