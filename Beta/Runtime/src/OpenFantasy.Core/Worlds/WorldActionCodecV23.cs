namespace OpenFantasy.Core.Worlds;

/// <summary>One unchanged ordinary command plus exact delegated-control attribution; nesting is forbidden.</summary>
public static class WorldActionCodecV23
{
    public static byte[] Encode(WorldActionCommand command)
    {
        if (command.ControlWitness is not { } witness) throw new ArgumentException("A delegated envelope requires its witness.");
        var ordinary = new WorldActionCommand(command.CommandId, command.PrincipalId, command.OriginSessionId,
            command.ActorId, command.WorldId, command.BranchId, command.ExpectedActorRevision, command.Intent);
        byte[] bytes = CanonicalEncodingV1.Frame("world.actor-action", CanonicalEncodingV1.Unsigned64(23),
            WorldActionCodec.Encode(ordinary), WorldControlWitnessCodecV1.Encode(witness));
        if (bytes.Length > WorldActionCodec.MaximumCommandByteLength) throw new ArgumentException("Delegated command exceeds the world envelope.");
        return bytes;
    }
    public static WorldActionCommand Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, WorldActionCodec.MaximumCommandByteLength, 4); reader.Tag("world.actor-action"u8);
            if (reader.Number() != 23) throw new FormatException("Unsupported delegated action version.");
            var inner = reader.Field(WorldActionCodec.MaximumCommandByteLength);
            if (WorldCodecVersion.Read(inner, "world.actor-action"u8, WorldActionCodec.MaximumCommandByteLength, 10) == 23)
                throw new FormatException("Nested delegated commands are forbidden.");
            var ordinary = WorldActionCodec.Decode(inner);
            var witness = WorldControlWitnessCodecV1.Decode(reader.Field(WorldControlWitnessCodecV1.MaximumByteLength)); reader.Complete();
            var result = new WorldActionCommand(ordinary.CommandId, ordinary.PrincipalId, ordinary.OriginSessionId, ordinary.ActorId,
                ordinary.WorldId, ordinary.BranchId, ordinary.ExpectedActorRevision, ordinary.Intent, witness);
            if (!Encode(result).AsSpan().SequenceEqual(bytes)) throw new FormatException("Noncanonical delegated action.");
            return result;
        }
        catch (ArgumentException error) { throw new FormatException("Invalid delegated action.", error); }
    }
}
