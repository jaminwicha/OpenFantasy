using System.Buffers.Binary;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Construction.ConstructionContinuationFixture;

namespace OpenFantasy.Core.Tests.Construction;

public sealed class ConstructionContinuationCodecTests
{
    [Fact]
    public void Identical_raw_inventory_cannot_substitute_completion_evidence_from_a_different_derived_prefix()
    {
        var left = new ConstructionContinuationFixture(readyToComplete: true);
        var right = new ConstructionContinuationFixture(readyToComplete: true);
        left.RecraftByproduct(ProjectA);
        right.NextId(); // A different real crafting command; all quantities and custody effects stay equal.
        right.RecraftByproduct(ProjectA);
        Assert.Equal(left.Inventory.ContentHash, right.Inventory.ContentHash);
        Assert.Equal(left.Production.Revision, right.Production.Revision);
        Assert.NotEqual(left.Production.ContentHash, right.Production.ContentHash);
        left.Advance();
        right.Advance();
        EntityId completionCommand = Id(880_000);
        var leftPlan = left.Plan(left.Command(completionCommand));
        var rightPlan = right.Plan(right.Command(completionCommand));
        left.Accept(leftPlan, completionCommand);
        right.Accept(rightPlan, completionCommand);
        var originalProof = Assert.IsType<ConstructionCompletionEvidence>(leftPlan.Evidence);
        var otherProof = Assert.IsType<ConstructionCompletionEvidence>(rightPlan.Evidence);
        Assert.Equal(originalProof.BeforeInventoryHash, otherProof.BeforeInventoryHash);
        Assert.Equal(originalProof.AfterInventoryHash, otherProof.AfterInventoryHash);
        Assert.Equal(originalProof.Command.ContentHash, otherProof.Command.ContentHash);
        Assert.Equivalent(originalProof.Batch, otherProof.Batch, true);
        Assert.NotEqual(originalProof.BeforeProductionHash, otherProof.BeforeProductionHash);
        Assert.Equal(left.Production.Continuation!.ContentHash, right.Production.Continuation!.ContentHash);
        byte[][] root = Fields(ProductionCodecV5.Encode(left.Production));
        var operations = Fields(root[12]);
        var completion = Fields(operations[^1]);
        completion[5] = otherProof.ContentHash.ToByteArray();
        operations[^1] = Frame(completion);
        root[12] = Frame(operations);
        var structurallyBound = ProductionCodecV5.Decode(Frame(root), left.Production.Continuation!, [], [otherProof]);
        var failure = Assert.Throws<ArgumentException>(() => structurallyBound.ValidateForAuthority(left.Inventory));
        Assert.Equal("Construction retirement requires actual matching completion evidence and controller.", failure.Message);
        left.Validate();
        right.Validate();
    }

    [Fact]
    public void Initial_selection_requires_opaque_admitted_construction_and_exact_actual_material_basis()
    {
        var method = Assert.Single(typeof(ProductionSnapshot).GetMethods(), value => value.Name == "ContinueConstruction");
        Assert.Equal(typeof(MaterialConstructionState), method.GetParameters()[1].ParameterType);
        Assert.DoesNotContain(typeof(MaterialConstructionState).GetConstructors(), constructor => constructor.IsPublic);
        var journey = new ConstructionContinuationFixture();
        var actual = MaterialConstructionState.Capture(journey.LegacyWorld);
        var genuine = journey.LegacyWorld.Production!.ContinueConstruction(journey.LegacyWorld.Inventory, actual);
        Assert.Single(genuine.Continuation!.ConstructionBatches);
        journey.TransferMovable(); // An actual material change cannot be paired with the older world capture.
        Assert.NotEqual(journey.LegacyWorld.Inventory.ContentHash, journey.Inventory.ContentHash);
        Assert.Throws<ArgumentException>(() => journey.LegacyWorld.Production!.ContinueConstruction(journey.Inventory, actual));
        journey.Validate();
    }

    [Fact]
    public void Reconstruction_requires_exact_opaque_completion_evidence_and_the_original_cut_context()
    {
        var journey = new ConstructionContinuationFixture(readyToComplete: true);
        journey.Work();
        var production = journey.Production;
        byte[] bytes = ProductionCodecV5.Encode(production);
        var context = production.Continuation!;
        var proof = Assert.Single(production.MaterialHistory!.ConstructionEvidence);
        var restored = ProductionCodecV5.Decode(bytes, context, [], [proof]);
        restored.ValidateForAuthority(journey.Inventory);
        Assert.Equal(bytes, ProductionCodecV5.Encode(restored));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV5.Decode(bytes, context, [], []));
        var other = new ConstructionContinuationFixture(readyToComplete: true);
        other.Advance();
        other.Work();
        var otherProof = Assert.Single(other.Production.MaterialHistory!.ConstructionEvidence);
        Assert.NotEqual(proof.ContentHash, otherProof.ContentHash);
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV5.Decode(bytes, context, [], [otherProof]));
        Assert.DoesNotContain(typeof(ConstructionCompletionEvidence).GetConstructors(), constructor => constructor.IsPublic);
        journey.Cut();
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV5.Decode(bytes, journey.Production.Continuation!, [], [proof]));
        Assert.Equal(bytes, ProductionCodecV5.Encode(production));
    }

    [Theory]
    [InlineData("evidence-hash")]
    [InlineData("command")]
    [InlineData("ingredient-revision")]
    [InlineData("ingredient-lot")]
    [InlineData("batch-actor")]
    public void Altered_completion_or_exact_source_batch_witness_is_not_reinterpreted_as_ordinary_crafting(string change)
    {
        var journey = new ConstructionContinuationFixture(readyToComplete: true);
        journey.Work();
        byte[] original = ProductionCodecV5.Encode(journey.Production);
        byte[][] root = Fields(original);
        if (change is "evidence-hash" or "command")
        {
            var operations = Fields(root[12]);
            var operation = Fields(operations[1]);
            if (change == "evidence-hash") operation[5][0] ^= 0x80;
            else operation[4] = Id(800_000).ToByteArray();
            operations[1] = Frame(operation);
            root[12] = Frame(operations);
        }
        else
        {
            var batches = Fields(root[9]);
            var batch = Fields(batches[1]);
            if (change == "batch-actor") batch[4] = Player.ToByteArray();
            else
            {
                var ingredients = Fields(batch[8]);
                var ingredient = Fields(ingredients[1]);
                if (change == "ingredient-revision") ingredient[4] = Number(99);
                else ingredient[3] = Id(800_001).ToByteArray();
                ingredients[1] = Frame(ingredient);
                batch[8] = Frame(ingredients);
            }
            batches[1] = Frame(batch);
            root[9] = Frame(batches);
        }
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV5.Decode(Frame(root), journey.Production.Continuation!, [],
            journey.Production.MaterialHistory!.ConstructionEvidence));
        Assert.Equal(original, ProductionCodecV5.Encode(journey.Production));
        journey.Validate();
    }

    [Fact]
    public void Previously_used_completion_cannot_be_reissued_after_a_cut_with_new_sequence_tokens()
    {
        var journey = new ConstructionContinuationFixture(readyToComplete: true);
        journey.Work();
        var proof = Assert.Single(journey.Production.MaterialHistory!.ConstructionEvidence);
        byte[][] old = Fields(ProductionCodecV5.Encode(journey.Production));
        var oldOperation = Fields(Fields(old[12])[1]);
        journey.Cut();
        byte[][] root = Fields(ProductionCodecV5.Encode(journey.Production));
        oldOperation[3] = Number(journey.Production.Revision + 1);
        root[4] = Number(journey.Production.Revision + 1);
        root[6] = Number(journey.Production.TotalBatchCount + 1);
        root[8] = Number(journey.Production.TotalMaterialOperationCount + 1);
        root[9] = old[9];
        root[12] = Frame([Fields(root[12])[0], Frame(oldOperation)]);
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV5.Decode(Frame(root), journey.Production.Continuation!, [], [proof]));
        Assert.Equal(2UL, journey.Production.TotalBatchCount);
        Assert.Equal(2, journey.Space.Structures.Count);
        journey.Validate();
    }

    [Fact]
    public void Wrong_installed_guard_and_dangling_historical_producer_fail_the_component_join()
    {
        var journey = new ConstructionContinuationFixture(readyToComplete: true);
        journey.Work();
        journey.Cut();
        var second = journey.Construction.FindProject(ProjectB)!;
        foreach (var wrong in new[]
        {
            second.Installed! with { ProductionBatchId = Id(800_002) },
            second.Installed! with { GuardLotRevision = 1 }
        })
        {
            // Supplied invalid reconstruction proposal, never adopted as earned state.
            Assert.Throws<ArgumentException>(() =>
            {
                var changed = new ConstructionProject(second.ProjectId, second.Definition, second.OwnerActorId,
                    second.BuilderActorId, second.QuestId, second.ProjectCustodianId, second.ReservationId,
                    second.SiteFeet, second.WorkFeet, second.RequiredAccess, second.Revision, second.Progress, wrong,
                    second.StockLocation);
                var proposed = new ConstructionSnapshot(journey.Construction.WorldId, journey.Construction.BranchId,
                    journey.Construction.Revision, journey.Construction.Definitions,
                    journey.Construction.Projects.Select(project => project.ProjectId == changed.ProjectId ? changed : project));
                proposed.ValidateForMaterialContinuation(journey.SourceWorld, journey.Inventory, journey.Production,
                    journey.Space, ConstructionPhysicalSnapshot.Create(proposed));
            });
        }
        journey.Validate();
    }

    [Fact]
    public void Capability_is_inherited_without_downgrade_and_legacy_bytes_keep_their_explicit_interpretation()
    {
        var journey = new ConstructionContinuationFixture();
        var original = journey.LegacyWorld.Production!;
        byte[] legacy = ProductionCodecV1.Encode(original);
        byte[] v5 = ProductionCodecV5.Encode(journey.Production);
        Assert.Equal(legacy, ProductionCodecV1.Encode(ProductionCodecV1.Decode(legacy)));
        Assert.Equal(legacy, ProductionCodecV1.Encode(original));
        Assert.Throws<ArgumentException>(() => ProductionCodecV1.Encode(journey.Production));
        Assert.Throws<ArgumentException>(() => ProductionCodecV2.Encode(journey.Production));
        Assert.Throws<ArgumentException>(() => ProductionCodecV3.Encode(journey.Production));
        Assert.Throws<ArgumentException>(() => ProductionCodecV4.Encode(journey.Production));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV1.Decode(v5));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV2.Decode(v5));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV3.Decode(v5, journey.Production.Continuation!));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV4.Decode(v5, journey.Production.Continuation!, []));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV5.Decode(legacy, journey.Production.Continuation!, [], []));
        var stillConstruction = journey.Production.ContinueMaterial(journey.Inventory);
        Assert.True(stillConstruction.Continuation!.SupportsConstruction);
        Assert.Equivalent(journey.Production.Continuation!.ConstructionBatches,
            stillConstruction.Continuation!.ConstructionBatches, true);
        Assert.Equal(journey.Production.Revision, stillConstruction.Revision);
        Assert.Equal(journey.Production.TotalBatchCount, stillConstruction.TotalBatchCount);
        Assert.Throws<ArgumentException>(() => ProductionCodecV4.Encode(stillConstruction));
        stillConstruction.ValidateForAuthority(journey.Inventory);
        journey.Cut();
        Assert.True(journey.Production.Continuation!.SupportsConstruction);
        Assert.False(journey.Production.SupportsConsumption);
        Assert.Single(journey.Production.Continuation!.ConstructionBatches);
        Assert.Equal(original.TotalBatchCount, journey.Production.TotalBatchCount);
        Assert.Equal(original.Revision, journey.Production.Revision);
        Assert.Equal(legacy, ProductionCodecV1.Encode(original));
        // Round-trip/source preservation, not a new pre-change golden-vector claim.
        Assert.Throws<ArgumentException>(() => new WorldSnapshot(journey.Inventory, journey.Space,
            journey.SourceWorld.Residents, journey.SourceWorld.AdmissionVersion, journey.SourceWorld.Calendar,
            journey.SourceWorld.Revision, journey.SourceWorld.AdvancingSteps, journey.SourceWorld.Dialogues,
            journey.SourceWorld.Gathering, journey.Production, journey.SourceWorld.Progression, journey.SourceWorld.Quests,
            journey.Construction, excavation: journey.SourceWorld.Excavation, constructionPhysical: journey.Physical));
    }

    private static byte[][] Fields(byte[] bytes)
    {
        Assert.True(bytes.AsSpan(0, 4).SequenceEqual("OFC\0"u8));
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
