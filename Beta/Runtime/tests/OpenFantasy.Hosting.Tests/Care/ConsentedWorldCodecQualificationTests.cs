using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Hosting.Tests.Care.WorldResourceJourneyTests;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>
/// Codec and archive qualification against the actual in-memory complete-world fixture graph.
/// Independent framing comes from the standalone generator; owner payloads are opaque imports.
/// Structural admission is asserted separately from historical authentication, which no decoder performs.
/// </summary>
public sealed partial class ConsentedWorldPublicationTests
{
    private const int ProductionField = 12;
    private const int LifecycleField = 21;
    private const int CoordinationField = 22;
    private const string QualificationJointRefusal = "The coordinated world requires its exact joint decode context.";
    private const string QualificationHashRefusal = "The coordinated archive does not match its requested closing-boundary hash.";
    private const string QualificationReferenceRefusal = "The coordinated archive does not match its exact reference and canonical closing boundary.";
    private const string QualificationSegmentRefusal = "Consent evidence must follow its boundary with exact admitted participants and scope.";
    private const string QualificationVersionRefusal = "The contract version is unsupported.";

    private static readonly JsonElement QualificationFixture = JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "consented-world-formats-v1.json"))).RootElement;

    [Fact]
    public void Independent_framing_composes_the_actual_root_binding_anchor_and_continued_production()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis));
        Step(session, fixture.Journey);
        WorldSnapshot closing = session.ReadStateForHost();
        byte[] closingBytes = WorldSnapshotCodec.Encode(closing);
        byte[][] closingRoot = QualificationAssertEnvelope(closingBytes);
        byte[][] closingJoint = ReadFrame(closingRoot[CoordinationField]);
        Assert.Equal(5, closingJoint.Length);
        Assert.Empty(closingJoint[3]);
        Assert.Equal(QualificationVector("historyEmpty"), closingJoint[4]);

        WorldMaterialCareArchiveSegment first = CutAndDecode(session, null, out WorldSnapshot reopened);
        Assert.Equal(closingBytes, first.Encode());
        WorldMaterialCareArchiveReference reference = first.Reference;
        Assert.Equal(QualificationSha(closingBytes), reference.BoundaryHash.Value);
        byte[] referenceBytes = QualificationCompose("archiveReference", new()
        {
            ["worldId"] = reference.WorldId.ToByteArray(), ["branchId"] = reference.BranchId.ToByteArray(),
            ["ordinal"] = Number(reference.Ordinal), ["boundarySequence"] = Number(reference.BoundarySequence),
            ["boundaryHash"] = reference.BoundaryHash.ToByteArray()
        });
        WorldMaterialCareArchiveAnchor anchor = first.Anchor;
        byte[] anchorBytes = QualificationCompose("archiveAnchor", new()
        {
            ["reference"] = referenceBytes, ["admissionVersion"] = anchor.AdmissionVersion.ToByteArray(),
            ["latestCareSequence"] = Number(anchor.LatestCareSequence),
            ["productionContext"] = anchor.Production.ContentHash.ToByteArray(),
            ["careContext"] = anchor.Care.ContentHash.ToByteArray(),
            ["progression"] = anchor.Progression.ContentHash.ToByteArray(),
            ["quests"] = closing.Quests!.ContentHash.ToByteArray(),
            ["lifecycle"] = SHA256.HashData(closingRoot[LifecycleField]),
            ["sourceBaseline"] = anchor.SourceBaselineHash.ToByteArray()
        });
        Assert.Equal(anchor.ContentHash.Value, QualificationSha(anchorBytes));
        byte[] binding = QualificationCompose("archiveBinding", new()
        {
            ["reference"] = referenceBytes, ["anchorHash"] = anchor.ContentHash.ToByteArray()
        });
        Assert.True(binding.Length <= 512);

        byte[] reopenedBytes = WorldSnapshotCodec.Encode(reopened);
        byte[][] root = QualificationAssertEnvelope(reopenedBytes);
        byte[][] joint = ReadFrame(root[CoordinationField]);
        Assert.Equal(binding, joint[3]);
        Assert.Equal(root[CoordinationField], QualificationCompose("coordinationV28", new()
        {
            ["care"] = joint[1], ["sources"] = joint[2], ["binding"] = binding,
            ["history"] = QualificationVector("historyEmpty")
        }));

        ProductionSnapshot production = reopened.Production!;
        byte[][] continued = ReadFrame(root[ProductionField]);
        Assert.Equal(11, continued.Length);
        Assert.Equal(root[ProductionField], QualificationCompose("productionV11", new()
        {
            ["worldId"] = production.WorldId.ToByteArray(), ["branchId"] = production.BranchId.ToByteArray(),
            ["revision"] = Number(production.Revision),
            ["contextHash"] = anchor.Production.ContentHash.ToByteArray(),
            ["totalBatches"] = Number(production.TotalBatchCount),
            ["totalConsumed"] = Number(production.TotalConsumedItemCount),
            ["batches"] = continued[8], ["lots"] = continued[9], ["consumed"] = continued[10]
        }));
        Assert.Equal(root[ProductionField], ProductionCodecV11.Encode(production));
        Assert.True(anchor.Production.SupportsConsentedWorld);
    }

    [Fact]
    public void Archive_bytes_of_both_cuts_refuse_wrong_ordinal_anchor_and_flipped_bytes()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis));
        Step(session, fixture.Journey);
        WorldMaterialCareArchiveSegment first = CutAndDecode(session, null, out WorldSnapshot reopened);
        session = new(reopened, session.OccupiedIdentities);
        Step(session, fixture.Journey);
        WorldMaterialCareArchiveSegment second = CutAndDecode(session, first.Anchor, out _);
        var coordinated = new CareWorldSession(WorldCoordinatedAdmission.CreateFresh(fixture.Genesis));
        Step(coordinated, fixture.Journey);
        WorldMaterialCareArchiveSegment other = CutAndDecode(coordinated, null, out _);
        Assert.Equal((0UL, 1UL), (first.Reference.Ordinal, second.Reference.Ordinal));

        foreach ((WorldMaterialCareArchiveSegment segment, WorldMaterialCareArchiveAnchor? previous) in new[]
            { (first, (WorldMaterialCareArchiveAnchor?)null), (second, first.Anchor) })
        {
            byte[] bytes = segment.Encode();
            WorldMaterialCareArchiveReference reference = segment.Reference;
            // Valid control: the exact bytes, reference and previous anchor reopen the same anchor.
            Assert.Equal(segment.Anchor.ContentHash,
                WorldMaterialCareArchiveSegment.Decode(reference, bytes, previous).Anchor.ContentHash);
            WorldMaterialCareArchiveReference[] wrong =
            [
                new(reference.WorldId, reference.BranchId, reference.Ordinal + 1, reference.BoundarySequence, reference.BoundaryHash),
                new(reference.WorldId, reference.BranchId, reference.Ordinal, reference.BoundarySequence + 1, reference.BoundaryHash),
                new(reference.BranchId, reference.WorldId, reference.Ordinal, reference.BoundarySequence, reference.BoundaryHash)
            ];
            Assert.All(wrong, expected => Assert.Equal(QualificationReferenceRefusal, Assert.Throws<FormatException>(
                () => WorldMaterialCareArchiveSegment.Decode(expected, bytes, previous)).Message));
            foreach (int index in new[] { 0, bytes.Length / 2, bytes.Length - 1 })
            {
                byte[] flipped = (byte[])bytes.Clone();
                flipped[index] ^= 1;
                Assert.Equal(QualificationHashRefusal, Assert.Throws<FormatException>(
                    () => WorldMaterialCareArchiveSegment.Decode(reference, flipped, previous)).Message);
            }
            Assert.Equal(QualificationHashRefusal, Assert.Throws<FormatException>(
                () => WorldMaterialCareArchiveSegment.Decode(reference, bytes[..^1], previous)).Message);
        }

        // The first root accepts no anchor; the second requires exactly the first segment's anchor.
        foreach (WorldMaterialCareArchiveAnchor supplied in new[] { first.Anchor, second.Anchor, other.Anchor })
        {
            Assert.Equal(QualificationJointRefusal, Assert.Throws<FormatException>(
                () => WorldMaterialCareArchiveSegment.Decode(first.Reference, first.Encode(), supplied)).Message);
        }
        foreach (WorldMaterialCareArchiveAnchor? supplied in new[] { null, second.Anchor, other.Anchor })
        {
            Assert.Equal(QualificationJointRefusal, Assert.Throws<FormatException>(
                () => WorldMaterialCareArchiveSegment.Decode(second.Reference, second.Encode(), supplied)).Message);
        }
    }

    [Fact]
    public void Evidence_sequence_outside_its_segment_is_refused_after_a_decoded_cut()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis));
        fixture.Journey.Approach(session);
        // A closing boundary needs a positive revision; approaching alone may publish nothing.
        Step(session, fixture.Journey);
        WorldMaterialCareArchiveSegment first = CutAndDecode(session, null, out WorldSnapshot reopened);
        session = new(reopened, session.OccupiedIdentities);
        WorldSnapshot before = session.ReadStateForHost();
        var pair = Pair(before, fixture.Journey, RawTerms(before, fixture.Helper, fixture.Player));
        WorldSnapshot gifted = CareRewardJourney.Commit(session, fixture.Journey.Batch(before,
            WorldBatchKind.AdvancingStep, [pair.Offer, pair.Acceptance])).Candidate;
        byte[] original = WorldSnapshotCodec.Encode(gifted);
        ulong boundary = first.Anchor.ClosingWorldSequence;
        Assert.Equal(gifted.Revision, Assert.Single(gifted.Coordination!.ConsentedMaterial!.Transfers).WorldSequence);
        Assert.True(boundary < gifted.Revision);

        foreach (ulong sequence in new[] { boundary, gifted.Revision + 1, ulong.MaxValue })
        {
            byte[] mutated = QualificationRewriteEvidence(original, record => record[1] = Number(sequence));
            FormatException refusal = Assert.Throws<FormatException>(
                () => WorldSnapshotCodec.DecodeCoordinated(mutated, first.Anchor));
            Assert.Equal(QualificationSegmentRefusal, Assert.IsType<ArgumentException>(refusal.InnerException).Message);
        }
        Assert.Equal(original, WorldSnapshotCodec.Encode(WorldSnapshotCodec.DecodeCoordinated(original, first.Anchor)));
    }

    [Fact]
    public void Profile_and_production_capability_mixes_are_refused_and_the_precut_reframe_is_only_structure()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var consented = new CareWorldSession(WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis));
        var coordinated = new CareWorldSession(WorldCoordinatedAdmission.CreateFresh(fixture.Genesis));
        Step(consented, fixture.Journey);
        Step(coordinated, fixture.Journey);
        byte[] root28 = WorldSnapshotCodec.Encode(consented.ReadStateForHost());
        byte[] root27 = WorldSnapshotCodec.Encode(coordinated.ReadStateForHost());

        byte[][] relabelled = ReadFrame(root28);
        relabelled[1] = Number(27);
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(WriteFrame(relabelled)));
        relabelled = ReadFrame(root27);
        relabelled[1] = Number(28);
        Assert.Equal("Unexpected world frame tag.",
            Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(WriteFrame(relabelled))).Message);

        // Deliberate boundary: re-framed pre-cut bytes are admitted as structure. No decoder can
        // recompute the fresh admission or authenticate lived history; the Host must pin genesis
        // and interpretation. This is neither a migration nor an authorized upgrade.
        byte[][] oldJoint = ReadFrame(relabelled[CoordinationField]);
        Assert.Equal(4, oldJoint.Length);
        relabelled[CoordinationField] = QualificationCompose("coordinationV28", new()
        {
            ["care"] = oldJoint[1], ["sources"] = oldJoint[2], ["binding"] = oldJoint[3],
            ["history"] = QualificationVector("historyEmpty")
        });
        WorldSnapshot reframed = WorldSnapshotCodec.Decode(WriteFrame(relabelled));
        Assert.Equal((ushort)28, reframed.ProfileVersion);
        Assert.Empty(reframed.Coordination!.ConsentedMaterial!.Transfers);
        Assert.Equal(coordinated.ReadStateForHost().AdmissionVersion, reframed.AdmissionVersion);
        Assert.NotEqual(consented.ReadStateForHost().AdmissionVersion, reframed.AdmissionVersion);

        WorldMaterialCareArchiveSegment first = CutAndDecode(consented, null, out WorldSnapshot reopened28);
        WorldMaterialCareArchiveSegment old = CutAndDecode(coordinated, null, out WorldSnapshot reopened27);
        byte[] cut28 = WorldSnapshotCodec.Encode(reopened28);
        byte[] cut27 = WorldSnapshotCodec.Encode(reopened27);
        byte[] production11 = ProductionCodecV11.Encode(reopened28.Production!);
        byte[] production9 = ProductionCodecV9.Encode(reopened27.Production!);
        Assert.Equal(production11, ReadFrame(cut28)[ProductionField]);
        Assert.Equal(production9, ReadFrame(cut27)[ProductionField]);

        byte[][] mixed = ReadFrame(cut28);
        mixed[ProductionField] = production9;
        Assert.Equal(QualificationVersionRefusal, Assert.Throws<ProductionFormatException>(
            () => WorldSnapshotCodec.DecodeCoordinated(WriteFrame(mixed), first.Anchor)).Message);
        mixed = ReadFrame(cut27);
        mixed[ProductionField] = production11;
        Assert.Equal(QualificationVersionRefusal, Assert.Throws<ProductionFormatException>(
            () => WorldSnapshotCodec.DecodeCoordinated(WriteFrame(mixed), old.Anchor)).Message);

        // Relabelling the continued frame alone never crosses interpretations: the context differs.
        byte[][] relabelled11 = ReadFrame(production11);
        relabelled11[1] = Number(9);
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV9.Decode(WriteFrame(relabelled11), old.Anchor.Production));
        byte[][] relabelled9 = ReadFrame(production9);
        relabelled9[1] = Number(11);
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV11.Decode(WriteFrame(relabelled9), first.Anchor.Production));

        // The ordered V10 consent decoder refuses both complete-world contexts and their frames.
        foreach ((byte[] bytes, ProductionContinuationContext context) in new[]
            { (production11, first.Anchor.Production), (production9, old.Anchor.Production) })
        {
            ProductionFormatException refusal = Assert.Throws<ProductionFormatException>(
                () => ProductionCodecV10.Decode(bytes, context, []));
            Assert.Equal(ProductionFormatFailure.InvalidValue, refusal.Failure);
            Assert.Equal("Continued production violates its closing context or lineage invariants: "
                + "Material decoding requires its exact context and bounded actual evidence.", refusal.Message);
            Assert.Throws<ArgumentException>(() => ProductionCodecV10.Encode(
                bytes == production11 ? reopened28.Production! : reopened27.Production!));
        }

        // A pre-cut root carrying a binding is refused without its anchor and is not a cut root with it.
        byte[][] bound = ReadFrame(root28);
        byte[][] boundJoint = ReadFrame(bound[CoordinationField]);
        boundJoint[3] = ReadFrame(ReadFrame(cut28)[CoordinationField])[3];
        Assert.NotEmpty(boundJoint[3]);
        bound[CoordinationField] = WriteFrame(boundJoint);
        Assert.Equal(QualificationJointRefusal,
            Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(WriteFrame(bound))).Message);
        Assert.ThrowsAny<FormatException>(() => WorldSnapshotCodec.DecodeCoordinated(WriteFrame(bound), first.Anchor));
        // A cut root whose binding is removed is refused with and without the anchor.
        byte[][] unbound = ReadFrame(cut28);
        byte[][] unboundJoint = ReadFrame(unbound[CoordinationField]);
        unboundJoint[3] = [];
        unbound[CoordinationField] = WriteFrame(unboundJoint);
        Assert.Equal(QualificationJointRefusal, Assert.Throws<FormatException>(
            () => WorldSnapshotCodec.DecodeCoordinated(WriteFrame(unbound), first.Anchor)).Message);
        Assert.ThrowsAny<FormatException>(() => WorldSnapshotCodec.Decode(WriteFrame(unbound)));
    }

    [Fact]
    public void A_recomputed_reference_admits_unauthenticated_history_as_structure_and_refuses_self_refuting_evidence()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis));
        fixture.Journey.Approach(session);
        WorldSnapshot before = session.ReadStateForHost();
        var pair = Pair(before, fixture.Journey, RawTerms(before, fixture.Helper, fixture.Player));
        CareRewardJourney.Commit(session, fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep,
            [pair.Offer, pair.Acceptance]));
        WorldMaterialCareArchiveSegment closed = WorldMaterialCareArchiveSegment.Seal(session.ReadStateForHost());
        byte[] original = closed.Encode();
        WorldMaterialCareArchiveReference Recomputed(byte[] bytes) => new(closed.Reference.WorldId,
            closed.Reference.BranchId, closed.Reference.Ordinal, closed.Reference.BoundarySequence,
            ContentHash.Compute(bytes));

        // The recorded owner hashes are historical claims. Structure cannot recompute them, so a
        // consistently re-hashed root is a different, structurally valid boundary with another
        // anchor identity. Only the authenticated journal and pinned chain can refuse it.
        byte[] claimed = SHA256.HashData("consented-format-v1/unauthenticated-history"u8);
        byte[] rehashed = QualificationRewriteEvidence(original, record => record[15] = claimed);
        Assert.Equal(QualificationHashRefusal, Assert.Throws<FormatException>(
            () => WorldMaterialCareArchiveSegment.Decode(closed.Reference, rehashed)).Message);
        WorldMaterialCareArchiveSegment admitted = WorldMaterialCareArchiveSegment.Decode(Recomputed(rehashed), rehashed);
        Assert.Equal(rehashed, admitted.Encode());
        Assert.NotEqual(closed.Reference.BoundaryHash, admitted.Reference.BoundaryHash);
        Assert.NotEqual(closed.Anchor.ContentHash, admitted.Anchor.ContentHash);
        Assert.Equal(closed.Continued.Inventory.ContentHash, admitted.Continued.Inventory.ContentHash);

        // A record that refutes itself stays refused however its reference is recomputed.
        // Reversed positions already refute the record as a standalone history.
        byte[] reversed = QualificationRewriteEvidence(original,
            record => (record[2], record[3]) = (record[3], record[2]), canonicalHistory: false);
        FormatException refusal = Assert.Throws<FormatException>(
            () => WorldMaterialCareArchiveSegment.Decode(Recomputed(reversed), reversed));
        Assert.Contains("Consent evidence action positions must follow unique actor order within each world sequence.",
            refusal.ToString());
        byte[] outside = QualificationRewriteEvidence(original, record => record[1] = Number(closed.Reference.BoundarySequence + 1));
        refusal = Assert.Throws<FormatException>(
            () => WorldMaterialCareArchiveSegment.Decode(Recomputed(outside), outside));
        Assert.Contains(QualificationSegmentRefusal, refusal.ToString());
        Assert.Equal(closed.Anchor.ContentHash,
            WorldMaterialCareArchiveSegment.Decode(closed.Reference, original).Anchor.ContentHash);
    }

    private static byte[] QualificationRewriteEvidence(byte[] rootBytes, Action<byte[][]> change,
        bool canonicalHistory = true)
    {
        byte[][] root = ReadFrame(rootBytes);
        byte[][] coordination = ReadFrame(root[CoordinationField]);
        byte[][] history = ReadFrame(coordination[4]);
        byte[][] record = ReadFrame(history[1]);
        change(record);
        history[1] = WriteFrame(record);
        coordination[4] = WriteFrame(history);
        if (canonicalHistory)
        {
            // The changed record remains a canonical history on its own; only the world can refuse it.
            _ = WorldConsentedMaterialCodec.Decode(coordination[4]);
        }
        else
        {
            Assert.Throws<FormatException>(() => WorldConsentedMaterialCodec.Decode(coordination[4]));
        }
        root[CoordinationField] = WriteFrame(coordination);
        return WriteFrame(root);
    }

    private static byte[][] QualificationAssertEnvelope(byte[] rootBytes)
    {
        byte[][] root = ReadFrame(rootBytes);
        Assert.Equal(24, root.Length);
        string[] owners = QualificationFixture.GetProperty("compositions").GetProperty("snapshotV28")
            .GetProperty("segments").EnumerateArray().Where(segment => segment.TryGetProperty("slot", out _))
            .Select(segment => segment.GetProperty("slot").GetString()!).ToArray();
        Assert.Equal(22, owners.Length);
        var slots = new Dictionary<string, byte[]>();
        for (int index = 0; index < owners.Length; index++)
        {
            slots[owners[index]] = root[index + 2];
        }
        Assert.Equal(rootBytes, QualificationCompose("snapshotV28", slots));
        return root;
    }

    private static string QualificationSha(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static byte[] QualificationVector(string name) =>
        QualificationPinned(QualificationFixture.GetProperty("vectors").GetProperty(name));

    private static byte[] QualificationPinned(JsonElement vector)
    {
        byte[] bytes = Convert.FromHexString(vector.GetProperty("hex").GetString()!);
        Assert.Equal(vector.GetProperty("length").GetInt32(), bytes.Length);
        Assert.Equal(vector.GetProperty("sha256").GetString(), QualificationSha(bytes));
        return bytes;
    }

    /// <summary>
    /// Fills independent framing with opaque slots. The pinned synthetic example first proves that
    /// this assembler reproduces the independent encoder for the same composition.
    /// </summary>
    private static byte[] QualificationCompose(string name, Dictionary<string, byte[]> slots)
    {
        JsonElement composition = QualificationFixture.GetProperty("compositions").GetProperty(name);
        JsonElement example = composition.GetProperty("example");
        Dictionary<string, byte[]> synthetic = example.GetProperty("slots").EnumerateObject()
            .ToDictionary(slot => slot.Name, slot => Convert.FromHexString(slot.Value.GetString()!));
        Assert.Equal(QualificationPinned(example), QualificationAssemble(composition, synthetic));
        return QualificationAssemble(composition, slots);
    }

    private static byte[] QualificationAssemble(JsonElement composition, Dictionary<string, byte[]> slots)
    {
        using var stream = new MemoryStream();
        foreach (JsonElement segment in composition.GetProperty("segments").EnumerateArray())
        {
            if (segment.TryGetProperty("hex", out JsonElement literal))
            {
                stream.Write(Convert.FromHexString(literal.GetString()!));
                continue;
            }
            byte[] value = slots[segment.GetProperty("slot").GetString()!];
            if (segment.TryGetProperty("width", out JsonElement width))
            {
                Assert.Equal(width.GetInt32(), value.Length);
            }
            else
            {
                byte[] prefix = new byte[sizeof(uint)];
                BinaryPrimitives.WriteUInt32BigEndian(prefix, checked((uint)value.Length));
                stream.Write(prefix);
            }
            stream.Write(value);
        }
        return stream.ToArray();
    }
}
