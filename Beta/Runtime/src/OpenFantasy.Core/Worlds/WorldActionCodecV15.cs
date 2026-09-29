using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Commands;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Exact paired material offer and acceptance submissions, independent of transport authentication.
/// Decoding validates shape only; no step profile admits these intents without its explicit
/// complete-world interpretation.
/// </summary>
public static class WorldActionCodecV15
{
    public const int MaximumCommandByteLength = 2_048;
    private const int MaximumIntentByteLength = 1_024;

    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        byte[] intent = command.Intent switch
        {
            OfferMaterialIntent offer => CanonicalEncodingV1.Frame("world.action.material-offer", Number(1),
                ConsentedMaterialCodecV1.EncodeTerms(offer.Terms), Number(offer.ExpectedRecipientRevision)),
            AcceptMaterialIntent accept => CanonicalEncodingV1.Frame("world.action.material-acceptance", Number(1),
                accept.OfferCommandId.ToByteArray(), accept.OfferHash.ToByteArray(),
                ConsentedMaterialCodecV1.EncodeTerms(accept.Terms), Number(accept.ExpectedSenderRevision)),
            _ => throw new ArgumentException("This command version requires a paired material intent.", nameof(command))
        };
        byte[] bytes = CanonicalEncodingV1.Frame("world.actor-action", Number(15), command.CommandId.ToByteArray(),
            command.PrincipalId.ToByteArray(), command.OriginSessionId.ToByteArray(), command.ActorId.ToByteArray(),
            command.WorldId.ToByteArray(), command.BranchId.ToByteArray(), Number(command.ExpectedActorRevision), intent);
        if (bytes.Length > MaximumCommandByteLength) throw new ArgumentException("Material command exceeds its bounded frame.");
        return bytes;
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumCommandByteLength, 10);
            reader.Tag("world.actor-action"u8);
            if (reader.Number() != 15) throw new FormatException("Unsupported material command version.");
            EntityId command = reader.Identity(), principal = reader.Identity(), session = reader.Identity();
            EntityId actor = reader.Identity(), world = reader.Identity(), branch = reader.Identity();
            ulong revision = reader.Number();
            var payload = new WorldFrameReader(reader.Field(MaximumIntentByteLength), MaximumIntentByteLength, 6);
            ReadOnlySpan<byte> tag = payload.Field(64);
            payload.Version();
            WorldActionIntent intent;
            if (tag.SequenceEqual("world.action.material-offer"u8))
            {
                intent = new OfferMaterialIntent(
                    ConsentedMaterialCodecV1.DecodeTerms(payload.Field(ConsentedMaterialCodecV1.MaximumTermsByteLength)),
                    payload.Number());
            }
            else if (tag.SequenceEqual("world.action.material-acceptance"u8))
            {
                EntityId offer = payload.Identity();
                CommandContentHash hash = CommandContentHash.FromBytes(payload.Exact(CommandContentHash.ByteLength));
                intent = new AcceptMaterialIntent(offer, hash,
                    ConsentedMaterialCodecV1.DecodeTerms(payload.Field(ConsentedMaterialCodecV1.MaximumTermsByteLength)),
                    payload.Number());
            }
            else throw new FormatException("Unsupported material intent.");
            payload.Complete();
            reader.Complete();
            var restored = new WorldActionCommand(command, principal, session, actor, world, branch, revision, intent);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes)) throw new FormatException("Material command is not canonical.");
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException
            or AuthorityFormatException)
        {
            throw new FormatException("Material command violates its invariants.", error);
        }
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
}
