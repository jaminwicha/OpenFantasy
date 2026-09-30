using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.ProposalMigration;

/// <summary>The closed proposal-transition wire contract: exact profiles, fields and canonical bytes.</summary>
public sealed class ProposalTransitionCodecTests
{
    private static readonly EntityId Cause = new(1, 1);
    private static readonly EntityId World = new(2, 2);
    private static readonly EntityId Branch = new(3, 3);
    private static readonly ContentHash Head = ContentHash.Compute([1]);
    private static readonly ContentHash Admission = ContentHash.Compute([2]);
    private static readonly ContentHash Anchor = ContentHash.Compute([3]);

    private static WorldProposalTransition Request(ContentHash? anchor = null, ulong ordinal = 3) =>
        new(Cause, World, Branch, 1579, 808, Head, Admission, anchor ?? Anchor, ordinal);

    [Fact]
    public void Request_round_trips_its_exact_canonical_bytes_and_digest()
    {
        WorldProposalTransition request = Request();
        byte[] bytes = WorldProposalTransitionCodecV1.Encode(request);
        WorldProposalTransition decoded = WorldProposalTransitionCodecV1.Decode(bytes);
        Assert.Equal(bytes, WorldProposalTransitionCodecV1.Encode(decoded));
        Assert.Equal(request.ContentHash, decoded.ContentHash);
        Assert.Equal((Cause, World, Branch, 1579UL, 808UL, Head, Admission, Anchor, 3UL),
            (decoded.CauseId, decoded.WorldId, decoded.BranchId, decoded.ExpectedRevision, decoded.ExpectedAdvancingSteps,
                decoded.ExpectedHeadHash, decoded.SourceAdmissionVersion, decoded.ExpectedAnchorHash!, decoded.TransitionOrdinal));
        var neverCut = new WorldProposalTransition(Cause, World, Branch, 4, 2, Head, Admission, null, 0);
        Assert.Null(WorldProposalTransitionCodecV1.Decode(WorldProposalTransitionCodecV1.Encode(neverCut)).ExpectedAnchorHash);
        Assert.NotEqual(request.ContentHash, neverCut.ContentHash);
    }

    [Theory]
    [InlineData(27UL, 28UL)]
    [InlineData(27UL, 29UL)]
    [InlineData(28UL, 30UL)]
    [InlineData(26UL, 30UL)]
    [InlineData(27UL, 31UL)]
    public void Only_the_named_27_to_30_interpretation_pair_decodes(ulong source, ulong target)
    {
        byte[] bytes = WorldProposalTransitionCodecV1.Encode(Request());
        int offset = FieldOffset(bytes, 2);
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(offset), source);
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(FieldOffset(bytes, 3)), target);
        Assert.Throws<FormatException>(() => WorldProposalTransitionCodecV1.Decode(bytes));
    }

    [Fact]
    public void Tag_version_digest_length_trailing_and_noncanonical_forgeries_fail()
    {
        byte[] valid = WorldProposalTransitionCodecV1.Encode(Request());
        byte[] tag = (byte[])valid.Clone();
        tag[16] = (byte)'x';
        Assert.Throws<FormatException>(() => WorldProposalTransitionCodecV1.Decode(tag));
        byte[] version = (byte[])valid.Clone();
        BinaryPrimitives.WriteUInt64BigEndian(version.AsSpan(FieldOffset(version, 1)), 2);
        Assert.Throws<FormatException>(() => WorldProposalTransitionCodecV1.Decode(version));
        Assert.Throws<FormatException>(() => WorldProposalTransitionCodecV1.Decode([.. valid, 0]));
        Assert.Throws<FormatException>(() => WorldProposalTransitionCodecV1.Decode(valid.AsSpan(0, valid.Length - 1)));
        // An anchor field of the wrong length, or an ordinal that disagrees with anchor presence.
        byte[] Forge(byte[] anchor) => Frame(System.Text.Encoding.UTF8.GetBytes("world.proposal-transition"), U(1), U(27), U(30),
            Cause.ToByteArray(), World.ToByteArray(), Branch.ToByteArray(), U(1579), U(808), Head.ToByteArray(),
            Admission.ToByteArray(), anchor, U(3));
        // The local frame writer reproduces the valid bytes exactly before any field is forged.
        Assert.Equal(valid, Forge(Anchor.ToByteArray()));
        Assert.Throws<FormatException>(() => WorldProposalTransitionCodecV1.Decode(Forge(new byte[31])));
        Assert.Throws<FormatException>(() => WorldProposalTransitionCodecV1.Decode(Forge([])));
    }

    [Fact]
    public void Constructor_refuses_scope_aliases_time_inversion_and_ordinal_mismatch()
    {
        Assert.Throws<ArgumentException>(() => new WorldProposalTransition(Cause, World, World, 5, 1, Head, Admission, Anchor, 1));
        Assert.Throws<ArgumentException>(() => new WorldProposalTransition(World, World, Branch, 5, 1, Head, Admission, Anchor, 1));
        Assert.Throws<ArgumentException>(() => new WorldProposalTransition(Cause, World, Branch, 5, 6, Head, Admission, Anchor, 1));
        Assert.Throws<ArgumentException>(() => new WorldProposalTransition(Cause, World, Branch, 5, 1, Head, Admission, Anchor, 0));
        Assert.Throws<ArgumentException>(() => new WorldProposalTransition(Cause, World, Branch, 5, 1, Head, Admission, null, 1));
        Assert.Throws<ArgumentException>(() => new WorldProposalTransition(Cause, World, Branch, ulong.MaxValue, 1, Head, Admission, Anchor, 1));
    }

    private static byte[] U(ulong value)
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    // OFC frame: magic, format version 1, field count, then big-endian length-prefixed fields.
    private static byte[] Frame(params byte[][] fields)
    {
        var bytes = new List<byte>("OFC\0"u8.ToArray());
        byte[] word = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(word, 1);
        bytes.AddRange(word);
        BinaryPrimitives.WriteUInt32BigEndian(word, (uint)fields.Length);
        bytes.AddRange(word);
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(word, (uint)field.Length);
            bytes.AddRange(word);
            bytes.AddRange(field);
        }
        return bytes.ToArray();
    }

    // Frame header: magic, version and field count (12 bytes), then length-prefixed fields; field 0 is the tag.
    private static int FieldOffset(byte[] bytes, int field)
    {
        int offset = 12;
        for (int index = 0; index < field; index++)
        {
            offset += 4 + (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset));
        }
        return offset + 4;
    }
}
