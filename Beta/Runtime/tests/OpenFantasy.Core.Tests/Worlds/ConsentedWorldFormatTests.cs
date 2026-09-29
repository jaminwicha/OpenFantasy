using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.ConsentedFormatFixture;
using Kind = OpenFantasy.Core.Authority.ConsentedMaterial.ConsentedMaterialLotKind;

namespace OpenFantasy.Core.Tests.Worlds;

/// <summary>
/// Expected bytes come from the standalone generator's semantic facts, never from a runtime codec.
/// Evidence has no public constructor, so its vectors enter through the decoder and must re-encode
/// to the same independent bytes with every field equal to the independently stated fact.
/// </summary>
public sealed class ConsentedWorldFormatTests
{
    private const string HistoryTag = "world.consented-material-history.v1";
    private const string FieldCountRefusal = "Invalid world frame field count.";
    private const string ByteBoundRefusal = "Invalid world frame header or byte bound.";
    private const string FieldBoundRefusal = "A world field is truncated or excessive.";
    private const string TrailingRefusal = "A world frame has trailing fields or bytes.";
    private const string EvidenceRefusal = "Consent evidence requires bound approvals, distinct identity roles and the exact moved interval.";
    private const string HistoryRefusal = "Consent evidence requires bounded exact order and distinct permanent identities.";
    private const string ProfileRefusal = "Paired material transfer requires its explicit complete-world profile.";

    // Header, tag prefix and tag, number prefix, then the last byte of the big-endian profile number.
    private const int ProfileNumberLastByte = 12 + 4 + 16 + 4 + 7;

    private static readonly CountedItemDefinition Material =
        new(Id(10), ContentHash.Compute("consented-gift-golden-material-v1"u8));

    private static ConsentedMaterialTerms RawTerms => Terms(Kind.CountedSource, Id(5), 4, 2);
    private static ConsentedMaterialTerms ProducedTerms => Terms(Kind.Produced, Id(6), 3, 7);

    private static WorldActionCommand Offer => new(Id(20), Id(21), Id(22), Id(23), Id(1), Id(2), 5,
        new OfferMaterialIntent(RawTerms, 6));

    private static WorldActionCommand Acceptance => new(Id(30), Id(31), Id(32), Id(24), Id(1), Id(2), 6,
        AcceptMaterialIntent.Approving(Offer, 5));

    [Fact]
    public void Independent_vectors_pin_the_offer_and_acceptance_bytes_hashes_and_fields()
    {
        Assert.Equal(Vector("termsRaw"), ConsentedMaterialCodecV1.EncodeTerms(RawTerms));
        Assert.Equal(Vector("termsProduced"), ConsentedMaterialCodecV1.EncodeTerms(ProducedTerms));
        foreach ((string name, WorldActionCommand command) in new[] { ("actionOffer", Offer), ("actionAcceptance", Acceptance) })
        {
            byte[] expected = Vector(name);
            Assert.Equal(expected, WorldActionCodecV15.Encode(command));
            Assert.Equal(expected, WorldActionCodec.Encode(command));
            Assert.Equal(Sha(expected), command.ContentHash.Value);
            WorldActionCommand decoded = WorldActionCodec.Decode(expected);
            Assert.Equal((command.CommandId, command.PrincipalId, command.OriginSessionId, command.ActorId,
                    command.WorldId, command.BranchId, command.ExpectedActorRevision),
                (decoded.CommandId, decoded.PrincipalId, decoded.OriginSessionId, decoded.ActorId,
                    decoded.WorldId, decoded.BranchId, decoded.ExpectedActorRevision));
            Assert.Equal(command.Intent, decoded.Intent);
        }
        AcceptMaterialIntent accepted = Assert.IsType<AcceptMaterialIntent>(Acceptance.Intent);
        Assert.Equal(Fact("offerMaterialCommandHash"),
            Convert.ToHexString(accepted.OfferHash.ToByteArray()).ToLowerInvariant());
        Assert.Equal((Id(20), RawTerms, 5UL), (accepted.OfferCommandId, accepted.Terms, accepted.ExpectedSenderRevision));
    }

    [Fact]
    public void Independent_vector_pins_the_profile_28_step_and_profile_27_refuses_it()
    {
        // Supplied out of actor order; the canonical batch orders samples and actions by actor.
        var batch = new WorldStepBatch(Id(40), Id(1), Id(2), 9, 8, WorldBatchKind.AdvancingStep,
            [new WorldMovementInput(new TrustedCommandDelivery(Id(29), Id(33)), Id(28), 0, -32),
                new WorldMovementInput(new TrustedCommandDelivery(Id(26), Id(27)), Id(25), 32, 0)],
            [Acceptance, Offer], 28);
        byte[] expected = Vector("stepBatch");
        Assert.Equal(expected, WorldStepCodecV28.Encode(batch));
        WorldStepBatch decoded = WorldStepCodecV28.Decode(expected);
        Assert.Equal((Id(40), Id(1), Id(2), 9UL, 8UL, WorldBatchKind.AdvancingStep, (ushort)28),
            (decoded.CauseId, decoded.WorldId, decoded.BranchId, decoded.ExpectedRevision,
                decoded.ExpectedAdvancingSteps, decoded.Kind, decoded.ProfileVersion));
        Assert.Equal(new[] { (Id(25), Id(26), Id(27), 32L, 0L), (Id(28), Id(29), Id(33), 0L, -32L) },
            decoded.Movement.Select(value => (value.ActorId, value.Delivery.PrincipalId, value.Delivery.SessionId,
                value.DeltaX, value.DeltaY)));
        Assert.Equal(new[] { Id(20), Id(30) }, decoded.Actions.Select(value => value.CommandId));
        Assert.Equal(expected, WorldStepCodecV28.Encode(decoded));

        Assert.Equal("Unsupported world step version.",
            Assert.Throws<FormatException>(() => WorldStepCodecV27.Decode(expected)).Message);
        byte[] relabelled = (byte[])expected.Clone();
        Assert.Equal((byte)28, relabelled[ProfileNumberLastByte]);
        relabelled[ProfileNumberLastByte] = 27;
        FormatException refusal = Assert.Throws<FormatException>(() => WorldStepCodecV27.Decode(relabelled));
        Assert.Equal(ProfileRefusal, Assert.IsType<ArgumentException>(refusal.InnerException).Message);
    }

    [Fact]
    public void Independent_vectors_pin_consent_evidence_and_history_bytes_hashes_and_every_field()
    {
        byte[] expected = Vector("history");
        WorldConsentedMaterialHistory history = WorldConsentedMaterialCodec.Decode(expected);
        Assert.Equal(expected, WorldConsentedMaterialCodec.Encode(history));
        Assert.Equal(Fact("historyContentHash"), history.ContentHash.Value);
        Assert.Equal(2, history.Transfers.Count);
        WorldConsentedTransferEvidence raw = history.Transfers[0];
        WorldConsentedTransferEvidence produced = history.Transfers[1];
        Assert.Equal(Vector("evidenceRaw"), raw.Encode());
        Assert.Equal(Vector("evidenceProduced"), produced.Encode());
        Assert.Equal(Sha(Vector("evidenceRaw")), raw.ContentHash.Value);
        Assert.Equal(Sha(Vector("evidenceProduced")), produced.ContentHash.Value);

        Assert.Equal((10UL, 0U, 1U, Id(20), Id(30), Id(5), Id(50), 0U, 12UL, 16UL),
            (raw.WorldSequence, raw.OfferActionIndex, raw.AcceptanceActionIndex, raw.CommandId, raw.AcceptanceId,
                raw.MovedLotId, raw.RightsOwnerId, raw.OutputIndex, raw.MovedStart, raw.MovedEnd));
        Assert.Equal((11UL, 2U, 5U, Id(60), Id(61), EntityId.DeriveSpawned(Id(60), 0), Id(51), 1U, 0UL, 3UL),
            (produced.WorldSequence, produced.OfferActionIndex, produced.AcceptanceActionIndex, produced.CommandId,
                produced.AcceptanceId, produced.MovedLotId, produced.RightsOwnerId, produced.OutputIndex,
                produced.MovedStart, produced.MovedEnd));
        Assert.Equal(Fact("splitChild"), produced.MovedLotId.ToString());
        Assert.Equal((RawTerms, ProducedTerms), (raw.Approval.Command.Terms, produced.Approval.Command.Terms));
        foreach ((string label, WorldConsentedTransferEvidence evidence) in new[] { ("A", raw), ("B", produced) })
        {
            Assert.Equal((Id(21), Id(22), Id(31), Id(32)), (evidence.Approval.SenderDelivery.PrincipalId,
                evidence.Approval.SenderDelivery.SessionId, evidence.Approval.RecipientDelivery.PrincipalId,
                evidence.Approval.RecipientDelivery.SessionId));
            Assert.Equal((Id(21), Id(22), Id(31), Id(32)), (evidence.Approval.Command.PrincipalId,
                evidence.Approval.Command.OriginSessionId, evidence.Approval.Acceptance.PrincipalId,
                evidence.Approval.Acceptance.OriginSessionId));
            Assert.True(evidence.Approval.Acceptance.Binds(evidence.Approval.Command));
            Assert.Equal(Hash(label, "original-inventory"), evidence.OriginalInventoryHash.ToByteArray());
            Assert.Equal(Hash(label, "original-production"), evidence.OriginalProductionHash.ToByteArray());
            Assert.Equal(Hash(label, "candidate-inventory"), evidence.CandidateInventoryHash.ToByteArray());
            Assert.Equal(Hash(label, "candidate-production"), evidence.CandidateProductionHash.ToByteArray());
        }
        Assert.Null(raw.ToProducedMovement());
        Assert.NotNull(produced.ToProducedMovement());
        Assert.True(history.ContainsIdentity(produced.MovedLotId) && history.ContainsIdentity(Id(61)));

        var empty = new WorldConsentedMaterialHistory([]);
        Assert.Equal(Vector("historyEmpty"), WorldConsentedMaterialCodec.Encode(empty));
        Assert.Equal(Fact("historyEmptyContentHash"), empty.ContentHash.Value);
        Assert.Empty(WorldConsentedMaterialCodec.Decode(Vector("historyEmpty")).Transfers);
    }

    [Fact]
    public void Exact_transfer_count_and_evidence_size_are_refused_with_their_format_reason()
    {
        // The hostile-frame builder is first bound to the independent bytes.
        Assert.Equal(Vector("evidenceRaw"), Evidence(10, 20, 30));
        Assert.Equal(256, WorldConsentedMaterialHistory.MaximumTransfers);
        Assert.Equal(4_096, WorldConsentedTransferEvidence.MaximumByteLength);
        Assert.Equal(1_049_728, WorldConsentedMaterialHistory.MaximumByteLength);

        byte[][] records = Enumerable.Range(0, 257).Select(index =>
            Evidence((ulong)index + 1, 2000 + 2 * (ulong)index, 2001 + 2 * (ulong)index)).ToArray();
        byte[] full = Frame(HistoryTag, records[..256]);
        WorldConsentedMaterialHistory admitted = WorldConsentedMaterialCodec.Decode(full);
        Assert.Equal(256, admitted.Transfers.Count);
        Assert.Equal(full, WorldConsentedMaterialCodec.Encode(admitted));
        Assert.True(full.Length <= WorldConsentedMaterialHistory.MaximumByteLength);
        Assert.Equal(FieldCountRefusal, Assert.Throws<FormatException>(
            () => WorldConsentedMaterialCodec.Decode(Frame(HistoryTag, records))).Message);

        byte[] record = Vector("evidenceRaw");
        byte[] atBound = new byte[WorldConsentedTransferEvidence.MaximumByteLength];
        byte[] overBound = new byte[WorldConsentedTransferEvidence.MaximumByteLength + 1];
        record.CopyTo(atBound, 0);
        record.CopyTo(overBound, 0);
        // The padded record passes the size bound and is refused only for its trailing bytes.
        Assert.Equal(TrailingRefusal, Assert.Throws<FormatException>(
            () => WorldConsentedMaterialCodec.Decode(Frame(HistoryTag, atBound))).Message);
        Assert.Equal(FieldBoundRefusal, Assert.Throws<FormatException>(
            () => WorldConsentedMaterialCodec.Decode(Frame(HistoryTag, overBound))).Message);
        Assert.Equal(ByteBoundRefusal, Assert.Throws<FormatException>(() => WorldConsentedMaterialCodec.Decode(
            new byte[WorldConsentedMaterialHistory.MaximumByteLength + 1])).Message);
    }

    [Fact]
    public void Identity_role_collisions_positions_and_order_are_refused_by_the_evidence_contract()
    {
        // Valid controls: a whole lot, a child derived from the offer, and the last action position.
        Assert.Single(WorldConsentedMaterialCodec.Decode(Frame(HistoryTag, Evidence(1, 20, 30))).Transfers);
        Assert.Single(WorldConsentedMaterialCodec.Decode(Frame(HistoryTag,
            Evidence(1, 20, 30, movedLot: EntityId.DeriveSpawned(Id(20), 0)))).Transfers);
        Assert.Single(WorldConsentedMaterialCodec.Decode(Frame(HistoryTag,
            Evidence(1, 20, 30, acceptanceIndex: 63))).Transfers);
        Assert.Equal(2, WorldConsentedMaterialCodec.Decode(Frame(HistoryTag,
            Evidence(1, 20, 30), Evidence(2, 40, 41))).Transfers.Count);

        byte[][] evidence =
        [
            Evidence(0, 20, 30),
            Evidence(1, 20, 30, movedLot: Id(30)),
            Evidence(1, 20, 30, movedLot: Id(20)),
            Evidence(1, 20, 30, movedLot: EntityId.DeriveSpawned(Id(30), 0)),
            Evidence(1, 20, 30, movedLot: EntityId.DeriveSpawned(Id(20), 1)),
            Evidence(1, 20, 30, offerIndex: 1, acceptanceIndex: 1),
            Evidence(1, 20, 30, acceptanceIndex: 64),
            Evidence(1, 20, 30, end: 15)
        ];
        Assert.All(evidence, record => Assert.Equal(EvidenceRefusal, ContractRefusal(Frame(HistoryTag, record))));

        byte[][] histories =
        [
            // A command identity reused as a later acceptance, and the reverse.
            Frame(HistoryTag, Evidence(1, 20, 30), Evidence(2, 40, 20)),
            Frame(HistoryTag, Evidence(1, 20, 30), Evidence(2, 30, 41)),
            Frame(HistoryTag, Evidence(1, 20, 30), Evidence(2, 40, 30)),
            Frame(HistoryTag, Evidence(1, 20, 30), Evidence(2, 20, 41)),
            // Regressing order, and a second record at an already occupied position.
            Frame(HistoryTag, Evidence(2, 20, 30), Evidence(1, 40, 41)),
            Frame(HistoryTag, Evidence(1, 20, 30), Evidence(1, 40, 41))
        ];
        Assert.All(histories, history => Assert.Equal(HistoryRefusal, ContractRefusal(history)));
        // The same two actors cannot occupy four positions of one batch.
        Assert.Equal("Consent evidence action positions must follow unique actor order within each world sequence.",
            ContractRefusal(Frame(HistoryTag, Evidence(1, 20, 30), Evidence(1, 40, 41, offerIndex: 2, acceptanceIndex: 3))));
    }

    [Fact]
    public void Exact_action_count_and_command_size_are_refused_with_their_format_reason()
    {
        WorldActionCommand[] commands = Enumerable.Range(0, 65).Select(index => new WorldActionCommand(
            Id(1000 + (ulong)index), Id(21), Id(22), Id(100 + (ulong)index), Id(1), Id(2), 0,
            new OfferMaterialIntent(Terms(Kind.CountedSource, Id(5), 4, 2, Id(100 + (ulong)index)), 0))).ToArray();
        byte[][] actions = commands.Select(WorldActionCodecV15.Encode).ToArray();
        Assert.Equal(64, WorldStepBatch.MaximumActorsPerBatch);
        byte[] full = Step(actions[..64]);
        WorldStepBatch admitted = WorldStepCodecV28.Decode(full);
        Assert.Equal(64, admitted.Actions.Count);
        Assert.Equal(full, WorldStepCodecV28.Encode(admitted));
        Assert.Equal(FieldCountRefusal, Assert.Throws<FormatException>(() => WorldStepCodecV28.Decode(Step(actions))).Message);
        Assert.Equal("A batch needs unique commands, one action per actor, in its own world/branch.",
            Assert.Throws<ArgumentException>(() => new WorldStepBatch(Id(40), Id(1), Id(2), 9, 8,
                WorldBatchKind.AdvancingStep, [], commands, 28)).Message);
        // Actions out of strict actor order are refused by the frame, never reordered.
        Assert.Equal("Discrete actions are not in strict actor order.",
            Assert.Throws<FormatException>(() => WorldStepCodecV28.Decode(Step(actions[1], actions[0]))).Message);

        Assert.Equal(2_048, WorldActionCodecV15.MaximumCommandByteLength);
        byte[] valid = Vector("actionOffer");
        byte[] atBound = new byte[WorldActionCodecV15.MaximumCommandByteLength];
        byte[] overBound = new byte[WorldActionCodecV15.MaximumCommandByteLength + 1];
        valid.CopyTo(atBound, 0);
        valid.CopyTo(overBound, 0);
        Assert.Equal(TrailingRefusal, Assert.Throws<FormatException>(() => WorldActionCodecV15.Decode(atBound)).Message);
        Assert.Equal(ByteBoundRefusal, Assert.Throws<FormatException>(() => WorldActionCodecV15.Decode(overBound)).Message);
        Assert.Equal(ByteBoundRefusal, Assert.Throws<FormatException>(() => WorldStepCodecV28.Decode(Step(overBound))).Message);
        Assert.Equal(256 * 1024, WorldStepCodecV28.MaximumBatchByteLength);
        Assert.Equal(ByteBoundRefusal, Assert.Throws<FormatException>(
            () => WorldStepCodecV28.Decode(new byte[WorldStepCodecV28.MaximumBatchByteLength + 1])).Message);
    }

    [Fact]
    public void Exact_movement_count_and_actor_order_are_enforced_in_profile_28()
    {
        WorldMovementInput[] samples = Enumerable.Range(0, 65).Select(index => new WorldMovementInput(
            new TrustedCommandDelivery(Id(21), Id(22)), Id(100 + (ulong)index), 32, 0)).ToArray();
        byte[] EncodeSamples(IEnumerable<WorldMovementInput> values) => Frame("world.step-movement",
            values.Select(value => Frame("world.control-sample", value.Delivery.PrincipalId.ToByteArray(),
                value.Delivery.SessionId.ToByteArray(), value.ActorId.ToByteArray(), U64(32), U64(0))).ToArray());
        byte[] StepWithMovement(byte[] movement)
        {
            byte[][] fields = ReadFrame(Step());
            fields[8] = movement;
            return WriteFrame(fields);
        }

        byte[] full = StepWithMovement(EncodeSamples(samples[..64]));
        WorldStepBatch admitted = WorldStepCodecV28.Decode(full);
        Assert.Equal(64, admitted.Movement.Count);
        Assert.Equal(samples[..64].Select(value => value.ActorId), admitted.Movement.Select(value => value.ActorId));
        Assert.Equal(full, WorldStepCodecV28.Encode(admitted));
        Assert.Equal(full, WorldStepCodecV28.Encode(new WorldStepBatch(Id(40), Id(1), Id(2), 9, 8,
            WorldBatchKind.AdvancingStep, samples[..64], [], 28)));

        Assert.Equal(FieldCountRefusal, Assert.Throws<FormatException>(() => WorldStepCodecV28.Decode(
            StepWithMovement(EncodeSamples(samples)))).Message);
        Assert.Equal("A world step accepts at most one movement sample per actor.", Assert.Throws<ArgumentException>(
            () => new WorldStepBatch(Id(40), Id(1), Id(2), 9, 8, WorldBatchKind.AdvancingStep, samples, [], 28)).Message);
        foreach (WorldMovementInput[] invalid in new[] { new[] { samples[1], samples[0] }, new[] { samples[0], samples[0] } })
        {
            Assert.Equal("Movement samples are not in strict actor order.", Assert.Throws<FormatException>(
                () => WorldStepCodecV28.Decode(StepWithMovement(EncodeSamples(invalid)))).Message);
        }
    }

    private static ConsentedMaterialTerms Terms(Kind kind, EntityId lot, ulong quantity, ulong revision,
        EntityId? sender = null) => new(Id(1), Id(2), sender ?? Id(23), Id(24), kind, lot, Material, Id(3), Id(4),
        quantity, revision);

    private static string ContractRefusal(byte[] history)
    {
        FormatException refusal = Assert.Throws<FormatException>(() => WorldConsentedMaterialCodec.Decode(history));
        Assert.Equal("Consent history violates its bounded evidence contract.", refusal.Message);
        return Assert.IsType<ArgumentException>(refusal.InnerException).Message;
    }

    private static byte[] Evidence(ulong sequence, ulong command, ulong acceptance, EntityId? movedLot = null,
        ulong offerIndex = 0, ulong acceptanceIndex = 1, ulong end = 16)
    {
        var submitted = new ConsentedMaterialCommand(Id(command), Id(21), Id(22), RawTerms);
        ConsentedMaterialAcceptance accepted = ConsentedMaterialAcceptance.Approving(submitted, Id(acceptance), Id(31), Id(32));
        return Frame("world.consented-transfer.v1", U64(sequence), U64(offerIndex), U64(acceptanceIndex),
            ConsentedMaterialCodecV1.Encode(submitted), Id(21).ToByteArray(), Id(22).ToByteArray(),
            ConsentedMaterialCodecV1.Encode(accepted), Id(31).ToByteArray(), Id(32).ToByteArray(),
            (movedLot ?? Id(5)).ToByteArray(), Id(50).ToByteArray(), U64(0), U64(12), U64(end),
            Hash("A", "original-inventory"), Hash("A", "original-production"), Hash("A", "candidate-inventory"),
            Hash("A", "candidate-production"));
    }

    private static byte[] Step(params byte[][] actions) => Frame("world.step-batch", U64(28), Id(40).ToByteArray(),
        Id(1).ToByteArray(), Id(2).ToByteArray(), U64(9), U64(8), U64(1), Frame("world.step-movement"),
        Frame("world.step-actions", actions));

    private static byte[] Hash(string label, string name) =>
        SHA256.HashData(Encoding.UTF8.GetBytes("consented-format-v1/" + label + "/" + name));

    private static EntityId Id(ulong suffix) => new(0x0102030405060708UL, 0x1122334455660000UL + suffix);
}

/// <summary>Reads the independent fixture and frames hostile inputs without any runtime encoder.</summary>
internal static class ConsentedFormatFixture
{
    private static readonly JsonElement Root = JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "consented-world-formats-v1.json"))).RootElement;

    internal static string Fact(string name) => Root.GetProperty("facts").GetProperty(name).GetString()!;

    internal static string Sha(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    internal static byte[] Vector(string name) => Pinned(Root.GetProperty("vectors").GetProperty(name));

    /// <summary>
    /// Fills independent framing with the supplied opaque slots. The pinned synthetic example first
    /// proves that this assembler reproduces the independent encoder for the same composition.
    /// </summary>
    internal static byte[] Compose(string name, Dictionary<string, byte[]> slots)
    {
        JsonElement composition = Root.GetProperty("compositions").GetProperty(name);
        JsonElement example = composition.GetProperty("example");
        Dictionary<string, byte[]> synthetic = example.GetProperty("slots").EnumerateObject()
            .ToDictionary(slot => slot.Name, slot => Convert.FromHexString(slot.Value.GetString()!));
        Assert.Equal(Pinned(example), Assemble(composition, synthetic));
        return Assemble(composition, slots);
    }

    internal static byte[] Frame(string tag, params byte[][] fields) =>
        WriteFrame([Encoding.UTF8.GetBytes(tag), .. fields]);

    internal static byte[] WriteFrame(byte[][] fields)
    {
        using var stream = new MemoryStream();
        stream.Write("OFC\0"u8);
        stream.Write(U32(1));
        stream.Write(U32(checked((uint)fields.Length)));
        foreach (byte[] field in fields)
        {
            stream.Write(U32(checked((uint)field.Length)));
            stream.Write(field);
        }
        return stream.ToArray();
    }

    internal static byte[][] ReadFrame(byte[] bytes)
    {
        Assert.True(bytes.AsSpan(0, 4).SequenceEqual("OFC\0"u8));
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8)));
        var fields = new byte[count][];
        int offset = 12;
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
            fields[index] = bytes.AsSpan(offset + 4, length).ToArray();
            offset += 4 + length;
        }
        Assert.Equal(bytes.Length, offset);
        return fields;
    }

    internal static byte[] U64(ulong value)
    {
        byte[] bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] U32(uint value)
    {
        byte[] bytes = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] Pinned(JsonElement vector)
    {
        byte[] bytes = Convert.FromHexString(vector.GetProperty("hex").GetString()!);
        Assert.Equal(vector.GetProperty("length").GetInt32(), bytes.Length);
        Assert.Equal(vector.GetProperty("sha256").GetString(), Sha(bytes));
        return bytes;
    }

    private static byte[] Assemble(JsonElement composition, Dictionary<string, byte[]> slots)
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
                stream.Write(U32(checked((uint)value.Length)));
            }
            stream.Write(value);
        }
        return stream.ToArray();
    }
}
