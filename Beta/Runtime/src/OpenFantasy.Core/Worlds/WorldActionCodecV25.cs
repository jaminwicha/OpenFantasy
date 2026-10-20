using OpenFantasy.Core.Cognition;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Cognition actions of profile 42: source inspection and recorded decisions. Ordinary and delegated envelopes keep
/// their original identities; a delegated command wraps this ordinary form unchanged.
/// </summary>
public static class WorldActionCodecV25
{
    private const int MaximumByteLength = CognitionCodecV2.MaximumDecisionByteLength + 512;

    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ControlWitness is not null)
            throw new ArgumentException("This version encodes the ordinary cognition command.");
        byte[] intent = command.Intent switch
        {
            InspectGatherSourceIntent inspect => CanonicalEncodingV1.Frame("world.action.inspect-source.v1",
                inspect.NodeId.ToByteArray(), CanonicalEncodingV1.Unsigned64(inspect.ExpectedNodeRevision)),
            RecordCognitionDecisionIntent decide => CanonicalEncodingV1.Frame("world.action.cognition-decision.v1",
                CanonicalEncodingV1.Unsigned64(decide.ExpectedCognitionRevision), CognitionCodecV2.EncodeDecision(decide.Decision)),
            _ => throw new ArgumentException("This version requires a cognition action.")
        };
        return CanonicalEncodingV1.Frame("world.actor-action", CanonicalEncodingV1.Unsigned64(25),
            command.CommandId.ToByteArray(), command.PrincipalId.ToByteArray(), command.OriginSessionId.ToByteArray(),
            command.ActorId.ToByteArray(), command.WorldId.ToByteArray(), command.BranchId.ToByteArray(),
            CanonicalEncodingV1.Unsigned64(command.ExpectedActorRevision), intent);
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, MaximumByteLength, 10);
            reader.Tag("world.actor-action"u8);
            if (reader.Number() != 25) throw new FormatException("Unsupported cognition action version.");
            EntityId command = reader.Identity(), principal = reader.Identity(), session = reader.Identity();
            EntityId actor = reader.Identity(), world = reader.Identity(), branch = reader.Identity();
            ulong revision = reader.Number();
            ReadOnlySpan<byte> intentBytes = reader.Field(MaximumByteLength);
            reader.Complete();
            var probe = new WorldFrameReader(intentBytes, MaximumByteLength, 3);
            bool inspection = probe.Field(64).SequenceEqual("world.action.inspect-source.v1"u8);
            var intent = new WorldFrameReader(intentBytes, MaximumByteLength, 3);
            WorldActionIntent decoded;
            if (inspection)
            {
                intent.Tag("world.action.inspect-source.v1"u8);
                decoded = new InspectGatherSourceIntent(intent.Identity(), intent.Number());
            }
            else
            {
                intent.Tag("world.action.cognition-decision.v1"u8);
                ulong expected = intent.Number();
                decoded = new RecordCognitionDecisionIntent(expected,
                    CognitionCodecV2.DecodeDecision(intent.Field(CognitionCodecV2.MaximumDecisionByteLength)));
            }
            intent.Complete();
            var result = new WorldActionCommand(command, principal, session, actor, world, branch, revision, decoded);
            if (!Encode(result).AsSpan().SequenceEqual(bytes)) throw new FormatException("Noncanonical cognition action.");
            return result;
        }
        catch (ArgumentException error) { throw new FormatException("Invalid cognition action.", error); }
    }
}
