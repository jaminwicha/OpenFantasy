using System.Buffers.Binary;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Worlds;

internal static class WorldSpatialCodecV1
{
    internal static byte[] Encode(LocalSpatialSnapshot space)
    {
        BodyDefinition[] definitions = space.Actors.Select(actor => actor.Condition.Definition)
            .GroupBy(definition => definition.DefinitionId).Select(group => group.First())
            .OrderBy(definition => definition.DefinitionId).ToArray();
        return CanonicalEncodingV1.Frame("world.spatial", Number(1), space.Scope.WorldId.ToByteArray(),
            space.Scope.BranchId.ToByteArray(), space.Scope.RealmId.ToByteArray(),
            CanonicalEncodingV1.Frame("world.regions", space.Regions.Select(region =>
                (ReadOnlyMemory<byte>)TerrainRegionCodecV1.Encode(region)).ToArray()),
            CanonicalEncodingV1.Frame("world.bodies", definitions.Select(definition =>
                (ReadOnlyMemory<byte>)BodyContractEncodingV1.EncodeDefinition(definition)).ToArray()),
            CanonicalEncodingV1.Frame("world.presences", space.Actors.Select(actor =>
                (ReadOnlyMemory<byte>)EncodeActor(actor)).ToArray()));
    }

    internal static LocalSpatialSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, WorldSnapshotCodecV1.MaximumStateByteLength, 8);
        reader.Tag("world.spatial"u8);
        reader.Version();
        var scope = new TerrainScope(reader.Identity(), reader.Identity(), reader.Identity());
        var regionsReader = new WorldFrameReader(reader.Field(2 * 1024 * 1024), 2 * 1024 * 1024, 17);
        regionsReader.Tag("world.regions"u8);
        List<TerrainRegionSnapshot> regions = [];
        while (regionsReader.Remaining != 0)
        {
            regions.Add(TerrainRegionCodecV1.Decode(regionsReader.Field(TerrainRegionCodecV1.MaximumEncodedByteLength)));
        }
        regionsReader.Complete();

        var definitionsReader = new WorldFrameReader(reader.Field(2 * 1024 * 1024), 2 * 1024 * 1024, 65);
        definitionsReader.Tag("world.bodies"u8);
        Dictionary<EntityId, BodyDefinition> definitions = [];
        while (definitionsReader.Remaining != 0)
        {
            BodyDefinition definition = BodyContractDecodingV1.DecodeDefinition(
                definitionsReader.Field(BodyContractDecodingV1.MaximumDefinitionByteLength));
            if (!definitions.TryAdd(definition.DefinitionId, definition))
            {
                throw new FormatException("The world repeats a body definition identity.");
            }
        }
        definitionsReader.Complete();

        var actorsReader = new WorldFrameReader(reader.Field(4 * 1024 * 1024), 4 * 1024 * 1024, 65);
        actorsReader.Tag("world.presences"u8);
        List<ActorPresence> actors = [];
        while (actorsReader.Remaining != 0)
        {
            actors.Add(DecodeActor(actorsReader.Field(64 * 1024), scope, definitions));
        }
        actorsReader.Complete();
        reader.Complete();
        return new LocalSpatialSnapshot(scope, regions, actors);
    }

    private static byte[] EncodeActor(ActorPresence actor) => CanonicalEncodingV1.Frame("world.presence",
        actor.Binding.DefinitionId.ToByteArray(), BodyContractEncodingV1.EncodeBinding(actor.Binding),
        BodyContractEncodingV1.EncodeCondition(actor.Condition), Signed(actor.Feet.X), Signed(actor.Feet.Y),
        Signed(actor.Feet.Z), Number(actor.Revision), actor.HomePlaceId.ToByteArray());

    private static ActorPresence DecodeActor(ReadOnlySpan<byte> bytes, TerrainScope scope,
        IReadOnlyDictionary<EntityId, BodyDefinition> definitions)
    {
        var reader = new WorldFrameReader(bytes, 64 * 1024, 9);
        reader.Tag("world.presence"u8);
        if (!definitions.TryGetValue(reader.Identity(), out BodyDefinition? definition))
        {
            throw new FormatException("A world actor references an absent body definition.");
        }
        ActorFormBinding binding = BodyContractDecodingV1.DecodeBinding(
            reader.Field(BodyContractDecodingV1.MaximumBindingByteLength), definition);
        BodyConditionSnapshot condition = BodyContractDecodingV1.DecodeCondition(
            reader.Field(BodyContractDecodingV1.MaximumConditionByteLength), definition);
        var feet = new SubunitPosition(reader.Signed(), reader.Signed(), reader.Signed());
        ulong revision = reader.Number();
        EntityId home = reader.Identity();
        reader.Complete();
        return new ActorPresence(binding, condition, scope, feet, revision, home);
    }

    internal static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
    internal static byte[] Signed(long value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }
}
