using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Production;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed partial class ConsentedWorldPublicationTests
{
    [Fact]
    public void Crafted_split_gift_recraft_ordinary_care_and_onward_gift_survive_two_decoded_cuts()
    {
        var fixture = new ConsentedPotionFixture();
        var ordinary = new OrdinaryCareWorldFixture(fixture.Genesis);
        var journey = ordinary.Journey;
        var session = new CareWorldSession(WorldConsentedMaterialAdmission.CreateFresh(fixture.Genesis));
        // Extra actual wolf damage leaves room for meaningful use both before and after the cut.
        WorldSnapshot state = ordinary.ReachFullDoseInjuredExploration(session);
        Assert.InRange(state.Progression!.GetActor(journey.Player).CurrentHp, 1, 19);

        EntityId helperBag = state.GetResident(journey.Helper).InventoryCustodianId;
        EntityId playerBag = state.GetResident(journey.Player).InventoryCustodianId;
        var berries = state.Inventory.Custody.Lots.Single(value => value.CustodianId == helperBag
            && value.Definition == fixture.Supply.Berries);
        WorldActionCommand craft = CraftCommand(state, journey, journey.Helper, fixture.Bulk,
            new(IngredientLotKind.Source, berries.LotId, berries.Revision, 12));
        state = CareRewardJourney.Commit(session, journey.Batch(state, WorldBatchKind.AdvancingStep, [craft])).Candidate;
        ProductionBatch producer = state.Production!.Batches.Single(value => value.CommandId == craft.CommandId);
        TransformedLot stock = state.Production.Lots.Single(value => value.Output.BatchId == producer.BatchId);
        Assert.Equal((journey.Helper, helperBag, 4UL), (producer.ActorId, stock.CustodianId, stock.Quantity));

        var terms = new ConsentedMaterialTerms(state.WorldId, state.BranchId, journey.Helper, journey.Player,
            ConsentedMaterialLotKind.Produced, stock.LotId, stock.Definition, helperBag, playerBag, 3, stock.Revision);
        var pair = Pair(state, journey, terms);
        var diagnostic = new WorldConsentedMaterialPlanner().Plan(state, pair.Offer, pair.Acceptance,
            WorldBatchKind.AdvancingStep,
            new TrustedCommandDelivery(pair.Offer.PrincipalId, pair.Offer.OriginSessionId),
            new TrustedCommandDelivery(pair.Acceptance.PrincipalId, pair.Acceptance.OriginSessionId),
            session.OccupiedIdentities.Contains);
        Assert.True(diagnostic.IsApplied, $"Decision refused: {diagnostic.OfferRejection}/{diagnostic.AcceptanceRejection}; "
            + $"recipient units {state.Production.GetCombinedStoredQuantity(state.Inventory, playerBag)}"
            + $"/{state.Inventory.Custody.GetStorage(playerBag).UnitCapacity}.");
        var gifted = session.Commit(journey.Batch(state, WorldBatchKind.AdvancingStep,
            [pair.Offer, pair.Acceptance]));
        Assert.Equal(2, gifted.Receipts.Count);
        Assert.All(gifted.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
        state = gifted.Candidate;
        EntityId childId = EntityId.DeriveSpawned(pair.Offer.CommandId, 0);
        Assert.Contains(gifted.Identities, value => value.Identity == childId
            && value.Kind == WorldCommittedIdentityKind.SpawnedEntity);
        TransformedLot child = state.Production!.Lots.Single(value => value.LotId == childId);
        Assert.Equal((playerBag, producer.BatchId, 0UL, 3UL),
            (child.CustodianId, child.Output.BatchId, child.Output.Start, child.Output.End));
        Assert.Equal(1UL, state.Production.Lots.Single(value => value.LotId == stock.LotId).Quantity);
        AssertInvalidEvidenceRejectedByWorld(state);

        WorldActionCommand recraft = CraftCommand(state, journey, journey.Player, fixture.Repack,
            new(IngredientLotKind.Transformed, childId, child.Revision, 1));
        state = CareRewardJourney.Commit(session, journey.Batch(state, WorldBatchKind.AdvancingStep, [recraft])).Candidate;
        ProductionBatch repacked = state.Production!.Batches.Single(value => value.CommandId == recraft.CommandId);
        RetiredIngredient input = Assert.Single(repacked.Ingredients);
        Assert.Equal((childId, producer.BatchId, 0UL, 1UL),
            (input.LotId, input.Output!.BatchId, input.Output.Start, input.Output.End));
        var dose = state.Production.Lots.Single(value => value.Output.BatchId == repacked.BatchId);
        var prepared = Assert.IsType<UseRestorativeItemIntent>(WorldRestorativeActions.Prepare(state,
            journey.Player, journey.Player, fixture.Self.DefinitionId, null));
        var use = new UseRestorativeItemIntent(prepared.RecipientActorId, prepared.UseDefinitionId,
            prepared.UseDefinitionVersion, prepared.PermissionId, prepared.PermissionGrantVersion,
            prepared.ExpectedPermissionRevision, prepared.ExpectedPatientPresenceRevision,
            prepared.ExpectedPatientProgressionRevision, prepared.ExpectedStorageRevision,
            prepared.ExpectedProductionRevision, new(IngredientLotKind.Transformed, dose.LotId, dose.Revision, 1));
        int beforeHp = state.Progression!.GetActor(journey.Player).CurrentHp;
        WorldActionCommand consume = journey.Command(state, journey.Player, use);
        state = CareRewardJourney.Commit(session, journey.Batch(state, WorldBatchKind.AdvancingStep, [consume])).Candidate;
        Assert.True(state.Progression!.GetActor(journey.Player).CurrentHp > beforeHp);
        Assert.Single(state.Care!.Ordinary!.Uses);
        Assert.DoesNotContain(state.Production!.Lots, value => value.LotId == dose.LotId);
        var consumed = state.Production.ConsumedItems.Single(value => value.CommandId == consume.CommandId);
        Assert.Equal(repacked.BatchId, consumed.Ingredient.Output!.BatchId);
        Assert.Equal(2UL, state.Production.Lots.Single(value => value.LotId == childId).Quantity);

        WorldMaterialCareArchiveSegment first = CutAndDecode(session, null, out WorldSnapshot reopened);
        Assert.Empty(reopened.Coordination!.ConsentedMaterial!.Transfers);
        Assert.Equal(producer.ActorId, reopened.Production!.Continuation!.Basis.BatchWitnesses
            .Single(value => value.BatchId == producer.BatchId).ActorId);
        session = new(reopened, session.OccupiedIdentities);
        child = reopened.Production.Lots.Single(value => value.LotId == childId);
        var onwardTerms = new ConsentedMaterialTerms(reopened.WorldId, reopened.BranchId, journey.Player,
            journey.Helper, ConsentedMaterialLotKind.Produced, child.LotId, child.Definition, playerBag,
            helperBag, 1, child.Revision);
        var onward = Pair(reopened, journey, onwardTerms);
        state = CareRewardJourney.Commit(session, journey.Batch(reopened, WorldBatchKind.AdvancingStep,
            [onward.Offer, onward.Acceptance])).Candidate;
        var returned = state.Production!.Lots.Single(value => value.LotId == EntityId.DeriveSpawned(onward.Offer.CommandId, 0));
        Assert.Equal((producer.BatchId, 1UL, 2UL, helperBag),
            (returned.Output.BatchId, returned.Output.Start, returned.Output.End, returned.CustodianId));
        Assert.Equal((2UL, 3UL), (state.Production.Lots.Single(value => value.LotId == childId).Output.Start,
            state.Production.Lots.Single(value => value.LotId == childId).Output.End));
        var importedRemainder = state.Production.Lots.Single(value => value.LotId == childId);
        var laterCraft = CraftCommand(state, journey, journey.Player, fixture.Repack,
            new(IngredientLotKind.Transformed, childId, importedRemainder.Revision, 1));
        state = CareRewardJourney.Commit(session, journey.Batch(state, WorldBatchKind.AdvancingStep, [laterCraft])).Candidate;
        var laterBatch = Assert.Single(state.Production!.Batches);
        Assert.Equal((producer.BatchId, 2UL, 3UL), (laterBatch.Ingredients.Single().Output!.BatchId,
            laterBatch.Ingredients.Single().Output!.Start, laterBatch.Ingredients.Single().Output!.End));
        var laterDose = state.Production.Lots.Single(value => value.Output.BatchId == laterBatch.BatchId);
        var laterPrepared = Assert.IsType<UseRestorativeItemIntent>(WorldRestorativeActions.Prepare(state,
            journey.Player, journey.Player, fixture.Self.DefinitionId, null));
        var laterUse = new UseRestorativeItemIntent(laterPrepared.RecipientActorId, laterPrepared.UseDefinitionId,
            laterPrepared.UseDefinitionVersion, laterPrepared.PermissionId, laterPrepared.PermissionGrantVersion,
            laterPrepared.ExpectedPermissionRevision, laterPrepared.ExpectedPatientPresenceRevision,
            laterPrepared.ExpectedPatientProgressionRevision, laterPrepared.ExpectedStorageRevision,
            laterPrepared.ExpectedProductionRevision, new(IngredientLotKind.Transformed, laterDose.LotId, laterDose.Revision, 1));
        int beforeLaterUse = state.Progression!.GetActor(journey.Player).CurrentHp;
        state = CareRewardJourney.Commit(session, journey.Batch(state, WorldBatchKind.AdvancingStep,
            [journey.Command(state, journey.Player, laterUse)])).Candidate;
        Assert.True(state.Progression!.GetActor(journey.Player).CurrentHp > beforeLaterUse);
        Assert.Equal(laterBatch.BatchId, Assert.Single(state.Production!.ConsumedItems).Ingredient.Output!.BatchId);
        WorldMaterialCareArchiveSegment second = CutAndDecode(session, first.Anchor, out WorldSnapshot again);
        Assert.Equal(1UL, second.Reference.Ordinal);
        Assert.Equal(3UL, again.Production!.TotalBatchCount);
        Assert.Equal(3UL, again.Production.TotalConsumedItemCount);
        Assert.Equal(1UL, again.Care!.TotalTreatments);
        Assert.True(again.Production.Continuation!.SupportsConsentedWorld);
        Assert.Contains(pair.Offer.CommandId, session.OccupiedIdentities);
        Assert.Contains(pair.Acceptance.CommandId, session.OccupiedIdentities);
        session = new(again, session.OccupiedIdentities);
        var held = again.Production.Lots.Single(value => value.LotId == stock.LotId);
        foreach (EntityId closedId in new[] { pair.Offer.CommandId, pair.Acceptance.CommandId, childId })
        {
            var retainedTerms = new ConsentedMaterialTerms(again.WorldId, again.BranchId, journey.Helper, journey.Player,
                ConsentedMaterialLotKind.Produced, held.LotId, held.Definition, helperBag, playerBag,
                held.Quantity, held.Revision);
            var fresh = Pair(again, journey, retainedTerms);
            var reused = new WorldActionCommand(closedId, fresh.Offer.PrincipalId, fresh.Offer.OriginSessionId,
                journey.Helper, again.WorldId, again.BranchId, fresh.Offer.ExpectedActorRevision, fresh.Offer.Intent);
            var accepted = journey.Command(again, journey.Player,
                AcceptMaterialIntent.Approving(reused, again.Space.GetActor(journey.Helper).Revision));
            Assert.Throws<InvalidOperationException>(() => session.Commit(journey.Batch(again,
                WorldBatchKind.AdvancingStep, [reused, accepted])));
            Assert.Equal(again.ContentHash, session.ReadStateForHost().ContentHash);
        }
    }

    private static WorldActionCommand CraftCommand(WorldSnapshot world, CareRewardJourney journey, EntityId actor,
        CraftRecipeDefinition recipe, IngredientRequest ingredient)
    {
        EntityId bag = world.GetResident(actor).InventoryCustodianId;
        return journey.Command(world, actor, new CraftIntent(new(recipe.DefinitionId, recipe.Version,
            world.Production!.Revision, bag, world.Inventory.Custody.GetStorage(bag).Revision, [ingredient])));
    }

    private sealed class ConsentedPotionFixture
    {
        internal ConsentedPotionFixture(bool reservoirHeadroom = false, int? rawLotTarget = null)
        {
            Supply = new RestorativeSupplyFixture();
            var berry = Supply.Recipe.BindingFor(Supply.Berries);
            var potion = Supply.Recipe.BindingFor(Supply.Potion);
            var package = new PackageId("openfantasy.consented-world-tests");
            var bulk = new MaterialRecipeDefinition(package, Id(1), "bulk_potion", new(1), null,
                new MaterialCatalog([berry.Physical, potion.Physical]),
                [new(RecipeMaterialRole.Input, berry.Physical.Pin(), 12), new(RecipeMaterialRole.Output, potion.Physical.Pin(), 4)]);
            Bulk = new(Id(2), bulk, [berry, potion], [new(Supply.Berries, 0, potion.Physical.MassPerUnit)]);
            var repack = new MaterialRecipeDefinition(package, Id(3), "repack_potion", new(1), null,
                new MaterialCatalog([potion.Physical]),
                [new(RecipeMaterialRole.Input, potion.Physical.Pin(), 1), new(RecipeMaterialRole.Output, potion.Physical.Pin(), 1)]);
            Repack = new(Id(4), repack, [potion], [new(Supply.Potion, 0, potion.Physical.MassPerUnit)]);
            // Additional finite berries and recipes are authored before genesis; no played state changes.
            var authored = new CareWorldFixture(freshBerryUnits: 12, extraFreshRecipes: [Bulk, Repack],
                freshReservoirHeadroom: reservoirHeadroom, freshRawLotTarget: rawLotTarget);
            BerryLot = authored.BerryLot;
            var basis = authored.Genesis;
            var remedy = Assert.Single(basis.Care!.Remedies);
            EntityId player = basis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
            var form = basis.Space.GetActor(player).Binding;
            Self = new(Id(5), remedy.DefinitionId, remedy.Version, RestorativeDeliveryMode.SelfUse,
                [new(form.DefinitionId, form.DefinitionVersion)]);
            Genesis = WorldRestorativeAdmission.CreateFresh(basis, new OrdinaryCareSnapshot([Self], [], []));
            Journey = new CareRewardJourney(false, Genesis);
        }

        internal RestorativeSupplyFixture Supply { get; }
        internal EntityId BerryLot { get; }
        internal CraftRecipeDefinition Bulk { get; }
        internal CraftRecipeDefinition Repack { get; }
        internal RestorativeUseDefinition Self { get; }
        internal WorldSnapshot Genesis { get; }
        internal CareRewardJourney Journey { get; }
        private static EntityId Id(ulong value) => new(0xc028000000000000, value);
    }
}
