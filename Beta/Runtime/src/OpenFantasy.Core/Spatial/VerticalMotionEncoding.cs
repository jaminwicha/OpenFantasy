using System.Buffers.Binary;
using OpenFantasy.Core.Actors;

namespace OpenFantasy.Core.Spatial;

/// <summary>Complete canonical dependencies of a supplied geometric calculation, not an authority archive.</summary>
internal static class VerticalMotionEncoding
{
    internal static byte[] EncodeScene(VerticalMotionScene scene) => CanonicalEncodingV1.Frame(
        "spatial.vertical-motion-scene.v1", scene.Scope.WorldId.ToByteArray(), scene.Scope.BranchId.ToByteArray(),
        scene.Scope.RealmId.ToByteArray(), Signed(scene.MaximumDropSubunits),
        CanonicalEncodingV1.Frame("regions", scene.Regions.Select(region =>
            (ReadOnlyMemory<byte>)region.ComputeContentHash().ToByteArray()).ToArray()),
        CanonicalEncodingV1.Frame("stationary-structures", scene.StationaryStructures.Select(structure =>
            (ReadOnlyMemory<byte>)Structure(structure)).ToArray()),
        CanonicalEncodingV1.Frame("stationary-actors", scene.StationaryActors.Select(actor =>
            (ReadOnlyMemory<byte>)Actor(actor)).ToArray()),
        CanonicalEncodingV1.Frame("moving-bodies", scene.MovingBodies.Select(body =>
            (ReadOnlyMemory<byte>)(body.Structure is { } structure ? Structure(structure) : Actor(body.Actor!))).ToArray()));

    internal static byte[] Structure(SpatialStructure structure) => CanonicalEncodingV1.Frame("structure",
        structure.StructureId.ToByteArray(), CanonicalEncodingV1.Frame("boxes", structure.Boxes.Select(box =>
            (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("box", Position(box.Minimum), Position(box.Maximum))).ToArray()),
        CanonicalEncodingV1.Frame("surfaces", structure.BearingSurfaces.Select(surface =>
            (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("surface", SignedWide(surface.Bounds.MinU),
                SignedWide(surface.Bounds.MinV), SignedWide(surface.Bounds.MaxU), SignedWide(surface.Bounds.MaxV),
                SignedWide(surface.TopTicks))).ToArray()));

    internal static byte[] Actor(ActorPresence actor) => CanonicalEncodingV1.Frame("actor",
        BodyContractEncodingV1.EncodeBinding(actor.Binding), BodyContractEncodingV1.EncodeDefinition(actor.Condition.Definition),
        BodyContractEncodingV1.EncodeCondition(actor.Condition), Position(actor.Feet),
        CanonicalEncodingV1.Unsigned64(actor.Revision), actor.HomePlaceId.ToByteArray());

    private static byte[] Position(SubunitPosition position) =>
        CanonicalEncodingV1.Frame("position", Signed(position.X), Signed(position.Y), Signed(position.Z));

    private static byte[] Signed(long value)
    {
        byte[] bytes = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] SignedWide(Int128 value)
    {
        byte[] bytes = new byte[16];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, unchecked((ulong)(value >> 64)));
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(sizeof(ulong)), unchecked((ulong)value));
        return bytes;
    }
}
