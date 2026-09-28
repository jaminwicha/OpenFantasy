using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ExcavationContinuationCodecTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(3, false)]
    [InlineData(1, true)]
    public void Actual_work_archives_require_every_opaque_proof_and_exact_owner(int swings, bool gather)
    {
        var journey = new MaterialExcavationFixture();
        journey.Cut();
        for (int index = 0; index < swings; index++) journey.Work();
        if (gather) journey.Gather();
        var state = journey.State;
        var evidence = state.Production.MaterialHistory!.ExcavationEvidence;
        Assert.Equal(swings + (gather ? 1 : 0), evidence.Count);
        Assert.DoesNotContain(typeof(ExcavationWorkEvidence).GetConstructors(), constructor => constructor.IsPublic);
        var dependencies = new ExcavationProductionDependencies(state, evidence);
        byte[] production = ProductionCodecV8.Encode(state.Production);
        var restoredProduction = ProductionCodecV8.Decode(production, state.Production.Continuation!, evidence);
        restoredProduction.ValidateForAuthority(state.Inventory);
        Assert.Equal(production, ProductionCodecV8.Encode(restoredProduction));
        byte[] bytes = MaterialExcavationCodecV1.Encode(state);
        var restored = MaterialExcavationCodecV1.Decode(bytes, dependencies, TestContext.Current.CancellationToken);
        Assert.Equal(state.ContentHash, restored.ContentHash);
        Assert.Equal(bytes, MaterialExcavationCodecV1.Encode(restored));
        Assert.ThrowsAny<FormatException>(() => ProductionCodecV8.Decode(production, state.Production.Continuation!, []));
        Assert.ThrowsAny<FormatException>(() => MaterialExcavationCodecV1.Decode(bytes, new(state), TestContext.Current.CancellationToken));
        if (evidence.Count > 1)
            Assert.ThrowsAny<FormatException>(() => MaterialExcavationCodecV1.Decode(bytes,
                new(state, evidence.Take(evidence.Count - 1).ToArray()), TestContext.Current.CancellationToken));

        journey.Cut();
        byte[] closed = MaterialExcavationCodecV1.Encode(journey.State);
        Assert.Equal(closed, MaterialExcavationCodecV1.Encode(MaterialExcavationCodecV1.Decode(closed,
            new(journey.State), TestContext.Current.CancellationToken)));
        Assert.ThrowsAny<FormatException>(() => MaterialExcavationCodecV1.Decode(bytes,
            new(journey.State, evidence), TestContext.Current.CancellationToken));
        Assert.Equal(bytes, MaterialExcavationCodecV1.Encode(state));
    }

    [Fact]
    public void Genuine_sibling_work_evidence_cannot_launder_a_different_produced_history_prefix()
    {
        var journey = Sibling(900_000);
        var sibling = Sibling(900_001);
        var proof = Assert.Single(journey.Production.MaterialHistory!.ExcavationEvidence);
        var other = Assert.Single(sibling.Production.MaterialHistory!.ExcavationEvidence);
        Assert.Equal(proof.BeforeInventoryHash, other.BeforeInventoryHash);
        Assert.Equal(proof.AfterInventoryHash, other.AfterInventoryHash);
        Assert.Equal(proof.BeforeBoundaryHash, other.BeforeBoundaryHash);
        Assert.NotEqual(proof.BeforeProductionHash, other.BeforeProductionHash);
        Assert.Equal(journey.Production.Continuation!.ContentHash, sibling.Production.Continuation!.ContentHash);
        byte[] bytes = ProductionCodecV8.Encode(journey.Production);
        Assert.ThrowsAny<FormatException>(() => ProductionCodecV8.Decode(bytes, journey.Production.Continuation, [other]));
        // Resolve the genuine sibling witness first: rejection must then concern its actual
        // preceding material prefix, rather than merely failing to find a proof by identity.
        byte[] hostile = ReplaceProof(bytes, proof, other);
        var decoded = ProductionCodecV8.Decode(hostile, journey.Production.Continuation, [other]);
        Assert.Equal(hostile, ProductionCodecV8.Encode(decoded));
        Assert.Throws<ArgumentException>(() => decoded.ValidateForAuthority(journey.Inventory));
        byte[] archive = MaterialExcavationCodecV1.Encode(journey.State);
        Assert.ThrowsAny<FormatException>(() => MaterialExcavationCodecV1.Decode(ReplaceProof(archive, proof, other),
            new(journey.State, [other]), TestContext.Current.CancellationToken));
        Assert.Equal(bytes, ProductionCodecV8.Encode(journey.Production));
        Assert.Equal(archive, MaterialExcavationCodecV1.Encode(journey.State));
    }

    [Fact]
    public void Explicit_V8_archives_refuse_downgrades_malformed_frames_and_cancellation()
    {
        var journey = new MaterialExcavationFixture();
        journey.Cut();
        journey.Work();
        var production = journey.Production;
        Assert.True(production.Continuation!.SupportsExcavation);
        Assert.False(production.SupportsConsumption);
        Assert.Throws<ArgumentException>(() => ProductionCodecV1.Encode(production));
        Assert.Throws<ArgumentException>(() => ProductionCodecV2.Encode(production));
        Assert.Throws<ArgumentException>(() => ProductionCodecV3.Encode(production));
        Assert.Throws<ArgumentException>(() => ProductionCodecV4.Encode(production));
        Assert.Throws<ArgumentException>(() => ProductionCodecV5.Encode(production));
        Assert.Throws<ArgumentException>(() => ProductionCodecV6.Encode(production));
        Assert.Throws<ArgumentException>(() => ProductionCodecV7.Encode(production));
        Assert.Throws<ArgumentException>(() => production.Continue(journey.Inventory));
        Assert.Throws<ArgumentException>(() => production.ContinueMaterial(journey.Inventory, journey.Gathering));
        Assert.Throws<ArgumentException>(() => production.ContinueConstruction(journey.Inventory,
            MaterialConstructionState.Capture(journey.InitialWorld), journey.Gathering));
        Assert.Throws<ArgumentException>(() => new MaterialGatherPlanner().Plan(journey.SourceWorld,
            journey.Inventory, production, journey.Gathering, journey.GatherCommand(), journey.Occupied));
        byte[] bytes = MaterialExcavationCodecV1.Encode(journey.State);
        var dependencies = new ExcavationProductionDependencies(journey.State, production.MaterialHistory!.ExcavationEvidence);
        Assert.ThrowsAny<FormatException>(() => MaterialExcavationCodecV1.Decode(bytes[..^1], dependencies, TestContext.Current.CancellationToken));
        Assert.ThrowsAny<FormatException>(() => MaterialExcavationCodecV1.Decode([.. bytes, 0], dependencies, TestContext.Current.CancellationToken));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => MaterialExcavationCodecV1.Decode(bytes, dependencies, canceled.Token));
        Assert.Equal(bytes, MaterialExcavationCodecV1.Encode(journey.State));
    }

    [Fact]
    public void Older_actual_owner_dependencies_cannot_restore_new_partial_work_even_with_unchanged_custody()
    {
        var journey = new MaterialExcavationFixture();
        journey.Cut();
        var before = journey.State;
        journey.Work();
        var state = journey.State;
        Assert.Equal(before.Inventory.ContentHash, state.Inventory.ContentHash);
        Assert.NotEqual(before.Excavation.ContentHash, state.Excavation.ContentHash);
        byte[] bytes = MaterialExcavationCodecV1.Encode(state);
        var evidence = state.Production.MaterialHistory!.ExcavationEvidence;
        Assert.ThrowsAny<FormatException>(() => MaterialExcavationCodecV1.Decode(bytes,
            new(before, evidence), TestContext.Current.CancellationToken));
        Assert.Equal(bytes, MaterialExcavationCodecV1.Encode(MaterialExcavationCodecV1.Decode(bytes,
            new(state, evidence), TestContext.Current.CancellationToken)));
    }

    private static MaterialExcavationFixture Sibling(ulong craftCause)
    {
        var journey = new MaterialExcavationFixture(secondSite: true, legacyWork: 3);
        journey.Cut();
        journey.Craft(IngredientLotKind.Source, journey.Extracted().LotId, 4, MaterialExcavationFixture.Id(craftCause));
        journey.Work(MaterialExcavationFixture.SecondSiteId);
        return journey;
    }

    private static byte[] ReplaceProof(byte[] bytes, ExcavationWorkEvidence original, ExcavationWorkEvidence replacement)
    {
        byte[] result = bytes.ToArray();
        byte[] before = original.ContentHash.ToByteArray(), after = replacement.ContentHash.ToByteArray();
        int replacements = 0;
        for (int offset = 0; offset <= result.Length - before.Length; offset++)
        {
            if (!result.AsSpan(offset, before.Length).SequenceEqual(before)) continue;
            after.CopyTo(result, offset);
            replacements++;
            offset += before.Length - 1;
        }
        Assert.True(replacements > 0);
        return result;
    }
}
