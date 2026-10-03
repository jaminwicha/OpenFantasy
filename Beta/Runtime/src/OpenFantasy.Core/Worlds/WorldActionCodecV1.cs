namespace OpenFantasy.Core.Worlds;

/// <summary>Discrete action bytes, separate from unchanged counted-command v1 and fixed-step inputs.</summary>
public static class WorldActionCodecV1
{
    public const int MaximumCommandByteLength = 1024;

    public static byte[] Encode(WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.ControlWitness is not null) throw new ArgumentException("Delegated attribution requires action codec 23.", nameof(command));
        return CanonicalEncodingV1.Frame("world.actor-action", Number(1), command.CommandId.ToByteArray(),
            command.PrincipalId.ToByteArray(), command.OriginSessionId.ToByteArray(), command.ActorId.ToByteArray(),
            command.WorldId.ToByteArray(), command.BranchId.ToByteArray(), Number(command.ExpectedActorRevision),
            EncodeIntent(command.Intent));
    }

    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumCommandByteLength, 10);
        reader.Tag("world.actor-action"u8);
        reader.Version();
        EntityId command = reader.Identity();
        EntityId principal = reader.Identity();
        EntityId session = reader.Identity();
        EntityId actor = reader.Identity();
        EntityId world = reader.Identity();
        EntityId branch = reader.Identity();
        ulong revision = reader.Number();
        WorldActionIntent intent = DecodeIntent(reader.Field(512));
        reader.Complete();
        try
        {
            return new WorldActionCommand(command, principal, session, actor, world, branch, revision, intent);
        }
        catch (ArgumentException error)
        {
            throw new FormatException("World action fields violate their invariants.", error);
        }
    }

    private static byte[] EncodeIntent(WorldActionIntent intent) => intent switch
    {
        SpeakToActorIntent speak => CanonicalEncodingV1.Frame("world.action.speak", Number(1),
            speak.TargetActorId.ToByteArray(), Number(speak.ExpectedTargetRevision)),
        GiveItemIntent give => CanonicalEncodingV1.Frame("world.action.give", Number(1),
            give.TargetActorId.ToByteArray(), Number(give.ExpectedTargetRevision), give.LotId.ToByteArray(),
            Number(give.ExpectedLotRevision), give.SourceCustodianId.ToByteArray(),
            Number(give.ExpectedSourceRevision), Number(give.Quantity)),
        CloseDialogueIntent close => CanonicalEncodingV1.Frame("world.action.close-dialogue", Number(1), close.DialogueId.ToByteArray()),
        _ => throw new ArgumentException("The world action kind is unsupported.", nameof(intent))
    };

    private static WorldActionIntent DecodeIntent(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, 512, 9);
        string tag = reader.Text(64);
        reader.Version();
        try
        {
            WorldActionIntent intent = tag switch
            {
                "world.action.speak" => new SpeakToActorIntent(reader.Identity(), reader.Number()),
                "world.action.give" => new GiveItemIntent(reader.Identity(), reader.Number(), reader.Identity(),
                    reader.Number(), reader.Identity(), reader.Number(), reader.Number()),
                "world.action.close-dialogue" => new CloseDialogueIntent(reader.Identity()),
                _ => throw new FormatException("Unsupported world action kind.")
            };
            reader.Complete();
            return intent;
        }
        catch (ArgumentException error)
        {
            throw new FormatException("World action payload violates its invariants.", error);
        }
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
}
