using System.Buffers.Binary;
using System.Text;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Progression.ProgressionFixture;
using static OpenFantasy.Core.Tests.Progression.ResourceHistory.ActorResourceHistoryTests;

namespace OpenFantasy.Core.Tests.ResourceEncoding;

/// <summary>
/// The structural frame retained by one immutable world resource history. Every expectation comes from
/// an independent writer of the unchanged V1 layout, never from a history's retained buffer.
/// </summary>
public sealed class WorldResourceHistoryEncodingTests
{
    private static readonly ContentHash Admission = ContentHash.Compute("resource encoding admission"u8);

    [Fact]
    public void Every_encode_reproduces_the_independent_v1_layout_for_each_simple_cause_kind()
    {
        var history = Build(Create(), 5);
        byte[] expected = ExpectedV1(history);

        Assert.Equal(expected, WorldResourceHistoryCodec.Encode(history));
        Assert.Equal(expected, WorldResourceHistoryCodecV1.Encode(history));
        Assert.Equal(expected, WorldResourceHistoryCodec.Encode(history));
        Assert.Equal(ContentHash.Compute(expected), history.ContentHash);
        Assert.True(expected.AsSpan().IndexOf("world.resource-restorative-use.v1"u8) >= 0);
        Assert.True(expected.AsSpan().IndexOf("world.resource-treatment.v1"u8) >= 0);
        Assert.True(expected.AsSpan().IndexOf("world.resource-reward.v1"u8) >= 0);

        var restored = WorldResourceHistoryCodec.Decode(expected);
        Assert.Equal(expected, WorldResourceHistoryCodec.Encode(restored));
        Assert.Equal(3, restored.History.Batches.Count);
        Assert.Throws<FormatException>(() => WorldResourceHistoryCodec.Decode([.. expected, 0]));
    }

    [Fact]
    public void Changing_public_outputs_cannot_change_the_owned_frame_or_later_exports()
    {
        var history = Build(Create(), 5);
        byte[] expected = ExpectedV1(history);
        byte[] first = WorldResourceHistoryCodec.Encode(history);
        byte[] second = WorldResourceHistoryCodecV1.Encode(history);

        Assert.NotSame(first, second);
        first.AsSpan().Clear();
        second.AsSpan().Fill(0xff);

        Assert.Equal(expected, WorldResourceHistoryCodec.Encode(history));
        Assert.Equal(expected, WorldResourceHistoryCodecV1.Encode(history));
        Assert.Equal(ContentHash.Compute(expected), history.ContentHash);
        Assert.Equal(expected, WorldResourceHistoryCodec.Encode(WorldResourceHistoryCodec.Decode(expected)));
    }

    [Fact]
    public void Caller_lists_changed_before_the_first_encode_cannot_alter_a_cold_history()
    {
        var basis = Create();
        List<ResourceChangeBatch> batches = [.. Batches(basis, 5)];
        List<WorldResourceEvidence> evidence = [.. Evidence(basis)];
        var history = new WorldResourceHistory(Admission, new ActorResourceHistory(basis, 0, batches), evidence);

        batches.Clear();
        evidence.Clear();

        // The expectation comes from a separately constructed equivalent, leaving this history cold.
        Assert.Equal(ExpectedV1(Build(basis, 5)), WorldResourceHistoryCodec.Encode(history));
        Assert.Equal(3, history.Evidence.Count);
        Assert.Equal(3, history.History.Batches.Count);
    }

    [Fact]
    public void Equal_identities_with_different_content_never_share_bytes()
    {
        var basis = Create();
        var first = Build(basis, 5);
        var other = Build(basis, 7);
        var equivalent = Build(basis, 5);

        Assert.Equal(first.History.Batches.Select(batch => batch.CommandId), other.History.Batches.Select(batch => batch.CommandId));
        Assert.Equal(first.Evidence.Select(item => item.CommandId), other.Evidence.Select(item => item.CommandId));
        byte[] firstBytes = WorldResourceHistoryCodec.Encode(first);
        byte[] otherBytes = WorldResourceHistoryCodec.Encode(other);

        Assert.False(firstBytes.AsSpan().SequenceEqual(otherBytes));
        Assert.Equal(ExpectedV1(other), otherBytes);
        Assert.Equal(53, WorldResourceHistoryCodec.Decode(otherBytes).History.Batches[0].Changes[0].After.CurrentHp);
        Assert.Equal(55, WorldResourceHistoryCodec.Decode(firstBytes).History.Batches[0].Changes[0].After.CurrentHp);
        // Equal content from distinct objects encodes equally; nothing is shared by identity.
        Assert.Equal(firstBytes, WorldResourceHistoryCodec.Encode(equivalent));
        Assert.Equal(firstBytes, WorldResourceHistoryCodec.Encode(first));
    }

    [Fact]
    public void A_successor_sharing_batches_and_evidence_leaves_the_earlier_frame_exact()
    {
        var basis = Create();
        var earlier = Build(basis, 5);
        byte[] original = WorldResourceHistoryCodec.Encode(earlier);
        var last = earlier.History.Batches[^1];
        var extra = new ResourceChangeBatch(4, 0, Id(1_003),
            [ActorResourceChange.FromTransition(0, Cause(), last.Changes[0].After.TakeDamage(2))]);
        var successor = new WorldResourceHistory(Admission, earlier.History.Append(extra),
            [.. earlier.Evidence, new WorldRewardResourceEvidence(Id(1_003))]);

        Assert.Same(earlier.Evidence[0], successor.Evidence[0]);
        Assert.Same(earlier.History.Batches[0], successor.History.Batches[0]);
        Assert.Equal(ExpectedV1(successor), WorldResourceHistoryCodec.Encode(successor));
        Assert.Equal(original, WorldResourceHistoryCodec.Encode(earlier));
        Assert.Equal(ExpectedV1(earlier), original);
        Assert.Equal(4, WorldResourceHistoryCodec.Decode(WorldResourceHistoryCodec.Encode(successor)).History.Batches.Count);
    }

    [Fact]
    public void Concurrent_cold_callers_observe_one_complete_frame()
    {
        var basis = Create();
        // Exercises concurrent first callers; no particular compare-exchange interleaving is assumed.
        byte[] expected = ExpectedV1(Build(basis, 5));
        var shared = Build(basis, 5);
        Parallel.For(0, 96, index =>
        {
            byte[] exported = (index % 3) switch
            {
                0 => WorldResourceHistoryCodec.Encode(shared),
                1 => WorldResourceHistoryCodecV1.Encode(shared),
                _ => ExpectedFrames.Copy(expected, shared.ContentHash)
            };
            Assert.Equal(expected, exported);
            exported.AsSpan().Clear();
        });
        Assert.Equal(expected, WorldResourceHistoryCodec.Encode(shared));
    }

    internal static byte[] ExpectedV1(WorldResourceHistory value) => ExpectedFrames.Frame("world.resource-history",
        ExpectedFrames.Number(1), value.AnchorAdmission.ToByteArray(), ExpectedFrames.Number(value.History.BasisWorldSequence),
        ProgressionCodecV1.Encode(value.History.Basis), ActorResourceHistoryCodecV1.Encode(value.History),
        ExpectedFrames.Frame("world.resource-evidence", value.Evidence.Select(ExpectedEvidence).ToArray()));

    private static byte[] ExpectedEvidence(WorldResourceEvidence evidence) => evidence switch
    {
        WorldRewardResourceEvidence reward => ExpectedFrames.Frame("world.resource-reward.v1", reward.CommandId.ToByteArray()),
        WorldTreatmentResourceEvidence treatment => ExpectedFrames.Frame("world.resource-treatment.v1",
            treatment.CommandId.ToByteArray(), treatment.TreatmentId.ToByteArray()),
        WorldRestorativeUseResourceEvidence use => ExpectedFrames.Frame("world.resource-restorative-use.v1",
            WorldActionCodec.Encode(use.Request), use.UseId.ToByteArray()),
        _ => throw new ArgumentException("This oracle covers only the simple cause kinds.")
    };

    private static WorldResourceHistory Build(ProgressionSnapshot basis, uint damage) =>
        new(Admission, new ActorResourceHistory(basis, 0, Batches(basis, damage)), Evidence(basis));

    private static IEnumerable<ResourceChangeBatch> Batches(ProgressionSnapshot basis, uint damage)
    {
        var damaged = basis.GetActor(Id(10)).TakeDamage(damage);
        var restored = damaged.Candidate.RestoreHealth(3);
        var reward = restored.Candidate.TakeDamage(1);
        yield return new(1, 0, Id(1_000), [ActorResourceChange.FromTransition(0, Cause(), damaged)]);
        yield return new(2, 1, Id(1_001), [ActorResourceChange.FromTransition(0, Cause(), restored)]);
        yield return new(3, 0, Id(1_002), [ActorResourceChange.FromTransition(0, Cause(), reward)]);
    }

    private static IEnumerable<WorldResourceEvidence> Evidence(ProgressionSnapshot basis)
    {
        yield return new WorldRewardResourceEvidence(Id(1_000));
        yield return new WorldTreatmentResourceEvidence(Id(1_001), Id(1_101));
        var request = new WorldActionCommand(Id(1_002), Id(20), Id(30), Id(10), basis.WorldId, basis.BranchId, 7,
            new UseRestorativeItemIntent(Id(10), Id(300), ContentHash.Compute("ordinary delivery pin"u8), null, null, null,
                4, 0, 5, 6, new(IngredientLotKind.Source, Id(400), 2, 1)));
        yield return new WorldRestorativeUseResourceEvidence(request, EntityId.DeriveSpawned(request.CommandId, 1));
    }
}

/// <summary>Independent writer of the documented canonical frame layout; it shares no production encoder.</summary>
internal static class ExpectedFrames
{
    internal static byte[] Frame(string tag, params byte[][] fields)
    {
        List<byte> output = [.. "OFC\0"u8.ToArray(), .. Unsigned32(1), .. Unsigned32(checked((uint)fields.Length + 1))];
        AppendField(output, Encoding.UTF8.GetBytes(tag));
        foreach (byte[] field in fields)
        {
            AppendField(output, field);
        }
        return [.. output];
    }

    internal static byte[] Number(ulong value)
    {
        byte[] bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    /// <summary>Returns a fresh copy only when the digest matches, so a concurrent reader can compare it.</summary>
    internal static byte[] Copy(byte[] expected, ContentHash observed) =>
        observed == ContentHash.Compute(expected) ? [.. expected] : [];

    private static void AppendField(List<byte> output, byte[] field)
    {
        output.AddRange(Unsigned32(checked((uint)field.Length)));
        output.AddRange(field);
    }

    private static byte[] Unsigned32(uint value)
    {
        byte[] bytes = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }
}
