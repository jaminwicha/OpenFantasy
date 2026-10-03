using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Worlds;

/// <summary>Explicit owner offers, named-home-actor acceptance and owner revocation. No delegated gameplay envelope.</summary>
public static class WorldActionCodecV22
{
    public const int MaximumCommandByteLength = 768;
    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        byte[] intent = command.Intent switch
        {
            OfferControlPermissionIntent offer => CanonicalEncodingV1.Frame("world.action.offer-control.v1", offer.GranteeActorId.ToByteArray(), Number(offer.ExpectedSlotRevision), Number((ulong)offer.Capabilities)),
            AcceptControlPermissionIntent accept => CanonicalEncodingV1.Frame("world.action.accept-control.v1", accept.OwnerActorId.ToByteArray(), Number(accept.ExpectedSlotRevision), accept.GrantCommandId.ToByteArray(), accept.TermsHash.ToByteArray()),
            RevokeControlPermissionIntent revoke => CanonicalEncodingV1.Frame("world.action.revoke-control.v1", revoke.GranteeActorId.ToByteArray(), Number(revoke.ExpectedSlotRevision)),
            _ => throw new ArgumentException("This codec requires a control permission command.", nameof(command))
        };
        return CanonicalEncodingV1.Frame("world.actor-action", Number(22), command.CommandId.ToByteArray(),
            command.PrincipalId.ToByteArray(), command.OriginSessionId.ToByteArray(), command.ActorId.ToByteArray(),
            command.WorldId.ToByteArray(), command.BranchId.ToByteArray(), Number(command.ExpectedActorRevision), intent);
    }
    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumCommandByteLength, 10);
            reader.Tag("world.actor-action"u8);
            if (reader.Number() != 22) throw new FormatException("Unsupported control permission action version.");
            var command = reader.Identity(); var principal = reader.Identity(); var session = reader.Identity();
            var actor = reader.Identity(); var world = reader.Identity(); var branch = reader.Identity();
            ulong revision = reader.Number();
            var fields = new WorldFrameReader(reader.Field(256), 256, 5);
            var tag = fields.Field(64); var other = fields.Identity(); ulong slotRevision = fields.Number();
            ControlPermissionIntent intent;
            if (tag.SequenceEqual("world.action.offer-control.v1"u8))
                intent = new OfferControlPermissionIntent(other, slotRevision, (WorldControlCapabilities)fields.Number());
            else if (tag.SequenceEqual("world.action.accept-control.v1"u8))
                intent = new AcceptControlPermissionIntent(other, slotRevision, fields.Identity(), new ContentHash(Convert.ToHexString(fields.Exact(32)).ToLowerInvariant()));
            else if (tag.SequenceEqual("world.action.revoke-control.v1"u8))
                intent = new RevokeControlPermissionIntent(other, slotRevision);
            else throw new FormatException("Unknown control permission intent.");
            fields.Complete(); reader.Complete();
            var result = new WorldActionCommand(command, principal, session, actor, world, branch, revision, intent);
            if (!Encode(result).AsSpan().SequenceEqual(bytes)) throw new FormatException("Noncanonical control permission action.");
            return result;
        }
        catch (ArgumentException error) { throw new FormatException("Invalid control permission action.", error); }
    }
    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
}
