using System.Buffers.Binary;
using System.Text;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.Networking.PlayerObservations;

public sealed class PlayerObservationFrameTests
{
    [Fact]
    public void Full_frame_matches_an_independent_exact_byte_vector_and_owns_its_payload()
    {
        byte[] payload = [1, 2, 3];
        var frame = new PlayerObservationFrame(Id(99), 1, 0, 3, 0, 0, payload);
        payload[0] = 9;
        byte[] expected = Convert.FromHexString("4F46564F00010000"
            + "10203040506070800000000000000063"
            + "00000000000000010000000000000000"
            + "00000003000000000000000000000003010203");
        Assert.Equal(expected, PlayerObservationFrameCodec.Encode(frame));
        PlayerObservationFrame decoded = PlayerObservationFrameCodec.Decode(expected);
        expected[^1] = 9;
        Assert.Equal(Id(99), decoded.StreamId);
        Assert.Equal(1UL, decoded.Revision);
        Assert.True(decoded.IsFull);
        Assert.Equal(new byte[] { 1, 2, 3 }, decoded.Reconstruct([]));
    }

    [Theory]
    [InlineData("truncated")]
    [InlineData("trailing")]
    [InlineData("magic")]
    [InlineData("version")]
    [InlineData("kind")]
    [InlineData("zero-revision")]
    [InlineData("empty")]
    [InlineData("oversize")]
    [InlineData("negative-prefix")]
    [InlineData("negative-suffix")]
    [InlineData("replacement")]
    [InlineData("overflow")]
    [InlineData("full-with-prefix")]
    [InlineData("delta-gap")]
    [InlineData("delta-overflow")]
    public void Invalid_wire_bounds_and_revisions_are_refused(string mutation)
    {
        byte[] bytes = PlayerObservationFrameCodec.Encode(new(Id(99), 1, 0, 3, 0, 0, [1, 2, 3]));
        switch (mutation)
        {
            case "truncated": bytes = bytes[..20]; break;
            case "trailing": bytes = [.. bytes, 0]; break;
            case "magic": bytes[0] = 0; break;
            case "version": bytes[5] = 2; break;
            case "kind": bytes[7] = 2; break;
            case "zero-revision": BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(24), 0); break;
            case "empty": BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(40), 0); break;
            case "oversize": BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(40), PlayerObservationFrameCodec.MaximumViewBytes + 1); break;
            case "negative-prefix": BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(44), -1); break;
            case "negative-suffix": BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(48), -1); break;
            case "replacement": BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(52), 2); break;
            case "overflow": BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(44), int.MaxValue); break;
            case "full-with-prefix":
                BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(40), 4);
                BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(44), 1);
                break;
            case "delta-gap":
            case "delta-overflow":
                bytes[7] = 1;
                BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(32), mutation == "delta-gap" ? 2UL : ulong.MaxValue);
                break;
        }
        Assert.Throws<FormatException>(() => PlayerObservationFrameCodec.Decode(bytes));
    }

    [Theory]
    [InlineData(2, 2, "XYZ", "abXYZef")]
    [InlineData(2, 2, "", "abef")]
    [InlineData(0, 0, "replace", "replace")]
    public void Delta_reconstructs_insertion_deletion_and_replacement(int prefix, int suffix, string replacement, string expected)
    {
        byte[] patch = Encoding.ASCII.GetBytes(replacement);
        var frame = new PlayerObservationFrame(Id(99), 2, 1, prefix + suffix + patch.Length, prefix, suffix, patch);
        PlayerObservationFrame decoded = PlayerObservationFrameCodec.Decode(PlayerObservationFrameCodec.Encode(frame));
        Assert.Equal(Encoding.ASCII.GetBytes(expected), decoded.Reconstruct("abcdef"u8));
        if (prefix + suffix > 1) Assert.Throws<FormatException>(() => decoded.Reconstruct("x"u8));
    }

    [Fact]
    public void Private_root_revision_does_not_advance_the_visible_stream_or_emit_a_delta()
    {
        WorldSnapshot world = Genesis();
        var otherRoot = new WorldSnapshot(world.Inventory, world.Space, world.Residents, world.AdmissionVersion,
            world.Calendar, world.Revision + 123, world.AdvancingSteps, world.Dialogues);
        Assert.NotEqual(world.ContentHash, otherRoot.ContentHash);
        var stream = new PlayerObservationStream();
        LocalWorldView first = new(world, Id(10));
        PlayerObservationFrame full = stream.Capture(first)!;
        Assert.Equal(1UL, full.Revision);
        Assert.Null(stream.Capture(first));
        Assert.Null(stream.Capture(new LocalWorldView(otherRoot, Id(10))));
        PlayerObservationFrame recovery = stream.Capture(new LocalWorldView(otherRoot, Id(10)), forceFull: true)!;
        Assert.True(recovery.IsFull);
        Assert.Equal(full.Revision, recovery.Revision);
        Assert.Equal(full.Replacement.ToArray(), recovery.Replacement.ToArray());
    }

    [Fact]
    public void Delta_loss_retains_one_coherent_snapshot_until_full_recovery()
    {
        var stream = new PlayerObservationStream();
        LocalWorldView a = ViewAt(1000), b = ViewAt(1001), c = ViewAt(1002);
        PlayerObservationFrame first = Wire(stream.Capture(a)!);
        PlayerObservationFrame second = Wire(stream.Capture(b)!);
        PlayerObservationFrame third = Wire(stream.Capture(c)!);
        Assert.False(second.IsFull);
        Assert.True(PlayerObservationFrameCodec.Encode(second).Length < PlayerObservationFrameCodec.Encode(first).Length);
        var replica = new PlayerObservationReplica(a.WorldId, a.BranchId, a.PlayerActorId);
        Assert.Equal(PlayerObservationApplyResult.RequiresFull, replica.Apply(second));
        Assert.Null(replica.ReadView());
        Assert.Equal(PlayerObservationApplyResult.Applied, replica.Apply(first));
        PlayerObservationSnapshot snapshot = replica.ReadSnapshot()!;
        Assert.Equal(PlayerObservationApplyResult.RequiresFull, replica.Apply(third));
        Assert.Same(snapshot, replica.ReadSnapshot());
        Assert.Equal(PlayerObservationApplyResult.Applied, replica.Apply(second));
        Assert.Equal(b.Calendar, replica.ReadView()!.Calendar);
        Assert.Equal(PlayerObservationApplyResult.Applied, replica.Apply(Wire(stream.Capture(c, forceFull: true)!)));
        Assert.Equal(3UL, replica.Revision);
        Assert.Equal(PlayerViewCodec.Encode(c), PlayerViewCodec.Encode(replica.ReadView()!));
        Assert.Equal(a.Calendar, snapshot.View.Calendar);
        Assert.Equal(1UL, snapshot.Revision);
        PlayerObservationSnapshot current = replica.ReadSnapshot()!;
        Assert.Equal(PlayerObservationApplyResult.Unchanged, replica.Apply(Wire(stream.Capture(c, forceFull: true)!)));
        Assert.Same(current, replica.ReadSnapshot());
        Assert.Throws<FormatException>(() => replica.Apply(first));
        Assert.Same(current, replica.ReadSnapshot());
    }

    [Theory]
    [InlineData("stream")]
    [InlineData("actor")]
    [InlineData("world")]
    [InlineData("branch")]
    [InlineData("conflict")]
    [InlineData("body")]
    [InlineData("basis")]
    public void Invalid_updates_never_partially_replace_the_last_good_snapshot(string mutation)
    {
        LocalWorldView original = ViewAt(1000);
        var stream = new PlayerObservationStream();
        PlayerObservationFrame first = Wire(stream.Capture(original)!);
        var replica = new PlayerObservationReplica(original.WorldId, original.BranchId, original.PlayerActorId);
        replica.Apply(first);
        PlayerObservationSnapshot before = replica.ReadSnapshot()!;
        WorldViewData values = WorldViewData.From(ViewAt(1001));
        values = mutation switch
        {
            "actor" => WorldViewData.From(new LocalWorldView(Genesis(), Id(11), regions: new LocalRegionObserver(Id(11)))),
            "world" => values with { WorldId = Id(998) },
            "branch" => values with { BranchId = Id(997) },
            _ => values
        };
        byte[] body = PlayerViewCodec.Encode(values.ToView());
        if (mutation == "body") body[0] = 0;
        var frame = new PlayerObservationFrame(mutation == "stream" ? Id(999) : first.StreamId,
            mutation == "conflict" ? 1UL : 2UL, 0, body.Length, 0, 0, body);
        if (mutation == "basis") frame = new(first.StreamId, 2, 1, before.Bytes.Length + 1, before.Bytes.Length + 1, 0, []);
        Assert.Throws<FormatException>(() => replica.Apply(Wire(frame)));
        Assert.Same(before, replica.ReadSnapshot());
    }

    private static PlayerObservationFrame Wire(PlayerObservationFrame frame) =>
        PlayerObservationFrameCodec.Decode(PlayerObservationFrameCodec.Encode(frame));

    private static LocalWorldView ViewAt(int calendar)
    {
        WorldSnapshot world = Genesis();
        var at = new WorldSnapshot(world.Inventory, world.Space, world.Residents, world.AdmissionVersion,
            new WorldInstant(0, calendar), 0, 0);
        return new LocalWorldView(at, Id(10), regions: new LocalRegionObserver(Id(10)));
    }
}
