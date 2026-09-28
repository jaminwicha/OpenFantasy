using System.Buffers.Binary;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;

namespace OpenFantasy.Core.Tests.Production;

public sealed class MaterialCustodyContinuationCodecTests
{
    [Fact]
    public void Reordered_consume_and_transfer_fail_intermediate_capacity_with_corrected_revision_tokens()
    {
        var (journey, product) = MaterialCustodyContinuationFixture.FullMixedDestination();
        journey.Cut();
        var before = journey.Inventory;
        var context = journey.Production.Continuation!;
        journey.Consume(IngredientLotKind.Transformed, product, 78200);
        var afterConsume = journey.Inventory;
        journey.Transfer(Id(70), Id(61), 1, 78201);
        var actual = journey.Production;
        Assert.Equal(10UL, actual.GetCombinedStoredQuantity(journey.Inventory, Id(61)));
        Assert.Empty(actual.Lots);
        actual.ValidateForAuthority(journey.Inventory);
        var originalTransfer = Assert.Single(actual.MaterialHistory!.Operations.OfType<MaterialTransferOperation>());
        var old = originalTransfer.Command;
        var corrected = new CommandEnvelope(old.CommandId, old.PrincipalId, old.OriginSessionId, old.ControlledActorId,
            old.WorldId, old.BranchId, old.ExpectedRevisions.Select(expected => new ExpectedRevision(expected.TargetKind,
                expected.TargetId, expected.TargetKind == RevisionTargetKind.Branch ? before.Custody.Revision
                    : expected.TargetId == Id(61) ? before.Custody.GetStorage(Id(61)).Revision : expected.Revision)), old.Payload);
        var rawOnly = new CountedTransferAuthority().Decide(before, corrected, originalTransfer.Delivery,
            new(before.WorldId, context.BaseProductionRevision + 1));
        Assert.Equal(CommandReceiptStatus.Applied, rawOnly.Receipt.Status); // Not a stale-token rejection.
        Assert.Equal(10UL, rawOnly.Candidate.Custody.GetStoredQuantity(Id(61)));
        Assert.Equal(rawOnly.Candidate.Custody.Lots, journey.Inventory.Custody.Lots);
        Assert.Equal(before.Custody.GetStorage(Id(61)).Revision + 1, afterConsume.Custody.GetStorage(Id(61)).Revision);
        Assert.Equal(rawOnly.Candidate.Custody.GetStorage(Id(61)).Revision + 1,
            journey.Inventory.Custody.GetStorage(Id(61)).Revision);
        Assert.Equal(rawOnly.Candidate.Custody.GetStorage(Id(60)).Revision,
            journey.Inventory.Custody.GetStorage(Id(60)).Revision);
        Assert.Equal(rawOnly.Candidate.Custody.Revision + 1, journey.Inventory.Custody.Revision);

        byte[][] root = Fields(ProductionCodecV4.Encode(actual));
        byte[][] operations = Fields(root[12]);
        byte[][] consume = Fields(operations[1]);
        byte[][] transfer = Fields(operations[2]);
        transfer[3] = Number(context.BaseProductionRevision + 1);
        consume[3] = Number(context.BaseProductionRevision + 2);
        byte[][] transferPayload = Fields(transfer[5]);
        transferPayload[1] = CommandCodecV1.Encode(corrected);
        transfer[5] = Frame(transferPayload);
        root[12] = Frame([operations[0], Frame(transfer), Frame(consume)]);
        var reordered = ProductionCodecV4.Decode(Frame(root), context, []);
        Assert.Equal(actual.Revision, reordered.Revision);
        Assert.Empty(reordered.Lots);
        var error = Assert.Throws<ArgumentException>(() => reordered.ValidateForAuthority(journey.Inventory));
        Assert.Equal("Combined raw and produced contents exceed the actual storage capacity.", error.Message);
        actual.ValidateForAuthority(journey.Inventory);
        // Final material owners fit and all old expected revisions are corrected. Only the
        // intermediate transfer still has one live produced unit on top of ten raw units.
    }

    [Theory]
    [InlineData("reorder")]
    [InlineData("reorder-renumber")]
    [InlineData("duplicate")]
    [InlineData("command")]
    [InlineData("proof-hash")]
    [InlineData("total")]
    public void Ordered_issuance_and_exact_proof_joins_cannot_be_rewritten(string change)
    {
        var journey = MaterialCustodyContinuationFixture.Create();
        journey.Gather(77000);
        journey.Gather(77001);
        byte[] bytes = ProductionCodecV4.Encode(journey.Production);
        var context = journey.Production.Continuation!;
        var evidence = journey.Production.MaterialHistory!.GatherEvidence.ToArray();
        byte[][] root = Fields(bytes);
        byte[][] operations = Fields(root[12]);
        if (change is "reorder" or "reorder-renumber")
        {
            (operations[1], operations[2]) = (operations[2], operations[1]);
            if (change == "reorder-renumber")
            {
                for (int index = 1; index < operations.Length; index++)
                {
                    var operation = Fields(operations[index]);
                    operation[3] = Number((ulong)index);
                    operations[index] = Frame(operation);
                }
            }
        }
        else if (change == "total") root[8] = Number(3);
        else
        {
            byte[][] operation = Fields(change == "duplicate" ? operations[1] : operations[2]);
            if (change == "duplicate") operation[3] = Number(2);
            if (change == "command") operation[4] = Id(77002).ToByteArray();
            if (change == "proof-hash") operation[5][0] ^= 0x80;
            operations[2] = Frame(operation);
        }
        root[12] = Frame(operations);
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV4.Decode(Frame(root), context, evidence));
        Assert.Equal(bytes, ProductionCodecV4.Encode(journey.Production));
    }

    [Fact]
    public void A_previously_accepted_issue_cannot_be_replayed_after_the_next_cut()
    {
        var journey = MaterialCustodyContinuationFixture.Create();
        journey.Gather(77100);
        byte[][] prior = Fields(ProductionCodecV4.Encode(journey.Production));
        byte[][] oldOperation = Fields(Fields(prior[12])[1]);
        var proof = Assert.Single(journey.Production.MaterialHistory!.GatherEvidence);
        journey.Cut();
        byte[][] root = Fields(ProductionCodecV4.Encode(journey.Production));
        oldOperation[3] = Number(2);
        root[4] = Number(2);
        root[8] = Number(2);
        root[12] = Frame([Fields(root[12])[0], Frame(oldOperation)]);
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV4.Decode(Frame(root), journey.Production.Continuation!, [proof]));
        Assert.Equal(3UL, journey.Gathering.GetNode(OpenFantasy.Core.Tests.Gathering.GatheringFixture.Node).NextUnissuedUnit);
    }

    [Fact]
    public void Dropping_an_issued_interval_and_rewriting_counters_cannot_match_the_actual_inventory()
    {
        var journey = MaterialCustodyContinuationFixture.Create();
        journey.Gather(77200);
        journey.Gather(77201);
        byte[][] root = Fields(ProductionCodecV4.Encode(journey.Production));
        byte[][] operations = Fields(root[12]);
        root[12] = Frame([operations[0], operations[1]]);
        root[4] = Number(1);
        root[8] = Number(1);
        var firstProof = journey.Production.MaterialHistory!.GatherEvidence[0];
        var falseEarlier = ProductionCodecV4.Decode(Frame(root), journey.Production.Continuation!, [firstProof]);
        Assert.Throws<ArgumentException>(() => falseEarlier.ValidateForAuthority(journey.Inventory));
        journey.Production.ValidateForAuthority(journey.Inventory);
    }

    [Fact]
    public void Actual_source_evidence_is_required_and_cannot_be_replaced_by_another_accepted_swing()
    {
        var journey = MaterialCustodyContinuationFixture.Create();
        journey.Gather(76000);
        journey.Gather(76001);
        byte[] bytes = ProductionCodecV4.Encode(journey.Production);
        var context = journey.Production.Continuation!;
        var evidence = journey.Production.MaterialHistory!.GatherEvidence.ToArray();
        Assert.Equal(2, evidence.Length);
        var restored = ProductionCodecV4.Decode(bytes, context, evidence);
        restored.ValidateForAuthority(journey.Inventory);
        Assert.Equal(bytes, ProductionCodecV4.Encode(restored));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV4.Decode(bytes, context, []));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV4.Decode(bytes, context, [evidence[0]]));
        var independent = MaterialCustodyContinuationFixture.Create();
        independent.Gather(76002);
        var other = Assert.Single(independent.Production.MaterialHistory!.GatherEvidence);
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV4.Decode(bytes, context, [evidence[0], other]));
        Assert.DoesNotContain(typeof(GatherIssueEvidence).GetConstructors(), constructor => constructor.IsPublic);
        journey.Cut();
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV4.Decode(bytes, journey.Production.Continuation!, evidence));
        Assert.Equal(bytes, ProductionCodecV4.Encode(restored));
    }

    [Fact]
    public void Admitted_capacity_without_captured_source_owner_cannot_authorize_a_gather()
    {
        var journey = MaterialCustodyContinuationFixture.Create();
        var inventory = journey.Inventory;
        // A fresh material-only admission has the same finite source capacity but deliberately
        // lacks the source cursor/effort owner. It is not an earned source transition.
        var materialOnly = new ProductionSnapshot(inventory.WorldId, inventory.BranchId, 0,
            journey.Production.Recipes, [], [], []).ContinueMaterial(inventory);
        Assert.Null(materialOnly.MaterialHistory!.Gathering);
        var plan = new MaterialGatherPlanner().Plan(journey.SourceWorld, inventory, materialOnly,
            journey.Gathering, journey.GatherCommand(76003), journey.IsOccupied);
        Assert.NotEqual(WorldActionRejection.None, plan.Rejection);
        Assert.Same(inventory, plan.CandidateInventory);
        Assert.Same(materialOnly, plan.CandidateProduction);
    }

    [Fact]
    public void Material_interpretation_does_not_silently_rewrite_legacy_continuation_bytes()
    {
        var legacy = ProductionContinuationFixture.Crafted();
        legacy.Cut();
        byte[] v3Bytes = ProductionCodecV3.Encode(legacy.Production);
        var material = legacy.Production.ContinueMaterial(legacy.Inventory);
        byte[] v4Bytes = ProductionCodecV4.Encode(material);
        Assert.NotEqual(v3Bytes, v4Bytes);
        Assert.Equal(v3Bytes, ProductionCodecV3.Encode(legacy.Production));
        Assert.Equal(v3Bytes, ProductionCodecV3.Encode(ProductionCodecV3.Decode(v3Bytes, legacy.Production.Continuation!)));
        Assert.Throws<ArgumentException>(() => ProductionCodecV1.Encode(material));
        Assert.Throws<ArgumentException>(() => ProductionCodecV2.Encode(material));
        Assert.Throws<ArgumentException>(() => ProductionCodecV3.Encode(material));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV1.Decode(v4Bytes));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV2.Decode(v4Bytes));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV3.Decode(v4Bytes, material.Continuation!));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV4.Decode(v3Bytes, material.Continuation!, []));
        Assert.Equal(legacy.Production.Revision, material.Revision);
        Assert.Equal(legacy.Production.TotalBatchCount, material.TotalBatchCount);
        Assert.Equal(legacy.Production.TotalConsumedItemCount, material.TotalConsumedItemCount);
        material.ValidateForAuthority(legacy.Inventory);
    }

    private static byte[][] Fields(byte[] bytes)
    {
        Assert.True(bytes.AsSpan(0, 4).SequenceEqual("OFC\0"u8));
        Assert.Equal(1U, BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(4, 4)));
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)));
        var fields = new byte[count][];
        int offset = 12;
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4)));
            offset += 4;
            fields[index] = bytes.AsSpan(offset, length).ToArray();
            offset += length;
        }
        Assert.Equal(bytes.Length, offset);
        return fields;
    }

    private static byte[] Number(ulong value)
    {
        byte[] bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    // Independent OFC frame writer targets actual accepted record bytes, not invented issuing evidence.
    private static byte[] Frame(byte[][] fields)
    {
        using var stream = new MemoryStream();
        stream.Write("OFC\0"u8);
        Span<byte> number = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(number, 1);
        stream.Write(number);
        BinaryPrimitives.WriteUInt32BigEndian(number, checked((uint)fields.Length));
        stream.Write(number);
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(number, checked((uint)field.Length));
            stream.Write(number);
            stream.Write(field);
        }
        return stream.ToArray();
    }
}
