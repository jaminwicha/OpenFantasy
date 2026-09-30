using System.Buffers.Binary;
using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Care;

namespace OpenFantasy.Hosting.Tests.ResourceEncoding;

/// <summary>
/// Actual resource-only (V2) and coordinated (V3) anchors framed around the structural V1 bytes that
/// each immutable history retains. Expected outer frames come from an independent layout writer.
/// </summary>
public sealed class AnchoredResourceHistoryEncodingTests
{
    private static readonly Func<EntityId, bool> NoHistoricalIdentity = _ => false;

    [Fact]
    public void Archive_anchor_frames_the_unchanged_structure_and_keeps_its_exact_refusals()
    {
        var fixture = new CareWorldFixture();
        var world = fixture.PrepareVictory(new(fixture.Genesis));
        var anchor = WorldResourceArchiveSegment.Seal(world).Anchor;
        var continued = new WorldResourceHistory(world.AdmissionVersion,
            new ActorResourceHistory(world.Progression!, world.Revision, []), [], anchor);
        var inline = new WorldResourceHistory(continued.AnchorAdmission, continued.History, continued.Evidence);

        // Encode the anchored history first, so its structure is captured without the inline twin.
        byte[] actual = WorldResourceHistoryCodec.Encode(continued);
        byte[] structure = WorldResourceHistoryCodecV1.Encode(inline);
        var reference = anchor.Reference;
        byte[] expected = Frame("world.resource-history", Number(2),
            Frame("world.resource-archive-reference.v1", reference.WorldId.ToByteArray(), reference.BranchId.ToByteArray(),
                Number(reference.Ordinal), Number(reference.BoundarySequence), reference.BoundaryHash.ToByteArray()),
            structure);

        Assert.Equal(expected, actual);
        Assert.Equal(ContentHash.Compute(expected), continued.ContentHash);
        actual.AsSpan().Clear();
        Assert.Equal(expected, WorldResourceHistoryCodec.Encode(continued));
        Assert.Throws<ArgumentException>(() => WorldResourceHistoryCodecV1.Encode(continued));

        Assert.Throws<FormatException>(() => WorldResourceHistoryCodec.Decode(expected));
        Assert.Throws<FormatException>(() => WorldResourceHistoryCodec.Decode(structure, anchor));
        var restored = WorldResourceHistoryCodec.Decode(expected, anchor);
        Assert.Same(anchor, restored.ArchiveAnchor);
        Assert.Equal(expected, WorldResourceHistoryCodec.Encode(restored));
        Assert.Equal(structure, WorldResourceHistoryCodec.Encode(WorldResourceHistoryCodec.Decode(structure)));
    }

    [Fact]
    public void Joint_anchor_frames_the_unchanged_structure_and_keeps_exact_context_refusals()
    {
        WorldSnapshot closing = Advance(WorldCoordinatedAdmission.CreateFresh(new OrdinaryCareWorldFixture().Genesis), 1);
        var segment = WorldMaterialCareArchiveSegment.Seal(closing);
        WorldSnapshot continued = segment.Continued;
        var history = continued.ResourceHistory!;
        var anchor = segment.Anchor;
        Assert.Same(anchor, history.CoordinatedAnchor);
        var inline = new WorldResourceHistory(history.AnchorAdmission, history.History, history.Evidence);

        byte[] actual = WorldResourceHistoryCodec.Encode(history);
        byte[] structure = WorldResourceHistoryCodecV1.Encode(inline);
        var reference = anchor.Reference;
        byte[] binding = Frame("world.material-care-binding.v1",
            Frame("world.material-care-reference.v1", reference.WorldId.ToByteArray(), reference.BranchId.ToByteArray(),
                Number(reference.Ordinal), Number(reference.BoundarySequence), reference.BoundaryHash.ToByteArray()),
            anchor.ContentHash.ToByteArray());
        byte[] expected = Frame("world.resource-history", Number(3), binding, structure);

        Assert.Equal(expected, actual);
        Assert.Throws<ArgumentException>(() => WorldResourceHistoryCodecV1.Encode(history));
        actual.AsSpan().Fill(0xff);
        Assert.Equal(expected, WorldResourceHistoryCodec.Encode(history));

        Assert.Throws<FormatException>(() => WorldResourceHistoryCodec.Decode(expected));
        Assert.Throws<FormatException>(() => WorldResourceHistoryCodec.DecodeCoordinated(structure, anchor));
        var restored = WorldResourceHistoryCodec.DecodeCoordinated(expected, anchor);
        Assert.Same(anchor, restored.CoordinatedAnchor);
        Assert.Equal(expected, WorldResourceHistoryCodec.Encode(restored));

        // A later joint boundary names a different closing root; its binding must refuse these bytes.
        var later = WorldMaterialCareArchiveSegment.Seal(Advance(continued, 2));
        Assert.Throws<FormatException>(() => WorldResourceHistoryCodec.DecodeCoordinated(expected, later.Anchor));

        byte[] world = WorldSnapshotCodec.Encode(continued);
        Assert.True(world.AsSpan().IndexOf(expected) > 0);
        Assert.Equal(world, WorldSnapshotCodec.Encode(WorldSnapshotCodec.DecodeCoordinated(world, anchor)));
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.DecodeCoordinated(world, later.Anchor));
    }

    private static WorldSnapshot Advance(WorldSnapshot state, int steps)
    {
        var processor = new WorldStepProcessor();
        for (int step = 0; step < steps; step++)
        {
            state = processor.Decide(state, Step(state, (ulong)step), NoHistoricalIdentity).Candidate;
        }
        return state;
    }

    private static WorldStepBatch Step(WorldSnapshot state, ulong salt) => new(
        EntityId.DeriveSpawned(state.WorldId, checked(0xE0C0_0000UL + state.Revision * 16 + salt)), state.WorldId,
        state.BranchId, state.Revision, state.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [], state.ProfileVersion);

    // Independent writer of the documented canonical frame layout; it shares no production encoder.
    private static byte[] Frame(string tag, params byte[][] fields)
    {
        List<byte> output = [.. "OFC\0"u8.ToArray(), .. Unsigned32(1), .. Unsigned32(checked((uint)fields.Length + 1))];
        foreach (byte[] field in (byte[][])[Encoding.UTF8.GetBytes(tag), .. fields])
        {
            output.AddRange(Unsigned32(checked((uint)field.Length)));
            output.AddRange(field);
        }
        return [.. output];
    }

    private static byte[] Number(ulong value)
    {
        byte[] bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] Unsigned32(uint value)
    {
        byte[] bytes = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }
}
