using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>
/// Observes actual complete profile 26 transactions. Every observed decision is compared with the
/// ordinary decision of the same batch; the journeys are the existing earned ones, never a reduced world.
/// </summary>
public sealed class LiveMaterialEvidenceTests
{
    [Fact]
    public void Acknowledgment_reports_nothing_and_the_administered_dose_pins_its_admitted_candidate()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(fixture.Genesis);
        var defeated = fixture.Journey.PrepareDefeat(session);
        AssertCompleteGraph(defeated);
        Assert.Empty(Observe(session, fixture.Journey.Batch(defeated, WorldBatchKind.Care,
            [fixture.Journey.Acknowledge(defeated)])).MaterialEvidence);

        var pending = session.ReadStateForHost();
        var command = fixture.Journey.Treat(pending);
        var batch = fixture.Journey.Batch(pending, WorldBatchKind.Care, [command]);
        var decision = Observe(session, batch);

        var evidence = Assert.Single(decision.MaterialEvidence);
        AssertBound(evidence, pending, batch, 0, WorldMaterialEvidenceKind.AftermathRemedy);
        Assert.Equal(evidence.InputWorldHash, evidence.SourceWorldHash);
        // Care applies no later step process, so the pinned transition ends at the final root.
        Assert.Equal(WorldMaterialOwnerPins.Observe(decision.Candidate), evidence.After);
        Assert.Equal(new[] { EntityId.DeriveSpawned(command.CommandId, 0), EntityId.DeriveSpawned(command.CommandId, 1) },
            evidence.CreatedIds.ToArray());
        Assert.NotEqual(evidence.Before.Progression, evidence.After.Progression);
        Assert.NotEqual(evidence.Before.Care, evidence.After.Care);
        AssertGraphPreserved(evidence);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Raw_self_use_and_real_crafted_aid_match_ordinary_execution_and_pin_each_transition(bool self)
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(fixture.Genesis);
        var state = fixture.ReachInjuredExploration(session);
        AssertCompleteGraph(state);
        if (self)
        {
            state = fixture.Supply(session, self: true);
        }
        else
        {
            var craftBatch = fixture.Journey.Batch(state, WorldBatchKind.AdvancingStep, [Craft(fixture, state)]);
            var crafted = Observe(session, craftBatch);
            var made = Assert.Single(crafted.MaterialEvidence);
            AssertBound(made, state, craftBatch, 0, WorldMaterialEvidenceKind.Craft);
            AssertSettledOwners(made, crafted);
            Assert.NotEqual(made.Before.Production, made.After.Production);
            Assert.Equal(made.Before.Progression, made.After.Progression);
            AssertGraphPreserved(made);
            state = session.ReadStateForHost();
        }

        EntityId actor = self ? fixture.Player : fixture.Helper;
        var batch = fixture.Journey.Batch(state, WorldBatchKind.AdvancingStep,
            [fixture.Journey.Command(state, actor, fixture.Intent(state, self))]);
        var decision = Observe(session, batch);

        var evidence = Assert.Single(decision.MaterialEvidence);
        AssertBound(evidence, state, batch, 0, WorldMaterialEvidenceKind.RestorativeUse);
        Assert.Equal(evidence.InputWorldHash, evidence.SourceWorldHash);
        AssertSettledOwners(evidence, decision);
        Assert.NotEqual(evidence.Before.Progression, evidence.After.Progression);
        Assert.NotEqual(evidence.Before.Care, evidence.After.Care);
        Assert.NotEqual(evidence.Before.Production, evidence.After.Production);
        AssertGraphPreserved(evidence);
        Assert.NotEqual(evidence.ContentHash, evidence.Before.ContentHash);
    }

    [Fact]
    public void Refused_use_keeps_the_exact_ordinary_refusal_and_reports_no_observation()
    {
        var fixture = new OrdinaryCareWorldFixture();
        var session = new CareWorldSession(fixture.Genesis);
        fixture.ReachInjuredExploration(session);
        var state = fixture.Supply(session, self: true);
        var accepted = fixture.Journey.Command(state, fixture.Player, fixture.Intent(state, self: true));
        var stale = new WorldActionCommand(accepted.CommandId, accepted.PrincipalId, accepted.OriginSessionId,
            accepted.ActorId, accepted.WorldId, accepted.BranchId, accepted.ExpectedActorRevision + 1, accepted.Intent);

        var decision = Observe(session, fixture.Journey.Batch(state, WorldBatchKind.AdvancingStep, [stale]));

        Assert.NotEqual(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
        Assert.Empty(decision.MaterialEvidence);
        Assert.Equal(WorldMaterialOwnerPins.Observe(state).Inventory, WorldMaterialOwnerPins.Observe(decision.Candidate).Inventory);
    }

    [Fact]
    public void Archival_cut_preserves_the_observation_and_separates_the_selected_and_interpreted_roots()
    {
        var fixture = new OrdinaryCareArchiveFixture();
        var session = new CareWorldSession(fixture.Genesis);
        var state = fixture.Prepare255(session);
        AssertCompleteGraph(state);
        // One potential writer still fits at 255. Earn the last entry with a real finite dose;
        // only the following selected use/refusal needs the existing resource archive cut.
        var filling = Observe(session, fixture.Journey.Batch(state, WorldBatchKind.AdvancingStep,
            [fixture.Journey.Command(state, fixture.Journey.Helper, fixture.Aid(state))]));
        Assert.Null(filling.SealedResourceSegment);
        state = session.ReadStateForHost();
        Assert.Equal(256, state.ResourceHistory!.History.Batches.Count);
        var occupied = session.OccupiedIdentities.ToHashSet();
        var processor = new WorldStepProcessor();

        var refusedBatch = fixture.Journey.Batch(state, WorldBatchKind.AdvancingStep,
            [fixture.Journey.Command(state, fixture.Journey.Player, fixture.UnownedSelfDose(state))]);
        var refused = processor.DecideWithMaterialEvidence(state, refusedBatch, occupied.Contains);
        AssertExact(processor.Decide(state, refusedBatch, occupied.Contains), refused);
        Assert.NotEqual(WorldActionRejection.None, Assert.Single(refused.Receipts).Rejection);
        Assert.NotNull(refused.SealedResourceSegment);
        Assert.Empty(refused.MaterialEvidence);

        var batch = fixture.Journey.Batch(state, WorldBatchKind.AdvancingStep,
            [fixture.Journey.Command(state, fixture.Journey.Helper, fixture.Aid(state))]);
        var decision = Observe(session, batch);

        Assert.NotNull(decision.SealedResourceSegment);
        var evidence = Assert.Single(decision.MaterialEvidence);
        AssertBound(evidence, state, batch, 0, WorldMaterialEvidenceKind.RestorativeUse);
        // The interpreted root carries the archive anchor and is never the selected durable root.
        Assert.NotEqual(evidence.InputWorldHash, evidence.SourceWorldHash);
        AssertSettledOwners(evidence, decision);
        AssertGraphPreserved(evidence);
    }

    [Fact]
    public void Earlier_profiles_are_refused_by_the_explicit_binding_and_ordinary_decisions_carry_none()
    {
        var fixture = new CareWorldFixture();
        Assert.NotEqual((ushort)26, fixture.Genesis.ProfileVersion);
        var processor = new WorldStepProcessor();

        Assert.Throws<InvalidOperationException>(() => processor.DecideWithMaterialEvidence(fixture.Genesis,
            fixture.Batch(fixture.Genesis, WorldBatchKind.AdvancingStep)));
        Assert.Throws<InvalidOperationException>(() => WorldMaterialOwnerPins.Observe(fixture.Genesis));
        Assert.Empty(processor.Decide(fixture.Genesis, fixture.Batch(fixture.Genesis, WorldBatchKind.AdvancingStep)).MaterialEvidence);
    }

    /// <summary>Observes the batch purely, then commits it through the ordinary path and compares both.</summary>
    private static WorldStepDecision Observe(CareWorldSession session, WorldStepBatch batch)
    {
        var occupied = session.OccupiedIdentities.ToHashSet();
        var observed = new WorldStepProcessor().DecideWithMaterialEvidence(session.ReadStateForHost(), batch, occupied.Contains);
        AssertExact(session.Commit(batch), observed);
        return observed;
    }

    private static void AssertExact(WorldStepDecision ordinary, WorldStepDecision observed)
    {
        Assert.Empty(ordinary.MaterialEvidence);
        Assert.Equal(WorldSnapshotCodec.Encode(ordinary.Candidate), WorldSnapshotCodec.Encode(observed.Candidate));
        Assert.Equal(ordinary.Receipts.Select(receipt => Convert.ToHexString(WorldReceiptCodecV1.Encode(receipt))),
            observed.Receipts.Select(receipt => Convert.ToHexString(WorldReceiptCodecV1.Encode(receipt))));
        Assert.Equal(ordinary.Identities, observed.Identities);
        Assert.Equal(ordinary.SealedResourceSegment is null, observed.SealedResourceSegment is null);
    }

    private static void AssertBound(WorldMaterialActionEvidence evidence, WorldSnapshot source, WorldStepBatch batch,
        int ordinal, WorldMaterialEvidenceKind kind)
    {
        Assert.Equal(source.WorldId, evidence.WorldId);
        Assert.Equal(source.BranchId, evidence.BranchId);
        Assert.Equal(source.ContentHash, evidence.InputWorldHash);
        Assert.Equal(source.Revision, evidence.SourceWorldRevision);
        Assert.Equal(source.Revision + 1, evidence.PublicationSequence);
        Assert.Equal(ordinal, evidence.ActionOrdinal);
        Assert.Equal(batch.Actions[ordinal].ContentHash, evidence.Command.ContentHash);
        Assert.Equal(kind, evidence.Kind);
        Assert.Equal(WorldMaterialOwnerPins.Observe(source), evidence.Before);
        Assert.NotEqual(evidence.Before, evidence.After);
    }

    /// <summary>The selected step changes time, gathering and liquid only; these owners remain as pinned.</summary>
    private static void AssertSettledOwners(WorldMaterialActionEvidence evidence, WorldStepDecision decision)
    {
        var final = WorldMaterialOwnerPins.Observe(decision.Candidate);
        Assert.Equal(final.Inventory, evidence.After.Inventory);
        Assert.Equal(final.Production, evidence.After.Production);
        Assert.Equal(final.Progression, evidence.After.Progression);
        Assert.Equal(final.Care, evidence.After.Care);
        Assert.Equal(final.Quests, evidence.After.Quests);
        Assert.Equal(final.Construction, evidence.After.Construction);
        Assert.Equal(final.Excavation, evidence.After.Excavation);
        Assert.Equal(decision.Identities.Where(value => value.Kind == WorldCommittedIdentityKind.SpawnedEntity)
            .Select(value => value.Identity), evidence.CreatedIds.OrderBy(value => value));
    }

    private static void AssertCompleteGraph(WorldSnapshot world)
    {
        Assert.Equal((ushort)26, world.ProfileVersion);
        Assert.NotNull(world.Excavation);
        Assert.NotNull(world.Construction);
        Assert.NotNull(world.Quests);
        Assert.NotNull(world.Gathering);
        Assert.NotNull(world.Reservoirs);
        Assert.NotNull(world.ReservoirSeals);
        Assert.NotNull(world.LiquidCells?.Topology);
        Assert.NotNull(world.EncounterLifecycle);
    }

    private static void AssertGraphPreserved(WorldMaterialActionEvidence evidence)
    {
        foreach (var pins in new[] { evidence.Before, evidence.After })
        {
            Assert.All(new[] { pins.Inventory, pins.Production, pins.Gathering, pins.Excavation, pins.Construction, pins.Space,
                pins.Quests, pins.Progression, pins.Care, pins.Reservoirs, pins.LiquidCells }, Assert.NotNull);
        }
        Assert.Equal(evidence.Before.Excavation, evidence.After.Excavation);
        Assert.Equal(evidence.Before.Construction, evidence.After.Construction);
        Assert.Equal(evidence.Before.Quests, evidence.After.Quests);
        Assert.Equal(evidence.Before.Space, evidence.After.Space);
    }

    private static WorldActionCommand Craft(OrdinaryCareWorldFixture fixture, WorldSnapshot state)
    {
        var storage = state.GetResident(fixture.Helper).InventoryCustodianId;
        var recipe = state.Production!.Recipes.Single(value => value.PhysicalRecipe.Key == CareJourneyAdmission.RecipeKey);
        var input = Assert.Single(recipe.Inputs);
        var lot = state.Inventory.Custody.Lots.Single(value => value.CustodianId == storage && value.Definition == recipe.ItemFor(input));
        return fixture.Journey.Command(state, fixture.Helper, new CraftIntent(new(recipe.DefinitionId, recipe.Version,
            state.Production.Revision, storage, state.Inventory.Custody.GetStorage(storage).Revision,
            [new(IngredientLotKind.Source, lot.LotId, lot.Revision, input.Count)])));
    }
}
