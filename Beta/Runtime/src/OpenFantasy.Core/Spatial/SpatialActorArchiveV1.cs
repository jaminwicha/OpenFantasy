using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Spatial;

/// <summary>Self-contained immutable actor geometry archive; decoding grants neither placement nor world ownership.</summary>
public static class SpatialActorArchiveV1
{
    public const int MaximumByteLength = 96 * 1024;

    public static byte[] Encode(ActorPresence actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        byte[] bytes = CanonicalEncodingV1.Frame("spatial.actor-archive.v1", actor.Scope.WorldId.ToByteArray(),
            actor.Scope.BranchId.ToByteArray(), actor.Scope.RealmId.ToByteArray(), VerticalMotionEncoding.Actor(actor));
        if (bytes.Length > MaximumByteLength)
            throw new ArgumentException("Actor geometry exceeds the archive byte budget.", nameof(actor));
        return bytes;
    }

    public static ActorPresence Decode(ReadOnlySpan<byte> bytes, SpatialOccupancyReference expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (bytes.Length > MaximumByteLength || expected.Kind != SpatialOccupancyKind.Actor
            || Content.ContentHash.Compute(bytes) != expected.ContentHash)
            throw new FormatException("Actor archive bytes do not match the required bounded reference.");
        try
        {
            var outer = new WorldFrameReader(bytes, MaximumByteLength, 5);
            outer.Tag("spatial.actor-archive.v1"u8);
            var scope = new TerrainScope(outer.Identity(), outer.Identity(), outer.Identity());
            if (scope != expected.Scope) throw new FormatException("Actor archive scope differs from its reference.");
            var reader = new WorldFrameReader(outer.Field(MaximumByteLength), MaximumByteLength, 7);
            outer.Complete();
            reader.Tag("actor"u8);
            ReadOnlySpan<byte> bindingBytes = reader.Field(BodyContractDecodingV1.MaximumBindingByteLength);
            var definition = BodyContractDecodingV1.DecodeDefinition(reader.Field(BodyContractDecodingV1.MaximumDefinitionByteLength));
            var binding = BodyContractDecodingV1.DecodeBinding(bindingBytes, definition);
            var condition = BodyContractDecodingV1.DecodeCondition(reader.Field(BodyContractDecodingV1.MaximumConditionByteLength), definition);
            var position = new WorldFrameReader(reader.Field(128), 128, 4);
            position.Tag("position"u8);
            var feet = new SubunitPosition(position.Signed(), position.Signed(), position.Signed());
            position.Complete();
            ulong revision = reader.Number();
            EntityId home = reader.Identity();
            reader.Complete();
            var actor = new ActorPresence(binding, condition, scope, feet, revision, home);
            if (SpatialOccupancyReference.Capture(actor) != expected || !Encode(actor).AsSpan().SequenceEqual(bytes))
                throw new FormatException("Actor archive identity, bounds or canonical contents differ from its reference.");
            return actor;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("Actor archive violates its geometric contracts.", error);
        }
    }
}
