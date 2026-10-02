using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.IO.Compression;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.Networking.PlayerObservationViews;

public sealed class PlayerViewCompressionTests
{
    private static readonly Lazy<LocalWorldView> LargeView = new(CreateLargeView);
    private static readonly Lazy<byte[]> LargeEncoded = new(() => PlayerViewCodec.Encode(LargeView.Value));

    [Fact]
    public void Large_typed_view_uses_bounded_compression_and_preserves_every_prepared_lot()
    {
        byte[] encoded = LargeEncoded.Value;
        Assert.True(encoded.AsSpan().StartsWith("OFVB"u8));
        Assert.InRange(encoded.Length, ObservationCompression.HeaderBytes + 1, PlayerViewCodec.MaximumBodyBytes);
        uint expandedLength = BinaryPrimitives.ReadUInt32BigEndian(encoded.AsSpan(8));
        Assert.InRange(expandedLength, (uint)PlayerViewCodec.MaximumBodyBytes + 1, (uint)PlayerViewCodec.MaximumExpandedJsonBytes);
        LocalWorldView decoded = PlayerViewCodec.Decode(encoded);
        Assert.Equal(4096, decoded.PlayerInventory.Lots.Count);
        Assert.Equal(4096, decoded.MaterialProposals!.Preparation!.Lots.Count);
        LocalMaterialProposalLot last = decoded.MaterialProposals.Preparation.Lots[^1];
        Assert.Equal(LargeView.Value.MaterialProposals!.Preparation!.Prepare(last.Kind, last.LotId, Id(11), 1),
            decoded.MaterialProposals.Preparation.Prepare(last.Kind, last.LotId, Id(11), 1));
    }

    [Fact]
    public void Valid_brotli_spelling_need_not_equal_the_local_encoders_bytes()
    {
        byte[] canonicalJson = ObservationCompression.Decode(LargeEncoded.Value);
        byte[] alternative = Wrap(canonicalJson, quality: 0);
        Assert.False(alternative.SequenceEqual(LargeEncoded.Value));
        LocalWorldView decoded = PlayerViewCodec.Decode(alternative);
        Assert.Equal(LargeEncoded.Value, PlayerViewCodec.Encode(decoded));
    }

    [Fact]
    public void Small_observations_keep_plain_canonical_json_and_reject_a_compressed_wrapper()
    {
        LocalWorldView view = new(Genesis(), Id(10));
        byte[] plain = PlayerViewCodec.Encode(view);
        Assert.Equal((byte)'{', plain[0]);
        Assert.False(ObservationCompression.IsCompressed(plain));
        Assert.Equal(plain, PlayerViewCodec.Encode(PlayerViewCodec.Decode(plain)));
        Assert.Throws<FormatException>(() => PlayerViewCodec.Decode(Wrap(plain)));
    }

    [Theory]
    [InlineData("short-header")]
    [InlineData("version")]
    [InlineData("reserved")]
    [InlineData("zero-length")]
    [InlineData("small-length")]
    [InlineData("oversized-length")]
    [InlineData("uint-max-length")]
    [InlineData("too-short-length")]
    [InlineData("too-long-length")]
    [InlineData("truncated")]
    [InlineData("trailing")]
    [InlineData("concatenated")]
    [InlineData("malformed-brotli")]
    [InlineData("malformed-json")]
    [InlineData("noncanonical-json")]
    public void Malformed_compressed_bodies_are_refused(string mutation)
    {
        byte[] encoded = LargeEncoded.Value.ToArray();
        uint declared = BinaryPrimitives.ReadUInt32BigEndian(encoded.AsSpan(8));
        switch (mutation)
        {
            case "short-header": encoded = "OFVB"u8.ToArray(); break;
            case "version": BinaryPrimitives.WriteUInt16BigEndian(encoded.AsSpan(4), 99); break;
            case "reserved": BinaryPrimitives.WriteUInt16BigEndian(encoded.AsSpan(6), 1); break;
            case "zero-length": BinaryPrimitives.WriteUInt32BigEndian(encoded.AsSpan(8), 0); break;
            case "small-length": BinaryPrimitives.WriteUInt32BigEndian(encoded.AsSpan(8), (uint)PlayerViewCodec.MaximumBodyBytes); break;
            case "oversized-length": BinaryPrimitives.WriteUInt32BigEndian(encoded.AsSpan(8), (uint)PlayerViewCodec.MaximumExpandedJsonBytes + 1); break;
            case "uint-max-length": BinaryPrimitives.WriteUInt32BigEndian(encoded.AsSpan(8), uint.MaxValue); break;
            case "too-short-length": BinaryPrimitives.WriteUInt32BigEndian(encoded.AsSpan(8), declared - 1); break;
            case "too-long-length": BinaryPrimitives.WriteUInt32BigEndian(encoded.AsSpan(8), declared + 1); break;
            case "truncated": encoded = encoded[..^1]; break;
            case "trailing": encoded = encoded.Concat(new byte[] { 0 }).ToArray(); break;
            case "concatenated": encoded = encoded.Concat(encoded.Skip(ObservationCompression.HeaderBytes)).ToArray(); break;
            case "malformed-brotli": encoded.AsSpan(ObservationCompression.HeaderBytes).Fill(0xff); break;
            case "malformed-json":
                byte[] invalidJson = ObservationCompression.Decode(encoded);
                invalidJson[0] = (byte)'!';
                encoded = Wrap(invalidJson);
                break;
            case "noncanonical-json":
                byte[] trailingSpace = ObservationCompression.Decode(encoded).Concat(new byte[] { (byte)' ' }).ToArray();
                encoded = Wrap(trailingSpace);
                break;
        }
        Assert.Throws<FormatException>(() => PlayerViewCodec.Decode(encoded));
    }

    [Fact]
    public void Expansion_beyond_the_declared_buffer_is_refused_without_decoder_growth()
    {
        byte[] bomb = new byte[2 * PlayerViewCodec.MaximumBodyBytes];
        Array.Fill(bomb, (byte)' ');
        byte[] encoded = Wrap(bomb);
        BinaryPrimitives.WriteUInt32BigEndian(encoded.AsSpan(8), (uint)PlayerViewCodec.MaximumBodyBytes + 1);
        Assert.Throws<FormatException>(() => PlayerViewCodec.Decode(encoded));
    }

    [Fact]
    public void Compression_that_cannot_fit_the_wire_cap_is_refused_without_larger_output_allocation()
    {
        byte[] noise = new byte[PlayerViewCodec.MaximumBodyBytes + 4096];
        new Random(761).NextBytes(noise);
        Assert.Throws<FormatException>(() => ObservationCompression.Encode(noise));
    }

    [Fact]
    public void Json_writer_refuses_output_beyond_the_expanded_limit_during_serialization()
    {
        LocalWorldView basis = new(Genesis(), Id(10));
        ContentHash hash = ContentHash.Compute("expanded-json-limit"u8);
        string description = new('x', 16_000);
        LocalQuestView[] quests = Enumerable.Range(0, 1100).Select(index => new LocalQuestView(Id((ulong)index + 30_000),
            hash, "A quest", description, Id(11), Id(80), 1, 0, null, 0, 1, 0, 0, "Coins", false, false)).ToArray();
        LocalWorldView oversized = (WorldViewData.From(basis) with { Quests = Array.AsReadOnly(quests) }).ToView();
        Assert.Throws<FormatException>(() => PlayerViewCodec.Encode(oversized));
    }

    [Fact]
    public void Bounded_writer_can_commit_exactly_the_limit_but_not_one_more_byte()
    {
        using var buffer = new BoundedObservationJsonBuffer();
        buffer.GetSpan(PlayerViewCodec.MaximumExpandedJsonBytes)[..PlayerViewCodec.MaximumExpandedJsonBytes].Clear();
        buffer.Advance(PlayerViewCodec.MaximumExpandedJsonBytes);
        Assert.Equal(PlayerViewCodec.MaximumExpandedJsonBytes, buffer.WrittenSpan.Length);
        buffer.GetSpan(1)[0] = 0;
        Assert.Throws<FormatException>(() => buffer.Advance(1));
        Assert.Throws<FormatException>(() => buffer.GetMemory(BoundedObservationJsonBuffer.MaximumReservationBytes + 1));
    }

    // This deliberately permits invalid lengths and alternate quality for decoder-boundary tests.
    private static byte[] Wrap(ReadOnlySpan<byte> json, int quality = ObservationCompression.Quality)
    {
        byte[] encoded = new byte[PlayerViewCodec.MaximumBodyBytes];
        bool success = BrotliEncoder.TryCompress(json, encoded.AsSpan(ObservationCompression.HeaderBytes),
            out int written, quality, ObservationCompression.Window);
        Assert.True(success);
        "OFVB"u8.CopyTo(encoded);
        BinaryPrimitives.WriteUInt16BigEndian(encoded.AsSpan(4), ObservationCompression.Version);
        BinaryPrimitives.WriteUInt32BigEndian(encoded.AsSpan(8), (uint)json.Length);
        return encoded.AsSpan(0, ObservationCompression.HeaderBytes + written).ToArray();
    }

    private static LocalWorldView CreateLargeView()
    {
        LocalWorldView basis = new(Genesis(), Id(10));
        var definition = new CountedItemDefinition(Id(80), ContentHash.Compute("large-observation-values"u8));
        LocalItemLotView[] lots = Enumerable.Range(0, 4096)
            .Select(index => new LocalItemLotView(Id((ulong)index + 10_000), definition.DefinitionId, 0, 1)).ToArray();
        var preparation = new LocalMaterialProposalPreparation(basis.WorldId, basis.BranchId, basis.PlayerActorId,
            basis.PlayerInventory.CustodianId, new Dictionary<EntityId, EntityId> { [Id(11)] = Id(61) },
            lots.Select(lot => new LocalMaterialProposalLot(ConsentedMaterialLotKind.CountedSource, lot.LotId,
                definition, lot.Revision, lot.Quantity)), 1300);
        return (WorldViewData.From(basis) with
        {
            PlayerInventory = new LocalInventoryView(basis.PlayerInventory.CustodianId, 0, lots),
            MaterialProposals = new LocalMaterialProposalView(Array.AsReadOnly(Array.Empty<MaterialProposalView>()), preparation)
        }).ToView();
    }
}
