using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Commands;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Exact saved-proposal submissions: creation, acceptance, decline and withdrawal. Decoding validates
/// shape only; no step profile admits these intents without its explicit complete-world interpretation.
/// </summary>
public static class WorldActionCodecV16
{
    public const int MaximumCommandByteLength = 2_048;
    private const int MaximumIntentByteLength = 1_024;

    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ControlWitness is not null) throw new ArgumentException("Delegated attribution requires action codec 23.", nameof(command));
        byte[] intent = command.Intent switch
        {
            ProposeMaterialIntent propose => CanonicalEncodingV1.Frame("world.action.material-proposal-create", Number(1),
                ConsentedMaterialCodecV1.EncodeTerms(propose.Terms), Number(propose.ExpiresAtAdvancingStep)),
            AcceptMaterialProposalIntent accept => CanonicalEncodingV1.Frame("world.action.material-proposal-accept",
                Number(1), accept.ProposalId.ToByteArray(), accept.GrantHash.ToByteArray(),
                ConsentedMaterialCodecV1.EncodeTerms(accept.Terms), Number(accept.ExpectedSenderRevision)),
            DeclineMaterialProposalIntent decline => CanonicalEncodingV1.Frame("world.action.material-proposal-decline",
                Number(1), decline.ProposalId.ToByteArray()),
            WithdrawMaterialProposalIntent withdraw => CanonicalEncodingV1.Frame("world.action.material-proposal-withdraw",
                Number(1), withdraw.ProposalId.ToByteArray()),
            _ => throw new ArgumentException("This command version requires a saved proposal intent.", nameof(command))
        };
        byte[] bytes = CanonicalEncodingV1.Frame("world.actor-action", Number(16), command.CommandId.ToByteArray(),
            command.PrincipalId.ToByteArray(), command.OriginSessionId.ToByteArray(), command.ActorId.ToByteArray(),
            command.WorldId.ToByteArray(), command.BranchId.ToByteArray(), Number(command.ExpectedActorRevision), intent);
        if (bytes.Length > MaximumCommandByteLength) throw new ArgumentException("Proposal command exceeds its bounded frame.");
        return bytes;
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumCommandByteLength, 10);
            reader.Tag("world.actor-action"u8);
            if (reader.Number() != 16) throw new FormatException("Unsupported proposal command version.");
            EntityId command = reader.Identity(), principal = reader.Identity(), session = reader.Identity();
            EntityId actor = reader.Identity(), world = reader.Identity(), branch = reader.Identity();
            ulong revision = reader.Number();
            var payload = new WorldFrameReader(reader.Field(MaximumIntentByteLength), MaximumIntentByteLength, 6);
            ReadOnlySpan<byte> tag = payload.Field(64);
            payload.Version();
            WorldActionIntent intent;
            if (tag.SequenceEqual("world.action.material-proposal-create"u8))
            {
                intent = new ProposeMaterialIntent(
                    ConsentedMaterialCodecV1.DecodeTerms(payload.Field(ConsentedMaterialCodecV1.MaximumTermsByteLength)),
                    payload.Number());
            }
            else if (tag.SequenceEqual("world.action.material-proposal-accept"u8))
            {
                EntityId proposal = payload.Identity();
                CommandContentHash hash = CommandContentHash.FromBytes(payload.Exact(CommandContentHash.ByteLength));
                intent = new AcceptMaterialProposalIntent(proposal, hash,
                    ConsentedMaterialCodecV1.DecodeTerms(payload.Field(ConsentedMaterialCodecV1.MaximumTermsByteLength)),
                    payload.Number());
            }
            else if (tag.SequenceEqual("world.action.material-proposal-decline"u8))
            {
                intent = new DeclineMaterialProposalIntent(payload.Identity());
            }
            else if (tag.SequenceEqual("world.action.material-proposal-withdraw"u8))
            {
                intent = new WithdrawMaterialProposalIntent(payload.Identity());
            }
            else throw new FormatException("Unsupported proposal intent.");
            payload.Complete();
            reader.Complete();
            var restored = new WorldActionCommand(command, principal, session, actor, world, branch, revision, intent);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes)) throw new FormatException("Proposal command is not canonical.");
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException
            or AuthorityFormatException)
        {
            throw new FormatException("Proposal command violates its invariants.", error);
        }
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
}
